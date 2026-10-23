// @role Editor tool: places trees and polar bears at random on the slopes
// @calls SlopeExtender
// @tune countPerSlope:int:0-60:Trees and bears per slope piece
// @tune sizeVariation:float:0-0.5:Random size difference
// @tune spacing:float:0.5-5:Minimum gap between props
// @tune maxSlopeAngle:float:10-80:Steepest ground a prop can stand on
// SlopeDecorator.cs —— 在加出来的雪坡（slope extra 1、2、3……）上随机摆北极熊和树
// 用法：Hierarchy 里新建一个空物体（叫 SlopeDecorator），挂上这个脚本，
//       把前两段坡上的一只北极熊、一棵树（想要几种就拖几个）拖进 Templates，
//       在组件右上角 ⋮ 里点 "Decorate Slopes（随机摆放）"，然后 Ctrl+S 保存场景。
// 不满意：点 "Clear Decorations（全部删掉）" 或 Ctrl+Z，再摆一次就是新的随机位置。
// 有东西悬空：点 "Snap To Ground（全部贴地）"，位置不变，只把它们往下贴到雪面上。
// 摆出来的东西是普通物体，在每段坡下面的 Decorations 里，可以单独挪动、删除。
// 复制出来的熊和树会保留原来的碰撞体和 Tag（Obstacle），撞到一样会输。

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class SlopeDecorator : MonoBehaviour
{
    [Tooltip("要摆的模型：从前两段坡上拖北极熊、树进来（可以拖好几种）")]
    public List<GameObject> templates = new List<GameObject>();
    [Tooltip("每段坡摆几个（从上面的模型里随机挑）")]
    public int countPerSlope = 15;
    [Tooltip("大小随机变化：0.15 = 原来大小的 85% ~ 115%")]
    [Range(0f, 0.5f)] public float sizeVariation = 0.15f;
    [Tooltip("勾上：随机转个方向")]
    public bool randomTurn = true;
    [Tooltip("两个东西之间至少隔多远（按模型自己的宽度算，1 = 一个模型宽）")]
    public float spacing = 1.5f;
    [Tooltip("离坡的左右边缘留多少空（坡宽的比例）")]
    [Range(0f, 0.4f)] public float edgeMargin = 0.08f;
    [Tooltip("坡面太陡的地方不放（法线和竖直方向的夹角上限）")]
    public float maxSlopeAngle = 50f;

    const string ExtraName = "slope extra ";
    const string GroupName = "Decorations";

    [ContextMenu("Decorate Slopes（随机摆放）")]
    public void Decorate()
    {
        templates.RemoveAll(t => t == null);
        if (templates.Count == 0)
        {
            Debug.LogError("SlopeDecorator：Templates 是空的。请把前两段坡上的北极熊、树拖进 Templates。");
            return;
        }

        Physics.SyncTransforms();   // 编辑模式下也让射线能打到最新位置的雪坡
        int pieces = 0, placed = 0;
        foreach (var piece in FindExtraSlopes())
        {
            pieces++;
            placed += DecoratePiece(piece);
        }
        if (pieces == 0)
            Debug.LogWarning("SlopeDecorator：场景里没有 slope extra 开头的坡。先用 SlopeExtender 加长雪道。");
        else
            Debug.Log("SlopeDecorator：在 " + pieces + " 段新坡上摆了 " + placed + " 个。记得 Ctrl+S 保存场景。");
        MarkDirty();
    }

    [ContextMenu("Clear Decorations（全部删掉）")]
    public void Clear()
    {
        int n = 0;
        foreach (var piece in FindExtraSlopes())
        {
            Transform g = piece.Find(GroupName);
            if (g == null) continue;
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(g.gameObject);
#else
            DestroyImmediate(g.gameObject);
#endif
            n++;
        }
        Debug.Log("SlopeDecorator：清掉了 " + n + " 段坡上摆的东西。");
        MarkDirty();
    }

    [ContextMenu("Snap To Ground（全部贴地）")]
    public void SnapAll()
    {
        Physics.SyncTransforms();
        int n = 0;
        foreach (var piece in FindExtraSlopes())
        {
            Transform g = piece.Find(GroupName);
            if (g == null) continue;
            var surface = SurfaceOf(piece);
            if (surface.Count == 0) continue;
            foreach (Transform child in g)
            {
#if UNITY_EDITOR
                Undo.RecordObject(child, "Snap To Ground");
#endif
                if (SnapToGround(child.gameObject, surface)) n++;
            }
        }
        Debug.Log("SlopeDecorator：把 " + n + " 个物体贴到了雪面上。记得 Ctrl+S 保存场景。");
        MarkDirty();
    }

    // 贴地：找出模型最底下那一圈点（树根、熊脚），每个点量一下正下方雪面多高，
    // 整个模型往下挪到每个点都碰到雪（坡是斜的，所以低的那边也不会悬空）
    bool SnapToGround(GameObject go, HashSet<Collider> surface)
    {
        var pts = new List<Vector3>();
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            Mesh m = null;
            if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh;
            else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
            if (m == null) continue;
            Vector3[] v;
            try { v = m.vertices; } catch { continue; }
            Matrix4x4 w = r.transform.localToWorldMatrix;
            int stride = Mathf.Max(1, v.Length / 3000);
            for (int i = 0; i < v.Length; i += stride)
            {
                Vector3 p = w.MultiplyPoint3x4(v[i]);
                pts.Add(p);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
        }
        if (pts.Count == 0)
        {
            // 量不到模型的点：就用包围盒的四个角
            Bounds b = WorldBounds(go);
            minY = b.min.y; maxY = b.max.y;
            pts.Add(new Vector3(b.min.x, minY, b.min.z)); pts.Add(new Vector3(b.max.x, minY, b.min.z));
            pts.Add(new Vector3(b.min.x, minY, b.max.z)); pts.Add(new Vector3(b.max.x, minY, b.max.z));
        }

        float h = Mathf.Max(0.01f, maxY - minY);
        float footTop = minY + h * 0.1f;     // 最底下 10% 高度算"脚"
        float dy = float.MaxValue;
        int hits = 0;
        foreach (var p in pts)
        {
            if (p.y > footTop) continue;
            Vector3 from = new Vector3(p.x, maxY + h + 50f, p.z);
            if (!HitSurface(from, h * 3f + 1000f, surface, out RaycastHit g)) continue;
            dy = Mathf.Min(dy, g.point.y - p.y);
            hits++;
        }
        if (hits == 0) return false;
        go.transform.position += Vector3.up * (dy - h * 0.02f);   // 再稍微陷进去一点点
        return true;
    }

    // 这段坡的雪面碰撞体
    HashSet<Collider> SurfaceOf(Transform piece)
    {
        var surface = new HashSet<Collider>();
        foreach (var c in piece.GetComponentsInChildren<Collider>())
            if (!c.isTrigger && c.name.ToLower().Contains("slope")) surface.Add(c);
        if (surface.Count == 0)
            foreach (var c in piece.GetComponentsInChildren<Collider>())
                if (!c.isTrigger && !c.CompareTag("Obstacle") && c.transform.parent != null
                    && c.transform.parent.name != GroupName) surface.Add(c);
        return surface;
    }

    List<Transform> FindExtraSlopes()
    {
        var list = new List<Transform>();
        foreach (var t in FindObjectsOfType<Transform>())
            if (t.name.StartsWith(ExtraName)) list.Add(t);
        list.Sort((a, b) => b.position.y.CompareTo(a.position.y));
        return list;
    }

    int DecoratePiece(Transform piece)
    {
        // 雪道本身的碰撞体（名字里带 slope 的，比如 realslope）；找不到就用这段坡下别的碰撞体
        var surface = SurfaceOf(piece);
        if (surface.Count == 0)
        {
            Debug.LogWarning("SlopeDecorator：" + piece.name + " 上没有碰撞体，没法知道雪面在哪。");
            return 0;
        }

        bool first = true;
        Bounds area = new Bounds();
        foreach (var c in surface)
        {
            if (first) { area = c.bounds; first = false; }
            else area.Encapsulate(c.bounds);
        }

        // 每段坡新建一个 Decorations，摆的东西都放在里面
        Transform group = piece.Find(GroupName);
        if (group == null)
        {
            var g = new GameObject(GroupName);
            g.transform.SetParent(piece, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(g, "Decorate Slopes");
#endif
            group = g.transform;
        }

        var used = new List<Vector4>();   // xyz = 位置，w = 半径
        float minNormalY = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
        int placed = 0;
        for (int tries = 0; tries < countPerSlope * 20 && placed < countPerSlope; tries++)
        {
            GameObject src = templates[Random.Range(0, templates.Count)];
            Bounds sb = WorldBounds(src);
            float scale = 1f + Random.Range(-sizeVariation, sizeVariation);
            float radius = Mathf.Max(sb.extents.x, sb.extents.z) * scale;

            Vector3 size = area.size;
            float x = Random.Range(area.min.x + size.x * edgeMargin, area.max.x - size.x * edgeMargin);
            float z = Random.Range(area.min.z + size.z * edgeMargin, area.max.z - size.z * edgeMargin);
            Vector3 from = new Vector3(x, area.max.y + 10f, z);

            if (!HitSurface(from, area.size.y + 20f, surface, out RaycastHit hit)) continue;
            if (hit.normal.y < minNormalY) continue;

            bool tooClose = false;
            foreach (var u in used)
                if (Vector3.Distance(hit.point, u) < (radius + u.w) * spacing) { tooClose = true; break; }
            if (tooClose) continue;

            GameObject copy = Instantiate(src, group);
            copy.name = src.name;
            Quaternion turn = randomTurn ? Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) : Quaternion.identity;
            copy.transform.rotation = turn * src.transform.rotation;
            copy.transform.localScale = src.transform.lossyScale * scale;
            if (copy.transform.parent != null)
            {
                Vector3 ps = copy.transform.parent.lossyScale;
                copy.transform.localScale = new Vector3(SafeDiv(copy.transform.localScale.x, ps.x),
                                                        SafeDiv(copy.transform.localScale.y, ps.y),
                                                        SafeDiv(copy.transform.localScale.z, ps.z));
            }

            // 先大概放到雪面上，再按脚底每个点贴紧雪面
            copy.transform.position = hit.point;
            Physics.SyncTransforms();
            Bounds cb = WorldBounds(copy);
            copy.transform.position += Vector3.up * (hit.point.y - cb.min.y);
            SnapToGround(copy, surface);

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(copy, "Decorate Slopes");
#endif
            used.Add(new Vector4(hit.point.x, hit.point.y, hit.point.z, radius));
            placed++;
        }
        return placed;
    }

    static float SafeDiv(float a, float b) { return Mathf.Abs(b) > 1e-6f ? a / b : a; }

    // 往下打射线，只认这段坡的雪面（跳过树、熊、别的坡）
    static bool HitSurface(Vector3 from, float dist, HashSet<Collider> surface, out RaycastHit best)
    {
        best = new RaycastHit();
        float bestD = float.MaxValue;
        bool found = false;
        foreach (var h in Physics.RaycastAll(from, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!surface.Contains(h.collider)) continue;
            if (h.distance < bestD) { bestD = h.distance; best = h; found = true; }
        }
        return found;
    }

    static Bounds WorldBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    void MarkDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
