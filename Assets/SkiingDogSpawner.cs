// @role Spawns skiing animal NPCs from the summit; they ski, dodge trees and bears, and hitting one loses
// @calls DogScoutBuilder Gamemode GhostController SkierController SlopeExtender SlopeDecorator
// @tune animalCap:int:10-400:Max animals on the mountain
// @tune nearCount:int:0-20:Animals kept just ahead of the player
// @tune topSpawnEvery:float:0.1-5:Seconds between spawns at the summit
// @tune fromBehindAtOnce:int:0-10:Animals passing the player from behind at once
// @tune fromBehindEvery:float:0.5-10:Seconds between passers from behind
// @tune frontEvery:float:0.5-10:Seconds between spawns ahead of the player
// @tune slowestSpeed:float:1-10:Slowest cruise speed
// @tune fastestSpeed:float:2-15:Fastest cruise speed
// @tune passSpeedUp:float:1-2:Speed multiplier while passing the player
// @tune dogSize:float:0.5-30:Animal size when Match Player Size is off
// SkiingDogSpawner.cs —— 小狗从山顶源源不断地滑下来，撞到小狗就判输
// 需要：DogScoutBuilder.cs 和 DogToon.shader 已经在 Assets 里（小狗模型由它们生成）。
// 用法：Hierarchy 里新建一个空物体，挂上这个脚本，Play 就行。
// 判输：每只小狗身上有碰撞体，Tag = Obstacle，所以 Gamemode 会像撞到树一样判 GAME OVER。
//
// 小狗和玩家没关系：
// · 开局时整条雪道上已经散着一些小狗（像是它们早就在滑了），之后每隔一会儿从山顶（玩家出发的地方）再出来一只。
// · 玩家附近小狗太少时，会在玩家前方的雪道上（镜头外）补几只，所以身边总有小狗可以去追。
// · 每只小狗有自己的速度（有快有慢），一直顺着雪道往下滑（看周围十几米哪边低），不会往回爬，也不会转圈。
// · 每只小狗只在自己那条线左右一小段范围里来回摆动。
// · 前面有树、北极熊、石头：提前看准哪边空就从哪边绕过去，太近了先减速；万一还是碰上了，会被推出来，不会穿进去。
// · 滑出雪道（到底了）就消失，回到山顶重新出来。
// 玩家要自己追上去靠近小狗，按 E 附身。

using System.Collections.Generic;
using UnityEngine;

public class SkiingDogSpawner : MonoBehaviour
{
    [Header("玩家")]
    [Tooltip("玩家（rider）。留空会自动找名字叫 rider 的物体。开局时玩家站的地方就是山顶出生点")]
    public Transform player;

    [Header("场景比例")]
    [Tooltip("勾上：按玩家模型的高度自动算场景比例（你的场景比真实世界大很多时，距离和速度会一起放大）")]
    public bool autoWorldScale = true;
    [Tooltip("场景比例。勾了 Auto World Scale 时 Play 会自动算出来；不勾就用这里填的数。下面的距离和速度都会乘上它")]
    public float worldScale = 1f;

    [Header("生成")]
    [Tooltip("整座山上最多同时有几只（太多会卡）")]
    public int animalCap = 150;
    [Tooltip("每隔几秒从山顶（整座雪山最高的地方）出来一只")]
    public float topSpawnEvery = 0.4f;
    [Tooltip("勾了 Come From Behind 时：同时有几只正从你身后追上来、从你身边滑过去")]
    public int fromBehindAtOnce = 2;
    [Tooltip("玩家前方（Near Front 米以内）至少要有几只。不够就在前方 Fill Front Near～Far 米的地方补（和从背后来的同时都有）")]
    public int nearCount = 3;
    [Tooltip("前方不够时，每隔几秒补一只")]
    public float frontEvery = 1f;
    [Tooltip("“玩家前方”指多远以内（真实米数，会乘上场景比例）。别调太大：调大了，很远的也算进去，就不往你身边补了")]
    public float nearFront = 40f;
    [Tooltip("勾上：一直有几只从玩家身后（镜头外）追上来，比玩家快一点从身边滑过去。不勾：只在前方补")]
    public bool comeFromBehind = true;
    [Tooltip("补在玩家前方时：最近多远（真实米数，会乘上场景比例）")]
    public float fillFrontNear = 12f;
    [Tooltip("补在玩家前方时：最远多远（真实米数，会乘上场景比例）")]
    public float fillFrontFar = 30f;
    [Tooltip("补的时候放在玩家身后多远：最近（真实米数，会乘上场景比例）。在镜头里看得见的地方不会补")]
    public float fillBehindNear = 25f;
    [Tooltip("补的时候放在玩家身后多远：最远（真实米数，会乘上场景比例）")]
    public float fillBehindFar = 50f;
    [Tooltip("从身后补的动物比玩家快多少倍（1.3 = 快 30%），追上来从身边滑过去，超过以后慢慢回到自己的速度")]
    public float passSpeedUp = 1.35f;
    [Tooltip("从身后补的动物离玩家的路线左右至少多远（按小狗大小算），免得从后面撞上玩家，也宽敞一点")]
    public float passSideGap = 3f;
    [Tooltip("每隔几秒补一只（一只一只来，不会一下子冒出一群）")]
    public float fromBehindEvery = 1f;
    [Tooltip("山顶出生的地方有多宽：玩家出发点左右各多少米（真实米数，会乘上场景比例）")]
    public float spawnWidth = 8f;
    [Tooltip("勾上：开局时整条雪道上就已经散着小狗；不勾：开局时雪道是空的，只从山顶慢慢出来")]
    public bool fillSlopeAtStart = true;
    [Tooltip("开局时雪道上的小狗前后大概隔多远（真实米数，会乘上场景比例）")]
    public float startGap = 10f;
    [Tooltip("出生时离玩家至少多远（真实米数，会乘上场景比例），免得一出来就撞上")]
    public float minGap = 3f;

    [Header("小狗滑雪")]
    [Tooltip("最慢的小狗滑多快（米/秒，会乘上场景比例）")]
    public float slowestSpeed = 2.5f;
    [Tooltip("最快的小狗滑多快（米/秒，会乘上场景比例）")]
    public float fastestSpeed = 5.5f;
    [Tooltip("勾上：你附近的动物会跟着你的速度滑（有的比你快一点、有的慢一点），一直在你身边，不会一下就被你甩没了。你很慢的时候它们照常按自己的速度往下滑，不会等你、也不会往回爬")]
    public bool keepPace = true;
    [Tooltip("“附近”多远以内的动物会跟着你的速度（真实米数，会乘上场景比例）")]
    public float paceRange = 70f;
    [Tooltip("跟速度时，最慢是你的几倍、最快是你的几倍（每只动物随机一个）")]
    public Vector2 paceMul = new Vector2(0.85f, 1.12f);
    [Tooltip("加速有多快（米/秒²，会乘上场景比例）。越小越平稳")]
    public float acceleration = 3f;
    [Tooltip("每只小狗离开自己那条线最多多远（真实米数，会乘上场景比例）。越小越老实")]
    public float wanderWidth = 2.5f;
    [Tooltip("每隔几秒换一个左右位置")]
    public float wanderEvery = 3f;
    [Tooltip("左右摆动的幅度（按小狗大小算，1 = 一只小狗的身高）")]
    public float swayWidth = 0.4f;
    [Tooltip("左右摆动一次要几秒")]
    public float swayPeriod = 2.5f;
    [Tooltip("落在玩家身后（山上）超过多远的，在需要的时候拿去山顶或者玩家前方重新用（真实米数，会乘上场景比例）")]
    public float recycleBehind = 60f;
    [Tooltip("小狗大小。1 = 大约 1.5 米高")]
    public float dogSize = 1f;
    [Tooltip("勾上：Play 时自动把小狗变成和玩家一样大（会忽略上面的 Dog Size）")]
    public bool matchPlayerSize = true;
    [Tooltip("滑雪板颜色")]
    public Color skiColor = new Color32(230, 80, 60, 255);

