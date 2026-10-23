// @role Builds invisible walls along both edges of the run at Play; touching one loses
// @calls SlopeExtender SlopeDecorator
// @tune wallHeight:float:1-20:Wall height
// @tune inset:float:-5-5:Moves the walls inward from the snow edge
// @tune segmentLength:float:2-20:Length of each wall piece
using System.Collections.Generic;
using UnityEngine;

// 雪道两边的隐形挡墙：开始游戏后自动沿着每一段雪道（slope、slope (1)、SlopeExtender 加的 slope extra …）的左右边缘建墙。
// 墙的 Tag 是 Obstacle：玩家撞到就输；小狗、小鸡会把它当成障碍物绕开，也穿不过去。
// 用法：Hierarchy 里建一个空物体，把这个脚本拖上去，点 Play。不会改场景文件，停止 Play 后墙就没了。
public class SlopeWalls : MonoBehaviour
{
    [Header("墙")]
    [Tooltip("墙有多高（米，会自动乘上世界的放大倍数）")]
    public float wallHeight = 6f;
    [Tooltip("墙有多厚（米）")]
    public float wallThickness = 1.5f;
    [Tooltip("墙往雪道里面收进来多少（米）。0 = 正好在雪道边缘；想让雪道窄一点就调大")]
    public float inset = 0f;
    [Tooltip("墙往雪面下面埋多深（米），免得墙底和雪面之间有缝")]
    public float sinkDepth = 1f;
    [Tooltip("撞到墙算输（Tag = Obstacle）。关掉就只是挡住，不算输")]
    public bool hitLoses = true;

    [Header("精度")]
    [Tooltip("沿着雪道每隔多少米放一段墙。越小越贴边，越大越省性能")]
    public float segmentLength = 6f;
    [Tooltip("横着扫雪道宽度时扫多少次。越大边缘找得越准")]
    public int acrossSamples = 160;

    [Header("调试")]
    [Tooltip("在 Game 画面里把墙显示成半透明红色，方便看位置。调好以后关掉")]
    public bool showWalls = false;
    [Tooltip("在 Console 里打印每段雪道建了多少墙")]
    public bool showDebug = true;

    const string RootName = "SideWalls";
    const string WallName = "SideWall";
    const string ObstacleTag = "Obstacle";

    float S = 1f;                       // 世界放大倍数（玩家身高 / 1.6 米）
    Transform root;
    readonly HashSet<Collider> done = new HashSet<Collider>();
    readonly List<GameObject> walls = new List<GameObject>();
    Material showMat;
    bool lastShow;

    System.Collections.IEnumerator Start()
    {
        // 等两帧，让 SlopeExtender、SlopeDecorator 先把雪道和树摆好
        yield return null;
        yield return null;
        S = MeasureScale();
        root = new GameObject(RootName).transform;
        lastShow = showWalls;
        while (true)
        {
            BuildNewPieces();
            yield return new WaitForSeconds(2f);
        }
    }

    void Update()
    {
        if (showWalls != lastShow)
        {
            lastShow = showWalls;
            foreach (var w in walls) if (w != null) SetVisible(w, showWalls);
        }
    }

    // 世界放大倍数：用玩家（SkierController）的模型高度算，和小狗生成器的算法一样
    float MeasureScale()
    {
        var rider = FindObjectOfType<SkierController>();
        if (rider == null) return 1f;
        var rs = rider.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 1f;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return Mathf.Clamp(b.size.y / 1.6f, 0.1f, 1000f);
    }

    // 找还没建过墙的雪道碰撞体
    void BuildNewPieces()
    {
        foreach (var c in FindObjectsOfType<Collider>())
        {
            if (done.Contains(c) || !IsSlopeSurface(c)) continue;
            done.Add(c);
            int n = BuildFor(c);
            if (showDebug) Debug.Log($"[SlopeWalls] {Path(c.transform)}：建了 {n} 段墙", c);
        }
    }

