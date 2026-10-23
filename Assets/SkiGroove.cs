// @role Procedural ski animation for every animal: bounce, sway, lean, arm swing, tail wag, ear flop
// @calls SkiingDogSpawner
// @tune strength:float:0-3:Overall strength
// @tune bounce:float:0-0.2:Up-down bounce
// @tune sway:float:0-20:Side sway in degrees
// @tune maxLean:float:0-45:Max lean into a turn in degrees
// @tune armSwing:float:0-40:Arm or wing swing in degrees
// @tune tailWag:float:0-60:Tail wag in degrees
// @tune earFlop:float:0-40:Ear flop in degrees
// SkiGroove（滑行律动）：让滑雪的小动物在滑的时候有动作。
// 用法：把这个脚本拖到场景里任意一个物体上（比如挂 SkiingDogSpawner 的那个），点 Play 就行。
// 它自己会找场景里所有滑雪的小动物（玩家附身的那只、山上的小狗 / 小鸡 / 腊肠狗 / 巴哥都算），
// 每一帧只动它们身上的 Body 和 Head，不碰滑雪板、不碰碰撞体、不改别的脚本。
// 动作：身体随节奏一蹲一起、左右晃，转弯时往弯里倾，头稳住并看向转弯方向，
//      手 / 翅膀张开保持平衡，尾巴摇，耳朵被风吹得一甩一甩。停下来时只轻轻呼吸。
// 想关掉：把这个组件的勾去掉，小动物会马上恢复原样。
using System.Collections.Generic;
using UnityEngine;

public class SkiGroove : MonoBehaviour
{
    [Header("整体")]
    [Tooltip("所有动作的总强度。1 = 默认，0.5 = 轻一点，2 = 夸张")]
    [Range(0f, 3f)] public float strength = 1f;
    [Tooltip("滑到这个速度（每秒几个身高）就算“很快”，动作节奏最快、蹲得最低")]
    public float fastSpeed = 8f;

    [Header("节奏")]
    [Tooltip("慢慢滑时一秒蹲起几下")]
    public float slowBeat = 1.1f;
    [Tooltip("滑得很快时一秒蹲起几下")]
    public float fastBeat = 2f;
    [Tooltip("蹲起的幅度（0.06 = 身高的 6%）")]
    public float bounce = 0.06f;
    [Tooltip("滑得快时额外蹲低多少")]
    public float tuck = 0.05f;
    [Tooltip("左右晃身体的角度（度）")]
    public float sway = 5f;
    [Tooltip("上半身左右扭的角度（度）")]
    public float twist = 6f;

    [Header("转弯")]
    [Tooltip("转弯时往弯里倾斜：每秒转 1 度，身体倾斜几度")]
    public float leanPerTurn = 0.25f;
    [Tooltip("最多倾斜几度")]
    public float maxLean = 20f;
    [Tooltip("头看向转弯方向，最多几度")]
    public float headLook = 18f;

    [Header("小部件")]
    [Tooltip("手臂 / 翅膀张开保持平衡的角度（度）")]
    public float armSwing = 12f;
    [Tooltip("尾巴摇的角度（度）")]
    public float tailWag = 20f;
    [Tooltip("耳朵甩的角度（度）")]
    public float earFlop = 12f;
    [Tooltip("点头的角度（度）")]
    public float headNod = 4f;

    [Tooltip("多久找一次新出来的小动物（秒）")]
    public float scanEvery = 1f;

    struct Piece
    {
        public Transform t;
        public Vector3 pos;
        public Quaternion rot;
    }

    class Rig
    {
        public Transform model, body, head;
        public Vector3 bodyPos, bodyScale, headPos;
        public Quaternion bodyRot, headRot;
        public List<Piece> tail = new List<Piece>();
        public List<Piece> armL = new List<Piece>(), armR = new List<Piece>();
        public List<Piece> earL = new List<Piece>(), earR = new List<Piece>();
        public Vector3 tailPivot, pivotL, pivotR;
        public Vector3 lastPos, dir = Vector3.forward;
        public bool hasLast;
        public float speed, turn, lean, phase, seed;
    }

    readonly List<Rig> rigs = new List<Rig>();
    readonly HashSet<Transform> known = new HashSet<Transform>();
    float scanTimer;

    void OnEnable() { scanTimer = 0f; }

    void OnDisable()
    {
        foreach (var r in rigs) Restore(r);
        rigs.Clear();
        known.Clear();
    }

    // ---------------- 找小动物 ----------------

    // 每只滑雪的小动物（包括玩家）身上都有 Rigidbody，模型在它下面几层，叫 XxxModel，里面有 Body
    void Scan()
    {
        rigs.RemoveAll(r => r.body == null || r.model == null);
        known.RemoveWhere(t => t == null);
        foreach (var rb in FindObjectsOfType<Rigidbody>())
            Search(rb.transform, 0);
    }

    void Search(Transform t, int depth)
    {
        if (t.name == "Body" && t.parent != null && t.parent.name.EndsWith("Model"))
        {
            if (!known.Contains(t) && IsSkier(t.parent)) Add(t);
            return;
        }
        if (depth >= 5) return;
        for (int i = 0; i < t.childCount; i++) Search(t.GetChild(i), depth + 1);
    }

