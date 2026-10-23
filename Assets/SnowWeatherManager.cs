// @role Snowstorm cycle (clear, then snow) that follows the camera, with fog and wind
// @calls GhostController
// @tune clearDuration:float:10-300:Seconds of clear weather
// @tune snowDuration:float:5-120:Seconds of snowstorm
// @tune maxSnowflakes:int:500-20000:Snowflakes on screen
// @tune snowFogStart:float:10-500:Fog start distance while snowing
// @tune snowFogEnd:float:100-2000:Fog end distance while snowing
// @tune skierWindStrength:float:0-1:How hard the wind pushes the skier
// @tune snowWindStrength:float:0-3:How hard the wind blows the snowflakes
using UnityEngine;

// 在 GhostController 移动镜头之后再执行，雪才会每帧准确地跟在镜头前面
[DefaultExecutionOrder(1000)]
public class SnowWeatherManager : MonoBehaviour
{
    [Header("绑定组件（可以留空，会自动查找）")]
    public Transform mainCamera;          // 可留空：自动用正在渲染的主镜头
    public ParticleSystem snowParticle;   // 可留空：自动找名字叫 SnowParticle 的粒子

    [Header("世界比例（你的场景放大了约 19 倍）")]
    [Tooltip("雪花大小和风力都会乘以这个数")]
    public float worldScale = 19f;
    [Tooltip("雪的发射器放在镜头前方多远")]
    public float forwardOffset = 150f;
    [Tooltip("雪的发射器放在镜头上方多高")]
    public float upOffset = 150f;
    [Tooltip("勾上：雪跟着镜头一起走（永远在眼前）；不勾：雪留在世界里，滑过去会被甩在身后，更真实")]
    public bool snowMovesWithCamera = false;

    [Header("雪花设置（会覆盖 SnowParticle 里原来的数值）")]
    [Tooltip("下雪的范围有多宽（以镜头前方为中心的正方形）")]
    public float snowAreaWidth = 800f;
    [Tooltip("下雪的范围有多厚")]
    public float snowAreaHeight = 50f;
    [Tooltip("最小雪花（正常比例，会乘以 World Scale）")]
    public float flakeSizeMin = 0.1f;
    [Tooltip("最大雪花（正常比例，会乘以 World Scale）")]
    public float flakeSizeMax = 0.3f;
    [Tooltip("同时最多有多少片雪花，越大越密，但越卡")]
    public int maxSnowflakes = 5000;
    [Tooltip("每片雪花存在几秒")]
    public float snowLifetime = 5f;

    [Header("定时下雪控制")]
    public float clearDuration = 90f;     // 晴天 90 秒 (1分半)
    public float snowDuration = 20f;      // 下雪 20 秒
    public bool isSnowing = false;        // 开局默认不下雪

    [Header("雾气视野控制")]
    public float clearFogStart = 600f;    // 晴天雾气起始距离
    public float clearFogEnd = 2000f;     // 晴天雾气结束距离
    public float snowFogStart = 60f;      // 下雪天雾气起始距离（拉近遮挡视野）
    public float snowFogEnd = 350f;       // 下雪天雾气结束距离
    public float transitionSpeed = 1.5f;  // 雾气过渡平滑度

    [Header("随机风向控制")]
    public float windChangeInterval = 4f; // 每 4 秒随机换一次风向
    [Tooltip("风对滑雪者的推力有多大：1 = 原来那么大，0.2 = 只有五分之一，0 = 完全不推人")]
    [Range(0f, 1f)]
    public float skierWindStrength = 0.15f;
    [Tooltip("风对雪花的影响有多大（只是看起来的效果，不会推人）")]
    public float snowWindStrength = 1f;

    private float weatherTimer = 0f;
    private float windTimer = 0f;
    private Vector3 targetWindForce;
    private Vector3 currentWindForce;

    private float currentFogStart;
    private float currentFogEnd;

    public static SnowWeatherManager Instance;

    void Awake()
    {
        Instance = this;
    }

    // 给滑雪者（SkierController）用的风：只乘以 Skier Wind Strength，和雪花的风分开
    public Vector3 GetCurrentWind() => currentWindForce * skierWindStrength;

