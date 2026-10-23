// @role Skis one body down the slope: board-nose steering, carving, braking, sticking to the snow
// @calls Gamemode GhostController
// @tune maxSpeed:float:20-200:Top speed before speedBoost
// @tune pushForce:float:5-40:Extra push when the board points downhill
// @tune brakeForce:float:5-50:Brake force when the board is sideways (S)
// @tune minSpeed:float:0-30:Lowest speed while pointing downhill
// @tune extraGravity:float:0-10:Extra gravity multiplier
// @tune speedBoost:float:1-3:Multiplies max speed, push and min speed
// @tune maxSteerAngle:float:20-90:How far A/D turn the nose, in degrees
// @tune edgeGrip:float:1-10:How hard the edges bite in a carve
// @tune carveSpeedRatio:float:0.5-1:Share of straight speed kept while carving
// @tune leanAngle:float:0-30:Visual lean into turns, in degrees
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SkierController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // 核心规则：人永远沿着"板头"方向滑（板子的长边方向）
    // 板头越对准下坡 → 越快；板子越横 → 越慢（侧滑刹车）
    //   W：板头转向正下坡（最快，直着冲）
    //   S：板子横过来（刹车）
    //   A：左边板头向前 → 往左前方滑
    //   D：右边板头向前 → 往右前方滑
    // 转弯是"刻滑"：速度方向跟着板头转，不会因为转弯把速度吃掉
    // ─────────────────────────────────────────────

    [Header("速度")]
    public float maxSpeed = 60f;          // 最高速度
    public float pushForce = 20f;         // 板头对准下坡时的额外推力
    public float brakeForce = 25f;        // 板子横过来时的刹车力
    public float minSpeed = 4f;           // 板头朝下时的最低速度（不会卡住）
    public float creepSpeed = 1.5f;       // 横板时慢慢往下蹭的速度
    public float extraGravity = 2f;       // 额外重力倍数（2 = 总共 3 倍重力）
    [Tooltip("整体加速：最高速度、推力、最低速度都乘上这个数。想更快就调大（1 = 原来的速度）")]
    public float speedBoost = 1.6f;

    [Header("板头方向")]
    public bool startSideways = true;     // 开局横板
    public float maxSteerAngle = 40f;     // A/D 把板头转到左/右前方多少度
    public float steerSpeed = 180f;       // A/D 转板最快速度：度/秒
    public float boardTurnSpeed = 360f;   // W/S 转板最快速度：度/秒
    [Tooltip("转板的柔和程度：越大越快到位，越小越柔")]
    public float turnEase = 6f;
    public bool invertSteering = false;   // A/D 反了就勾上
    [Tooltip("板刃抓地：速度方向跟上板头的快慢（越大转弯越利落）")]
    public float edgeGrip = 6f;
    [Tooltip("A/D 斜着滑时的速度 = 直滑速度 × 这个数（0.87 = 只慢一点点）")]
    [Range(0.5f, 1f)] public float carveSpeedRatio = 0.87f;

    [Header("滑溜")]
    [Tooltip("rider 身上没设物理材质的碰撞体，开局自动换成这个（不填 = 用 rider 自己的 snow，再没有就自动生成一个零摩擦的）")]
    public PhysicMaterial skiMaterial;
    [Tooltip("勾上：身上所有碰撞体（包括雪板）都换成零摩擦，不会被雪面“粘住”变慢。第一个 rider 身上带着有摩擦的材质，所以以前比动物慢")]
    public bool forceSlippery = true;

    [Header("下坡方向")]
    [Tooltip("不勾（推荐）：W 永远沿开局时的下坡方向直冲，不会被坡面带歪。勾上：'正下坡'会慢慢跟着脚下坡面走")]
    public bool followSlopeDir = false;
    public float slopeFollowSpeed = 1.5f;
    [Tooltip("勾上（推荐）：'正下坡'跟着整条雪道走（看周围十几米哪边最低，不看脚下的小坑小包），按 W 时板头一直朝着真正的山下")]
    public bool followMainSlope = true;
    [Tooltip("防侧滑：板头朝前时，横着往左右飘的速度多快被吃掉（越大越稳、越像在轨道上）")]
    public float sideGrip = 8f;
    [Tooltip("坡面角度变化的平滑程度：越小越稳（低面数的雪坡一格一格的，太跟手人会晃）")]
    public float normalSmooth = 8f;

    [Header("过坡缝保速")]
    [Tooltip("勾上：滑到两段坡的接缝、小坎时，被撞掉的速度会补回来")]
    public bool keepSpeedOnBumps = true;

    [Header("阻力")]
    public float glideDrag = 0.02f;       // 板头朝下：几乎没阻力
    public float brakeDrag = 0.8f;        // 横板：阻力大

    [Header("模型朝向校正")]
    [Tooltip("按 W 时板子的长边要顺着下坡。不对就试 0 / 90 / -90 / 180")]
    public float boardYawOffset = 90f;
    [Tooltip("转弯时身体往弯里倾斜的角度。倾斜方向反了就填负数，0 = 不倾斜")]
    public float leanAngle = 10f;
    [Tooltip("模型转动的平滑程度：越大越跟手，越小越柔")]
    public float visualSmooth = 15f;

    [Header("贴地")]
    [Tooltip("离地多远以内会被吸回坡面（碰撞体半高的倍数）")]
    public float snapRange = 0.6f;
    public float stickForce = 30f;
    [Tooltip("勾上：板子一直贴着雪面，过坡顶、小坎也不会飞起来")]
    public bool hugGround = true;
    [Tooltip("脚下这么多（按 1.6 米高的小狗算，会跟着小狗实际大小自动放大）以内有坡面，就直接贴回去")]
    public float hugDistance = 2.5f;
    [Tooltip("量坡面角度时，往板头前后各看多远（按 1.6 米高的小狗算，自动放大）")]
    public float boardHalfLength = 0.7f;

    [Header("幽灵附身")]
    [Tooltip("没被幽灵附身时：Parked = 刹车停在原地等你；Cruise = 自己左右摆着往下滑")]
    public FreeMode whenFree = FreeMode.Parked;
    [Tooltip("Cruise 时左右摆一次要几秒")]
    public float cruiseSwingTime = 3f;
    public enum FreeMode { Parked, Cruise }

    [Header("拖入 visual（必须拖）")]
    public Transform model;

    [Header("雪板碰撞：拖入雪板模型（Board），可不填")]
    public Transform boardMesh;

    [HideInInspector] public bool canControl = true;
    // 现在是不是被幽灵附身（只有被附身的那个听键盘）。由 GhostController 设置
    [HideInInspector] public bool possessed = true;
    public float CurrentSpeed => rb.velocity.magnitude;
    public bool IsGrounded => grounded;
    public float SteerAngle => boardYaw;
    public bool BoardSideways => Mathf.Abs(boardYaw) > 45f;
    // 给其他脚本用（比如 GhostController）：人物头顶的世界坐标、人物整体高度（米）
    public Vector3 HeadTop { get { UpdateBodyBounds(); return headTop; } }
    public float BodyHeight { get { UpdateBodyBounds(); return bodyHeight; } }
    // 身体（碰撞体）中心、脚底（板底）中间、速度、板头方向（水平）。rider 的中心点离身体很远，要用这些
    public Vector3 BodyCenter => ColCenter();
    // 和 BodyCenter 一样，但跟着画面插值走（镜头跟这个才丝滑；碰撞体的位置只在物理帧更新，直接跟会抖）
    public Vector3 SmoothCenter => rb != null ? transform.position + (ColCenter() - rb.position) : ColCenter();
    // 现在的最高速度（已经乘上 Speed Boost）
    public float MaxSpeedNow => maxSpeed * speedBoost;
    public Vector3 FeetPos { get { Bounds b = BodyBounds(); return new Vector3(b.center.x, b.min.y, b.center.z); } }
    public Vector3 Velocity => rb != null ? rb.velocity : Vector3.zero;
    public Vector3 Heading => baseDir.sqrMagnitude > 0.0001f ? NoseDir.normalized : transform.forward;

    Rigidbody rb;
    Collider col;
    Vector3 groundNormal = Vector3.up;
    bool grounded;
    Vector3 baseDir;      // 正下坡方向（水平）
    float boardYaw;       // 板头相对正下坡的角度：0 = 朝下，负 = 左前，正 = 右前，±90 = 横板
    float latchTarget;    // W/S 按下后要转到的角度
    bool latched;
    float lean;           // 当前倾斜角度（平滑过的）
    float lastTouchTime = -10f;   // 最后一次碰到地面（碰撞体真的贴着东西）的时间
    Vector3 touchNormal = Vector3.up;

    Vector3 headTop;
    float bodyHeight = 1.5f;
    int boundsFrame = -1;
    Renderer[] bodyRenderers;

    Collider[] bodyCols;      // rider 身上所有（不是 Trigger 的）碰撞体
    float S = 1f;             // 场景放大倍数：小狗实际身高 / 1.6 米
    float lastSlopeSpeed;     // 上一帧沿坡面的速度（过坡缝保速用）

    float cruisePhase;
    float mainSlopeTimer;
    Vector3 mainSlopeDir;

    Vector3 pivotLocal;
    Vector3 pivotOffset;
    BoxCollider boardBox;

    // 板头方向（水平）
    Vector3 NoseDir => Quaternion.AngleAxis(boardYaw, Vector3.up) * baseDir;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponentInChildren<Collider>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;   // 画面跟着物理插值，不会一顿一顿的
    }

    void Start()
    {
        CacheColliders();
        // 场景放大倍数：按小狗模型的实际高度算（1.6 米 = 1 倍）
        if (model != null)
        {
            Renderer[] rr = model.GetComponentsInChildren<Renderer>();
            if (rr.Length > 0)
            {
                Bounds bb = rr[0].bounds;
                foreach (Renderer r in rr) bb.Encapsulate(r.bounds);
                S = Mathf.Max(0.1f, bb.size.y / 1.6f);
            }
        }

        // 正下坡方向
        baseDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (CastGround(out RaycastHit hit, 10f))
        {
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, hit.normal);
            downhill.y = 0f;
            if (downhill.sqrMagnitude > 0.0001f) baseDir = downhill;
            groundNormal = hit.normal;
        }
        if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector3.forward;
        // (注：开局找地面之前先算好碰撞体，见 CacheColliders)
        baseDir.Normalize();

        boardYaw = startSideways ? 90f : 0f;
        cruisePhase = Random.Range(0f, 2f * Mathf.PI);

        // 人物中轴底部
        if (model != null)
        {
            Renderer[] rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                Vector3 p = new Vector3(b.center.x, b.min.y, b.center.z);
                pivotLocal = model.InverseTransformPoint(p);
                pivotOffset = p - transform.position;
            }
        }

        // 雪板碰撞体（可选）
        if (boardMesh != null)
        {
            GameObject go = new GameObject("BoardCollider");
            go.transform.SetParent(transform, false);
            boardBox = go.AddComponent<BoxCollider>();
            MeshFilter mf = boardMesh.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds mb = mf.sharedMesh.bounds;
                Vector3 s = mb.size;
                float minT = Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * 0.05f;
                s = new Vector3(Mathf.Max(s.x, minT), Mathf.Max(s.y, minT), Mathf.Max(s.z, minT));
                boardBox.center = mb.center;
                boardBox.size = s;
            }
            if (col != null) boardBox.sharedMaterial = col.sharedMaterial;
            SyncBoardCollider();
            CacheColliders();
        }

        MakeSlippery();
        mainSlopeDir = baseDir;
        if (followMainSlope) { Vector3 m = MainDownhill(); if (m != Vector3.zero) baseDir = mainSlopeDir = m; }
    }

    // 整条雪道往下的方向：看周围一圈 16 个方向、十几米开外的雪面，哪边最低。
    // 只看大致往前的方向（离现在的“正下坡”70 度以内），不会掉头
    Vector3 MainDownhill()
    {
        Vector3 c = ColCenter();
        float r = 12f * S;
        float bestDrop = 0f;
        Vector3 best = Vector3.zero;
        float baseY = RayDown(c, HalfHeight() + (hugDistance + 1f) * S, out RaycastHit me) ? me.point.y : c.y - HalfHeight();
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI / 8f;
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            if (baseDir.sqrMagnitude > 0.0001f && Vector3.Dot(dir, baseDir) < 0.34f) continue;
            if (!RayDown(c + dir * r + Vector3.up * 10f * S, 40f * S, out RaycastHit h)) continue;
            float drop = baseY - h.point.y + Vector3.Dot(dir, baseDir) * 0.05f * r;   // 差不多低时选接近原来方向的，免得来回抖
            if (drop > bestDrop) { bestDrop = drop; best = dir; }
        }
        return best;
    }

    // 没有物理材质的碰撞体摩擦是 0.6，碰到同样没材质的雪坡就被“粘住”
    void MakeSlippery()
    {
        if (forceSlippery)
        {
            var zero = new PhysicMaterial("SkiNoFriction");
            zero.dynamicFriction = 0f;
            zero.staticFriction = 0f;
            zero.bounciness = 0f;
            zero.frictionCombine = PhysicMaterialCombine.Minimum;
            zero.bounceCombine = PhysicMaterialCombine.Minimum;
            foreach (Collider c in bodyCols) c.sharedMaterial = zero;
            return;
        }
        PhysicMaterial m = skiMaterial;
        if (m == null && col != null) m = col.sharedMaterial;
        if (m == null)
            foreach (Collider c in bodyCols) if (c.sharedMaterial != null) { m = c.sharedMaterial; break; }
        if (m == null)
        {
            m = new PhysicMaterial("SkiSlippery");
            m.dynamicFriction = 0f;
            m.staticFriction = 0f;
            m.bounciness = 0f;
            m.frictionCombine = PhysicMaterialCombine.Minimum;
            m.bounceCombine = PhysicMaterialCombine.Minimum;
        }
        foreach (Collider c in bodyCols)
            if (c.sharedMaterial == null) c.sharedMaterial = m;
    }

    void Update()
    {
        if (!canControl) return;

        float v, h;
        if (possessed)
        {
            v = KeyInput.Vertical();
            h = KeyInput.Horizontal();
            if (invertSteering) h = -h;
        }
        else FreeInput(out v, out h);

        // W：板头转向正下坡
        if (v > 0.5f) { latchTarget = 0f; latched = true; }
        // S：板子横过来（往离得近的那一边转）
        else if (v < -0.5f) { latchTarget = boardYaw >= 0f ? 90f : -90f; latched = true; }

        if (Mathf.Abs(h) > 0.1f)
        {
            // A：左边板头向前；D：右边板头向前
            latched = false;
            float target = h < 0f ? -maxSteerAngle : maxSteerAngle;

            // 横板时板子两头都可以当板头：按哪边就让哪边的头转到前面
            if (target < 0f && boardYaw > 89f) boardYaw = -90f;
            if (target > 0f && boardYaw < -89f) boardYaw = 90f;

            boardYaw = EaseTowards(boardYaw, target, steerSpeed);
        }
        else if (latched)
        {
            boardYaw = EaseTowards(boardYaw, latchTarget, boardTurnSpeed);
            if (Mathf.Approximately(boardYaw, latchTarget)) latched = false;
        }
    }

    // 没被附身时的"自动按键"：Parked 一直按 S（横板刹车）；Cruise 左右交替按 A/D
    void FreeInput(out float v, out float h)
    {
        v = 0f; h = 0f;
        if (whenFree == FreeMode.Parked) { v = -1f; return; }
        float w = 2f * Mathf.PI / Mathf.Max(0.5f, cruiseSwingTime);
        h = Mathf.Sin(Time.time * w + cruisePhase) >= 0f ? 1f : -1f;
    }

    // 柔和地转：离目标远时快，快到时慢下来（不会"咔"一下停住）
    float EaseTowards(float current, float target, float maxRate)
    {
        float rate = Mathf.Min(maxRate, Mathf.Abs(target - current) * turnEase + 20f);
        return Mathf.MoveTowards(current, target, rate * Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (rb.isKinematic) return;   // 已经停好了（或者游戏结束）
        float dt = Time.fixedDeltaTime;
        rb.AddForce(Physics.gravity * extraGravity, ForceMode.Acceleration);

        bool rayHit = CastGround(out RaycastHit hit, 1f + snapRange);
        // 射线没打到，但碰撞体刚刚还贴着坡面 → 也算在地上（避免被误判成"腾空太久"）
        bool touching = Time.time - lastTouchTime < 0.2f;

        // 贴地：脚下 hugDistance 以内有坡面 → 把人直接放回坡面上
        float gap = 0f;
        bool hugHit = false;
        if (hugGround && RayDown(ColCenter(), HalfHeight() + hugDistance * S, out RaycastHit far))
        {
            gap = far.distance - HalfHeight();
            hugHit = true;
            if (!rayHit) { hit = far; rayHit = true; }
        }

        grounded = rayHit || touching || hugHit;
        Vector3 targetNormal = rayHit ? SampleNormal(hit.normal) : (touching ? touchNormal : Vector3.up);
        groundNormal = Vector3.Slerp(groundNormal, targetNormal, 1f - Mathf.Exp(-normalSmooth * dt));

        if (grounded)
        {
            float away = Vector3.Dot(rb.velocity, groundNormal);
            if (away > 0f) rb.velocity -= groundNormal * away;
            if (hugHit && gap > 0.01f * S) rb.position += Vector3.down * gap;   // 飘起来了 → 贴回雪面
            rb.AddForce(-groundNormal * stickForce, ForceMode.Acceleration);

            // "正下坡"跟着整条雪道慢慢转（每隔一会儿看一次周围哪边最低）
            if (followMainSlope)
            {
                mainSlopeTimer -= dt;
                if (mainSlopeTimer <= 0f)
                {
                    mainSlopeTimer = 0.3f;
                    Vector3 m = MainDownhill();
                    if (m != Vector3.zero) mainSlopeDir = m;
                }
                baseDir = Vector3.Slerp(baseDir, mainSlopeDir, 1f - Mathf.Exp(-slopeFollowSpeed * dt)).normalized;
            }
            // "正下坡"慢慢跟着真实坡面走：W 才是真正顺着坡直冲，A/D 左右才对称
            else if (followSlopeDir)
            {
                Vector3 dh = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
                dh.y = 0f;
                if (dh.sqrMagnitude > 0.0004f)
                    baseDir = Vector3.Slerp(baseDir, dh.normalized, 1f - Mathf.Exp(-slopeFollowSpeed * dt)).normalized;
            }
        }

        if (!canControl) return;

        // 没被附身、Parked：刹车停下，停稳后原地固定住，等幽灵回来
        if (!possessed && whenFree == FreeMode.Parked)
        {
            rb.velocity = Vector3.MoveTowards(rb.velocity, Vector3.zero, 8f * S * dt);
            if (grounded && rb.velocity.magnitude < 0.3f * S)
            {
                rb.velocity = Vector3.zero;
                rb.isKinematic = true;
            }
            lastSlopeSpeed = 0f;
            return;
        }

        Vector3 nose = Vector3.ProjectOnPlane(NoseDir, groundNormal).normalized;   // 板头方向（贴坡）
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

        float facing = Mathf.Cos(boardYaw * Mathf.Deg2Rad);   // 1 = 板头朝下，0 = 横板
        // 刹车只在板子明显横过来（超过约 50°）时才起作用，A/D 的 ±40° 不会被刹车拖慢
        float brake = Mathf.InverseLerp(0.35f, 1f, 1f - facing);
        brake *= brake;

        if (grounded)
        {
            Vector3 vel = rb.velocity;
            Vector3 vNormal = Vector3.Project(vel, groundNormal);
            Vector3 vSlope = vel - vNormal;
            float speed = vSlope.magnitude;

            // ⓪ 过坡缝保速：一帧里速度突然掉了一成以上（被坡缝、小坎卡了一下）→ 补回来
            //    按 S 刹车、阻力都是慢慢减速，不会被这里误补
            if (keepSpeedOnBumps && facing > 0.5f && speed > 0.05f && speed < lastSlopeSpeed * 0.9f)
            {
                speed = lastSlopeSpeed * 0.98f;
                vSlope = vSlope.normalized * speed;
                rb.velocity = vNormal + vSlope;
            }

            // ① 刻滑：速度方向跟着板头转，速度大小保留（转弯不掉速）
            //    板子越横，抓地越弱 → 变成侧滑
            float grip = edgeGrip * Mathf.Clamp01(facing * 1.5f);
            if (speed > 0.05f && grip > 0f)
            {
                Vector3 turned = Vector3.RotateTowards(vSlope, nose * speed, grip * dt, 0f);
                rb.velocity = vNormal + turned;
            }

            // ①′ 防侧滑：板头朝前时，把横着往左右飘的速度吃掉，板头指哪就往哪滑，不会乱飘
            if (facing > 0.6f && sideGrip > 0f)
            {
                Vector3 side = Vector3.Cross(groundNormal, nose).normalized;
                float sideV = Vector3.Dot(rb.velocity, side);
                rb.velocity -= side * sideV * (1f - Mathf.Exp(-sideGrip * dt));
            }

            // ② 不会倒着滑
            float along = Vector3.Dot(rb.velocity, nose);
            if (along < 0f && facing > 0.3f) rb.AddForce(nose * -along * edgeGrip, ForceMode.Acceleration);

            // ③ 推力：W 直滑最快；A/D 斜滑保持直滑的 carveSpeedRatio（默认 0.87）
            Vector3 gTotal = Physics.gravity * (1f + extraGravity);
            if (CarveFraction() >= 0f)
            {
                float r = CarveRatio();
                float straightAccel = pushForce * speedBoost + Vector3.Dot(gTotal, downhill);   // 正下坡时的加速度
                float naturalAccel = Vector3.Dot(gTotal, nose);                    // 斜着时重力本来给的
                float push = r * straightAccel - naturalAccel;
                if (push > 0f) rb.AddForce(nose * push, ForceMode.Acceleration);
            }
            else
            {
                rb.AddForce(nose * pushForce * speedBoost * facing, ForceMode.Acceleration);
            }

            // ④ 板子越横，刹车越强
            if (speed > creepSpeed)
                rb.AddForce(-vSlope.normalized * brakeForce * brake, ForceMode.Acceleration);

            // ⑤ 保底：板头朝下时不会卡住；横板时慢慢往下蹭
            if (facing > 0.5f)
            {
                if (along < minSpeed * speedBoost) rb.AddForce(nose * minSpeed * speedBoost * 3f, ForceMode.Acceleration);
            }
            else if (speed < creepSpeed)
            {
                rb.AddForce(downhill * creepSpeed * 3f, ForceMode.Acceleration);
            }
        }

        rb.drag = Mathf.Lerp(glideDrag, brakeDrag, brake);

        // 速度上限：斜滑时上限也是直滑的 carveSpeedRatio 倍（柔和地降下来，不会一下顿住）
        float top = MaxSpeedNow;
        float cap = CarveFraction() >= 0f ? top * CarveRatio() : top;
        float spd = rb.velocity.magnitude;
        if (spd > top) { rb.velocity = rb.velocity.normalized * top; spd = top; }
        if (spd > cap) rb.velocity = rb.velocity.normalized * Mathf.MoveTowards(spd, cap, 8f * S * dt);

        lastSlopeSpeed = grounded ? Vector3.ProjectOnPlane(rb.velocity, groundNormal).magnitude : 0f;
    }

    // A/D 转了多少：0 = 正下坡，1 = 转到 maxSteerAngle；-1 = 正在横板/刹车（不算斜滑）
    float CarveFraction()
    {
        float a = Mathf.Abs(boardYaw);
        if (a > maxSteerAngle + 0.5f) return -1f;
        return Mathf.Clamp01(a / Mathf.Max(1f, maxSteerAngle));
    }

    float CarveRatio()
    {
        return Mathf.Lerp(1f, carveSpeedRatio, CarveFraction());
    }

    // 在板头前后、左右各打一条射线，用这四个点算出坡面角度 → 板子整条和雪面相切
    Vector3 SampleNormal(Vector3 fallback)
    {
        Vector3 c = ColCenter();
        float d = HalfHeight() + (hugDistance + 1f) * S;
        Vector3 fwd = NoseDir.normalized * boardHalfLength * S;
        Vector3 right = Vector3.Cross(Vector3.up, NoseDir).normalized * 0.25f * S;
        if (RayDown(c + fwd, d, out RaycastHit a) && RayDown(c - fwd, d, out RaycastHit b) &&
            RayDown(c + right, d, out RaycastHit e) && RayDown(c - right, d, out RaycastHit g))
        {
            Vector3 n = Vector3.Cross(a.point - b.point, e.point - g.point);
            if (n.y < 0f) n = -n;
            if (n.sqrMagnitude > 0.0001f)
            {
                n.Normalize();
                if (n.y > 0.3f) return n;
            }
        }
        return fallback;
    }

    void LateUpdate()
    {
        if (model == null) return;
        Vector3 nose = Vector3.ProjectOnPlane(NoseDir, groundNormal);
        if (nose.sqrMagnitude < 0.001f) return;
        nose.Normalize();

        // 转弯倾斜：A/D 转多少，身体就往弯里倾多少
        float steer = Mathf.Abs(boardYaw) <= maxSteerAngle + 1f ? boardYaw / Mathf.Max(1f, maxSteerAngle) : 0f;
        float k = 1f - Mathf.Exp(-visualSmooth * Time.deltaTime);
        lean = Mathf.Lerp(lean, -steer * leanAngle, k);

        // 板子长边对准板头方向、贴合坡面
        Quaternion rot = Quaternion.AngleAxis(lean, nose)
                       * Quaternion.AngleAxis(boardYawOffset, groundNormal)
                       * Quaternion.LookRotation(nose, groundNormal);
        model.rotation = Quaternion.Slerp(model.rotation, rot, k);

        // 绕中轴底部原地转
        Vector3 current = model.TransformPoint(pivotLocal);
        model.position += (transform.position + pivotOffset) - current;

        SyncBoardCollider();
    }

    // ───────── 幽灵附身用 ─────────

    // 附身 / 离开。离开后按 When Free 停下或自己滑
    public void Possess(bool on)
    {
        possessed = on;
        latched = false;
        if (on && rb != null && rb.isKinematic && canControl) rb.isKinematic = false;
    }

    // 把整个 rider 搬过去：脚底（板底）落在 feet，速度 = velocity，板头转向速度方向
    public void TeleportFeetTo(Vector3 feet, Vector3 velocity)
    {
        if (rb.isKinematic && canControl) rb.isKinematic = false;
        Vector3 delta = feet + Vector3.up * 0.05f * S - FeetPos;
        transform.position += delta;
        rb.position = transform.position;
        if (model != null && model.parent != transform) model.position += delta;
        Physics.SyncTransforms();
        rb.velocity = velocity;

        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        if (flat.sqrMagnitude > 0.01f && baseDir.sqrMagnitude > 0.0001f)
        {
            float yaw = Vector3.SignedAngle(baseDir, flat.normalized, Vector3.up);
            boardYaw = Mathf.Clamp(yaw, -maxSteerAngle, maxSteerAngle);
        }
        latched = false;
        groundNormal = Vector3.up;
        lastSlopeSpeed = 0f;
        lastTouchTime = -10f;
    }

    // 撞到东西 / 穿过终点线：交给 Gamemode 判断（只算被附身的那个）
    void OnCollisionEnter(Collision c)
    {
        if (possessed && Gamemode.Instance != null) Gamemode.Instance.SkierHit(this, c.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        if (possessed && Gamemode.Instance != null) Gamemode.Instance.SkierTrigger(this, other);
    }

    // 碰撞体贴着坡面时记下来（hitbox、雪板碰撞体都会触发）
    void OnCollisionStay(Collision c)
    {
        for (int i = 0; i < c.contactCount; i++)
        {
            Vector3 n = c.GetContact(i).normal;
            if (n.y > 0.4f)   // 朝上的面才算地面（撞到树侧面不算）
            {
                lastTouchTime = Time.time;
                touchNormal = n;
                return;
            }
        }
    }

    // 用 visual 下所有模型的包围盒算出头顶位置和身高（每帧只算一次）
    void UpdateBodyBounds()
    {
        if (boundsFrame == Time.frameCount) return;
        boundsFrame = Time.frameCount;
        if (model == null)
        {
            Bounds cb = BodyBounds();
            bodyHeight = cb.size.y;
            headTop = new Vector3(cb.center.x, cb.max.y, cb.center.z);
            return;
        }
        if (bodyRenderers == null || bodyRenderers.Length == 0) bodyRenderers = model.GetComponentsInChildren<Renderer>();
        if (bodyRenderers.Length == 0) { headTop = model.position + Vector3.up * bodyHeight; return; }
        Bounds b = bodyRenderers[0].bounds;
        foreach (Renderer r in bodyRenderers) if (r != null) b.Encapsulate(r.bounds);
        bodyHeight = b.size.y;
        headTop = new Vector3(b.center.x, b.max.y, b.center.z);
    }

    void SyncBoardCollider()
    {
        if (boardBox == null || boardMesh == null) return;
        boardBox.transform.SetPositionAndRotation(boardMesh.position, boardMesh.rotation);
        boardBox.transform.localScale = boardMesh.lossyScale;
    }

    // 往下找地面。会跳过自己身上的碰撞体（hitbox、雪板碰撞体），
    // 以前射线会打到自己的雪板，把板子当成地面，导致方向歪掉
    bool CastGround(out RaycastHit hit, float rangeMul)
    {
        return RayDown(ColCenter(), HalfHeight() * (1f + rangeMul) + 0.3f * S, out hit);
    }

    void CacheColliders()
    {
        var list = new System.Collections.Generic.List<Collider>();
        foreach (Collider c in GetComponentsInChildren<Collider>())
            if (c.enabled && !c.isTrigger) list.Add(c);
        bodyCols = list.ToArray();
    }

    // 把 rider 身上所有碰撞体合在一起算：中心、半高（底部 = 板底/脚底）
    Bounds BodyBounds()
    {
        if (bodyCols == null || bodyCols.Length == 0) CacheColliders();
        bool first = true;
        Bounds b = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
        foreach (Collider c in bodyCols)
        {
            if (c == null || !c.enabled) continue;
            if (first) { b = c.bounds; first = false; }
            else b.Encapsulate(c.bounds);
        }
        return b;
    }

    Vector3 ColCenter() { return BodyBounds().center; }
    float HalfHeight() { return BodyBounds().extents.y; }

    bool RayDown(Vector3 origin, float distance, out RaycastHit hit)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance,
                                               ~0, QueryTriggerInteraction.Ignore);
        hit = new RaycastHit();
        bool found = false;
        float best = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.attachedRigidbody == rb) continue;
            if (h.distance < best) { best = h.distance; hit = h; found = true; }
        }
        return found;
    }
}

// 键盘读取：新旧输入系统都能用
public static class KeyInput
{
    public static float Vertical()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return 0f;
        return (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
             - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
#else
        return Input.GetAxisRaw("Vertical");
#endif
    }

    public static float Horizontal()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return 0f;
        return (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
             - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
#else
        return Input.GetAxisRaw("Horizontal");
#endif
    }

    public static bool PossessPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    public static bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}