    [Header("可选")]
    [Tooltip("可选：拖进你做好的小狗 Prefab。留空就用 DogScoutBuilder 自动生成一只")]
    public GameObject dogPrefab;
    [Tooltip("小狗碰撞体的 Tag，Gamemode 撞到这个 Tag 就判输")]
    public string obstacleTag = "Obstacle";
    [Tooltip("勾上：Play 时在画面左下角显示小狗的数量，方便检查")]
    public bool showDebug = false;

    class Dog
    {
        public Transform t;
        public Rigidbody rb;
        public CapsuleCollider col;
        public Vector3 pos;
        public Vector3 vel;
        public Vector3 dir;                // 往哪边滑（水平），慢慢转向 downhill
        public Vector3 downhill;           // 这一带整体往山下的方向（看前后左右十几米的高低，不看脚下的小坑小包）
        public float downhillTimer;
        public float speed, targetSpeed;   // 现在的速度、这只小狗现在想滑的速度
        public float cruise;               // 这只小狗自己平时的速度
        public bool passing;               // 从玩家身后补上来、正在超过玩家
        public float passMul;
        public float pace;                 // 跟玩家速度时是玩家的几倍
        public Vector3 checkPos;           // 卡住检查：上一次检查时在哪
        public float checkTimer;
        public int stuck;
        public float drift, wantDrift;     // 离开自己那条线多远、想去哪
        public float wanderTimer;
        public float avoidTimer;           // 正在绕障碍物（这段时间左右移动快一点，也不随便换位置）
        public float avoidSide;            // 正在往哪边绕（-1 左，1 右），绕的过程中不改主意，免得左右犹豫撞上去
        public float phase;
        public bool active;
    }

    readonly List<Dog> dogs = new List<Dog>();
    // 所有生成器（小狗、小鸡、腊肠狗）的动物都在这里面：互相不当成障碍物，不然在山顶挤在一起时会互相躲、一直刹车
    static readonly HashSet<Collider> dogColliders = new HashSet<Collider>();
    readonly HashSet<Collider> snow = new HashSet<Collider>();
    GameObject template;
    Rigidbody playerRb;
    float ws = 1f;          // 场景比例
    Vector3 pPos;           // 玩家身体脚底的位置（rider 的中心点离身体很远，不能直接用 player.position）
    Vector3 top;            // 山顶出生点（玩家开局的位置）
    Vector3 mainDir;        // 整条雪道往下的大方向（水平）
    Collider[] playerCols;
    float spawnTimer, nearTimer, frontTimer, learnTimer;
    int spawned, topBlocked, nearSpawned, nearBlocked, nearNoSlope, recycledEnd, recycledBehind, recycledFar, lostReports;
    string groundUnderPlayer = "";
    string startError = "";
    static readonly RaycastHit[] hits = new RaycastHit[32];
    static readonly Collider[] overlaps = new Collider[32];

    void Start()
    {
        if (player == null)
        {
            var go = GameObject.Find("rider");
            if (go != null) player = go.transform;
        }
        if (player == null)
        {
            startError = "找不到玩家。请把 rider 拖到 Player 一栏。";
            Debug.LogError("SkiingDogSpawner：" + startError);
            enabled = false;
            return;
        }
        playerRb = player.GetComponent<Rigidbody>();
        playerCols = player.GetComponentsInChildren<Collider>();
        pPos = PlayerPos();
        dogColliders.RemoveWhere(c => c == null);   // 上一次 Play 留下的
        if (autoWorldScale) worldScale = MeasureWorldScale();
        if (matchPlayerSize)
        {
            Transform body = FindChild(player, "DogModel");
            dogSize = body != null ? body.lossyScale.y : Mathf.Max(0.1f, worldScale * 1.6f / 1.55f);
        }
        ws = Mathf.Max(0.01f, worldScale);

        // 山顶和下坡方向：看玩家开局脚下的雪
        top = pPos;
        Vector3 g, n;
        int count = Physics.RaycastNonAlloc(pPos + Vector3.up * 30f * ws, Vector3.down, hits, 80f * ws, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var c = hits[i].collider;
            if (c.attachedRigidbody != null || IsPlayer(c) || IsProp(c)) continue;
            snow.Add(c);
            if (c.sharedMaterial != null) snowMats.Add(c.sharedMaterial);
        }
        if (Ground(pPos, out g, out n)) top = g;
        else Debug.LogWarning("SkiingDogSpawner：玩家脚下找不到雪地碰撞体，小狗不知道往哪边是下坡。请确认雪坡的子物体上有 Mesh Collider。");
        mainDir = Flat(Downhill(n));
        if (mainDir == Vector3.zero) mainDir = Flat(player.forward);
        if (mainDir == Vector3.zero) mainDir = Vector3.forward;
        top = FindSummit(top);   // 出生点放在整座雪山的最高处，不是玩家开局的地方

        template = MakeTemplate();
        for (int i = 0; i < Mathf.Min(animalCap, 400); i++)   // 再多会很卡
        {
            var d = MakeDog(i);
            if (d == null) return;
            dogs.Add(d);
        }
        if (fillSlopeAtStart) FillSlope();

        int placed = 0;
        foreach (var d in dogs) if (d.active) placed++;
        Debug.Log("SkiingDogSpawner：场景比例 = " + ws.ToString("0.0") + "，小狗大小 = " + dogSize.ToString("0.0")
                  + "，山顶在 " + top + "，下坡方向 " + mainDir + "，开局雪道上放了 " + placed + " 只");
    }

    // 从 from 顺着雪道往山上走，一直走到不再变高（或者没有雪了），那里就是山顶
    Vector3 FindSummit(Vector3 from)
    {
        Vector3 at = from, best = from;
        Vector3 up = -mainDir;
        for (int i = 0; i < 600; i++)
        {
            Vector3 g, n;
            if (!Ground(at + up * 5f * ws, out g, out n)) break;
            if (g.y < at.y - 0.5f * ws) break;    // 过了山顶开始往下了
            at = g;
            if (g.y > best.y) best = g;
        }
        return best;
    }

