// @role The player is a ghost: press E near a skier or animal to possess it; runs the camera
// @calls SkierController SkiingDogSpawner Gamemode GhostFluid CameraFollow
// @tune possessRange:float:2-15:How close a target must be to possess, in body heights
// @tune ghostSize:float:0.3-1.5:Ghost size relative to the body
// @tune followSmooth:float:1-15:Camera follow smoothing
// @tune animalSpeed:float:0.2-1.5:Speed of a possessed animal relative to the rider
// @tune pullOutTime:float:0.1-1.5:Seconds the ghost takes to leave the body
// @tune flyTime:float:0.1-1:Seconds the ghost flies to the new body
// @tune slowMotion:float:0.05-1:Time scale during possession
// GhostController.cs —— 玩家是一个幽灵，可以附身在滑雪者身上控制他滑雪
// 靠近另一个滑雪者（场景里别的 rider，或者 SkiingDogSpawner 生成的滑雪小狗），他头顶出现发光小球和 [ E ] Possess，
// 按 E：慢动作，镜头拉远成第三人称，幽灵从现在的身体里飘出来悬在空中、转向目标，
// 然后飞快冲进新的滑雪者，镜头同时飞快拉到他的俯视跟随视角。
// 现在控制的角色头顶有一个小圆点。从按下 E 到附身成功，玩家撞到树也不会死（无敌）。
// 幽灵离开一只滑雪动物以后，它会回到生成器里，接着自己往下滑。
//
// 不用手动挂：Gamemode 开局会自动生成一个 GhostManager 物体并挂上它。
// 想改参数：Play 之前在 Hierarchy 里新建空物体挂上这个脚本，改好就行（Gamemode 看到已经有了就不会再生成）。
// 幽灵模型默认用 Assets/Ghost/Resources/ghost.obj；也可以把你自己的幽灵拖进 Ghost Model。

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostController : MonoBehaviour
{
    [Header("附身")]
    [Tooltip("开局幽灵在谁身上。留空 = 名字叫 rider 的那个")]
    public SkierController startSkier;
    [Tooltip("离多近能附身（几个身高）")]
    public float possessRange = 6f;
    [Tooltip("勾上：SkiingDogSpawner 生成的滑雪小狗也能附身（附身后和它交换身体）")]
    public bool possessSkiingDogs = true;

    [Header("幽灵模型")]
    [Tooltip("可不填：自动用 Resources 里的 ghost 模型。可以拖场景里的物体或者 Prefab")]
    public GameObject ghostModel;
    [Tooltip("幽灵大小（滑雪者身高的几倍）")]
    public float ghostSize = 0.7f;
    [Tooltip("悬空时在头顶上方多高（几个身高）")]
    public float hoverHeight = 0.8f;
    [Tooltip("幽灵的脸朝后就填 180")]
    public float ghostFacingOffset = 0f;
    [Tooltip("勾上：自动给幽灵换上卡通材质（黑身体、白眼睛）。想用自己的材质就取消")]
    public bool autoGhostMaterials = true;
    public Color ghostColor = new Color(0.08f, 0.08f, 0.10f, 1f);
    public Color eyeColor = Color.white;
    [Tooltip("可附身目标头顶小球的颜色")]
    public Color markerColor = new Color(0.45f, 0.95f, 1f, 0.85f);
    [Tooltip("现在控制的角色头顶小圆点的颜色")]
    public Color currentDotColor = new Color(1f, 0.93f, 0.55f, 1f);
    [Tooltip("小圆点多大（身高的几倍）")]
    public float currentDotSize = 0.1f;

    [Header("镜头")]
    [Tooltip("镜头跟随的紧密程度（开局会自动读取 maincamera 上 CameraFollow 的 Smooth Speed）")]
    public float followSmooth = 5f;
    [Tooltip("附身时镜头往后拉远到平时距离的几倍（镜头方向不变，只是往后退）")]
    public float pullOutDistance = 3f;

    [Header("速度感")]
    [Tooltip("滑得越快，视野（FOV）越宽，最多加这么多度。0 = 关掉")]
    public float speedFovBoost = 0f;
    [Tooltip("滑得越快，镜头往后退多一点（平时距离的几倍）")]
    public float speedPullBack = 0f;
    [Tooltip("高速时镜头轻微抖动的幅度（身高的几倍）。0 = 不抖")]
    public float speedShake = 0f;
    [Tooltip("高速时板子后面扬起的雪雾。不勾 = 关掉")]
    public bool snowSpray = true;

    [Header("附身动物的速度")]
    [Tooltip("附身到滑雪动物身上时，速度是第一个 rider 的几倍。1 = 一样快，0.7 = 慢三成")]
    [Range(0.2f, 1.5f)] public float animalSpeed = 0.7f;

    [Header("附身动画")]
    [Tooltip("镜头拉远、幽灵飘出来要几秒")]
    public float pullOutTime = 0.5f;
    [Tooltip("幽灵悬空转向目标要几秒")]
    public float hoverTime = 0.45f;
    [Tooltip("幽灵冲进新身体、镜头拉过去要几秒")]
    public float flyTime = 0.35f;
    [Tooltip("动画时的慢动作：越小越慢")]
    [Range(0.05f, 1f)] public float slowMotion = 0.2f;

    // 幽灵现在在谁身上
    public static SkierController Current { get; private set; }
    // 附身过程中（按下 E 到附身成功）和刚附身的一小会儿不判撞（无敌）
    public static bool InGrace => possessing || Time.time < graceUntil;
    static bool possessing;
    static float graceUntil;

    struct Target
    {
        public SkierController skier;
        public SkiingDogSpawner spawner;
        public int dog;
    }

    Camera cam;
    CameraFollow oldFollow;
    Transform ghost;
    float ghostBaseHeight = 1.55f;
    readonly List<Material> ghostMats = new List<Material>();
    Transform marker;
    Material markerMat;
    Transform currentDot;
    float baseFov = 60f;
    float speedK;          // 0 = 停着，1 = 最高速度（平滑过的）
    ParticleSystem spray;
    // 附身过的滑雪动物是从哪个生成器来的：幽灵离开后还给它，让它接着滑
    readonly Dictionary<SkierController, SkiingDogSpawner> dogOrigin = new Dictionary<SkierController, SkiingDogSpawner>();
    readonly HashSet<SkierController> animalBodies = new HashSet<SkierController>();   // 附身动物时加上的 SkierController
    float riderSpeedBoost = -1f;                                                        // 第一个 rider 原本的 Speed Boost

    SkierController[] skiers = new SkierController[0];
    SkiingDogSpawner[] spawners = new SkiingDogSpawner[0];
    float refreshTimer;
    bool collisionsDone;

    bool hasTarget;
    Target target;
    bool animating;
    // 镜头：平时和原来 CameraFollow 一样，保持开局时相对身体的位置和角度，永远不转
    Vector3 camOffset;
    Quaternion camRot;
    float camBaseHeight = 1f;
    bool camReady;
    float fixedBase;
    GUIStyle promptStyle;

    void Awake()
    {
        fixedBase = Time.fixedDeltaTime;
        Refresh();
        Current = FindStartSkier();
        foreach (var s in skiers) s.possessed = s == Current;
        if (Current == null) Debug.LogError("GhostController：场景里找不到 SkierController，幽灵没法附身。");
    }

    void Start()
    {
        cam = Camera.main;
        if (cam == null) cam = FindObjectOfType<Camera>();
        if (cam != null)
        {
            oldFollow = cam.GetComponent<CameraFollow>();
            if (oldFollow != null)
            {
                followSmooth = oldFollow.smoothSpeed;
                oldFollow.enabled = false;
            }
            if (Current != null)
            {
                camOffset = cam.transform.position - Current.BodyCenter;
                camRot = cam.transform.rotation;
                camBaseHeight = Height(Current);
                camReady = true;
            }
        }
        if (cam != null) baseFov = cam.fieldOfView;
        MakeGhost();
        MakeMarker();
        if (snowSpray) MakeSpray();
        currentDot = MakeDot("CurrentDot", currentDotColor);
    }

    void OnDestroy()
    {
        if (Current == null || Current.gameObject.scene == gameObject.scene) Current = null;
        if (animating) { Time.timeScale = 1f; Time.fixedDeltaTime = fixedBase; }
        possessing = false;
    }

    SkierController FindStartSkier()
    {
        if (startSkier != null) return startSkier;
        foreach (var s in skiers) if (s.gameObject.name == "rider") return s;
        if (Gamemode.Instance != null)
        {
            var own = Gamemode.Instance.GetComponent<SkierController>();
            if (own != null) return own;
        }
        return skiers.Length > 0 ? skiers[0] : null;
    }

    void Refresh()
    {
        skiers = FindObjectsOfType<SkierController>();
        spawners = possessSkiingDogs ? FindObjectsOfType<SkiingDogSpawner>() : new SkiingDogSpawner[0];
    }

    // ---------------- 每帧 ----------------

    void Update()
    {
        if (Current == null) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 1f;
            Refresh();
            if (!animating) foreach (var s in skiers) s.possessed = s == Current;
        }
        if (!collisionsDone) { collisionsDone = true; IgnoreSkierCollisions(); }

        bool playing = Gamemode.Instance == null || Gamemode.Instance.IsPlaying;
        if (animating)
        {
            if (!playing) CancelAnimation();
            if (marker != null) marker.gameObject.SetActive(false);
            return;
        }

        hasTarget = playing && FindTarget(out target);
        UpdateMarker();

        if (hasTarget && KeyInput.PossessPressed())
            StartCoroutine(PossessRoutine(target));
    }

    void LateUpdate()
    {
        UpdateCurrentDot();
        UpdateSpeedFeel();
        if (Current == null || !camReady || animating) return;
        float h = Height(Current);
        Vector3 want = Current.SmoothCenter + camOffset * (h / camBaseHeight) * (1f + speedPullBack * speedK);
        cam.transform.position = Vector3.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-followSmooth * Time.unscaledDeltaTime));
        // 高速时轻轻抖一下（只抖画面，不影响跟随）
        if (speedShake > 0f && speedK > 0.5f)
        {
            float t = Time.time * 25f;
            float amp = speedShake * h * (speedK - 0.5f) * 2f;
            cam.transform.position += cam.transform.right * (Mathf.PerlinNoise(t, 0f) - 0.5f) * amp
                                    + cam.transform.up * (Mathf.PerlinNoise(0f, t) - 0.5f) * amp;
        }
        cam.transform.rotation = camRot;
    }

    // 速度感：越快视野越宽、镜头退一点、板子后面扬雪
    void UpdateSpeedFeel()
    {
        float target = 0f;
        if (Current != null && !animating)
        {
            float top = Mathf.Max(1f, Current.MaxSpeedNow);
            target = Mathf.Clamp01(Current.Velocity.magnitude / top);
            target = target * target * (3f - 2f * target);
        }
        speedK = Mathf.Lerp(speedK, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
        if (cam != null && !animating) cam.fieldOfView = baseFov + speedFovBoost * speedK;

        if (spray == null) return;
        bool on = Current != null && !animating && Current.IsGrounded && speedK > 0.25f;
        var em = spray.emission;
        em.rateOverTime = on ? Mathf.Lerp(20f, 120f, speedK) : 0f;
        if (Current == null) return;
        float h = Height(Current);
        Vector3 back = -Current.Velocity;
        back.y = 0f;
        spray.transform.position = Current.SmoothCenter - Vector3.up * h * 0.45f;
        if (back.sqrMagnitude > 0.01f) spray.transform.rotation = Quaternion.LookRotation(back.normalized + Vector3.up * 0.6f);
        var main = spray.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f * h, 0.25f * h);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * h, 2f * h);
        main.gravityModifier = 0.4f;
    }

    void MakeSpray()
    {
        var go = new GameObject("SnowSpray");
        spray = go.AddComponent<ParticleSystem>();
        spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = spray.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startColor = new Color(1f, 1f, 1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var shape = spray.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.1f;
        var col = spray.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var em = spray.emission;
        em.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (sh != null) r.sharedMaterial = new Material(sh) { color = Color.white };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spray.Play();
    }

    // 平时镜头的位置：身体中心 + 开局时的偏移（新身体大小不同就按比例缩放）
    Vector3 FollowPos(Vector3 center, float h)
    {
        return center + camOffset * (h / camBaseHeight);
    }

    // ---------------- 找可以附身的目标 ----------------

    bool FindTarget(out Target best)
    {
        best = new Target { dog = -1 };
        Vector3 me = Current.BodyCenter;
        float h = Height(Current);
        float bestDist = possessRange * h;
        bool found = false;

        foreach (var s in skiers)
        {
            if (s == null || s == Current || !s.isActiveAndEnabled) continue;
            float d = Vector3.Distance(me, s.BodyCenter);
            if (d < bestDist) { bestDist = d; best = new Target { skier = s, dog = -1 }; found = true; }
        }
        foreach (var sp in spawners)
        {
            if (sp == null || !sp.isActiveAndEnabled) continue;
            for (int i = 0; i < sp.DogCount; i++)
            {
                if (!sp.DogInfo(i, out Vector3 feet, out _)) continue;
                float d = Vector3.Distance(me, feet + Vector3.up * sp.DogHeight * 0.5f);
                if (d < bestDist) { bestDist = d; best = new Target { spawner = sp, dog = i }; found = true; }
            }
        }
        return found;
    }

    bool Valid(Target t)
    {
        if (t.skier != null) return t.skier.isActiveAndEnabled;
        if (t.spawner != null) return t.spawner.DogInfo(t.dog, out _, out _);
        return false;
    }

    Vector3 Center(Target t)
    {
        if (t.skier != null) return t.skier.BodyCenter;
        t.spawner.DogInfo(t.dog, out Vector3 feet, out _);
        return feet + Vector3.up * t.spawner.DogHeight * 0.5f;
    }

    Vector3 HeadTop(Target t)
    {
        if (t.skier != null) return t.skier.HeadTop;
        t.spawner.DogInfo(t.dog, out Vector3 feet, out _);
        return feet + Vector3.up * t.spawner.DogHeight;
    }

    float Height(Target t)
    {
        return t.skier != null ? Height(t.skier) : Mathf.Max(0.1f, t.spawner.DogHeight);
    }

    Vector3 Velocity(Target t)
    {
        if (t.skier != null) return t.skier.Velocity;
        t.spawner.DogInfo(t.dog, out _, out Vector3 v);
        return v;
    }

    static float Height(SkierController s)
    {
        return Mathf.Max(0.1f, s.BodyHeight);
    }

    // ---------------- 附身动画 ----------------

    IEnumerator PossessRoutine(Target t)
    {
        animating = true;
        possessing = true;                // 无敌：附身过程中撞到东西不算
        SkierController from = Current;
        float hFrom = Height(from);
        SetSlowMotion(true);

        if (ghost != null) ghost.gameObject.SetActive(true);
        SetGhostAlpha(1f);
        Vector3 camStart = camReady ? cam.transform.position : Vector3.zero;
        Vector3 fwd = Flat(from.Heading);

        // ① 镜头拉远，幽灵从身体里飘出来
        for (float e = 0f; e < pullOutTime; e += Time.unscaledDeltaTime)
        {
            if (!Valid(t)) { CancelAnimation(); yield break; }
            float k = Smooth(e / pullOutTime);
            Vector3 hover = HoverPoint(from, hFrom);
            PlaceGhost(Vector3.Lerp(from.BodyCenter, hover, 1f - (1f - k) * (1f - k)),
                       Mathf.Lerp(0.3f, 1f, k) * hFrom, Vector3.Slerp(fwd, Flat(Center(t) - hover), k), true);
            if (camReady) cam.transform.SetPositionAndRotation(Vector3.Lerp(camStart, WidePos(from, t, hFrom), k), camRot);
            yield return null;
        }

        // ② 悬在空中，转向目标
        for (float e = 0f; e < hoverTime; e += Time.unscaledDeltaTime)
        {
            if (!Valid(t)) { CancelAnimation(); yield break; }
            Vector3 hover = HoverPoint(from, hFrom) + Vector3.up * Mathf.Sin(e * 9f) * 0.06f * hFrom;
            PlaceGhost(hover, hFrom, Flat(Center(t) - hover), true);
            if (camReady)
            {
                float k = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(cam.transform.position, WidePos(from, t, hFrom), k), camRot);
            }
            yield return null;
        }

        // ③ 飞快冲进新身体，镜头同时拉到他的俯视跟随视角
        Vector3 flyStart = ghost != null ? GhostCenter() : HoverPoint(from, hFrom);
        Vector3 camFly = camReady ? cam.transform.position : Vector3.zero;
        for (float e = 0f; e < flyTime; e += Time.unscaledDeltaTime)
        {
            if (!Valid(t)) { CancelAnimation(); yield break; }
            float k = Mathf.Clamp01(e / flyTime);
            float hT = Height(t);
            Vector3 c = Center(t);
            Vector3 p = Vector3.Lerp(flyStart, c, k * k);   // 越飞越快
            PlaceGhost(p, Mathf.Lerp(hFrom, hT, k) * Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(0.6f, 1f, k)), Flat(c - flyStart), false);
            SetGhostAlpha(1f - Mathf.InverseLerp(0.75f, 1f, k));
            if (camReady) cam.transform.SetPositionAndRotation(Vector3.Lerp(camFly, FollowPos(c, hT), Smooth(k)), camRot);
            yield return null;
        }

        DoPossess(from, t);
        if (ghost != null) ghost.gameObject.SetActive(false);
        SetSlowMotion(false);
        animating = false;
        possessing = false;
    }

    // 附身完成：换控制，或者和小狗交换身体
    void DoPossess(SkierController from, Target t)
    {
        if (t.skier != null)
        {
            from.Possess(false);
            GiveBack(from);
            t.skier.canControl = true;
            t.skier.Possess(true);
            Current = t.skier;
            foreach (var sp in FindObjectsOfType<SkiingDogSpawner>()) sp.SetPlayer(t.skier.transform);
            if (oldFollow != null) oldFollow.target = t.skier.transform;
        }
        else if (t.spawner != null)
        {
            SkierController dog = TurnDogIntoSkier(from, t.spawner, t.dog);
            if (dog != null)
            {
                dogOrigin[dog] = t.spawner;
                from.Possess(false);
                GiveBack(from);
                dog.Possess(true);
                Current = dog;
                Refresh();
                IgnoreSkierCollisions();
                foreach (var sp in FindObjectsOfType<SkiingDogSpawner>()) sp.SetPlayer(dog.transform);
                if (oldFollow != null) oldFollow.target = dog.transform;
            }
        }
        graceUntil = Time.time + 0.5f;
        if (Gamemode.Instance != null) Gamemode.Instance.OnPossessed();
    }

    // 刚离开的身体如果是滑雪动物：还给它的生成器，变回普通的滑雪动物接着往下滑
    void GiveBack(SkierController from)
    {
        if (from == null || !dogOrigin.TryGetValue(from, out SkiingDogSpawner sp)) return;
        dogOrigin.Remove(from);
        if (sp == null) return;
        Vector3 vel = from.Velocity;
        sp.AdoptDog(from.transform, vel);
    }

    // 把滑雪小狗变成可以用键盘控制的滑雪者：滑雪手感（速度、重力、转弯……）全部照抄原来的玩家
    SkierController TurnDogIntoSkier(SkierController from, SkiingDogSpawner spawner, int index)
    {
        Transform dog = spawner.ReleaseDog(index, out Vector3 vel);
        if (dog == null) return null;
        dog.gameObject.tag = "Untagged";   // 不再是障碍物
        Vector3 face = Flat(vel);
        if (face == Vector3.zero) face = Flat(from.Heading);
        if (face == Vector3.zero) face = Vector3.forward;
        dog.rotation = Quaternion.LookRotation(face, Vector3.up);   // 站直

        Rigidbody rb = dog.GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        SkierController sk = dog.gameObject.AddComponent<SkierController>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from), sk);   // 照抄所有设置
        sk.model = dog.Find("DogModel");
        sk.boardMesh = null;
        sk.boardYawOffset = 0f;      // 小狗踩的是两块滑雪板，板头就是脸的方向
        sk.startSideways = false;
        sk.canControl = true;
        // 动物滑得比 rider 慢：按 rider 原本的 Speed Boost 乘 animalSpeed（从动物跳到动物也不会越来越慢）
        if (!animalBodies.Contains(from) || riderSpeedBoost < 0f) riderSpeedBoost = from.speedBoost;
        sk.speedBoost = riderSpeedBoost * animalSpeed;
        animalBodies.Add(sk);
        rb.velocity = Vector3.ClampMagnitude(vel, sk.MaxSpeedNow);
        return sk;
    }

    void CancelAnimation()
    {
        StopAllCoroutines();
        if (possessing) graceUntil = Time.time + 0.5f;   // 取消了也多给一点点无敌时间
        possessing = false;
        if (ghost != null) ghost.gameObject.SetActive(false);
        SetSlowMotion(false);
        animating = false;
    }

    void SetSlowMotion(bool on)
    {
        // 游戏结束/开始界面时不改时间
        if (Gamemode.Instance != null && !Gamemode.Instance.IsPlaying && on) return;
        if (!on && Time.timeScale == 0f) { Time.fixedDeltaTime = fixedBase; return; }
        Time.timeScale = on ? slowMotion : 1f;
        Time.fixedDeltaTime = fixedBase * (on ? slowMotion : 1f);
    }

    Vector3 HoverPoint(SkierController s, float h)
    {
        return s.HeadTop + Vector3.up * (hoverHeight + ghostSize * 0.5f) * h;
    }

    // 拉远的镜头：方向和平时一模一样，只是往后退，把两个人和幽灵都框进画面
    Vector3 WidePos(SkierController from, Target t, float h)
    {
        Vector3 a = from.BodyCenter;
        Vector3 b = Center(t);
        Vector3 mid = (a + b) * 0.5f + Vector3.up * hoverHeight * h * 0.5f;
        float dist = camOffset.magnitude * (h / camBaseHeight) * pullOutDistance + Vector3.Distance(a, b) * 0.8f;
        return mid - (camRot * Vector3.forward) * dist;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // ---------------- 幽灵模型 ----------------

    void MakeGhost()
    {
        GameObject src = ghostModel != null ? ghostModel : Resources.Load<GameObject>("ghost");
        GameObject g;
        if (src == null)
        {
            Debug.LogWarning("GhostController：找不到幽灵模型（Assets/Ghost/Resources/ghost.obj），先用一个简单的球代替。");
            g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(g.GetComponent<Collider>());
        }
        else if (src.scene.IsValid()) g = src;          // 场景里的物体：直接用
        else g = Instantiate(src);                       // Prefab / 模型文件：复制一个
        g.name = "Ghost";
        g.SetActive(true);
        ghost = g.transform;

        foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);

        // 量出模型本来多高（按缩放 1 算）
        ghost.localScale = Vector3.one;
        ghost.rotation = Quaternion.identity;
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            ghostBaseHeight = Mathf.Max(0.01f, b.size.y);
        }

        if (autoGhostMaterials) ApplyGhostMaterials(rs);
        else foreach (var r in rs) ghostMats.AddRange(r.materials);

        if (g.GetComponent<GhostFluid>() == null && src != null) g.AddComponent<GhostFluid>();
        g.SetActive(false);
    }

    void ApplyGhostMaterials(Renderer[] rs)
    {
        Shader toon = Shader.Find("DownDown/ToonGhost");
        if (toon == null) toon = Shader.Find("Unlit/Color");
        foreach (var r in rs)
        {
            Material[] old = r.sharedMaterials;
            var mats = new Material[Mathf.Max(1, old.Length)];
            for (int i = 0; i < mats.Length; i++)
            {
                string matName = i < old.Length && old[i] != null ? old[i].name : "";
                bool eye = r.gameObject.name.Contains("Eye") || matName.Contains("Eye");
                var m = new Material(toon) { name = eye ? "GhostEye" : "GhostBody" };
                m.color = eye ? eyeColor : ghostColor;
                if (m.HasProperty("_Unlit")) m.SetFloat("_Unlit", eye ? 1f : 0f);
                if (eye && m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0f);
                mats[i] = m;
                ghostMats.Add(m);
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // center = 幽灵身体中间的位置；height = 幽灵要多高（米）
    void PlaceGhost(Vector3 center, float height, Vector3 face, bool smoothTurn)
    {
        if (ghost == null) return;
        float h = ghostSize * height;
        ghost.localScale = Vector3.one * (h / ghostBaseHeight);
        ghost.position = center - Vector3.up * h * 0.5f;   // 模型原点在脚底
        if (face.sqrMagnitude > 0.0001f)
        {
            Quaternion rot = Quaternion.LookRotation(face, Vector3.up) * Quaternion.Euler(0f, ghostFacingOffset, 0f);
            ghost.rotation = smoothTurn ? Quaternion.Slerp(ghost.rotation, rot, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)) : rot;
        }
    }

    Vector3 GhostCenter()
    {
        return ghost.position + Vector3.up * ghostBaseHeight * ghost.localScale.y * 0.5f;
    }

    void SetGhostAlpha(float a)
    {
        foreach (var m in ghostMats)
        {
            if (m == null || !m.HasProperty("_Color")) continue;
            Color c = m.color;
            c.a = a;
            m.color = c;
        }
    }

    // ---------------- 目标头顶的发光小球 + 提示 ----------------

    void MakeMarker()
    {
        marker = MakeDot("PossessMarker", markerColor);
        markerMat = marker.GetComponent<Renderer>().sharedMaterial;
    }

    // 一个不挡东西、不投影子的发光小球
    Transform MakeDot(string dotName, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = dotName;
        Destroy(go.GetComponent<Collider>());
        Shader toon = Shader.Find("DownDown/ToonGhost");
        if (toon == null) toon = Shader.Find("Unlit/Color");
        var mat = new Material(toon) { name = dotName };
        mat.color = color;
        if (mat.HasProperty("_Unlit")) mat.SetFloat("_Unlit", 1f);
        if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", 0f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.SetActive(false);
        return go.transform;
    }

    // 现在控制的角色头顶的小圆点（附身动画时藏起来，幽灵飞走了）
    void UpdateCurrentDot()
    {
        if (currentDot == null) return;
        bool show = Current != null && !animating && (Gamemode.Instance == null || Gamemode.Instance.IsPlaying);
        currentDot.gameObject.SetActive(show);
        if (!show) return;
        float h = Height(Current);
        currentDot.position = Current.HeadTop + (Current.SmoothCenter - Current.BodyCenter) + Vector3.up * 0.3f * h;
        currentDot.localScale = Vector3.one * currentDotSize * h;
    }

    void UpdateMarker()
    {
        if (marker == null) return;
        marker.gameObject.SetActive(hasTarget);
        if (!hasTarget) return;
        float h = Height(target);
        float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
        marker.position = MarkerPoint();
        marker.localScale = Vector3.one * 0.18f * h * pulse;
    }

    Vector3 MarkerPoint()
    {
        return HeadTop(target) + Vector3.up * 0.3f * Height(target);
    }

    void OnGUI()
    {
        if (!hasTarget || animating || cam == null || !Valid(target)) return;
        if (Gamemode.Instance != null && !Gamemode.Instance.IsPlaying) return;
        Vector3 sp = cam.WorldToScreenPoint(MarkerPoint() + Vector3.up * 0.25f * Height(target));
        if (sp.z <= 0f) return;
        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            promptStyle.normal.textColor = Color.white;
        }
        var rect = new Rect(sp.x - 70f, Screen.height - sp.y - 34f, 140f, 32f);
        GUI.Box(rect, "[ E ]  Possess", promptStyle);
    }

    // 滑雪者之间互相穿过，不会撞在一起
    void IgnoreSkierCollisions()
    {
        var cols = new List<Collider[]>();
        foreach (var s in skiers) if (s != null) cols.Add(s.GetComponentsInChildren<Collider>());
        for (int i = 0; i < cols.Count; i++)
            for (int j = i + 1; j < cols.Count; j++)
                foreach (var a in cols[i])
                    foreach (var b in cols[j])
                        Physics.IgnoreCollision(a, b);
    }
}
