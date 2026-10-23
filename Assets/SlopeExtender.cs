// @role Editor tool: adds more slope pieces below the last one to make the run longer
// @tune extraPieces:int:1-10:How many slope pieces to add
// SlopeExtender.cs —— 把雪道加长：照着 slope → slope (1) 的接法，在最下面一段后面一段接一段往下接，一段比一段低
// 用法：Hierarchy 里新建一个空物体（叫 SlopeExtender），挂上这个脚本，
//       在组件右上角 ⋮ 里点 "Extend Slope（加长雪道）"，然后 Ctrl+S 保存场景。
// 不满意：点 "Remove Extra Slopes（删掉加的坡）"，或者直接 Ctrl+Z。
// 加出来的坡是普通物体（名字叫 slope extra 1、2、3……），可以像别的物体一样单独挪动、删除。

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class SlopeExtender : MonoBehaviour
{
    [Tooltip("要接几段")]
    public int extraPieces = 2;
    [Tooltip("从哪段雪坡往下接。留空 = 自动找名字以 slope 开头、位置最低的那段")]
    public Transform lastSlope;
    [Tooltip("照着哪两段坡的接法来接：上面一段、下面一段。留空 = 自动用最低的两段（slope 和 slope (1)）")]
    public Transform patternUpper;
    public Transform patternLower;
    [Tooltip("接缝处有缝就调大一点（0.01 = 每段往回挪 1%，和上一段重叠）")]
    [Range(0f, 0.1f)] public float overlap = 0f;
    [Tooltip("勾上：把雪的物理材质（snow）也给加出来的坡用上")]
    public PhysicMaterial snowMaterial;

    const string ExtraName = "slope extra ";

    [ContextMenu("Extend Slope（加长雪道）")]
    public void ExtendSlope()
    {
        Transform last = lastSlope != null ? lastSlope : FindLowestSlope();
        if (last == null)
        {
            Debug.LogError("SlopeExtender：找不到雪坡。请把最下面那段雪坡拖进 Last Slope。");
            return;
        }

        // 接法：下面一段相对上面一段挪了多少、转了多少（你自己摆的 slope → slope (1)）
        Transform upper = patternUpper, lower = patternLower;
        if (upper == null || lower == null) FindTwoLowest(out upper, out lower);
        Vector3 step;
        Quaternion turn = Quaternion.identity;
        if (upper != null && lower != null && upper != lower)
        {
            step = lower.position - upper.position;
            turn = lower.rotation * Quaternion.Inverse(upper.rotation);
            if (step.y >= 0f) Debug.LogWarning("SlopeExtender：" + lower.name + " 没有比 " + upper.name + " 低，接出来可能不对。");
        }
        else if (!MeasureSlope(last, out step))
        {
            Debug.LogError("SlopeExtender：只找到一段坡，也量不出坡的方向。请把上下两段坡拖进 Pattern Upper / Pattern Lower。");
            return;
        }

        int start = CountExtras() + 1;
        Transform prev = last;
        Vector3 curStep = step;
        Quaternion rot = last.rotation;
        for (int i = 0; i < extraPieces; i++)
        {
            GameObject copy = Instantiate(last.gameObject, last.parent);
            copy.name = ExtraName + (start + i);
            rot = turn * rot;
            copy.transform.SetPositionAndRotation(prev.position + curStep * (1f - overlap), rot);
            curStep = turn * curStep;
            copy.transform.localScale = last.localScale;
            foreach (var ex in copy.GetComponentsInChildren<SlopeExtender>(true)) DestroyImmediate(ex);
            if (snowMaterial != null)
                foreach (var c in copy.GetComponentsInChildren<Collider>(true)) c.sharedMaterial = snowMaterial;
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(copy, "Extend Slope");
#endif
            prev = copy.transform;
        }

        // 下一次再点就从最新的一段接着往下
        lastSlope = prev;
        if (upper != null && lower != null) { patternUpper = upper; patternLower = lower; }
        Debug.Log("SlopeExtender：在 " + last.name + " 后面接了 " + extraPieces + " 段雪坡，每段往前挪 " +
                  new Vector2(step.x, step.z).magnitude.ToString("0") + " 米、往下降 " + (-step.y).ToString("0") + " 米。记得 Ctrl+S 保存场景。", prev.gameObject);
        MarkDirty();
    }

    [ContextMenu("Remove Extra Slopes（删掉加的坡）")]
    public void RemoveExtras()
    {
        int n = 0;
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (t == null || !t.name.StartsWith(ExtraName)) continue;
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(t.gameObject);
#else
            DestroyImmediate(t.gameObject);
#endif
            n++;
        }
        lastSlope = null;
        patternUpper = null;
        patternLower = null;
        Debug.Log("SlopeExtender：删掉了 " + n + " 段加出来的雪坡。");
        MarkDirty();
    }

    int CountExtras()
    {
        int n = 0;
        foreach (var t in FindObjectsOfType<Transform>()) if (t.name.StartsWith(ExtraName)) n++;
        return n;
    }

    // 最低的两段坡（不算加出来的）：倒数第二段 = upper，最低一段 = lower
    void FindTwoLowest(out Transform upper, out Transform lower)
    {
        upper = null;
        lower = null;
        var list = new List<Transform>();
        foreach (var t in FindObjectsOfType<Transform>())
        {
            string n = t.name.ToLower();
            if (!n.StartsWith("slope") || t.name.StartsWith(ExtraName)) continue;
            if (t.parent != null && t.parent.name.ToLower().StartsWith("slope")) continue;
            if (t.GetComponentsInChildren<Renderer>().Length == 0) continue;
            list.Add(t);
        }
        list.Sort((a, b) => a.position.y.CompareTo(b.position.y));
        if (list.Count >= 2) { lower = list[0]; upper = list[1]; }
    }

    // 名字以 slope 开头、有模型的物体里，最低的那个（也就是最下面那段坡）
    Transform FindLowestSlope()
    {
        Transform best = null;
        float lowest = float.MaxValue;
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (!t.name.ToLower().StartsWith("slope")) continue;
            if (t.parent != null && t.parent.name.ToLower().StartsWith("slope")) continue;   // 只看最外层
            Renderer[] rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            if (b.min.y < lowest) { lowest = b.min.y; best = t; }
        }
        return best;
    }

    // 只有一段坡时的备用办法：最高的一圈点 → 最低的一圈点
    bool MeasureSlope(Transform slope, out Vector3 step)
    {
        step = Vector3.zero;
        var pts = new List<Vector3>();
        foreach (var mf in slope.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            if (!mf.name.ToLower().Contains("slope")) continue;   // 只量雪道本身（比如 realslope），不量山
            Matrix4x4 w = mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices) pts.Add(w.MultiplyPoint3x4(v));
        }
        if (pts.Count < 3) return false;
        float min = float.MaxValue, max = float.MinValue;
        foreach (var p in pts) { min = Mathf.Min(min, p.y); max = Mathf.Max(max, p.y); }
        float band = (max - min) * 0.02f;
        Vector3 top = Vector3.zero, bottom = Vector3.zero;
        int nt = 0, nb = 0;
        foreach (var p in pts)
        {
            if (p.y >= max - band) { top += p; nt++; }
            if (p.y <= min + band) { bottom += p; nb++; }
        }
        if (nt == 0 || nb == 0) return false;
        step = bottom / nb - top / nt;
        return step.y < 0f;
    }

    void MarkDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