    bool IsSlopeSurface(Collider c)
    {
        if (c == null || c.isTrigger || !c.enabled || c.attachedRigidbody != null) return false;
        Vector3 size = c.bounds.size;
        if (Mathf.Max(size.x, size.z) < 20f * S) return false;      // 树、熊、石头没这么大
        bool slope = false;
        for (Transform t = c.transform; t != null; t = t.parent)
        {
            if (t == root || t.name == "Decorations" || t.name == RootName) return false;
            if (t.CompareTag(ObstacleTag)) return false;
            string n = t.name.ToLower();
            if (n.Contains("mountain")) return false;
            if (n.Contains("slope")) slope = true;
        }
        return slope;
    }

    // 给一段雪道建左右两排墙，返回建了多少段
    int BuildFor(Collider c)
    {
        Bounds b = c.bounds;
        float top = b.max.y + 5f * S;
        float rayLen = b.size.y + 10f * S;

        // 1. 先找这一段雪道往哪边往下：在雪面上取一些点，拟合一个斜面
        Vector3 down = DownhillOf(c, b, top, rayLen);
        if (down == Vector3.zero) return 0;
        Vector3 across = Vector3.Cross(Vector3.up, down).normalized;

        // 2. 算出雪道沿着往下方向、横向的范围
        float aMin, aMax, xMin, xMax;
        Extent(b, down, out aMin, out aMax);
        Extent(b, across, out xMin, out xMax);

        float step = Mathf.Max(segmentLength * S, (aMax - aMin) / 400f);
        int samples = Mathf.Max(2, acrossSamples);
        float dx = (xMax - xMin) / samples;

        // 3. 每隔一段往下走，横着扫一遍，最宽的那一段连续雪面的两头就是左右边缘
        var left = new List<Vector3>();
        var right = new List<Vector3>();
        for (float a = aMin + step * 0.5f; a <= aMax; a += step)
        {
            int bestStart = -1, bestEnd = -1, runStart = -1;
            Vector3[] pts = new Vector3[samples + 1];
            bool[] ok = new bool[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float x = xMin + dx * i;
                Vector3 flat = down * a + across * x;
                Vector3 from = new Vector3(flat.x, top, flat.z);
                RaycastHit h;
                ok[i] = c.Raycast(new Ray(from, Vector3.down), out h, rayLen) && h.normal.y > 0.4f;
                if (ok[i]) pts[i] = h.point;
                if (ok[i] && runStart < 0) runStart = i;
                if ((!ok[i] || i == samples) && runStart >= 0)
                {
                    int end = ok[i] ? i : i - 1;
                    if (bestStart < 0 || end - runStart > bestEnd - bestStart) { bestStart = runStart; bestEnd = end; }
                    runStart = -1;
                }
            }
            if (bestStart < 0 || bestEnd - bestStart < 3) continue;
            Vector3 l = Refine(c, pts[bestStart], -across, dx, top, rayLen);
            Vector3 r = Refine(c, pts[bestEnd], across, dx, top, rayLen);
            l += across * (inset * S);
            r -= across * (inset * S);
            left.Add(l);
            right.Add(r);
        }

        int count = 0;
        count += BuildRow(left, -across, c.transform.name);
        count += BuildRow(right, across, c.transform.name);
        return count;
    }

    // 往外再细扫一点，把边缘找得更准
    Vector3 Refine(Collider c, Vector3 edge, Vector3 outward, float dx, float top, float rayLen)
    {
        Vector3 best = edge;
        for (int i = 1; i <= 8; i++)
        {
            Vector3 p = edge + outward * (dx * i / 8f);
            RaycastHit h;
            if (c.Raycast(new Ray(new Vector3(p.x, top, p.z), Vector3.down), out h, rayLen) && h.normal.y > 0.4f) best = h.point;
            else break;
        }
        return best;
    }

