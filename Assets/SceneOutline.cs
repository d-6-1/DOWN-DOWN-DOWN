// @role Adds a dark toon outline to everything in the scene at Play
// @tune outlineWidth:float:0-0.1:Outline width relative to rider height
// @tune checkEvery:float:0.5-10:Seconds between scans for new objects
// SceneOutline.cs —— 给场景里所有东西（雪坡、树、石头、北极熊……）自动加上和开场平台一样的深色描边
// 原理和小狗、小鸡一样：每个模型复制一层，沿着表面往外撑一点，只画背面 → 边缘一圈深色描边。
// 小狗、小鸡、巴哥、登山者、开场平台、幽灵本来就有描边/特殊材质，会自动跳过。
// 半透明的东西（红色墙壁预览、雪花）也跳过。后来才出现的东西（加出来的雪坡、生成的装饰）每隔两秒检查一次，也会加上。
//
// 用法：Hierarchy 里新建一个空物体（叫 SceneOutline），挂上这个脚本。只在 Play 时生效，不改场景里的任何东西。
// 需要 DogToon.shader（小狗用的卡通着色器）在 Assets 里。

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SceneOutline : MonoBehaviour
{
    [Tooltip("描边粗细：第一个 rider 身高的几倍（开场平台用的是 0.02）")]
    public float outlineWidth = 0.02f;
    [Tooltip("描边颜色（和小狗小鸡一样的深棕色）")]
    public Color outlineColor = new Color32(60, 30, 15, 255);
    [Tooltip("勾上：后来才出现的东西也加描边（每隔几秒检查一次）")]
    public bool keepChecking = true;
    [Tooltip("多少秒检查一次新东西")]
    public float checkEvery = 2f;
    [Tooltip("名字里带这些字的物体不加描边（大小写不管）。比如不想给天空球加，就写 sky")]
    public string[] skipNames = { "sky", "WallView" };

    const string OutlineName = "SceneOutline";
    Material mat;
    float width, bodyHeight;
    readonly HashSet<Renderer> done = new HashSet<Renderer>();
    readonly Dictionary<Mesh, Mesh> hulls = new Dictionary<Mesh, Mesh>();
    readonly HashSet<Mesh> batchesDone = new HashSet<Mesh>();

    IEnumerator Start()
    {
        Shader toon = Shader.Find("DogScout/Toon");
        if (toon == null)
        {
            Debug.LogError("SceneOutline：没找到 DogToon.shader（DogScout/Toon），没法加描边。");
            yield break;
        }
        mat = new Material(toon) { name = "SceneOutline" };
        mat.color = outlineColor;
        mat.SetFloat("_Cull", (float)CullMode.Front);

        // 等一帧：让加雪坡、摆装饰、生成小狗这些脚本先跑完
        yield return null;
        SkierController rider = GhostController.Current != null ? GhostController.Current : FindObjectOfType<SkierController>();
        bodyHeight = rider != null ? Mathf.Max(0.1f, rider.BodyHeight) : 1.6f;
        width = outlineWidth * bodyHeight;
        mat.SetFloat("_OutlineWidth", width);

        int n = AddOutlines();
        Debug.Log($"SceneOutline：给 {n} 个物体加了描边（描边粗细 {width:F2} 米）。");
        while (keepChecking)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, checkEvery));
            AddOutlines();
        }
    }

    // Play 时在 Inspector 里改粗细、颜色，马上就能看到
    void OnValidate()
    {
        if (mat == null || bodyHeight <= 0f) return;
        mat.color = outlineColor;
        width = outlineWidth * bodyHeight;
        mat.SetFloat("_OutlineWidth", width);
    }

    // 先把要加的都找出来，再一起加（不然刚加的描边会影响后面的判断）
    int AddOutlines()
    {
        var todo = new List<Renderer>();
        foreach (var r in FindObjectsOfType<Renderer>())
        {
            if (r == null || done.Contains(r)) continue;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;   // 粒子、文字这些不管
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            done.Add(r);
            if (ShouldOutline(r)) todo.Add(r);
        }
        int count = 0;
        foreach (var r in todo) if (AddOutline(r)) count++;
        return count;
    }

    bool ShouldOutline(Renderer r)
    {
        string n = r.gameObject.name;
        if (n == OutlineName || n == "Outline") return false;
        foreach (var s in skipNames)
            if (!string.IsNullOrEmpty(s) && n.ToLower().Contains(s.ToLower())) return false;

        // 半透明的、已经是卡通/幽灵材质的不加
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) continue;
            if (m.renderQueue >= 2900) return false;
            string sh = m.shader != null ? m.shader.name : "";
            if (sh.Contains("DogScout") || sh.Contains("Ghost") || sh.Contains("Particle") || sh.Contains("Sprite") || sh.Contains("UI/")) return false;
        }

        // 已经有描边的模型（小狗、小鸡、rider……）整个跳过
        if (r.transform.Find("Outline") != null || r.transform.Find(OutlineName) != null) return false;
        for (Transform t = r.transform; t != null; t = t.parent)
        {
            foreach (var c in t.GetComponents<MonoBehaviour>())
            {
                if (c == null) continue;
                string type = c.GetType().Name;
                if (type.EndsWith("Builder") || type.Contains("Spawner") || c is SkierController || c is GhostFluid || c is OpeningScene)
                    return false;
            }
            // 兄弟零件有描边 → 是小狗小鸡身上没描边的小零件（眼珠、钉子），也跳过
            if (t.parent != null)
                foreach (Transform sib in t.parent)
                    if (sib != t && sib.Find("Outline") != null) return false;
        }
        return true;
    }

    bool AddOutline(Renderer r)
    {
        // 勾了 Static 的物体会被合并成一个大网格：直接给合并后的大网格加一层描边（顶点已经是世界坐标）
        if (r.isPartOfStaticBatch)
        {
            var mfS = r.GetComponent<MeshFilter>();
            if (mfS == null || mfS.sharedMesh == null || !batchesDone.Add(mfS.sharedMesh)) return false;
            var go = new GameObject(OutlineName + "_Static");
            go.AddComponent<MeshFilter>().sharedMesh = Hull(mfS.sharedMesh);
            SetupRenderer(go.AddComponent<MeshRenderer>());
            return true;
        }

        if (r is SkinnedMeshRenderer smr)
        {
            if (smr.sharedMesh == null) return false;
            var go = new GameObject(OutlineName);
            go.transform.SetParent(r.transform, false);
            var o = go.AddComponent<SkinnedMeshRenderer>();
            o.sharedMesh = Hull(smr.sharedMesh);
            o.bones = smr.bones;
            o.rootBone = smr.rootBone;
            o.localBounds = smr.localBounds;
            SetupRenderer(o);
            return true;
        }

        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        var g = new GameObject(OutlineName);
        g.layer = r.gameObject.layer;
        g.transform.SetParent(r.transform, false);
        g.AddComponent<MeshFilter>().sharedMesh = Hull(mf.sharedMesh);
        SetupRenderer(g.AddComponent<MeshRenderer>());
        return true;
    }

    void SetupRenderer(Renderer o)
    {
        o.sharedMaterial = mat;
        o.shadowCastingMode = ShadowCastingMode.Off;
        o.receiveShadows = false;
    }

    // 描边用的网格：同样的形状，但同一个位置的顶点法线取平均（低多边形的硬边也不会裂开）
    Mesh Hull(Mesh src)
    {
        if (hulls.TryGetValue(src, out Mesh m)) return m;
        if (!src.isReadable)
        {
            // 模型导入设置没勾 Read/Write：只能直接用原网格（有硬边的地方描边可能断开）
            hulls[src] = src;
            return src;
        }
        Vector3[] v = src.vertices;
        Vector3[] nrm = src.normals;
        if (nrm == null || nrm.Length != v.Length) { src.RecalculateNormals(); nrm = src.normals; }

        float q = Mathf.Max(1e-6f, src.bounds.size.magnitude * 1e-5f);
        var sum = new Dictionary<Vector3Int, Vector3>();
        var keys = new Vector3Int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            keys[i] = new Vector3Int(Mathf.RoundToInt(v[i].x / q), Mathf.RoundToInt(v[i].y / q), Mathf.RoundToInt(v[i].z / q));
            sum.TryGetValue(keys[i], out Vector3 s);
            sum[keys[i]] = s + nrm[i];
        }
        var smooth = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 s = sum[keys[i]];
            smooth[i] = s.sqrMagnitude > 1e-12f ? s.normalized : nrm[i];
        }

        m = new Mesh { name = src.name + "_Outline" };
        if (v.Length > 65000) m.indexFormat = IndexFormat.UInt32;
        m.vertices = v;
        m.normals = smooth;
        var tris = new List<int>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var mt = src.GetTopology(s);
            if (mt == MeshTopology.Triangles) tris.AddRange(src.GetTriangles(s));
        }
        m.SetTriangles(tris, 0);
        if (src.boneWeights != null && src.boneWeights.Length == v.Length)
        {
            m.boneWeights = src.boneWeights;
            m.bindposes = src.bindposes;
        }
        m.RecalculateBounds();
        hulls[src] = m;
        return m;
    }
}
