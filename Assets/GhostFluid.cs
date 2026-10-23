// @role Makes the ghost mesh wobble, drip and trail like a fluid
// @calls GhostController
// @tune wobble:float:0-0.1:Jelly wobble amount
// @tune hemFlow:float:0-0.2:Wave along the bottom edge
// @tune drip:float:0-0.2:How far the tips drip down
// @tune trail:float:0-0.6:How far the lower body trails when flying
// @tune speed:float:0.5-5:Animation speed
using System.Collections.Generic;
using UnityEngine;

// 让幽灵的身体像流体一样流动：果冻般晃动、下摆一圈圈飘动、尖角往下滴落拉长、
// 飞行时下半身拖在后面。保持 low poly 的平面着色。
// GhostController 会自动把它加到幽灵上，不用手动挂。
public class GhostFluid : MonoBehaviour
{
    [Tooltip("整体果冻晃动幅度（身高的比例）")]
    public float wobble = 0.025f;
    [Tooltip("下摆飘动幅度")]
    public float hemFlow = 0.08f;
    [Tooltip("下摆尖角往下滴落拉长的幅度")]
    public float drip = 0.07f;
    [Tooltip("呼吸：上下伸缩")]
    public float breathe = 0.02f;
    [Tooltip("飞行时下半身拖在后面的程度")]
    public float trail = 0.25f;
    public float speed = 2.2f;

    class Part
    {
        public Mesh mesh;
        public Vector3[] baseRoot;   // 原始顶点（幽灵根物体坐标）
        public Vector3[] work;
        public Vector4[] tangents;
        public Matrix4x4 fromRoot;
        public int[][] groups;       // 位置相同的顶点（用来算描边用的平滑法线）
    }

    readonly List<Part> parts = new List<Part>();
    float minY, height, cx, cz;
    Vector3 lastPos, vel;
    bool ready;

    void Start()
    {
        var all = new List<Vector3>();

        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh src = mf.sharedMesh;
            if (src == null) continue;

            // 拆开共享顶点：每个三角形独立 → 平面着色（low poly）
            Vector3[] sv = src.vertices;
            var verts = new List<Vector3>();
            var subTris = new List<int[]>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                int[] t = src.GetTriangles(s);
                var nt = new int[t.Length];
                for (int i = 0; i < t.Length; i++) { nt[i] = verts.Count; verts.Add(sv[t[i]]); }
                subTris.Add(nt);
            }

            var mesh = new Mesh { name = src.name + " (fluid)" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.subMeshCount = subTris.Count;
            for (int s = 0; s < subTris.Count; s++) mesh.SetTriangles(subTris[s], s);
            mesh.MarkDynamic();
            mf.mesh = mesh;

            Matrix4x4 toRoot = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var p = new Part
            {
                mesh = mesh,
                fromRoot = toRoot.inverse,
                baseRoot = new Vector3[verts.Count],
                work = new Vector3[verts.Count],
                tangents = new Vector4[verts.Count]
            };
            var dict = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < verts.Count; i++)
            {
                p.baseRoot[i] = toRoot.MultiplyPoint3x4(verts[i]);
                all.Add(p.baseRoot[i]);
                var key = Vector3Int.RoundToInt(verts[i] * 10000f);
                if (!dict.TryGetValue(key, out var list)) dict[key] = list = new List<int>();
                list.Add(i);
            }
            p.groups = new int[dict.Count][];
            int g = 0;
            foreach (var list in dict.Values) p.groups[g++] = list.ToArray();
            parts.Add(p);
        }

        if (all.Count == 0) return;
        var bmin = all[0]; var bmax = all[0];
        foreach (var v in all) { bmin = Vector3.Min(bmin, v); bmax = Vector3.Max(bmax, v); }
        minY = bmin.y;
        height = Mathf.Max(bmax.y - bmin.y, 0.0001f);
        cx = (bmin.x + bmax.x) * 0.5f;
        cz = (bmin.z + bmax.z) * 0.5f;
        lastPos = transform.position;
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready) return;
        float t = Time.unscaledTime;   // 慢动作时也照样流动
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        // 幽灵移动速度（转到幽灵自己的坐标里），用来让身体拖在后面
        Vector3 v = (transform.position - lastPos) / dt;
        lastPos = transform.position;
        vel = Vector3.Lerp(vel, v, 1f - Mathf.Exp(-8f * dt));
        float scale = Mathf.Max(transform.lossyScale.y, 0.0001f);
        Vector3 velLocal = transform.InverseTransformDirection(vel) / (scale * height);   // 每秒移动几个身高
        Vector3 trailDir = Vector3.ClampMagnitude(-velLocal * 0.15f, 1f);

        foreach (var p in parts)
        {
            for (int i = 0; i < p.baseRoot.Length; i++)
                p.work[i] = p.fromRoot.MultiplyPoint3x4(p.baseRoot[i] + Displace(p.baseRoot[i], t, trailDir));

            p.mesh.vertices = p.work;
            p.mesh.RecalculateNormals();   // 顶点没共享 → 每个面一个法线（平面着色）

            // 描边用的平滑法线 → 放进 tangent
            Vector3[] n = p.mesh.normals;
            foreach (var grp in p.groups)
            {
                Vector3 sum = Vector3.zero;
                foreach (int i in grp) sum += n[i];
                Vector3 s = sum.normalized;
                foreach (int i in grp) p.tangents[i] = new Vector4(s.x, s.y, s.z, 1f);
            }
            p.mesh.tangents = p.tangents;
            p.mesh.RecalculateBounds();
        }
    }

    Vector3 Displace(Vector3 p, float t, Vector3 trailDir)
    {
        float h = height;
        float yN = Mathf.Clamp01((p.y - minY) / h);   // 0 = 下摆，1 = 头顶
        float low = 1f - yN;
        Vector3 radial = new Vector3(p.x - cx, 0f, p.z - cz);
        float rLen = radial.magnitude;
        Vector3 rDir = rLen > 0.0001f ? radial / rLen : Vector3.zero;
        float ang = Mathf.Atan2(p.z - cz, p.x - cx);

        // ① 果冻晃动：波纹从下往上走，越往下越明显
        float w = Mathf.Sin(t * speed + yN * 6f + ang * 2f) * wobble * h * (0.3f + low);

        // ② 下摆：一圈圈绕着转的波浪
        float hem = Mathf.Clamp01((0.45f - yN) / 0.45f);
        hem *= hem;
        float flow = Mathf.Sin(ang * 3f - t * speed * 1.3f);

        Vector3 d = rDir * (w + flow * hemFlow * h * 0.5f * hem);
        d.y += flow * hemFlow * h * 0.6f * hem;

        // ③ 尖角往下滴落、再缩回去
        float dw = Mathf.Max(0f, Mathf.Sin(ang * 5f + t * speed * 0.7f));
        d.y -= dw * dw * drip * h * hem;

        // ④ 呼吸
        d.y += Mathf.Sin(t * speed * 0.8f) * breathe * h * (yN - 0.3f);

        // ⑤ 下半身左右慢慢摆
        d.x += Mathf.Sin(t * speed * 0.6f - yN * 2f) * wobble * h * low;

        // ⑥ 飞行时，下半身拖在后面
        d += trailDir * trail * h * low * low;

        return d;
    }
}