    void Start()
    {
        FindCamera();

        // 如果拖进来的是 Assets 里的预制体（不是场景里的那个），它不会显示在画面里，
        // 所以改用场景里的 SnowParticle；场景里没有就用预制体生成一个
        if (snowParticle != null && !snowParticle.gameObject.scene.IsValid())
        {
            ParticleSystem prefab = snowParticle;
            snowParticle = null;
            GameObject found = GameObject.Find(prefab.gameObject.name);
            if (found != null) snowParticle = found.GetComponent<ParticleSystem>();
            if (snowParticle == null) snowParticle = Instantiate(prefab);
            Debug.Log("SnowWeatherManager：Snow Particle 栏里是预制体，已自动换成场景里的雪。");
        }
        if (snowParticle == null)
        {
            GameObject found = GameObject.Find("SnowParticle");
            if (found != null) snowParticle = found.GetComponent<ParticleSystem>();
        }

        if (snowParticle == null)
            Debug.LogError("SnowWeatherManager：找不到雪的粒子，请把 SnowParticle 拖进 Snow Particle 栏。");
        else
            SetupParticle();

        if (mainCamera == null)
            Debug.LogError("SnowWeatherManager：找不到镜头。");
        else
            Debug.Log("SnowWeatherManager：雪跟随的镜头是 " + mainCamera.name);

        // 1. 强制初始化为“晴天”状态
        isSnowing = false;
        weatherTimer = 0f; // 计时器归零，开始计算 90 秒晴天

        // 2. 初始视野设为晴天的远距离视野
        currentFogStart = clearFogStart;
        currentFogEnd = clearFogEnd;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = clearFogStart;
        RenderSettings.fogEndDistance = clearFogEnd;

        // 3. 初始停止粒子播放，并清掉开局就在山脚下的雪
        if (snowParticle != null)
        {
            snowParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        RandomizeWind();
        currentWindForce = targetWindForce;
    }

    // 优先用正在渲染画面的主镜头（和 GhostController 用的是同一个）
    void FindCamera()
    {
        Camera c = Camera.main;
        if (c == null || !c.isActiveAndEnabled)
        {
            c = null;
            foreach (Camera each in FindObjectsOfType<Camera>())
            {
                if (!each.isActiveAndEnabled || each.targetTexture != null) continue;
                if (c == null || each.depth > c.depth) c = each;
            }
        }
        if (c != null) mainCamera = c.transform;
    }

    // 直接把粒子的参数设成固定值（不管 SnowParticle 预制体里原来填的是多少）
    void SetupParticle()
    {
        // 粒子不要挂在会动的物体（比如滑雪者）下面，否则会被带着跑
        if (snowParticle.transform.parent != null && snowParticle.transform.parent != transform)
            snowParticle.transform.SetParent(null, true);
        snowParticle.transform.localScale = Vector3.one;

        var main = snowParticle.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = snowMovesWithCamera ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startLifetime = snowLifetime;
        main.startSpeed = 0f;   // 下落和飘动全部交给下面的风
        main.startSize = new ParticleSystem.MinMaxCurve(flakeSizeMin * worldScale, flakeSizeMax * worldScale);
        main.gravityModifier = 0f;
        main.maxParticles = maxSnowflakes;

        var emission = snowParticle.emission;
        emission.enabled = true;
        emission.rateOverTime = maxSnowflakes / Mathf.Max(0.1f, snowLifetime);

        var shape = snowParticle.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = new Vector3(snowAreaWidth, snowAreaHeight, snowAreaWidth);

        var rend = snowParticle.GetComponent<ParticleSystemRenderer>();
        if (rend != null) rend.maxParticleSize = 1f;
    }

    void Update()
    {
        // 1. 自动循环计时：开局 90 秒晴天 -> 20 秒下雪
        weatherTimer += Time.deltaTime;
        float currentCycleDuration = isSnowing ? snowDuration : clearDuration;

        if (weatherTimer >= currentCycleDuration)
        {
            weatherTimer = 0f;
            isSnowing = !isSnowing;
            if (isSnowing) RandomizeWind();
        }

        // P 键手动测试开关
        if (Input.GetKeyDown(KeyCode.P))
        {
            isSnowing = !isSnowing;
            weatherTimer = 0f;
            if (isSnowing) RandomizeWind();
        }

        // 4. 动态平滑雾气控制
        float targetStart = isSnowing ? snowFogStart : clearFogStart;
        float targetEnd = isSnowing ? snowFogEnd : clearFogEnd;

        currentFogStart = Mathf.Lerp(currentFogStart, targetStart, Time.deltaTime * transitionSpeed);
        currentFogEnd = Mathf.Lerp(currentFogEnd, targetEnd, Time.deltaTime * transitionSpeed);

        RenderSettings.fogStartDistance = currentFogStart;
        RenderSettings.fogEndDistance = currentFogEnd;
    }

    // 镜头这一帧移动完以后，再把雪放到镜头前面
    void LateUpdate()
    {
        if (mainCamera == null || !mainCamera.gameObject.activeInHierarchy) FindCamera();
        if (mainCamera == null || snowParticle == null) return;

        // 2. 粒子跟随与开关
        snowParticle.transform.position = mainCamera.position + (mainCamera.forward * forwardOffset) + (Vector3.up * upOffset);
        snowParticle.transform.rotation = Quaternion.identity;

        if (isSnowing && !snowParticle.isEmitting) snowParticle.Play();
        else if (!isSnowing && snowParticle.isEmitting) snowParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // 3. 动态风向控制
        if (isSnowing)
        {
            windTimer += Time.deltaTime;
            if (windTimer >= windChangeInterval)
            {
                windTimer = 0f;
                RandomizeWind();
            }

            currentWindForce = Vector3.Lerp(currentWindForce, targetWindForce, Time.deltaTime * 1.5f);

            var velocityModule = snowParticle.velocityOverLifetime;
            velocityModule.enabled = true;
            velocityModule.space = ParticleSystemSimulationSpace.World;
            velocityModule.x = currentWindForce.x * worldScale * snowWindStrength;
            velocityModule.y = currentWindForce.y * worldScale;
            velocityModule.z = currentWindForce.z * worldScale * snowWindStrength;
        }
    }

    void RandomizeWind()
    {
        // 大幅拉大 X 和 Z 轴的强风数值（数值越大，风力越狂暴、对人的推力越强）
        // 这里是“正常比例”的数值，用到粒子上时会自动乘以 worldScale
        float randomX = Random.Range(-70f, 70f); // 强侧风
        float randomY = Random.Range(-12f, -8f); // 自然下落速度
        float randomZ = Random.Range(-70f, 70f); // 强前后风

        targetWindForce = new Vector3(randomX, randomY, randomZ);
    }
}
