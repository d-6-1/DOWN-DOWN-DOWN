// @role Opening cutscene: ghost on a platform looks at its own corpse, press E to start skiing
// @calls ClimberBuilder Gamemode GhostController SkierController GhostFluid PossessionMusic
// @tune platformBack:float:2-20:Platform distance uphill of the rider, in body heights
// @tune corpseSize:float:0.5-3:Corpse size
// @tune corpseDistance:float:1-6:Corpse distance from the ghost
// @tune lookSpeed:float:20-200:Look-around speed, degrees per second
// @tune aimTolerance:float:1-20:How close the aim must be to the corpse, in degrees
// @tune fadeInTime:float:0-5:Fade from black at the start
// @tune flyTime:float:0.2-3:Seconds the ghost flies into the body
// @tune musicVolume:float:0-1:Opening music volume
// @tune musicFade:float:0-5:Music fade time
// OpeningScene.cs —— 开场剧情：你已经变成了幽灵，飘在第一个雪坡最顶上的平台上，旁边躺着你自己的身体（登山者）
// 流程：开始界面点 START（或按 R）→ 画面从黑慢慢亮起来，镜头在幽灵身后
//      → 幽灵不能走，W/A/S/D 只能转视野（A/D 左右看，W/S 上下看）
//      → 视角对准尸体时，尸体头顶出现发光小球和 [ E ] Possess
//      → 按 E：幽灵飞进尸体，画面一白，正常游戏开始（幽灵在第一个 rider 身上）。
//
// 用法：Hierarchy 里新建一个空物体（叫"开场"就行），挂上这个脚本，其它什么都不用拖。
// 平台默认自动放在第一个 rider 的身后（上坡方向）；想自己摆位置：新建空物体放到想要的地方，拖进 Platform Point。
// 需要同一个文件夹里的 ClimberBuilder.cs（登山者模型），以及原来的 GhostController / Gamemode / SkierController。
// 所有长度都按"几个身高"算（身高 = 第一个 rider 的身高），场景放大多少倍都不用改。

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CPart = ClimberBuilder.Part;
using CShape = ClimberBuilder.Shape;

public class OpeningScene : MonoBehaviour
{
    [Header("开关")]
    [Tooltip("不勾 = 不播开场，直接正常游戏")]
    public bool playOpening = true;
    [Tooltip("勾上：游戏结束按 R 重新开始后也再播一次开场。不勾 = 只有第一次播")]
    public bool playAgainOnRestart = false;

    [Header("平台位置")]
    [Tooltip("可不填：自动放在第一个 rider 身后（上坡方向）。想自己摆：新建空物体放到想要的位置（平台表面的中心），拖进来")]
    public Transform platformPoint;
    [Tooltip("自动摆放时：平台中心在 rider 身后（上坡方向）几个身高")]
    public float platformBack = 7f;
    [Tooltip("平台半径（几个身高）")]
    public float platformRadius = 4f;
    [Tooltip("平台表面比雪地高多少（几个身高）")]
    public float platformLift = 0.35f;

    [Header("尸体")]
    [Tooltip("尸体多大：1 = 躺平的长度和 rider 身高差不多")]
    public float corpseSize = 1.25f;
    [Tooltip("尸体离幽灵多远（几个身高）")]
    public float corpseDistance = 2.4f;
    [Tooltip("开局时尸体在视野右边多少度（负数 = 左边）。越大越要转头才能对准")]
    public float corpseAngle = 70f;
    [Tooltip("勾上：附身以后尸体消失（你回到身体里，从这里滑下去）。不勾 = 尸体一直留在平台上")]
    public bool corpseVanishes = true;

    [Header("幽灵和视角")]
    [Tooltip("幽灵大小（身高的几倍）")]
    public float ghostSize = 0.7f;
    [Tooltip("幽灵飘在平台上方多高（几个身高）")]
    public float ghostHover = 0.45f;
    [Tooltip("镜头在幽灵身后多远（几个身高）")]
    public float cameraBack = 2.2f;
    [Tooltip("镜头比幽灵高多少（几个身高）")]
    public float cameraUp = 0.5f;
    [Tooltip("镜头往右偏多少（几个身高），这样幽灵不会挡住正中间")]
    public float cameraSide = 0.7f;
    [Tooltip("转视野的速度：度/秒")]
    public float lookSpeed = 75f;
    [Tooltip("最多往上看多少度")]
    public float maxLookUp = 35f;
    [Tooltip("最多往下看多少度")]
    public float maxLookDown = 60f;
    [Tooltip("对准尸体的宽容度（度）。越大越容易对准")]
    public float aimTolerance = 6f;