    // 只要脚下有板子 / 滑雪板的（开场那个登山者的身体不算）
    static bool IsSkier(Transform model)
    {
        if (model.name == "ClimberModel") return false;
        Transform m = model;
        for (int up = 0; up < 2 && m != null; up++, m = m.parent)   // 自己和上一层（山上的小狗，滑雪板挂在外面那层）
            for (int i = 0; i < m.childCount; i++)
            {
                string n = m.GetChild(i).name;
                if (n.StartsWith("Board") || n.StartsWith("Binding") || n.StartsWith("Ski")) return true;
            }
        return false;
    }

    void Add(Transform body)
    {
        known.Add(body);
        var r = new Rig
        {
            model = body.parent,
            body = body,
            bodyPos = body.localPosition,
            bodyRot = body.localRotation,
            bodyScale = body.localScale,
            phase = Random.value * 10f,
            seed = Random.value * 10f,
        };
        r.lastPos = r.model.position;

        Transform head = r.model.Find("Head");   // 头和身体是兄弟时，头要跟着身体一起倾斜
        if (head != null)
        {
            r.head = head;
            r.headPos = head.localPosition;
            r.headRot = head.localRotation;
            CollectEars(head, r);
        }

        Vector3 shoulderL = Vector3.zero, shoulderR = Vector3.zero;
        bool hasL = false, hasR = false;
        for (int i = 0; i < body.childCount; i++)
        {
            Transform c = body.GetChild(i);
            string n = c.name;
            var p = new Piece { t = c, pos = c.localPosition, rot = c.localRotation };
            char side = Side(n);
            if (n.StartsWith("Tail"))
            {
                if (r.tail.Count == 0) r.tailPivot = p.pos;
                r.tail.Add(p);
            }
            else if (n.StartsWith("Shoulder") && !n.Contains("Fluff") && side != ' ')
            {
                if (side == 'L') { shoulderL = p.pos; hasL = true; } else { shoulderR = p.pos; hasR = true; }
            }
            else if (side != ' ' && (n.StartsWith("Arm") || n.StartsWith("Paw") || n.StartsWith("Claw") || n.StartsWith("Wing")))
            {
                (side == 'L' ? r.armL : r.armR).Add(p);
            }
        }
        if (r.head == null) CollectEars(body, r);
        r.pivotL = hasL ? shoulderL : (r.armL.Count > 0 ? r.armL[0].pos : Vector3.zero);
        r.pivotR = hasR ? shoulderR : (r.armR.Count > 0 ? r.armR[0].pos : Vector3.zero);
        rigs.Add(r);
    }