    // 在 center 附近（左右 width 范围里）找个空地方放下，最多试 tries 次
    bool TryPlace(Dog d, Vector3 center, Vector3 right, float width, bool moving, int tries, float space = 0f)
    {
        for (int i = 0; i < tries; i++)
        {
            Vector3 p = center + right * Random.Range(-width, width);
            if (space > 0f && AnimalWithin(p, space)) continue;
            if (Place(d, p, moving)) return true;
        }
        return false;
    }

    // ---------------- 生成小狗 ----------------

    GameObject MakeTemplate()
    {
        GameObject model;
        if (dogPrefab != null)
        {
            model = Instantiate(dogPrefab);
        }
        else
        {
            // 挂上 DogScoutBuilder 时它会自动生成 DogModel
            model = new GameObject("DogTemplate");
            model.SetActive(false);
            var builder = model.AddComponent<DogScoutBuilder>();
            builder.size = dogSize;
            builder.buildOnPlayIfMissing = false;
            model.SetActive(true);
            builder.Build();
        }
        model.name = "DogTemplate";
        model.transform.SetParent(transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // 模板身上不能有碰撞体，也不能有脚本再去生成
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var b in model.GetComponentsInChildren<DogScoutBuilder>(true)) DestroyImmediate(b);

        AddSkis(model.transform);
        model.SetActive(false);
        return model;
    }

    // 两块滑雪板，垫在两只脚下面，顺着脸的方向（+Z）
    void AddSkis(Transform root)
    {
        // 借用小狗身上的卡通材质（不是描边那个），换个颜色
        Material mat = null;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var src = r.sharedMaterial;
            if (src == null || r.gameObject.name == "Outline") continue;
            mat = new Material(src) { name = "DogSki" };
            mat.color = skiColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", skiColor);
            break;
        }