    // 一排墙：相邻两个边缘点之间放一块盒子碰撞体，盒子贴着边缘、往外放
    int BuildRow(List<Vector3> pts, Vector3 outward, string pieceName)
    {
        int n = 0;
        float h = wallHeight * S, th = wallThickness * S, sink = sinkDepth * S;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector3 p1 = pts[i], p2 = pts[i + 1];
            Vector3 seg = p2 - p1;
            float len = seg.magnitude;
            if (len < 0.01f) continue;
            Quaternion rot = Quaternion.LookRotation(seg / len, Vector3.up);
            Vector3 side = rot * Vector3.right;
            if (Vector3.Dot(side, outward) < 0f) side = -side;
            Vector3 up = rot * Vector3.up;
            Vector3 center = (p1 + p2) * 0.5f + side * (th * 0.5f) + up * (h * 0.5f - sink);

            var go = new GameObject(WallName);
            go.transform.SetParent(root, false);
            go.transform.position = center;
            go.transform.rotation = rot;
            if (hitLoses) go.tag = ObstacleTag;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(th, h, len + th);      // 稍微长一点，两段之间不会有缝
            if (showWalls) SetVisible(go, true);
            walls.Add(go);
            n++;
        }
        return n;
    }

    // 往下的方向：在雪面上取 7x7 个点，拟合 y = a·x + b·z，往下就是 -(a, b)
    Vector3 DownhillOf(Collider c, Bounds b, float top, float rayLen)
    {
        double sx = 0, sz = 0, sy = 0, sxx = 0, szz = 0, sxz = 0, sxy = 0, szy = 0;
        int n = 0;
        for (int i = 0; i < 7; i++)
            for (int j = 0; j < 7; j++)
            {
                float x = Mathf.Lerp(b.min.x, b.max.x, (i + 0.5f) / 7f);
                float z = Mathf.Lerp(b.min.z, b.max.z, (j + 0.5f) / 7f);
                RaycastHit h;
                if (!c.Raycast(new Ray(new Vector3(x, top, z), Vector3.down), out h, rayLen)) continue;
                double px = h.point.x - b.center.x, pz = h.point.z - b.center.z, py = h.point.y;
                sx += px; sz += pz; sy += py; sxx += px * px; szz += pz * pz; sxz += px * pz; sxy += px * py; szy += pz * py;
                n++;
            }
        if (n < 4) return Vector3.zero;
        // 去掉平均值后解 2x2 方程
        double mx = sx / n, mz = sz / n, my = sy / n;
        double cxx = sxx / n - mx * mx, czz = szz / n - mz * mz, cxz = sxz / n - mx * mz;
        double cxy = sxy / n - mx * my, czy = szy / n - mz * my;
        double det = cxx * czz - cxz * cxz;
        if (System.Math.Abs(det) < 1e-9) return Vector3.zero;
        double a = (cxy * czz - czy * cxz) / det;
        double bb = (czy * cxx - cxy * cxz) / det;
        Vector3 d = new Vector3((float)-a, 0f, (float)-bb);
        if (d.sqrMagnitude < 1e-8f) return Vector3.zero;
        return d.normalized;
    }

    static void Extent(Bounds b, Vector3 axis, out float min, out float max)
    {
        min = float.MaxValue; max = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, 0f, (i & 2) == 0 ? b.min.z : b.max.z);
            float d = Vector3.Dot(p, axis);
            if (d < min) min = d;
            if (d > max) max = d;
        }
    }

    // 显示用的半透明方块：放在墙下面当子物体，大小和碰撞体一样，没有碰撞体
    void SetVisible(GameObject go, bool on)
    {
        Transform view = go.transform.Find("WallView");
        if (on && view == null)
        {
            if (showMat == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                showMat = new Material(sh != null ? sh : Shader.Find("Standard"));
                showMat.color = new Color(1f, 0.1f, 0.1f, 0.35f);
            }
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = "WallView";
            cube.transform.SetParent(go.transform, false);
            cube.transform.localScale = go.GetComponent<BoxCollider>().size;
            var mr = cube.GetComponent<MeshRenderer>();
            mr.sharedMaterial = showMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            view = cube.transform;
        }
        if (view != null) view.gameObject.SetActive(on);
    }

    static string Path(Transform t)
    {
        string p = t.name;
        for (t = t.parent; t != null; t = t.parent) p = t.name + "/" + p;
        return p;
    }

    // Scene 窗口里画出墙的位置（Play 的时候）
    void OnDrawGizmos()
    {
        if (root == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        foreach (var w in walls)
        {
            if (w == null) continue;
            var box = w.GetComponent<BoxCollider>();
            Gizmos.matrix = w.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