    static void CollectEars(Transform parent, Rig r)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (!c.name.StartsWith("Ear")) continue;
            char side = Side(c.name);
            if (side == ' ') continue;
            (side == 'L' ? r.earL : r.earR).Add(new Piece { t = c, pos = c.localPosition, rot = c.localRotation });
        }
    }

    // 名字结尾（去掉数字）是 L 还是 R。前后腿（FL、BR 这种）不算手
    static char Side(string n)
    {
        int end = n.Length;
        while (end > 0 && char.IsDigit(n[end - 1])) end--;
        if (end < 2) return ' ';
        char last = n[end - 1];
        if (last != 'L' && last != 'R') return ' ';
        char before = n[end - 2];
        if (before == 'F' || before == 'B') return ' ';
        return last;
    }

    // ---------------- 每一帧动起来 ----------------

    void LateUpdate()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f) { scanTimer = Mathf.Max(0.2f, scanEvery); Scan(); }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        foreach (var r in rigs)
        {
            if (r.body == null || r.model == null) continue;
            if (!r.body.gameObject.activeInHierarchy) { r.hasLast = false; continue; }
            Animate(r, dt);
        }
    }

    static float Smooth(float a, float b, float rate, float dt) { return Mathf.Lerp(a, b, 1f - Mathf.Exp(-rate * dt)); }

    void Animate(Rig r, float dt)
    {
        // 先算它滑得多快、往哪转（用身高当单位，场景放大 19 倍也一样）
        float size = Mathf.Max(0.0001f, Mathf.Abs(r.model.lossyScale.y));
        Vector3 p = r.model.position;
        Vector3 v = r.hasLast ? (p - r.lastPos) / dt : Vector3.zero;
        r.lastPos = p;
        r.hasLast = true;
        Vector3 flat = Vector3.ProjectOnPlane(v, Vector3.up);
        float spd = flat.magnitude / size;
        float turnNow = 0f;
        if (spd > 60f) spd = r.speed;                 // 一帧跳太远是被传送了（附身、回收），不算
        else if (spd > 0.3f)
        {
            Vector3 d = flat.normalized;
            turnNow = Mathf.Clamp(Vector3.SignedAngle(r.dir, d, Vector3.up) / dt, -180f, 180f);
            r.dir = d;
        }
        r.speed = Smooth(r.speed, spd, 4f, dt);
        r.turn = Smooth(r.turn, turnNow, 5f, dt);

        float moving = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 2f, r.speed));
        float fast = Mathf.Clamp01(r.speed / Mathf.Max(0.1f, fastSpeed));
        float k = strength;

        // 节奏：滑得越快越密；停下来只慢慢呼吸
        float beat = Mathf.Lerp(0.5f, Mathf.Lerp(slowBeat, fastBeat, fast), moving);
        r.phase += dt * beat * Mathf.PI * 2f;
        float s1 = Mathf.Sin(r.phase);                       // 蹲起
        float s2 = Mathf.Sin(r.phase * 0.5f + r.seed);       // 左右晃（慢一半）
        float down = 0.5f + 0.5f * s1;

        // 往弯里倾：绕着滑行方向转
        float leanTarget = Mathf.Clamp(-r.turn * leanPerTurn, -maxLean, maxLean) * moving;
        r.lean = Smooth(r.lean, leanTarget, 6f, dt);
        Vector3 travel = r.model.InverseTransformDirection(r.dir);
        travel.y = 0f;
        travel = travel.sqrMagnitude > 0.0001f ? travel.normalized : Vector3.forward;
        float roll = (r.lean + sway * s2 * moving) * k;
        Quaternion delta = Quaternion.AngleAxis(roll, travel)
                         * Quaternion.AngleAxis(twist * s2 * moving * k, Vector3.up);

        // 蹲：从脚底往下压，所以脚一直踩在板子上
        float squash = (Mathf.Lerp(0.015f, bounce, moving) * down + tuck * fast * moving) * k;
        squash = Mathf.Clamp(squash, 0f, 0.4f);
        Vector3 sc = new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f);

        r.body.localRotation = delta * r.bodyRot;
        r.body.localPosition = r.bodyPos;
        r.body.localScale = Vector3.Scale(r.bodyScale, sc);

        // 头：跟着身体走，但自己稳住一点，看向转弯方向，随节奏轻轻点头
        if (r.head != null)
        {
            r.head.localPosition = r.bodyPos + delta * Vector3.Scale(r.headPos - r.bodyPos, sc);
            float look = Mathf.Clamp(r.turn * 0.4f, -headLook, headLook) * moving * k;
            r.head.localRotation = delta
                                 * Quaternion.AngleAxis(-roll * 0.6f, travel)
                                 * Quaternion.AngleAxis(look, Vector3.up)
                                 * Quaternion.AngleAxis(headNod * Mathf.Sin(r.phase - 0.8f) * moving * k, Vector3.right)
                                 * r.headRot;
        }

        // 手 / 翅膀：张开保持平衡，身体往哪边歪，另一边的手抬高
        float bodyRollZ = roll * Vector3.Dot(travel, Vector3.forward);
        float raise = armSwing * moving * k * (0.6f + 0.4f * Mathf.Sin(r.phase + 1.5f));
        Turn(r.armL, r.pivotL, Quaternion.AngleAxis(-raise + bodyRollZ * 0.8f, Vector3.forward));
        Turn(r.armR, r.pivotR, Quaternion.AngleAxis(raise + bodyRollZ * 0.8f, Vector3.forward));

        // 尾巴：一直摇，滑起来摇得更欢
        float wag = tailWag * k * (0.3f + 0.7f * moving) * Mathf.Sin(r.phase * 2f + r.seed);
        Turn(r.tail, r.tailPivot, Quaternion.AngleAxis(wag, Vector3.up));

        // 耳朵：被风往后吹，随节奏甩
        float flop = earFlop * k * moving * Mathf.Sin(r.phase - 1f);
        float blow = -earFlop * k * fast;
        foreach (var e in r.earL) e.t.localRotation = Quaternion.AngleAxis(flop, Vector3.forward) * Quaternion.AngleAxis(blow, Vector3.right) * e.rot;
        foreach (var e in r.earR) e.t.localRotation = Quaternion.AngleAxis(-flop, Vector3.forward) * Quaternion.AngleAxis(blow, Vector3.right) * e.rot;
    }

    // 一组零件绕同一个点一起转（比如手臂、爪子、爪尖绕肩膀）
    static void Turn(List<Piece> parts, Vector3 pivot, Quaternion q)
    {
        foreach (var p in parts)
        {
            if (p.t == null) continue;
            p.t.localPosition = pivot + q * (p.pos - pivot);
            p.t.localRotation = q * p.rot;
        }
    }

    static void Restore(Rig r)
    {
        if (r.body != null)
        {
            r.body.localPosition = r.bodyPos;
            r.body.localRotation = r.bodyRot;
            r.body.localScale = r.bodyScale;
        }
        if (r.head != null)
        {
            r.head.localPosition = r.headPos;
            r.head.localRotation = r.headRot;
        }
        foreach (var list in new[] { r.tail, r.armL, r.armR, r.earL, r.earR })
            foreach (var p in list)
                if (p.t != null) { p.t.localPosition = p.pos; p.t.localRotation = p.rot; }
    }
}