        for (int side = -1; side <= 1; side += 2)
        {
            var ski = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(ski.GetComponent<Collider>());
            ski.name = side < 0 ? "SkiL" : "SkiR";
            ski.transform.SetParent(root, false);
            ski.transform.localPosition = new Vector3(0.18f * side, 0.015f, 0.08f) * dogSize;
            ski.transform.localScale = new Vector3(0.11f, 0.03f, 1.3f) * dogSize;
            if (mat != null) ski.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    Dog MakeDog(int index)
    {
        var root = new GameObject("skiing dog");
        root.transform.SetParent(transform, false);
        try { root.tag = obstacleTag; }
        catch (UnityException)
        {
            startError = "项目里没有叫 " + obstacleTag + " 的 Tag。请确认树用的 Tag 名字，并填到 Obstacle Tag 一栏。";
            Debug.LogError("SkiingDogSpawner：" + startError);
            Destroy(root);
            return null;
        }

        var model = Instantiate(template, root.transform, false);
        model.name = "DogModel";
        model.SetActive(true);

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var col = root.AddComponent<CapsuleCollider>();
        ShapeCollider(col);
        dogColliders.Add(col);

        root.SetActive(false);
        return new Dog { t = root.transform, rb = rb, col = col };
    }

    // 碰撞体的形状：小狗、小鸡是竖着的胶囊。腊肠狗那种长身体的，在子类里改成横着的
    protected virtual void ShapeCollider(CapsuleCollider col)
    {
        col.center = new Vector3(0f, 0.75f, 0.05f) * dogSize;
        col.height = 1.5f * dogSize;
        col.radius = 0.38f * dogSize;
    }

    // 开局：从山顶顺着雪道往下，每隔一段放一只，一直放到雪道尽头或者放满
    void FillSlope()
    {
        Vector3 at = top;
        int misses = 0;
        int next = 0;
        for (int guard = 0; guard < 5000 && next < dogs.Count && misses < 4; guard++)
        {
            Vector3 g, n;
            if (!Ground(at, out g, out n)) { misses++; at += mainDir * startGap * ws; continue; }
            Vector3 fwd = MacroDown(g, Forward(n));
            // 顺着坡面往下走一段（带上高度变化，这样两段坡上下错开也能接上）
            Vector3 down3 = Vector3.ProjectOnPlane(fwd, n).normalized;
            at = g + down3 * startGap * ws * Random.Range(0.6f, 1.4f);
            if (!Ground(at, out g, out n)) { misses++; continue; }
            misses = 0;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            if (Place(dogs[next], g + right * Random.Range(-spawnWidth, spawnWidth) * ws, true)) next++;
        }
    }

    // 把一只收起来的小狗放到 spot 附近的雪面上。moving = true 时一出来就是它自己的速度
    bool Place(Dog d, Vector3 spot, bool moving)
    {
        Vector3 ground, normal;
        if (!Ground(spot, out ground, out normal)) return false;
        Vector3 toPlayer = pPos - ground;
        toPlayer.y = 0f;
        if (toPlayer.magnitude < minGap * ws) return false;
        if (NearObstacle(ground)) return false;

        Vector3 fwd = MacroDown(ground, Forward(normal));
        d.downhill = fwd;
        d.dir = fwd;
        d.downhillTimer = Random.Range(0f, 0.5f);
        d.targetSpeed = d.cruise = Random.Range(slowestSpeed, fastestSpeed) * ws;
        if (d.passing) SetPassing(d, false);
        d.pace = Random.Range(paceMul.x, paceMul.y);
        d.checkPos = ground;
        d.checkTimer = 1.5f;
        d.stuck = 0;
        d.speed = moving ? d.targetSpeed : 0f;
        d.vel = fwd * d.speed;
        d.drift = 0f;
        d.wantDrift = 0f;
        d.wanderTimer = Random.Range(0f, wanderEvery);
        d.avoidTimer = 0f;
        d.phase = Random.Range(0f, 2f * Mathf.PI);
        d.pos = ground;
        d.t.SetPositionAndRotation(ground, Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, normal).normalized, normal));
        d.rb.position = ground;
        d.rb.rotation = d.t.rotation;
        d.t.gameObject.SetActive(true);
        d.active = true;
        spawned++;
        return true;
    }

    void Hide(Dog d)
    {
        d.active = false;
        d.t.gameObject.SetActive(false);
    }

    // ---------------- 每帧滑雪 ----------------

    void FixedUpdate()
    {
        if (player == null) return;
        float dt = Time.fixedDeltaTime;
        if (!reported && Time.timeSinceLevelLoad > 20f) { reported = true; LogSummary(); }
        pPos = PlayerPos();
        learnTimer -= dt;
        if (learnTimer <= 0f) { learnTimer = 0.5f; LearnSnowUnderPlayer(); }
        Vector3 pVel = playerRb != null ? playerRb.velocity : Vector3.zero;
        pVel.y = 0f;
        float playerSpeed = pVel.magnitude;
        float playerDownSpeed = playerSpeed;

        // 山顶每隔一会儿出来一只
        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
            spawnTimer = topSpawnEvery * Random.Range(0.7f, 1.3f);
            Dog d = FreeDog();
            if (d == null) d = FarBehindDog();       // 都在用：拿一只远远落在玩家身后、看不见的来用
            if (d != null)
            {
                if (d.active) Hide(d);
                Vector3 right = Vector3.Cross(Vector3.up, mainDir);
                if (!TryPlace(d, top, right, spawnWidth * ws, true, 6, 3f * dogSize)) topBlocked++;   // 一出来就在滑；旁边已经有动物就不放，免得在山顶堵成一堆
            }
        }

        // 玩家身边太少：在玩家身后（镜头外）补一只，让它比玩家快一点，从玩家身边滑过去
        //（池子满了就先拿一只离玩家最远、看不见的来用）
        Vector3 back = Flat(-pVel);
        if (playerSpeed < 1f * ws || Vector3.Dot(back, mainDir) > 0f) back = -mainDir;   // 停着或者方向怪：就用整条雪道往上的方向

        // ① 从背后追上来的：在镜头后面补，比玩家快一点从身边滑过去
        nearTimer -= dt;
        if (comeFromBehind && nearTimer <= 0f && PasserCount() < fromBehindAtOnce)
        {
            nearTimer = fromBehindEvery * Random.Range(0.7f, 1.3f);
            Dog d = FreeDog();
            if (d == null) d = FarthestDog();
            Vector3 spot;
            if (d != null && BehindSlope(pPos, back, BehindDistance(), out spot))
            {
                if (d.active) { Hide(d); recycledFar++; }
                if (PlaceBehind(d, spot, -back, playerSpeed)) nearSpawned++;
                else nearBlocked++;
            }
            else if (d != null) nearNoSlope++;
        }

        // ② 前方不够：在玩家前方不远处补，离玩家的路线左右空开一点
        frontTimer -= dt;
        if (frontTimer <= 0f && FrontCount() < nearCount)
        {
            frontTimer = frontEvery * Random.Range(0.7f, 1.3f);
            Dog d = FreeDog();
            if (d == null) d = FarthestDog();
            Vector3 spot;
            if (d != null && AlongSlope(pPos, Random.Range(fillFrontNear, fillFrontFar) * ws, out spot))
            {
                if (d.active) { Hide(d); recycledFar++; }
                if (PlaceAside(d, spot, -back)) nearSpawned++;
                else nearBlocked++;
            }
            else if (d != null) nearNoSlope++;
        }

        foreach (var d in dogs)
        {
            if (!d.active) continue;

            Vector3 ground, normal;
            if (!Ground(d.pos, out ground, out normal)) { ReportLost(d.pos); Hide(d); recycledEnd++; continue; }  // 滑出雪道（到底了）

            // 落在玩家身后（比玩家高）很远、又比玩家慢（永远追不上了），或者落后实在太远：收回去
            // 以前落在玩家身后就马上收走，结果刚从山顶出来的全被收掉了，所以现在不收，池子满了再拿来用
            Vector3 flatGap = pPos - d.pos;
            flatGap.y = 0f;
            bool close = flatGap.magnitude < 150f * ws;   // 离玩家远的看不清，少算一点，省性能

            // 方向：转向这一带整体往山下的方向。离它超过 60 度就直接拉回来，所以绝不会掉头往山上走
            d.downhillTimer -= dt;
            if (d.downhillTimer <= 0f)
            {
                d.downhillTimer = Random.Range(0.4f, 0.6f);
                d.downhill = MacroDown(ground, d.downhill);
            }
            d.dir = Vector3.Slerp(d.dir, d.downhill, 2f * dt).normalized;
            if (Vector3.Dot(d.dir, d.downhill) < 0.5f) d.dir = Vector3.RotateTowards(d.downhill, d.dir, 60f * Mathf.Deg2Rad, 0f).normalized;
            Vector3 fwd = d.dir;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            // 隔一会儿换一个左右位置，只在自己那条线附近
            d.wanderTimer -= dt;
            if (d.wanderTimer <= 0f)
            {
                d.wanderTimer = wanderEvery * Random.Range(0.6f, 1.4f);
                d.wantDrift = Random.Range(-wanderWidth, wanderWidth) * ws;
            }

            // 从身后追上来的：一直比玩家快一点；超过玩家一段以后，慢慢回到自己平时的速度
            if (d.passing)
            {
                // 离得远的时候追得更快（大约 2.5 秒追上来），追到旁边以后就是比玩家快一点
                float gapBehind = d.pos.y > pPos.y ? flatGap.magnitude : 0f;
                d.targetSpeed = Mathf.Max(d.cruise, playerSpeed * d.passMul, playerSpeed + gapBehind / 2.5f);
                bool passed = d.pos.y < pPos.y - 1f * ws && flatGap.magnitude > 25f * ws;
                if (passed || flatGap.magnitude > 6f * fillBehindFar * ws) { SetPassing(d, false); d.targetSpeed = d.cruise; }
            }

            // 在你附近：跟着你的速度滑（只会比自己平时快，不会因为你慢就停下等你）
            else if (keepPace && flatGap.magnitude < paceRange * ws)
                d.targetSpeed = Mathf.Max(d.cruise, playerSpeed * d.pace);
            else d.targetSpeed = d.cruise;

            // 前面有树、北极熊、石头：提前看哪边空，从那边绕过去；越近越慢
            float want = d.targetSpeed;
            d.avoidTimer -= dt;
            Vector3 along = Vector3.ProjectOnPlane(fwd, normal).normalized;     // 贴着坡面往前看（不然下坡时会从矮的北极熊头顶上看过去）
            Vector3 center = ground + normal * 0.75f * dogSize;
            float look = Mathf.Max(6f * dogSize, d.speed * 1.8f);
            float castR = 0.6f * dogSize;
            float hitDist;
            Collider blocker;
            if (ObstacleAhead(center, along, castR, look, out hitDist, out blocker) && close)
            {
                float dl, dr;
                Collider unused;
                if (!ObstacleAhead(center, Quaternion.AngleAxis(-35f, normal) * along, castR, look, out dl, out unused)) dl = 2f * look;
                if (!ObstacleAhead(center, Quaternion.AngleAxis(35f, normal) * along, castR, look, out dr, out unused)) dr = 2f * look;
                Bounds bb = blocker.bounds;
                float obsSide = Vector3.Dot(bb.center - ground, right);           // 障碍物在左边还是右边
                float away;
                if (d.avoidTimer > 0f && d.avoidSide != 0f) away = d.avoidSide;      // 已经决定了往哪边绕
                else if (Mathf.Abs(dl - dr) > 0.5f * dogSize) away = dr > dl ? 1f : -1f;   // 哪边空往哪边
                else away = obsSide > 0f ? -1f : 1f;                                   // 一样空：往障碍物的另一边
                d.avoidSide = away;
                float half = Mathf.Min(Mathf.Max(bb.extents.x, bb.extents.z), 8f * dogSize);
                d.wantDrift = d.drift + obsSide + away * (half + 1.1f * dogSize);
                d.avoidTimer = 1.2f;
                d.wanderTimer = Mathf.Max(d.wanderTimer, 1.5f);
                want = Mathf.Min(want, Mathf.Lerp(0.3f * d.targetSpeed, d.targetSpeed, hitDist / look));
            }

            // 前面很近的地方是玩家：往旁边让，必要时放慢一点，不从后面撞上去
            Vector3 toPlayer = pPos - d.pos;
            toPlayer.y = 0f;
            float ahead = Vector3.Dot(toPlayer, fwd);
            float side = Vector3.Dot(toPlayer, right);
            // 以前会一直减速到和玩家一样快：玩家开局停着不动时，后面的动物全堵在玩家身后，挤成一堆下不来。
            // 现在是快速往旁边绕过去，只有正对着玩家、又很近的时候才稍微慢一点
            if (ahead > 0f && ahead < 12f * dogSize && Mathf.Abs(side) < 2.5f * dogSize)
            {
                d.wantDrift = d.drift + (side > 0f ? -1f : 1f) * (3f * dogSize - Mathf.Abs(side));
                d.avoidTimer = Mathf.Max(d.avoidTimer, 0.5f);
                if (ahead < 4f * dogSize && Mathf.Abs(side) < 1.2f * dogSize)
                    want = Mathf.Min(want, Mathf.Max(playerDownSpeed, 0.4f * d.cruise));
            }

            d.speed = Mathf.MoveTowards(d.speed, want, (want < d.speed ? 3f : d.passing ? 6f : 1f) * acceleration * ws * dt);   // 刹车比加速快

            // 左右：慢慢靠向想去的位置 + 一点点摆动
            bool avoiding = d.avoidTimer > 0f;
            float sideMax = avoiding ? 0.9f * d.speed + 2f * ws : 0.35f * d.speed + 0.5f * ws;
            float sideSpeed = Mathf.Clamp((d.wantDrift - d.drift) * (avoiding ? 2.5f : 0.8f), -sideMax, sideMax);
            float moving = Mathf.Clamp01(d.speed / (2f * ws));
            float w = 2f * Mathf.PI / Mathf.Max(0.5f, swayPeriod);
            d.phase += w * dt * moving;
            float sway = swayWidth * dogSize * w * Mathf.Cos(d.phase) * moving;
            d.drift += sideSpeed * dt;
            d.vel = fwd * d.speed + right * sideSpeed;

            // 走一步，再贴回雪面上
            Vector3 step = (d.vel + right * sway) * dt;
            Vector3 next = ground + step;
            Vector3 g2, n2;
            if (Ground(next, out g2, out n2)) { next = g2; normal = n2; }
            if (close) next = PushOut(d, next, ref normal);     // 万一碰上了树、熊或者别的小狗：推出来，不穿模
            d.pos = next;
            d.rb.MovePosition(next);

            // 朝向：脸朝前进方向（只会朝山下）
            Vector3 face = Vector3.ProjectOnPlane(moving > 0.2f ? step : fwd, normal);
            if (face.sqrMagnitude < 0.0001f || Vector3.Dot(face, fwd) <= 0f) face = Vector3.ProjectOnPlane(fwd, normal);
            Quaternion rot = Quaternion.LookRotation(face.normalized, normal);
            d.rb.MoveRotation(Quaternion.Slerp(d.rb.rotation, rot, 6f * dt));

            // 卡住了（1.5 秒里没怎么往前走）：换个方向挤出来；连着卡两次，就把它往山下挪一段
            d.checkTimer -= dt;
            if (d.checkTimer <= 0f)
            {
                d.checkTimer = 1.5f;
                Vector3 moved = d.pos - d.checkPos;
                moved.y = 0f;
                d.checkPos = d.pos;
                if (moved.magnitude < 0.3f * d.cruise * 1.5f)
                {
                    d.stuck++;
                    d.wantDrift = d.drift + Random.Range(-2f, 2f) * dogSize * 2f;
                    d.avoidTimer = 1f;
                    if (d.stuck >= 2)
                    {
                        Vector3 spot;
                        bool near = Vector3.Distance(d.pos, pPos) < 150f * ws;
                        if (near || !AlongSlope(d.pos, 15f * ws, out spot) || !TryPlace(d, spot, right, 3f * ws, true, 4, 2f * dogSize))
                        {
                            if (!near) { Hide(d); recycledEnd++; }   // 在玩家看不见的地方：直接收回去
                        }
                    }
                }
                else d.stuck = 0;
            }
        }
    }

    // 落在玩家身后（比玩家高）最远的那只，超过 recycleBehind 才算
    Dog FarBehindDog()
    {
        Dog best = null;
        float far = recycleBehind * ws;
        foreach (var d in dogs)
        {
            if (!d.active || d.pos.y <= pPos.y) continue;
            Vector3 v = d.pos - pPos;
            v.y = 0f;
            if (v.magnitude > far) { far = v.magnitude; best = d; }
        }
        if (best != null) recycledBehind++;
        return best;
    }

    Dog FreeDog()
    {
        foreach (var d in dogs) if (!d.active) return d;
        return null;
    }

    // 离玩家最远、而且不在玩家附近的那只（池子满了要补的时候拿它来用）
    Dog FarthestDog()
    {
        Dog best = null;
        float bestScore = 0f;
        foreach (var d in dogs)
        {
            if (!d.active) continue;
            float dist = Vector3.Distance(d.pos, pPos);
            bool behind = d.pos.y > pPos.y + 2f * ws;
            if (behind ? dist < recycleBehind * ws : dist < nearFront * ws * 1.5f) continue;   // 看得见的不拿
            float score = behind ? dist * 2f : dist;      // 先拿身后的
            if (score > bestScore) { bestScore = score; best = d; }
        }
        return best;
    }

    // 玩家前方（比玩家低）Near Front 米以内、不是正在超车的
    int FrontCount()
    {
        int c = 0;
        float r = nearFront * ws;
        foreach (var d in dogs)
        {
            if (!d.active || d.passing || d.pos.y > pPos.y) continue;
            Vector3 v = d.pos - pPos;
            v.y = 0f;
            if (v.magnitude < r) c++;
        }
        return c;
    }

    int PasserCount()
    {
        int c = 0;
        foreach (var d in dogs) if (d.active && d.passing) c++;
        return c;
    }

    // 身后多远：一定要在镜头后面（镜头本身就在玩家身后一段），再往后 fillBehindNear～fillBehindFar 米
    float BehindDistance()
    {
        float camBack = 0f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 v = cam.transform.position - pPos;
            v.y = 0f;
            camBack = v.magnitude;
        }
        return camBack + Random.Range(fillBehindNear, fillBehindFar) * ws * 0.5f;
    }

    int NearCount()
    {
        int c = 0;
        float r = nearFront * ws;
        foreach (var d in dogs)
        {
            if (!d.active) continue;
            if (d.pos.y > pPos.y + 2f * ws && !d.passing) continue;     // 身后（比玩家高）的看不见，不算；正从身后追上来的算
            Vector3 v = d.pos - pPos;
            v.y = 0f;
            if (v.magnitude < r) c++;
        }
        return c;
    }

    // 从 from 顺着雪道往下走 dist 远（一小段一小段地贴着坡面走，这样两段坡上下错开也能接上）
    bool AlongSlope(Vector3 from, float dist, out Vector3 spot)
    {
        spot = from;
        Vector3 g, n;
        if (!Ground(from, out g, out n)) return false;
        float stepLen = 5f * ws;
        Vector3 keep = Forward(n);
        for (float done = 0f; done < dist; done += stepLen)
        {
            keep = MacroDown(g, keep);
            Vector3 down3 = Vector3.ProjectOnPlane(keep, n).normalized;
            Vector3 next = g + down3 * Mathf.Min(stepLen, dist - done);
            if (!Ground(next, out g, out n)) return false;
        }
        spot = g;
        return true;
    }

    // 从 from 往 back（山上）方向贴着雪面走 dist 远
    bool BehindSlope(Vector3 from, Vector3 back, float dist, out Vector3 spot)
    {
        spot = from;
        Vector3 g, n;
        if (!Ground(from, out g, out n)) return false;
        float stepLen = 5f * ws;
        for (float done = 0f; done < dist; done += stepLen)
        {
            Vector3 next = g + back * Mathf.Min(stepLen, dist - done);
            if (!Ground(next, out g, out n)) return false;
        }
        spot = g;
        return true;
    }

    // 在玩家身后放一只：左右离开玩家的路线一段，不能在镜头里看得见，周围要空，一出来就比玩家快
    bool PlaceBehind(Dog d, Vector3 spot, Vector3 fwd, float playerSpeed)
    {
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        float minSide = passSideGap * dogSize;
        float maxSide = Mathf.Max(minSide + dogSize, spawnWidth * ws);
        Camera cam = Camera.main;
        for (int i = 0; i < 8; i++)
        {
            float side = Random.Range(minSide, maxSide) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 p = spot + right * side;
            Vector3 g, n;
            if (!Ground(p, out g, out n)) continue;
            if (cam != null && i < 5)   // 前几次挑镜头看不见的地方，实在找不到就算了
            {
                Vector3 v = cam.WorldToViewportPoint(g + Vector3.up * dogSize);
                if (v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f) continue;   // 镜头里看得见：换个地方
            }
            if (AnimalWithin(g, 3f * dogSize)) continue;   // 别和别的动物挤在一起
            if (!Place(d, g, true)) continue;
            d.passMul = passSpeedUp * Random.Range(0.9f, 1.15f);
            SetPassing(d, true);
            Vector3 gap = pPos - g;
            gap.y = 0f;
            d.targetSpeed = d.speed = Mathf.Max(d.cruise, playerSpeed * d.passMul, playerSpeed + gap.magnitude / 2.5f);
            d.vel = d.dir * d.speed;
            return true;
        }
        return false;
    }

    // 在玩家前方放一只：左右离开玩家的路线一段（不挡路），周围要空
    bool PlaceAside(Dog d, Vector3 spot, Vector3 fwd)
    {
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        float minSide = passSideGap * dogSize;
        float maxSide = Mathf.Max(minSide + dogSize, spawnWidth * ws);
        for (int i = 0; i < 8; i++)
        {
            float side = Random.Range(minSide, maxSide) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 g, n;
            if (!Ground(spot + right * side, out g, out n)) continue;
            if (AnimalWithin(g, 3f * dogSize)) continue;   // 别和别的动物挤在一起
            if (Place(d, g, true)) return true;
        }
        return false;
    }

    // 正在从身后超车的动物：擦到玩家也不算撞（不判输、也不会把玩家推开），超过去以后恢复
    void SetPassing(Dog d, bool on)
    {
        d.passing = on;
        try { d.t.gameObject.tag = on ? "Untagged" : obstacleTag; } catch (UnityException) { }
        if (playerCols != null)
            foreach (var pc in playerCols)
                if (pc != null && d.col != null) Physics.IgnoreCollision(d.col, pc, on);
    }

    bool AnimalWithin(Vector3 p, float r)
    {
        int n = Physics.OverlapSphereNonAlloc(p + Vector3.up * dogSize, r, overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) if (dogColliders.Contains(overlaps[i])) return true;
        return false;
    }

    // 这一带整体往山下的方向：看周围一圈 12 个方向、十米开外的雪面，哪边最低就往哪边。
    // 脚下的小坑小包骗不了它（以前只看脚下，掉进小坑时会以为坑的另一边是“下坡”，就往山上爬了）
    Vector3 MacroDown(Vector3 p, Vector3 keep)
    {
        keep = Flat(keep);
        float r = 10f * ws;
        float bestScore = float.MinValue;
        Vector3 best = Vector3.zero;
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6f;
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            if (Vector3.Dot(dir, mainDir) < 0.25f) continue;    // 只看大致往山下的方向，不会往两边的坡边或者翻过山顶往回走
            Vector3 g, n;
            if (!Ground(p + dir * r, out g, out n)) continue;
            float drop = p.y - g.y;
            if (drop <= 0f) continue;
            float score = drop + (keep != Vector3.zero ? Vector3.Dot(dir, keep) * 0.1f * r : 0f);   // 差不多低时，选和原来方向接近的，免得来回抖
            if (score > bestScore) { bestScore = score; best = dir; }
        }
        if (best == Vector3.zero) return mainDir;   // 平地：顺着整条雪道的方向滑
        return best;
    }

    // 这里往哪边是“往下滑”：脚下雪坡最陡的下坡方向（雪道拐弯也跟着拐，永远朝山下）。
    // 几乎是平地、看不出下坡时，就保持原来的方向
    Vector3 Forward(Vector3 normal) { return Forward(normal, mainDir); }

    Vector3 Forward(Vector3 normal, Vector3 keep)
    {
        if (normal.y > 0.995f) return keep;
        Vector3 local = Flat(Downhill(normal));
        return local != Vector3.zero ? local : keep;
    }

    // 第一次有小狗因为“脚下找不到雪”消失时，把它脚下有什么打印到 Console，方便查
    void ReportLost(Vector3 p)
    {
        if (lostReports >= 3) return;
        lostReports++;
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 30f * ws, Vector3.down, hits, 80f * ws, ~0, QueryTriggerInteraction.Ignore);
        string what = n == 0 ? "什么都没有（雪道到头了）" : "";
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            what += "\n  " + c.name + "（Tag " + c.tag + "，大小 " + c.bounds.size.ToString("0") + "）";
        }
        Debug.Log("SkiingDogSpawner：一只在 " + p.ToString("0") + " 的小狗脚下找不到雪，消失了。它脚下是：" + what);
    }

    // ---------------- 小工具 ----------------

    // 玩家身体的位置：用玩家身上碰撞体（hitbox）的范围来算，取脚底中间
    Vector3 PlayerPos()
    {
        bool any = false;
        Bounds b = new Bounds(player.position, Vector3.zero);
        if (playerCols != null)
            foreach (var c in playerCols)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; }
                else b.Encapsulate(c.bounds);
            }
        if (!any) return player.position;
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    bool IsPlayer(Collider c)
    {
        if (playerCols == null) return false;
        foreach (var pc in playerCols) if (pc == c) return true;
        return false;
    }

    static Transform FindChild(Transform t, string name)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true))
            if (c.name == name) return c;
        return null;
    }

    // 玩家模型有多高 ÷ 1.6 米 = 场景比真实世界大几倍
    float MeasureWorldScale()
    {
        bool any = false;
        Bounds b = new Bounds(player.position, Vector3.zero);
        foreach (var r in player.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any || b.size.y < 0.01f) return worldScale;
        return Mathf.Clamp(b.size.y / 1.6f, 0.1f, 1000f);
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
    }

    bool NearObstacle(Vector3 p)
    {
        int n = Physics.OverlapSphereNonAlloc(p + Vector3.up * dogSize, 1.4f * dogSize, overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = overlaps[i];
            if (dogColliders.Contains(c)) return true;              // 别和别的小狗叠在一起
            if (IsObstacle(c)) return true;                          // 树、北极熊、石头
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return true;  // 玩家
        }
        return false;
    }

    // 树、北极熊、石头这类摆在雪道上的东西：自己或上面某一层的 Tag 是 Obstacle，或者放在 Decorations 里，而且不是一大块地面。
    // （SlopeDecorator 把树和熊放在 slope 下面，以前按名字被当成了雪地，所以小狗不躲它们，还会穿过去）
    bool IsProp(Collider c)
    {
        Vector3 size = c.bounds.size;
        if (size.x >= 30f * ws && size.z >= 30f * ws) return false;
        for (Transform t = c.transform; t != null; t = t.parent)
            if (t.CompareTag(obstacleTag) || t.name == "Decorations") return true;
        return false;
    }

    // 小狗要绕开、不能穿过去的东西：除了雪地、小狗自己、玩家以外，所有实心的碰撞体
    bool IsObstacle(Collider c)
    {
        if (c == null || c.isTrigger || dogColliders.Contains(c) || IsPlayer(c)) return false;
        if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return false;   // 会动的滑雪者（玩家）另外处理
        return !IsSnow(c);
    }

    // 从 from 往 dir 方向扫一个球，看最近的障碍物有多远
    bool ObstacleAhead(Vector3 from, Vector3 dir, float radius, float dist, out float hitDist, out Collider blocker)
    {
        hitDist = dist;
        blocker = null;
        int n = Physics.SphereCastNonAlloc(from, radius, dir, hits, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (!IsObstacle(c)) continue;
            if (hits[i].distance < hitDist || blocker == null) { hitDist = hits[i].distance; blocker = c; }
        }
        return blocker != null;
    }

    // 小狗站在 p 时如果和障碍物或者别的小狗叠在一起，就沿水平方向推出来（最多推三次），再贴回雪面
    Vector3 PushOut(Dog d, Vector3 p, ref Vector3 normal)
    {
        Quaternion rot = d.rb.rotation;
        float r = d.col.radius;
        float half = Mathf.Max(0f, d.col.height * 0.5f - r);
        for (int iter = 0; iter < 3; iter++)
        {
            Vector3 c = p + rot * d.col.center;
            Vector3 axis = rot * (d.col.direction == 0 ? Vector3.right : d.col.direction == 2 ? Vector3.forward : Vector3.up);
            int n = Physics.OverlapCapsuleNonAlloc(c - axis * half, c + axis * half, r, overlaps, ~0, QueryTriggerInteraction.Ignore);
            bool moved = false;
            for (int i = 0; i < n; i++)
            {
                var o = overlaps[i];
                if (o == d.col) continue;
                bool otherDog = dogColliders.Contains(o);
                if (!otherDog && !IsObstacle(o)) continue;
                Vector3 dir;
                float dist;
                if (!Physics.ComputePenetration(d.col, p, rot, o, o.transform.position, o.transform.rotation, out dir, out dist)) continue;
                dir.y = 0f;
                float flat = dir.magnitude;
                if (flat < 0.05f) continue;
                dir /= flat;
                p += dir * ((otherDog ? 0.5f : 1f) * dist / Mathf.Max(flat, 0.3f) + 0.02f * dogSize);   // 两只小狗各让一半
                moved = true;
            }
            if (!moved) break;
            Vector3 g, nn;
            if (Ground(p, out g, out nn)) { p = g; normal = nn; }
        }
        return p;
    }

    // 这个碰撞体算不算雪地（算一次就记住）：
    // 玩家踩过的、名字叫 slope… / mountain…（包括 SlopeExtender 加的 slope extra）的、和雪坡同一个物理材质的、
    // 或者没设成 Obstacle 的大块地面，都算。树、石头、小狗、玩家都不算。
    readonly Dictionary<Collider, bool> snowCache = new Dictionary<Collider, bool>();
    readonly HashSet<PhysicMaterial> snowMats = new HashSet<PhysicMaterial>();

    bool IsSnow(Collider c)
    {
        if (snow.Contains(c)) return true;
        bool yes;
        if (snowCache.TryGetValue(c, out yes)) return yes;
        yes = false;
        if (c.attachedRigidbody == null && !dogColliders.Contains(c) && !IsPlayer(c) && !IsProp(c))
        {
            Vector3 size = c.bounds.size;
            bool wide = Mathf.Max(size.x, size.z) >= 10f * ws;     // 树和熊再大也没这么宽，雪道一段有几百米
            for (Transform t = c.transform; t != null && !yes && wide; t = t.parent)
            {
                string n = t.name.ToLower();
                if (n.StartsWith("slope") || n.StartsWith("mountain")) yes = true;
            }
            if (!yes && c.sharedMaterial != null && snowMats.Contains(c.sharedMaterial)) yes = true;
            if (!yes)
            {
                Vector3 sz = c.bounds.size;
                float big = Mathf.Max(sz.x, sz.z);
                if (c.CompareTag(obstacleTag)) yes = sz.x >= 30f * ws && sz.z >= 30f * ws;
                else yes = big >= 15f * ws;
            }
        }
        snowCache[c] = yes;
        return yes;
    }

    // 玩家脚下踩着的东西一定是雪：每隔一会儿看一眼，记下来
    void LearnSnowUnderPlayer()
    {
        int n = Physics.RaycastNonAlloc(pPos + Vector3.up * 2f * ws, Vector3.down, hits, 10f * ws, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Collider under = null;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c.attachedRigidbody != null || IsPlayer(c) || dogColliders.Contains(c)) continue;
            if (hits[i].distance < best) { best = hits[i].distance; under = c; }
        }
        groundUnderPlayer = under != null ? under.name : "（空中）";
        if (under == null || IsProp(under)) return;
        if (snow.Add(under) && under.sharedMaterial != null) snowMats.Add(under.sharedMaterial);
        snowCache[under] = true;
    }

    // 从上往下找雪地（只认 IsSnow 的），所以小狗不会爬到石头上去。
    // 有好几层雪地时（两段坡上下叠着），选高度最接近 p 的那一层
    bool Ground(Vector3 p, out Vector3 point, out Vector3 normal)
    {
        point = p;
        normal = Vector3.up;
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 30f * ws, Vector3.down, hits, 80f * ws, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (!IsSnow(c)) continue;
            float diff = Mathf.Abs(hits[i].point.y - p.y);
            if (diff < best)
            {
                best = diff;
                point = hits[i].point;
                normal = hits[i].normal;
                if (normal.y < 0f) normal = -normal;   // 坡面反了也让小狗站在上面
                found = true;
            }
        }
        return found;
    }

    // Play 20 秒时在 Console 里打一行：现在雪道上有几只、为什么出不来（方便截图给我看）
    bool reported;
    void LogSummary()
    {
        int count = 0;
        foreach (var d in dogs) if (d.active) count++;
        Debug.Log(name + "：20 秒时雪道上 " + count + " / " + dogs.Count + " 只，你身边 " + NearCount() + " 只（想要至少 " + nearCount
                  + "）。一共出来过 " + spawned + " 只，在你前方补的 " + nearSpawned + " 只。山顶被挡 " + topBlocked
                  + " 次，前方被挡 " + nearBlocked + " 次，前方找不到雪道 " + nearNoSlope + " 次。消失：滑出雪道 " + recycledEnd
                  + "，落在后面 " + recycledBehind + "。山顶在 " + top.ToString("0") + "，玩家在 " + pPos.ToString("0"));
    }

    // 画面左下角的检查信息
    void OnGUI()
    {
        if (!showDebug) return;
        string text;
        if (startError != "") text = "小狗出错：" + startError;
        else if (player == null) text = "小狗：没有玩家";
        else
        {
            int count = 0, near = 0;
            float nearest = float.MaxValue;
            foreach (var d in dogs)
            {
                if (!d.active) continue;
                count++;
                float dist = Vector3.Distance(d.pos, pPos);
                nearest = Mathf.Min(nearest, dist);
                if (dist < 40f * ws) near++;
            }
            text = "小狗：在雪道上 " + count + " / " + dogs.Count + " 只，" + (40f * ws).ToString("0") + " 米内 " + near + " 只";
            if (count > 0) text += "，最近一只 " + nearest.ToString("0") + " 米";
            text += "\n一共出来过 " + spawned + " 只（其中在玩家前方补的 " + nearSpawned + " 只）。消失：滑出雪道 " + recycledEnd
                  + "，落在后面 " + recycledBehind + "，太远挪过来 " + recycledFar;
            text += "\n前方补不了：找不到雪道 " + nearNoSlope + " 次，位置被挡 " + nearBlocked + " 次。玩家脚下：" + groundUnderPlayer;
        }
        GUI.color = Color.black;
        GUI.Label(new Rect(12, Screen.height - 80, 1000, 75), text);
    }

    // ---------------- 幽灵附身用（GhostController 调用） ----------------

    public int DogCount => dogs.Count;
    public virtual float DogHeight => 1.5f * dogSize;

    // 第 i 只小狗现在在哪（脚底）、速度多少。不在雪道上就返回 false
    public bool DogInfo(int i, out Vector3 feet, out Vector3 velocity)
    {
        feet = Vector3.zero;
        velocity = Vector3.zero;
        if (i < 0 || i >= dogs.Count || !dogs[i].active) return false;
        feet = dogs[i].pos;
        velocity = dogs[i].vel;
        return true;
    }

    // 把第 i 只小狗放到 feet（以前的“交换身体”用），它接着按自己的速度滑
    public void SwapDog(int i, Vector3 feet, Vector3 velocity)
    {
        if (i < 0 || i >= dogs.Count) return;
        var d = dogs[i];
        Vector3 ground, normal;
        if (!Ground(feet, out ground, out normal)) { ground = feet; normal = Vector3.up; }
        Vector3 fwd = Forward(normal);
        d.speed = Mathf.Max(0f, Vector3.Dot(velocity, fwd));
        d.vel = fwd * d.speed;
        d.pos = ground;
        d.drift = d.wantDrift = 0f;
        d.t.SetPositionAndRotation(ground, Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, normal).normalized, normal));
        d.rb.position = ground;
        d.rb.rotation = d.t.rotation;
    }

    // 幽灵附身到第 i 只小狗：这只小狗从此不归这个脚本管（交给键盘控制），再准备一只新的，以后从山顶出来
    public Transform ReleaseDog(int i, out Vector3 velocity)
    {
        velocity = Vector3.zero;
        if (i < 0 || i >= dogs.Count || !dogs[i].active) return null;
        var d = dogs[i];
        velocity = d.vel;
        dogs.RemoveAt(i);
        d.t.SetParent(null, true);
        d.t.name = "possessed dog";

        var fresh = MakeDog(dogs.Count);
        if (fresh != null) dogs.Add(fresh);
        return d.t;
    }

    // 幽灵离开了这只动物（附身到别的身上去了）：把它收回来，变回普通的滑雪动物，接着往下滑（以前会停在原地）
    public void AdoptDog(Transform root, Vector3 velocity)
    {
        if (root == null) return;
        foreach (var sk in root.GetComponents<SkierController>()) { sk.enabled = false; Destroy(sk); }
        var rb = root.GetComponent<Rigidbody>();
        var col = root.GetComponent<CapsuleCollider>();
        if (rb == null || col == null) return;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        try { root.gameObject.tag = obstacleTag; } catch (UnityException) { }
        root.SetParent(transform, true);
        root.name = "skiing dog";
        Transform model = root.Find("DogModel");
        if (model != null) { model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; }   // 附身时身体被歪过，摆正
        dogColliders.Add(col);

        var d = new Dog { t = root, rb = rb, col = col };
        Vector3 g, n;
        if (!Ground(root.position, out g, out n)) { g = root.position; n = Vector3.up; }
        Vector3 fwd = MacroDown(g, Forward(n));
        d.downhill = d.dir = fwd;
        d.downhillTimer = 0.5f;
        d.targetSpeed = d.cruise = Random.Range(slowestSpeed, fastestSpeed) * ws;
        d.speed = Mathf.Clamp(Vector3.Dot(velocity, fwd), d.cruise * 0.5f, d.cruise * 3f);   // 带着原来的速度，慢慢回到自己的速度
        d.vel = fwd * d.speed;
        d.wanderTimer = Random.Range(0f, wanderEvery);
        d.phase = Random.Range(0f, 2f * Mathf.PI);
        d.pos = g;
        root.SetPositionAndRotation(g, Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, n).normalized, n));
        rb.position = g;
        rb.rotation = root.rotation;
        root.gameObject.SetActive(true);
        d.active = true;
        dogs.Add(d);

        // 附身时补过一只新的，现在收回来一只，所以去掉一只没在用的，总数不变
        for (int i = 0; i < dogs.Count; i++)
            if (!dogs[i].active) { Destroy(dogs[i].t.gameObject); dogs.RemoveAt(i); break; }
    }

    // 幽灵换到另一个滑雪者身上：小狗以后躲着他（山顶出生点不变）
    public void SetPlayer(Transform newPlayer)
    {
        if (newPlayer == null) return;
        player = newPlayer;
        playerRb = player.GetComponent<Rigidbody>();
        playerCols = player.GetComponentsInChildren<Collider>();
        pPos = PlayerPos();
    }

    // 坡面上最陡的下坡方向
    static Vector3 Downhill(Vector3 normal)
    {
        Vector3 d = Vector3.ProjectOnPlane(Vector3.down, normal);
        if (d.sqrMagnitude < 0.0001f) return Vector3.forward;
        return d.normalized;
    }
}
