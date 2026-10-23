// @role Builds the chicken model in code, with a scarf and wood plank board
// @tune size:float:0.5-30:Chicken size
// @tune outlineWidth:float:0-0.05:Outline thickness
// ChickenBuilder.cs —— 三渲二 小鸡（圆滚滚的橙棕色身体、奶白色肚子、红鸡冠、大白眼睛、细细的深色腿、脖子上一条围巾）
// 和 DogScoutBuilder 一样：用代码拼出低面数的球、胶囊、尖锥、方块，再加卡通明暗和深色描边。
// 需要 DogToon.shader（卡通着色器，和小狗共用）已经在 Assets 里。
// 用法：把这个脚本挂到任意物体上 → 组件右上角 ⋮ →「Build Chicken（生成小鸡）」。
// 生成的小鸡叫 ChickenModel，脚底在 y = 0，脸朝 +Z（蓝色箭头方向），零件都没有碰撞体。
// 默认脚下踩一块和围巾同色的旧木板拼成的滑雪板（Add Snowboard）。size = 1 时大约 1.55 米高（和小狗差不多大），所以滑雪板、碰撞体都能和小狗共用。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChickenBuilder : MonoBehaviour
{
    [Tooltip("整体大小。1 = 大约 1.5 米高（带滑雪板）")]
    public float size = 1f;

    [Tooltip("勾上：身体、头用圆润平滑的网格；不勾：一面一面的低多边形")]
    public bool smoothBody = true;

    [Tooltip("围巾颜色")]
    public Color scarfColor = new Color32(196, 112, 110, 255);

    [Tooltip("勾上：脚下踩一块和围巾同色的旧木板拼成的滑雪板（小鸡整体抬高到板子上面，板底在 y = 0）")]
    public bool addSnowboard = true;

    [Tooltip("勾上：每只小鸡 Play 时自动换一条和别的小鸡都不一样颜色的围巾（SkiingChickenSpawner 会自动勾上）")]
    public bool uniqueScarfOnPlay = false;

    [Tooltip("黑色描边的粗细（米）。离镜头远时可以调大一点")]
    public float outlineWidth = 0.012f;

    [Tooltip("卡通着色器，会自动找 DogScout/Toon，一般不用管")]
    public Shader toonShader;

    [Tooltip("勾上：进入 Play 时如果还没有小鸡，就自动生成一只")]
    public bool buildOnPlayIfMissing = true;

    [Tooltip("编辑器里生成时，材质和网格存到这个文件夹（这样能做成 Prefab）")]
    public string assetFolder = "Assets/Chicken";

    public const string RootName = "ChickenModel";
    const float BoardLift = 0.06f;   // 雪板厚度：有雪板时身体和头往上抬这么多
    const string ToonShaderName = "DogScout/Toon";
    static readonly Color32 OutlineColor = new Color32(60, 30, 15, 255);

    enum Shape { Empty, Sphere, Capsule, Cylinder, Cube, Cone }

    struct Part
    {
        public string name, parent, color;
        public Shape shape;
        public Vector3 pos, rot, scale;
        public bool outline;
        public Part(string name, string parent, Shape shape, Vector3 pos, Vector3 rot, Vector3 scale, string color, bool outline)
        {
            this.name = name; this.parent = parent; this.shape = shape;
            this.pos = pos; this.rot = rot; this.scale = scale; this.color = color; this.outline = outline;
        }
    }

    // 颜色（从参考图里取的）。Scarf 用上面的 scarfColor
    static readonly Dictionary<string, Color32> Colors = new Dictionary<string, Color32>
    {
        { "Feather", new Color32(222, 137, 72, 255) },
        { "FeatherDark", new Color32(196, 105, 48, 255) },
        { "Cream", new Color32(246, 226, 205, 255) },
        { "Comb", new Color32(214, 30, 32, 255) },
        { "Beak", new Color32(236, 170, 60, 255) },
        { "White", new Color32(252, 252, 248, 255) },
        { "Pupil", new Color32(40, 60, 110, 255) },
        { "Leg", new Color32(70, 52, 50, 255) },
        { "BoardDark", new Color32(45, 40, 45, 255) },
        // 钉在木板上的钉子
        { "Nail", new Color32(70, 66, 64, 255) },
    };

    // 零件清单：名字, 父物体, 形状, 位置, 旋转(度), 缩放, 颜色, 要不要描边
    static readonly Part[] Parts =
    {
        // 滑雪板：顺着脸的方向（+Z）。五块鸡圈那样的旧木板拼在一起（长短、角度都有点不齐），刷成和围巾一样的颜色，
        // 板面上露出钉子头，两只脚踩在上面，脚趾上压着深色固定带
        new Part("BoardPlank0", "ChickenModel", Shape.Cube, new Vector3(-0.168f, 0.03f, 0.02f), new Vector3(0f, 1.5f, 0f), new Vector3(0.078f, 0.06f, 1.34f), "Scarf", true),
        new Part("BoardPlank1", "ChickenModel", Shape.Cube, new Vector3(-0.084f, 0.03f, 0.05f), new Vector3(0f, -1f, 0f), new Vector3(0.078f, 0.06f, 1.42f), "Scarf", true),
        new Part("BoardPlank2", "ChickenModel", Shape.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(0f, 0.8f, 0f), new Vector3(0.078f, 0.06f, 1.38f), "Scarf", true),
        new Part("BoardPlank3", "ChickenModel", Shape.Cube, new Vector3(0.084f, 0.03f, 0.04f), new Vector3(0f, -1.2f, 0f), new Vector3(0.078f, 0.06f, 1.44f), "Scarf", true),
        new Part("BoardPlank4", "ChickenModel", Shape.Cube, new Vector3(0.168f, 0.03f, 0.01f), new Vector3(0f, 1f, 0f), new Vector3(0.078f, 0.06f, 1.3f), "Scarf", true),
        new Part("BoardNail0", "ChickenModel", Shape.Sphere, new Vector3(-0.18f, 0.06f, -0.46f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail1", "ChickenModel", Shape.Sphere, new Vector3(-0.156f, 0.06f, 0.5f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail2", "ChickenModel", Shape.Sphere, new Vector3(-0.096f, 0.06f, -0.44f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail3", "ChickenModel", Shape.Sphere, new Vector3(-0.072f, 0.06f, 0.49f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail4", "ChickenModel", Shape.Sphere, new Vector3(-0.012f, 0.06f, -0.45f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail5", "ChickenModel", Shape.Sphere, new Vector3(0.012f, 0.06f, 0.51f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail6", "ChickenModel", Shape.Sphere, new Vector3(0.072f, 0.06f, -0.46f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail7", "ChickenModel", Shape.Sphere, new Vector3(0.096f, 0.06f, 0.5f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail8", "ChickenModel", Shape.Sphere, new Vector3(0.156f, 0.06f, -0.44f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail9", "ChickenModel", Shape.Sphere, new Vector3(0.18f, 0.06f, 0.49f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BindingL", "ChickenModel", Shape.Cube, new Vector3(-0.16f, 0.09f, 0.07f), new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.045f, 0.09f), "BoardDark", true),
        new Part("BindingR", "ChickenModel", Shape.Cube, new Vector3(0.16f, 0.09f, 0.07f), new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.045f, 0.09f), "BoardDark", true),
        new Part("Body", "ChickenModel", Shape.Empty, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f), null, false),
        new Part("Head", "ChickenModel", Shape.Empty, new Vector3(0f, 1.15f, 0.2f), new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f), null, false),
        // 身体：上面橙棕色的蛋形，下面奶白色的大肚子
        new Part("BodyBall", "Body", Shape.Sphere, new Vector3(0f, 0.66f, -0.03f), new Vector3(-12f, 0f, 0f), new Vector3(0.8f, 0.84f, 0.86f), "Feather", true),
        new Part("Belly", "Body", Shape.Sphere, new Vector3(0f, 0.53f, 0.06f), new Vector3(0f, 0f, 0f), new Vector3(0.74f, 0.64f, 0.76f), "Cream", true),
        new Part("Neck", "Body", Shape.Capsule, new Vector3(0f, 0.97f, 0.13f), new Vector3(-18f, 0f, 0f), new Vector3(0.34f, 0.2f, 0.34f), "Feather", true),
        // 翅膀：身体两边深一点的椭圆
        new Part("WingL", "Body", Shape.Sphere, new Vector3(-0.38f, 0.7f, -0.06f), new Vector3(-20f, -8f, -12f), new Vector3(0.1f, 0.3f, 0.44f), "FeatherDark", true),
        new Part("WingR", "Body", Shape.Sphere, new Vector3(0.38f, 0.7f, -0.06f), new Vector3(-20f, 8f, 12f), new Vector3(0.1f, 0.3f, 0.44f), "FeatherDark", true),
        // 尾巴：屁股后面往上翘的几片尖羽毛
        new Part("Tail0", "Body", Shape.Cone, new Vector3(-0.1f, 0.92f, -0.38f), new Vector3(-38f, 0f, 22f), new Vector3(0.2f, 0.2f, 0.1f), "FeatherDark", true),
        new Part("Tail1", "Body", Shape.Cone, new Vector3(0f, 0.97f, -0.4f), new Vector3(-32f, 0f, 0f), new Vector3(0.22f, 0.24f, 0.1f), "Feather", true),
        new Part("Tail2", "Body", Shape.Cone, new Vector3(0.1f, 0.92f, -0.38f), new Vector3(-38f, 0f, -22f), new Vector3(0.2f, 0.2f, 0.1f), "FeatherDark", true),
        // 围巾：围在脖子根上，前面打个结，两条尾巴垂下来
        new Part("Scarf", "Body", Shape.Cylinder, new Vector3(0f, 0.99f, 0.13f), new Vector3(-18f, 0f, 0f), new Vector3(0.45f, 0.08f, 0.45f), "Scarf", true),
        new Part("ScarfKnot", "Body", Shape.Sphere, new Vector3(0.1f, 0.94f, 0.32f), new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.1f, 0.08f), "Scarf", true),
        new Part("ScarfEndA", "Body", Shape.Cube, new Vector3(0.08f, 0.83f, 0.35f), new Vector3(-20f, 0f, -10f), new Vector3(0.09f, 0.2f, 0.03f), "Scarf", true),
        new Part("ScarfEndB", "Body", Shape.Cube, new Vector3(0.16f, 0.85f, 0.32f), new Vector3(-20f, 0f, 20f), new Vector3(0.08f, 0.17f, 0.03f), "Scarf", true),
        // 腿：细细的深色腿，三个脚趾朝前、一个朝后
        new Part("LegL", "Body", Shape.Capsule, new Vector3(-0.16f, 0.15f, 0.02f), new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.14f, 0.05f), "Leg", true),
        new Part("ToeL0", "Body", Shape.Capsule, new Vector3(-0.2f, 0.02f, 0.08f), new Vector3(90f, -25f, 0f), new Vector3(0.035f, 0.07f, 0.035f), "Leg", false),
        new Part("ToeL1", "Body", Shape.Capsule, new Vector3(-0.16f, 0.02f, 0.1f), new Vector3(90f, 0f, 0f), new Vector3(0.035f, 0.08f, 0.035f), "Leg", false),
        new Part("ToeL2", "Body", Shape.Capsule, new Vector3(-0.12f, 0.02f, 0.08f), new Vector3(90f, 25f, 0f), new Vector3(0.035f, 0.07f, 0.035f), "Leg", false),
        new Part("ToeL3", "Body", Shape.Capsule, new Vector3(-0.16f, 0.02f, -0.04f), new Vector3(90f, 0f, 0f), new Vector3(0.035f, 0.05f, 0.035f), "Leg", false),
        new Part("LegR", "Body", Shape.Capsule, new Vector3(0.16f, 0.15f, 0.02f), new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.14f, 0.05f), "Leg", true),
        new Part("ToeR0", "Body", Shape.Capsule, new Vector3(0.12f, 0.02f, 0.08f), new Vector3(90f, -25f, 0f), new Vector3(0.035f, 0.07f, 0.035f), "Leg", false),
        new Part("ToeR1", "Body", Shape.Capsule, new Vector3(0.16f, 0.02f, 0.1f), new Vector3(90f, 0f, 0f), new Vector3(0.035f, 0.08f, 0.035f), "Leg", false),
        new Part("ToeR2", "Body", Shape.Capsule, new Vector3(0.2f, 0.02f, 0.08f), new Vector3(90f, 25f, 0f), new Vector3(0.035f, 0.07f, 0.035f), "Leg", false),
        new Part("ToeR3", "Body", Shape.Capsule, new Vector3(0.16f, 0.02f, -0.04f), new Vector3(90f, 0f, 0f), new Vector3(0.035f, 0.05f, 0.035f), "Leg", false),
        // 头：小圆头、红鸡冠、红肉垂、黄嘴、两边大白眼睛
        new Part("HeadBall", "Head", Shape.Sphere, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.36f, 0.36f), "Feather", true),
        new Part("Comb0", "Head", Shape.Sphere, new Vector3(0f, 0.19f, 0.08f), new Vector3(20f, 0f, 0f), new Vector3(0.06f, 0.13f, 0.09f), "Comb", true),
        new Part("Comb1", "Head", Shape.Sphere, new Vector3(0f, 0.21f, -0.01f), new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.15f, 0.1f), "Comb", true),
        new Part("Comb2", "Head", Shape.Sphere, new Vector3(0f, 0.18f, -0.09f), new Vector3(-20f, 0f, 0f), new Vector3(0.06f, 0.12f, 0.09f), "Comb", true),
        new Part("Beak", "Head", Shape.Cone, new Vector3(0f, -0.01f, 0.2f), new Vector3(90f, 0f, 0f), new Vector3(0.09f, 0.06f, 0.07f), "Beak", true),
        new Part("Wattle", "Head", Shape.Sphere, new Vector3(0f, -0.12f, 0.17f), new Vector3(15f, 0f, 0f), new Vector3(0.07f, 0.15f, 0.07f), "Comb", true),
        new Part("EyeWhiteL", "Head", Shape.Sphere, new Vector3(-0.12f, 0.04f, 0.09f), new Vector3(0f, -45f, 0f), new Vector3(0.14f, 0.15f, 0.07f), "White", true),
        new Part("PupilL", "Head", Shape.Sphere, new Vector3(-0.136f, 0.04f, 0.11f), new Vector3(0f, -45f, 0f), new Vector3(0.06f, 0.07f, 0.035f), "Pupil", false),
        new Part("EyeWhiteR", "Head", Shape.Sphere, new Vector3(0.12f, 0.04f, 0.09f), new Vector3(0f, 45f, 0f), new Vector3(0.14f, 0.15f, 0.07f), "White", true),
        new Part("PupilR", "Head", Shape.Sphere, new Vector3(0.136f, 0.04f, 0.11f), new Vector3(0f, 45f, 0f), new Vector3(0.06f, 0.07f, 0.035f), "Pupil", false),
    };

    // 每只小鸡轮流用这些围巾颜色（偏淡、低饱和，和雪地画面统一）；超过 12 只以后自动算出新的淡颜色，不会重复
    static readonly Color32[] ScarfPalette =
    {
        new Color32(196, 112, 110, 255), // 豆沙红
        new Color32(116, 148, 190, 255), // 雾蓝
        new Color32(222, 196, 124, 255), // 奶黄
        new Color32(130, 170, 132, 255), // 灰绿
        new Color32(160, 134, 186, 255), // 淡紫
        new Color32(222, 162, 178, 255), // 藕粉
        new Color32(112, 172, 170, 255), // 灰青
        new Color32(232, 228, 220, 255), // 米白
        new Color32(96, 108, 142, 255),  // 灰蓝
        new Color32(170, 190, 122, 255), // 橄榄绿
        new Color32(116, 110, 116, 255), // 炭灰
        new Color32(152, 190, 214, 255), // 浅天蓝
    };
    static int nextScarf;

    public static Color NextScarfColor()
    {
        int i = nextScarf++;
        if (i < ScarfPalette.Length) return ScarfPalette[i];
        float hue = Mathf.Repeat(0.11f + (i - ScarfPalette.Length) * 0.618034f, 1f);
        return Color.HSVToRGB(hue, i % 2 == 0 ? 0.4f : 0.3f, i % 3 == 0 ? 0.72f : 0.86f);
    }

    void Reset() { FindShader(); }
    void OnValidate() { FindShader(); }

    void Awake()
    {
        SendLightDirection();
        if (buildOnPlayIfMissing && transform.Find(RootName) == null)
            Build();
        if (uniqueScarfOnPlay && Application.isPlaying)
            SetScarfColor(NextScarfColor());
        // 有滑雪板就不要小狗生成器加的两根红色雪橇
        if (addSnowboard && Application.isPlaying)
            foreach (var n in new[] { "SkiL", "SkiR" })
            {
                var ski = transform.Find(n);
                if (ski != null) Destroy(ski.gameObject);
            }
    }

    void FindShader()
    {
        if (toonShader == null) toonShader = Shader.Find(ToonShaderName);
    }

    // 把场景里平行光（Directional Light）的方向告诉卡通着色器
    static void SendLightDirection()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (var l in FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
        }
        Vector3 dir = sun != null ? -sun.transform.forward : new Vector3(0.4f, 0.8f, -0.45f);
        Shader.SetGlobalVector("_DogToonLightDir", dir.normalized);
    }

    // 换围巾颜色（只换这一只小鸡，不影响别的小鸡）。滑雪板木板和围巾同色，一起换
    public void SetScarfColor(Color c)
    {
        scarfColor = c;
        var root = transform.Find(RootName);
        if (root == null) return;
        Material mat = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Scarf") && !t.name.StartsWith("BoardPlank")) continue;
            var r = t.GetComponent<MeshRenderer>();
            if (r == null) continue;
            if (mat == null)
            {
                mat = new Material(r.sharedMaterial) { name = "ChickenScarf" };
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            }
            r.sharedMaterial = mat;
        }
    }

    [ContextMenu("Build Chicken（生成小鸡）")]
    public void Build()
    {
        Clear();
        FindShader();
        SendLightDirection();
        if (toonShader == null)
            Debug.LogWarning("没找到 DogToon.shader，小鸡会用普通材质（没有三渲二效果）。请确认 DogToon.shader 在 Assets 里并且没有报错。");

        var made = new Dictionary<string, Transform>();
        var root = new GameObject(RootName).transform;
        root.SetParent(transform, false);
        root.localScale = Vector3.one * size;
        made[RootName] = root;

        var mats = new Dictionary<string, Material>();
        Material outlineMat = GetMaterial("Outline", OutlineColor, true);

        foreach (var p in Parts)
        {
            bool boardPart = p.name.StartsWith("Board") || p.name.StartsWith("Binding");
            if (boardPart && !addSnowboard) continue;

            var go = new GameObject(p.name);
            var t = go.transform;
            t.SetParent(made[p.parent], false);
            t.localPosition = p.pos;
            if (addSnowboard && !boardPart && p.parent == RootName) t.localPosition += Vector3.up * BoardLift;
            t.localEulerAngles = p.rot;
            t.localScale = p.scale;
            made[p.name] = t;

            if (p.shape == Shape.Empty) continue;

            Material mat;
            if (!mats.TryGetValue(p.color, out mat))
            {
                Color32 col = p.color == "Scarf" ? (Color32)scarfColor : Colors[p.color];
                mat = GetMaterial(p.color, col, false);
                if (p.color == "Scarf") { mat.color = col; if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col); }
                mats[p.color] = mat;
            }
            go.AddComponent<MeshFilter>().sharedMesh = GetMesh(p.shape, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;

            // 描边：同样形状的平滑网格，用描边材质往外撑一圈
            if (p.outline && toonShader != null)
            {
                var ol = new GameObject("Outline");
                ol.transform.SetParent(t, false);
                ol.AddComponent<MeshFilter>().sharedMesh = GetMesh(p.shape, true);
                var r = ol.AddComponent<MeshRenderer>();
                r.sharedMaterial = outlineMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    [ContextMenu("Clear Chicken（删除小鸡）")]
    public void Clear()
    {
        var old = transform.Find(RootName);
        if (old != null) DestroyImmediate(old.gameObject);
    }

    // ---------------- 材质 ----------------

    Material GetMaterial(string name, Color32 color, bool isOutline)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !string.IsNullOrEmpty(assetFolder))
        {
            string path = assetFolder + "/Materials/ChickenToon_" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            // 已经有同样着色器的材质就直接用（保留你手动改过的颜色）
            if (existing != null && existing.shader == PickShader()) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(path);

            CreateFolders(assetFolder + "/Materials");
            var m = NewMaterial(name, color, isOutline);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
#endif
        return NewMaterial(name, color, isOutline);
    }

    Shader PickShader()
    {
        if (toonShader != null) return toonShader;
        Shader s = null;
        if (GraphicsSettings.currentRenderPipeline != null) s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    Material NewMaterial(string name, Color color, bool isOutline)
    {
        var m = new Material(PickShader()) { name = "ChickenToon_" + name };
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        if (m.HasProperty("_OutlineWidth"))
        {
            m.SetFloat("_OutlineWidth", isOutline ? outlineWidth * size : 0f);
            m.SetFloat("_Cull", isOutline ? (float)CullMode.Front : (float)CullMode.Back);
        }
        return m;
    }

    // ---------------- 网格（低多边形） ----------------

    // 球、胶囊、圆柱可以做成平滑的；尖锥（尾羽、嘴）和方块（围巾尾巴）一直保持棱角
    bool IsSmooth(Shape s)
    {
        return smoothBody && (s == Shape.Sphere || s == Shape.Capsule || s == Shape.Cylinder);
    }

    Mesh GetMesh(Shape shape, bool forOutline)
    {
        bool hiRes = IsSmooth(shape);
        bool smooth = hiRes || forOutline;   // 平滑网格可以直接拿来做描边
        string name = (hiRes ? "ChickenSmooth_" + shape : "ChickenLowPoly_" + shape + (forOutline ? "_Outline" : ""));
#if UNITY_EDITOR
        if (!Application.isPlaying && !string.IsNullOrEmpty(assetFolder))
        {
            string path = assetFolder + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            CreateFolders(assetFolder + "/Meshes");
            var m = BuildMesh(shape, hiRes, smooth, name);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
#endif
        return BuildMesh(shape, hiRes, smooth, name);
    }

    static Mesh BuildMesh(Shape shape, bool hiRes, bool smooth, string name)
    {
        var v = new List<Vector3>();
        var t = new List<int>();
        switch (shape)
        {
            case Shape.Sphere: IcoSphere(v, t, hiRes ? 3 : 1); break;
            case Shape.Cylinder:
                Lathe(v, t, hiRes ? 24 : 8, new[] { new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(0.5f, -1), new Vector2(0, -1) });
                break;
            case Shape.Capsule:
                if (hiRes) { Lathe(v, t, 24, CapsuleProfile(new[] { 67.5f, 45f, 22.5f, 0f })); break; }
                float c45 = 0.5f * Mathf.Cos(45 * Mathf.Deg2Rad), s45 = 0.5f * Mathf.Sin(45 * Mathf.Deg2Rad);
                Lathe(v, t, 8, new[] { new Vector2(0, 1), new Vector2(c45, 0.5f + s45), new Vector2(0.5f, 0.5f),
                                       new Vector2(0.5f, -0.5f), new Vector2(c45, -0.5f - s45), new Vector2(0, -1) });
                break;
            case Shape.Cone:
                // 六棱锥：用来做尖尖的尾羽和嘴
                Lathe(v, t, 6, new[] { new Vector2(0, 1), new Vector2(0.5f, -1), new Vector2(0, -1) });
                break;
            default: Cube(v, t); break;
        }
        FaceOutward(v, t);

        var mesh = new Mesh { name = name };
        if (smooth)
        {
            // 平滑：顶点共用，法线平滑（也用来做描边，撑开后不会裂缝）
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
        }
        else
        {
            // 低多边形：每个三角形用自己的顶点，法线就是面的朝向 → 一面一面的硬边
            var fv = new List<Vector3>();
            var ft = new List<int>();
            for (int i = 0; i < t.Count; i++) { fv.Add(v[t[i]]); ft.Add(i); }
            mesh.SetVertices(fv);
            mesh.SetTriangles(ft, 0);
        }
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 胶囊的轮廓：上下两个半球 + 中间直筒，angles 是半球上每一圈的角度（从上往下）
    static Vector2[] CapsuleProfile(float[] angles)
    {
        var p = new List<Vector2> { new Vector2(0, 1) };
        foreach (float a in angles)
            p.Add(new Vector2(0.5f * Mathf.Cos(a * Mathf.Deg2Rad), 0.5f + 0.5f * Mathf.Sin(a * Mathf.Deg2Rad)));
        for (int i = angles.Length - 1; i >= 0; i--)
            p.Add(new Vector2(0.5f * Mathf.Cos(angles[i] * Mathf.Deg2Rad), -0.5f - 0.5f * Mathf.Sin(angles[i] * Mathf.Deg2Rad)));
        p.Add(new Vector2(0, -1));
        return p.ToArray();
    }

    // 二十面体细分：细分 1 次 = 80 个三角形（低多边形），细分 3 次 = 1280 个三角形（圆润） = 80 个三角形的低多边形球，直径 1
    static void IcoSphere(List<Vector3> v, List<int> t, int subdivisions)
    {
        float a = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] baseV =
        {
            new Vector3(-1, a, 0), new Vector3(1, a, 0), new Vector3(-1, -a, 0), new Vector3(1, -a, 0),
            new Vector3(0, -1, a), new Vector3(0, 1, a), new Vector3(0, -1, -a), new Vector3(0, 1, -a),
            new Vector3(a, 0, -1), new Vector3(a, 0, 1), new Vector3(-a, 0, -1), new Vector3(-a, 0, 1)
        };
        foreach (var p in baseV) v.Add(p.normalized * 0.5f);
        t.AddRange(new[] { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                           3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 });

        for (int s = 0; s < subdivisions; s++)
        {
            var cache = new Dictionary<long, int>();
            var nt = new List<int>();
            for (int f = 0; f < t.Count; f += 3)
            {
                int A = t[f], B = t[f + 1], C = t[f + 2];
                int ab = Mid(v, cache, A, B), bc = Mid(v, cache, B, C), ca = Mid(v, cache, C, A);
                nt.AddRange(new[] { A, ab, ca, B, bc, ab, C, ca, bc, ab, bc, ca });
            }
            t.Clear();
            t.AddRange(nt);
        }
    }

    static int Mid(List<Vector3> v, Dictionary<long, int> cache, int i, int j)
    {
        long key = ((long)Mathf.Min(i, j) << 32) + Mathf.Max(i, j);
        int idx;
        if (cache.TryGetValue(key, out idx)) return idx;
        v.Add(((v[i] + v[j]) * 0.5f).normalized * 0.5f);
        idx = v.Count - 1;
        cache[key] = idx;
        return idx;
    }

    // 旋转体：profile 里每个点是 (半径, 高度)，半径为 0 表示顶点（尖端）
    static void Lathe(List<Vector3> v, List<int> t, int sides, Vector2[] profile)
    {
        var rings = new List<int[]>();
        foreach (var pr in profile)
        {
            var ring = new int[sides];
            if (pr.x == 0)
            {
                v.Add(new Vector3(0, pr.y, 0));
                for (int i = 0; i < sides; i++) ring[i] = v.Count - 1;
            }
            else
            {
                for (int i = 0; i < sides; i++)
                {
                    float ang = 2 * Mathf.PI * i / sides;
                    v.Add(new Vector3(pr.x * Mathf.Cos(ang), pr.y, pr.x * Mathf.Sin(ang)));
                    ring[i] = v.Count - 1;
                }
            }
            rings.Add(ring);
        }
        for (int k = 0; k < rings.Count - 1; k++)
        {
            int[] A = rings[k], B = rings[k + 1];
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                if (A[i] != A[j]) t.AddRange(new[] { A[i], A[j], B[i] });
                if (B[i] != B[j]) t.AddRange(new[] { A[j], B[j], B[i] });
            }
        }
    }

    static void Cube(List<Vector3> v, List<int> t)
    {
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                    v.Add(new Vector3(x - 0.5f, y - 0.5f, z - 0.5f));
        t.AddRange(new[] { 0,1,3, 0,3,2, 4,6,7, 4,7,5, 0,4,5, 0,5,1, 2,3,7, 2,7,6, 0,2,6, 0,6,4, 1,5,7, 1,7,3 });
    }

    // 所有形状都以原点为中心，把每个三角形翻成朝外
    static void FaceOutward(List<Vector3> v, List<int> t)
    {
        for (int f = 0; f < t.Count; f += 3)
        {
            Vector3 a = v[t[f]], b = v[t[f + 1]], c = v[t[f + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f) < 0)
            {
                int tmp = t[f + 1]; t[f + 1] = t[f + 2]; t[f + 2] = tmp;
            }
        }
    }

#if UNITY_EDITOR
    static void CreateFolders(string folder)
    {
        string[] bits = folder.Split('/');
        string current = bits[0];
        for (int i = 1; i < bits.Length; i++)
        {
            string next = current + "/" + bits[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, bits[i]);
            current = next;
        }
    }
#endif
}