    [Header("文字")]
    public string lookHint = "A / D  -  look around        W / S  -  look up / down";
    public string possessText = "[ E ]  Possess";

    [Header("附身动画")]
    [Tooltip("开场画面从黑变亮要几秒")]
    public float fadeInTime = 1.5f;
    [Tooltip("幽灵飞进尸体要几秒")]
    public float flyTime = 0.8f;
    [Tooltip("画面变白、再亮回来各要几秒")]
    public float flashTime = 0.45f;
    public Color markerColor = new Color(0.45f, 0.95f, 1f, 0.85f);

    [Header("音乐")]
    [Tooltip("开场放的音乐（从开始界面一直放到按 E 附身）。空着 = 开场没有音乐")]
    public AudioClip openingMusic;
    [Tooltip("只有场景里没有 PossessionMusic（附身音乐）时才用：开始滑雪后放这首。有 PossessionMusic 就交给它放骑手的歌")]
    public AudioClip skiMusic;
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Tooltip("开场音乐淡出、滑雪音乐淡入用几秒")]
    public float musicFade = 1.5f;

    // 只在第一次进游戏时播（按 R 重新开始会重新加载场景，静态变量会保留下来）
    static bool playedOnce;
    // 撞车失败后由 CrashTransition 设成 true：下一次一定播开场
    public static bool forceNext;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { playedOnce = false; forceNext = false; }

    enum State { Waiting, Looking, Possessing, Done }
    State state = State.Done;

    SkierController rider;
    Rigidbody riderRb;
    bool riderWasKinematic;
    GhostController ghostCtl;
    Camera cam;
    Vector3 camSavedPos;
    Quaternion camSavedRot;
    float camSavedFov;

    float h = 1f;                 // 1 个身高（米）
    Vector3 downhill = Vector3.forward;
    Vector3 platformTop;          // 平台表面中心
    Transform platform, corpse, ghost, marker;
    Vector3 ghostFloor;           // 幽灵脚下的平台表面
    float ghostBaseHeight = 1.55f;
    readonly List<Material> ghostMats = new List<Material>();

    float yaw, pitch;
    bool aiming, lookedAround;
    float fade = 0f;              // 画面盖一层颜色：0 = 没有
    Color fadeColor = Color.black;
    GUIStyle promptStyle, hintStyle;
    AudioSource openingPlayer;
    MonoBehaviour possessionMusic;

    void Start()
    {
        rider = FindRider();
        if (rider == null)
        {
            Debug.LogError("OpeningScene：场景里找不到 SkierController（rider），开场没法播。");
            enabled = false;
            return;
        }
        cam = Camera.main;
        if (cam == null) cam = FindObjectOfType<Camera>();

        bool play = playOpening && (forceNext || !playedOnce || playAgainOnRestart);
        forceNext = false;
        MeasureLayout();
        BuildPlatform();
        if (!play) { enabled = false; return; }   // 重新开始：平台还在，但不播开场

        playedOnce = true;
        BuildCorpse();
        StartOpeningMusic();
        state = State.Waiting;
    }

    SkierController FindRider()
    {
        if (GhostController.Current != null) return GhostController.Current;
        var all = FindObjectsOfType<SkierController>();
        foreach (var s in all) if (s.gameObject.name == "rider") return s;
        return all.Length > 0 ? all[0] : null;
    }

    void Update()
    {
        switch (state)
        {
            case State.Waiting:
                // 等开始界面点了 START
                if (Gamemode.Instance == null || Gamemode.Instance.IsPlaying) BeginOpening();
                break;
            case State.Looking:
                UpdateLooking();
                break;
        }
    }

    void LateUpdate()
    {
        if (state != State.Looking) return;
        PlaceCamera();
    }

    // ---------------- 开场开始 / 结束 ----------------

    void BeginOpening()
    {
        state = State.Looking;

        // 正常游戏先停住：rider 冻住不动，幽灵附身系统和 Gamemode（HUD、腾空判定）先关掉
        ghostCtl = FindObjectOfType<GhostController>();
        if (ghostCtl != null) ghostCtl.enabled = false;
        SetActiveByName("CurrentDot", false);
        SetActiveByName("PossessMarker", false);
        if (Gamemode.Instance != null) Gamemode.Instance.enabled = false;

        rider.canControl = false;
        riderRb = rider.GetComponent<Rigidbody>();
        if (riderRb != null)
        {
            riderWasKinematic = riderRb.isKinematic;
            if (!riderRb.isKinematic) riderRb.velocity = Vector3.zero;
            riderRb.isKinematic = true;
        }

        if (cam != null)
        {
            camSavedPos = cam.transform.position;
            camSavedRot = cam.transform.rotation;
            camSavedFov = cam.fieldOfView;
        }

        MakeGhost();
        MakeMarker();

        // 一开始面朝山下（看得到整条雪道），尸体在视野右边
        yaw = Mathf.Atan2(downhill.x, downhill.z) * Mathf.Rad2Deg;
        pitch = 12f;
        PlaceGhost(true);
        PlaceCamera();

        fadeColor = Color.black;
        fade = 1f;
        StartCoroutine(FadeTo(0f, fadeInTime));
    }

    void FinishOpening()
    {
        // 镜头放回原来的位置，一切恢复正常
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(camSavedPos, camSavedRot);
            cam.fieldOfView = camSavedFov;
        }
        if (riderRb != null) riderRb.isKinematic = riderWasKinematic;
        rider.canControl = true;
        if (Gamemode.Instance != null) Gamemode.Instance.enabled = true;
        if (ghostCtl != null) ghostCtl.enabled = true;

        if (ghost != null) Destroy(ghost.gameObject);
        if (marker != null) Destroy(marker.gameObject);
        if (corpse != null && corpseVanishes) corpse.gameObject.SetActive(false);
        state = State.Done;
        StartCoroutine(SwitchToSkiMusic());
    }

    // ---------------- 音乐 ----------------

    // 开场时：附身音乐（PossessionMusic）先停下，放开场音乐
    void StartOpeningMusic()
    {
        if (openingMusic == null) return;
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "PossessionMusic") { possessionMusic = mb; break; }
        if (possessionMusic != null)
        {
            possessionMusic.enabled = false;
            foreach (var src in possessionMusic.GetComponents<AudioSource>()) { src.Stop(); src.volume = 0f; }
        }

        openingPlayer = gameObject.AddComponent<AudioSource>();
        openingPlayer.clip = openingMusic;
        openingPlayer.loop = true;
        openingPlayer.playOnAwake = false;
        openingPlayer.spatialBlend = 0f;           // 2D 声音：场景放大了也一样大
        openingPlayer.ignoreListenerPause = true;
        openingPlayer.volume = musicVolume;
        openingPlayer.Play();
    }

    // 开始滑雪：开场音乐慢慢淡出，滑雪音乐慢慢淡入
    IEnumerator SwitchToSkiMusic()
    {
        if (openingPlayer == null) yield break;
        AudioSource ski = null;
        if (possessionMusic != null) possessionMusic.enabled = true;   // 它会自己淡入骑手的歌
        else if (skiMusic != null)
        {
            ski = gameObject.AddComponent<AudioSource>();
            ski.clip = skiMusic;
            ski.loop = true;
            ski.spatialBlend = 0f;
            ski.ignoreListenerPause = true;
            ski.volume = 0f;
            ski.Play();
        }
        float start = openingPlayer.volume;
        float time = Mathf.Max(0.01f, musicFade);
        for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
        {
            float k = e / time;
            openingPlayer.volume = start * (1f - k);
            if (ski != null) ski.volume = musicVolume * k;
            yield return null;
        }
        openingPlayer.Stop();
        if (ski != null) ski.volume = musicVolume;
    }

    static void SetActiveByName(string objName, bool on)
    {
        var go = GameObject.Find(objName);
        if (go != null) go.SetActive(on);
    }

    // ---------------- 转视野、对准尸体 ----------------

    void UpdateLooking()
    {
        float dt = Time.unscaledDeltaTime;
        float x = KeyInput.Horizontal();
        float y = KeyInput.Vertical();
        if (Mathf.Abs(x) > 0.1f || Mathf.Abs(y) > 0.1f) lookedAround = true;
        yaw += x * lookSpeed * dt;
        pitch = Mathf.Clamp(pitch - y * lookSpeed * dt, -maxLookUp, maxLookDown);

        PlaceGhost(false);
        aiming = cam != null && fade < 0.5f && AimingAtCorpse();

        if (marker != null)
        {
            marker.gameObject.SetActive(aiming);
            float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
            marker.position = MarkerPoint();
            marker.localScale = Vector3.one * 0.18f * h * pulse;
        }

        if (aiming && KeyInput.PossessPressed())
        {
            state = State.Possessing;
            if (marker != null) marker.gameObject.SetActive(false);
            StartCoroutine(PossessRoutine());
        }
    }

    Quaternion LookRot => Quaternion.Euler(pitch, yaw, 0f);

    Vector3 GhostCenter()
    {
        return ghostFloor + Vector3.up * (ghostHover + ghostSize * 0.5f) * h;
    }

    void PlaceCamera()
    {
        if (cam == null) return;
        Quaternion rot = LookRot;
        Vector3 pivot = GhostCenter() + Vector3.up * cameraUp * h;
        Vector3 pos = pivot - rot * Vector3.forward * cameraBack * h + Quaternion.Euler(0f, yaw, 0f) * Vector3.right * cameraSide * h;
        cam.transform.SetPositionAndRotation(pos, rot);
    }

    bool AimingAtCorpse()
    {
        if (corpse == null) return false;
        Bounds b = CorpseBounds();
        Vector3 to = b.center - cam.transform.position;
        float dist = to.magnitude;
        if (dist < 0.001f) return true;
        float radius = Mathf.Max(b.extents.x, b.extents.z) * 0.6f;
        float allowed = aimTolerance + Mathf.Atan2(radius, dist) * Mathf.Rad2Deg;
        return Vector3.Angle(cam.transform.forward, to) < allowed;
    }

    Bounds CorpseBounds()
    {
        var rs = corpse.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(corpse.position, Vector3.one * h);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    Vector3 MarkerPoint()
    {
        Bounds b = CorpseBounds();
        return new Vector3(b.center.x, b.max.y + 0.35f * h, b.center.z);
    }

    // ---------------- 按 E：飞进尸体 ----------------

    IEnumerator PossessRoutine()
    {
        Vector3 start = GhostCenter();
        Vector3 target = CorpseBounds().center;
        Quaternion camFrom = cam != null ? cam.transform.rotation : Quaternion.identity;

        for (float e = 0f; e < flyTime; e += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(e / flyTime);
            Vector3 p = Vector3.Lerp(start, target, k * k);   // 越飞越快
            float size = ghostSize * h * Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(0.5f, 1f, k));
            SetGhost(p, size, target - start, true);
            SetGhostAlpha(1f - Mathf.InverseLerp(0.7f, 1f, k));
            if (cam != null)
            {
                Quaternion look = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
                cam.transform.rotation = Quaternion.Slerp(camFrom, look, Smooth(k));
            }
            yield return null;
        }
        if (ghost != null) ghost.gameObject.SetActive(false);

        // 尸体抽动一下
        Vector3 basePos = corpse.localPosition;
        for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
        {
            corpse.localPosition = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(e * 40f)) * 0.04f * h * (1f - e / 0.3f);
            yield return null;
        }
        corpse.localPosition = basePos;

        // 画面一白 → 回到正常游戏 → 再亮回来
        fadeColor = Color.white;
        yield return FadeTo(1f, flashTime);
        FinishOpening();
        yield return FadeTo(0f, flashTime * 1.6f);
        enabled = false;
    }

    IEnumerator FadeTo(float to, float time)
    {
        float from = fade;
        for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
        {
            fade = Mathf.Lerp(from, to, Smooth(e / time));
            yield return null;
        }
        fade = to;
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // ---------------- 摆位置 ----------------

    void MeasureLayout()
    {
        h = Mathf.Max(0.1f, rider.BodyHeight);
        Vector3 feet = rider.FeetPos;

        // 下坡方向：rider 脚下坡面的朝向；坡是平的就用镜头的朝向
        downhill = Vector3.zero;
        if (GroundAt(feet, out _, out Vector3 n))
        {
            downhill = new Vector3(n.x, 0f, n.z);
            if (downhill.magnitude < 0.05f) downhill = Vector3.zero;
        }
        if (downhill == Vector3.zero && cam != null) downhill = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z);
        if (downhill.sqrMagnitude < 0.0001f) downhill = Vector3.forward;
        downhill.Normalize();

        if (platformPoint != null)
        {
            platformTop = platformPoint.position;
        }
        else
        {
            Vector3 c = feet - downhill * platformBack * h;
            float ground = GroundAt(c, out Vector3 hit, out _) ? hit.y : feet.y + platformBack * h * 0.3f;
            platformTop = new Vector3(c.x, ground + platformLift * h, c.z);
        }
        Debug.Log($"OpeningScene：平台放在 {platformTop}（1 个身高 = {h:F1} 米）。位置不满意可以用 Platform Point 自己摆。");
    }

    // 从上往下打一条射线找雪地（跳过滑雪者、小动物、树这些）
    bool GroundAt(Vector3 p, out Vector3 point, out Vector3 normal)
    {
        point = p; normal = Vector3.up;
        Vector3 from = p + Vector3.up * 60f * h;
        var hits = Physics.RaycastAll(from, Vector3.down, 200f * h, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        foreach (var hit in hits)
        {
            if (hit.collider.attachedRigidbody != null) continue;
            if (IsObstacle(hit.collider.transform)) continue;
            if (platform != null && hit.collider.transform.IsChildOf(platform)) continue;
            if (hit.distance < best) { best = hit.distance; point = hit.point; normal = hit.normal; found = true; }
        }
        return found;
    }

    static bool IsObstacle(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag("Obstacle")) return true;
            t = t.parent;
        }
        return false;
    }

    // ---------------- 平台（低多边形的雪岩台子） ----------------

    static readonly Dictionary<string, Color32> PlatformColors = new Dictionary<string, Color32>
    {
        { "Rock", new Color32(150, 152, 166, 255) },
        { "RockDark", new Color32(118, 120, 136, 255) },
        { "Snow", new Color32(248, 250, 252, 255) },
        { "Wood", new Color32(160, 116, 76, 255) },
        { "Flag", new Color32(196, 112, 110, 255) },
    };

    void BuildPlatform()
    {
        var holder = new GameObject("OpeningPlatform").transform;
        holder.position = platformTop;
        holder.rotation = Quaternion.LookRotation(downhill, Vector3.up);
        platform = holder;

        // 石头往下要伸到雪地下面（坡是斜的，下坡那一边会悬空一截）
        float lowest = platformTop.y;
        for (int i = 0; i < 8; i++)
        {
            Vector3 edge = platformTop + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * platformRadius * h;
            if (GroundAt(edge, out Vector3 g, out _)) lowest = Mathf.Min(lowest, g.y);
        }
        float depth = (platformTop.y - lowest) / h + 1.5f;   // 几个身高
        float R = platformRadius;

        var parts = new List<CPart>
        {
            // 八角形的岩石台子，上面一层雪，边上一圈鼓起来的雪堆
            new CPart("Rock", "Platform", CShape.Cylinder, new Vector3(0f, -depth * 0.5f, 0f), Vector3.zero, new Vector3(R * 2f, depth * 0.5f, R * 2f), "Rock", true),
            new CPart("RockBand", "Platform", CShape.Cylinder, new Vector3(0f, -0.35f, 0f), new Vector3(0f, 22.5f, 0f), new Vector3(R * 2.06f, 0.12f, R * 2.06f), "RockDark", true),
            new CPart("SnowCap", "Platform", CShape.Cylinder, new Vector3(0f, -0.03f, 0f), new Vector3(0f, 22.5f, 0f), new Vector3(R * 2.04f, 0.06f, R * 2.04f), "Snow", true),
        };
        for (int i = 0; i < 10; i++)
        {
            float a = i * 36f + (i % 2) * 9f;
            Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * R * 0.93f;
            float s = 0.7f + 0.25f * ((i * 7) % 3);
            parts.Add(new CPart("SnowLump" + i, "Platform", CShape.Sphere, new Vector3(p.x, -0.02f, p.z), new Vector3(0f, a, 0f), new Vector3(s * 1.3f, s * 0.45f, s * 0.9f), "Snow", true));
        }
        // 上坡那边：一小堆石头垒起来的路标，插着一面小旗
        Vector3 cairn = new Vector3(-R * 0.6f, 0f, -R * 0.68f);
        parts.Add(new CPart("Cairn0", "Platform", CShape.Sphere, cairn + new Vector3(0f, 0.12f, 0f), new Vector3(0f, 20f, 0f), new Vector3(0.62f, 0.32f, 0.55f), "Rock", true));
        parts.Add(new CPart("Cairn1", "Platform", CShape.Sphere, cairn + new Vector3(0.03f, 0.36f, 0.02f), new Vector3(0f, 70f, 0f), new Vector3(0.46f, 0.26f, 0.42f), "RockDark", true));
        parts.Add(new CPart("Cairn2", "Platform", CShape.Sphere, cairn + new Vector3(-0.02f, 0.55f, 0f), new Vector3(0f, 10f, 0f), new Vector3(0.3f, 0.2f, 0.28f), "Rock", true));
        parts.Add(new CPart("CairnSnow", "Platform", CShape.Sphere, cairn + new Vector3(-0.02f, 0.64f, 0f), Vector3.zero, new Vector3(0.22f, 0.08f, 0.2f), "Snow", false));
        parts.Add(new CPart("FlagPole", "Platform", CShape.Cylinder, cairn + new Vector3(0.12f, 0.9f, 0.05f), new Vector3(0f, 0f, -4f), new Vector3(0.04f, 0.6f, 0.04f), "Wood", true));
        parts.Add(new CPart("Flag", "Platform", CShape.Cube, cairn + new Vector3(0.36f, 1.3f, 0.05f), new Vector3(0f, 0f, -6f), new Vector3(0.44f, 0.26f, 0.025f), "Flag", true));
        // 散落的几块石头
        parts.Add(new CPart("Stone0", "Platform", CShape.Sphere, new Vector3(R * 0.55f, 0.1f, -R * 0.35f), new Vector3(0f, 30f, 0f), new Vector3(0.4f, 0.26f, 0.32f), "RockDark", true));
        parts.Add(new CPart("Stone1", "Platform", CShape.Sphere, new Vector3(-R * 0.7f, 0.09f, R * 0.25f), new Vector3(0f, 75f, 0f), new Vector3(0.32f, 0.22f, 0.3f), "Rock", true));

        ClimberBuilder.BuildParts(holder, "Platform", parts.ToArray(), PlatformColors, h, 0.02f, false, null);
    }

    void BuildCorpse()
    {
        // 幽灵在平台中间偏上坡一点，尸体在幽灵前方偏右 corpseAngle 度
        ghostFloor = platformTop - downhill * 0.9f * h;
        Vector3 side = Quaternion.Euler(0f, corpseAngle, 0f) * downhill;
        Vector3 spot = ghostFloor + side * corpseDistance * h;

        var holder = new GameObject("OpeningCorpse").transform;
        holder.SetParent(platform, true);
        holder.position = spot;
        // 横躺着对着镜头（头朝左边），这样一眼就能看出是个人
        Vector3 toCorpse = spot - (ghostFloor - downhill * cameraBack * h);
        toCorpse.y = 0f;
        if (toCorpse.sqrMagnitude < 0.0001f) toCorpse = side;
        holder.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, -90f, 0f) * toCorpse.normalized, Vector3.up);
        holder.localScale = Vector3.one * corpseSize * h / 1.75f;   // 模型站起来大约 1.75 米
        corpse = holder;
        var b = holder.gameObject.AddComponent<ClimberBuilder>();   // 一挂上就自动生成模型
        if (holder.Find(ClimberBuilder.RootName) == null) b.Build();
    }

    // ---------------- 幽灵 ----------------

    void MakeGhost()
    {
        GameObject src = Resources.Load<GameObject>("ghost");
        GameObject g;
        if (src == null)
        {
            Debug.LogWarning("OpeningScene：找不到幽灵模型（Assets/Ghost/Resources/ghost.obj），先用一个胶囊代替。");
            g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        }
        else g = Instantiate(src);
        g.name = "OpeningGhost";
        g.SetActive(true);
        ghost = g.transform;
        foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);

        ghost.localScale = Vector3.one;
        ghost.rotation = Quaternion.identity;
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            ghostBaseHeight = Mathf.Max(0.01f, b.size.y);
        }

        // 和 GhostController 一样的卡通材质：黑身体、白眼睛
        Color body = ghostCtl != null ? ghostCtl.ghostColor : new Color(0.08f, 0.08f, 0.10f, 1f);
        Color eye = ghostCtl != null ? ghostCtl.eyeColor : Color.white;
        Shader toon = Shader.Find("DownDown/ToonGhost");
        if (toon == null) toon = Shader.Find("Unlit/Color");
        foreach (var r in rs)
        {
            Material[] old = r.sharedMaterials;
            var mats = new Material[Mathf.Max(1, old.Length)];
            for (int i = 0; i < mats.Length; i++)
            {
                string matName = i < old.Length && old[i] != null ? old[i].name : "";
                bool isEye = r.gameObject.name.Contains("Eye") || matName.Contains("Eye");
                var m = new Material(toon) { name = isEye ? "GhostEye" : "GhostBody" };
                m.color = isEye ? eye : body;
                if (m.HasProperty("_Unlit")) m.SetFloat("_Unlit", isEye ? 1f : 0f);
                if (isEye && m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0f);
                mats[i] = m;
                ghostMats.Add(m);
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (src != null && g.GetComponent<GhostFluid>() == null) g.AddComponent<GhostFluid>();
    }

    // 幽灵飘在原地，轻轻上下浮动，脸朝视野的方向
    void PlaceGhost(bool snap)
    {
        Vector3 c = GhostCenter() + Vector3.up * Mathf.Sin(Time.unscaledTime * 2.2f) * 0.06f * h;
        SetGhost(c, ghostSize * h, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, !snap);
    }

    void SetGhost(Vector3 center, float height, Vector3 face, bool smoothTurn)
    {
        if (ghost == null) return;
        ghost.localScale = Vector3.one * (height / ghostBaseHeight);
        ghost.position = center - Vector3.up * height * 0.5f;   // 模型原点在脚底
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
        {
            Quaternion rot = Quaternion.LookRotation(face, Vector3.up) * Quaternion.Euler(0f, ghostCtl != null ? ghostCtl.ghostFacingOffset : 0f, 0f);
            ghost.rotation = smoothTurn ? Quaternion.Slerp(ghost.rotation, rot, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)) : rot;
        }
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

    // 尸体头顶的发光小球（和附身别人时的一样）
    void MakeMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "OpeningMarker";
        Destroy(go.GetComponent<Collider>());
        Shader toon = Shader.Find("DownDown/ToonGhost");
        if (toon == null) toon = Shader.Find("Unlit/Color");
        var mat = new Material(toon) { name = "OpeningMarker" };
        mat.color = markerColor;
        if (mat.HasProperty("_Unlit")) mat.SetFloat("_Unlit", 1f);
        if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", 0f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.SetActive(false);
        marker = go.transform;
    }

    // ---------------- 文字和黑屏/白屏 ----------------

    void OnGUI()
    {
        if (state == State.Waiting || (state == State.Done && fade <= 0f)) return;
        GUI.depth = -100;   // 盖在其它界面上面
        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            promptStyle.normal.textColor = Color.white;
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
        }

        if (state == State.Looking && fade < 0.5f)
        {
            if (aiming && cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(MarkerPoint() + Vector3.up * 0.25f * h);
                if (sp.z > 0f) GUI.Box(new Rect(sp.x - 70f, Screen.height - sp.y - 34f, 140f, 32f), possessText, promptStyle);
            }
            if (!lookedAround || !aiming)
            {
                var r = new Rect(0f, Screen.height - 70f, Screen.width, 30f);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), lookHint, hintStyle);
                GUI.color = Color.white;
                GUI.Label(r, lookHint, hintStyle);
            }
        }

        if (fade > 0f)
        {
            Color c = fadeColor;
            c.a = fade;
            GUI.color = c;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
