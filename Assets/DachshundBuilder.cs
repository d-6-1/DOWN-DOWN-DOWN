// @role Builds the dachshund model in code, with a scarf and two skis
// @tune size:float:0.5-30:Dachshund size
// @tune outlineWidth:float:0-0.05:Outline thickness
// DachshundBuilder.cs —— 三渲二 腊肠狗（长长的深藏青色身体、棕色嘴巴/眉毛/胸口/爪子、耷拉的长耳朵、大圆眼睛、细尾巴、一条围巾）
// 和 ChickenBuilder、PugBuilder 一样：用代码拼出低面数的球、胶囊、方块，再加卡通明暗和深色描边。
// 需要 DogToon.shader（卡通着色器，和小狗、小鸡、巴哥共用）已经在 Assets 里。
// 用法：把这个脚本挂到任意物体上 → 组件右上角 ⋮ →「Build Dachshund（生成腊肠狗）」。
// 想一次要六种围巾颜色：⋮ →「Build 6 Scarf Dachshunds（生成六只不同围巾的腊肠狗）」，会在旁边排出六只，每只可以拖进 Project 窗口做成 Prefab。
// 生成的腊肠狗叫 DachshundModel，脚底在 y = 0，脸朝 +Z（蓝色箭头方向），零件都没有碰撞体。
// 默认脚下踩一对和围巾同色的旧木板滑雪板（Add Skis）：两只左脚踩左板，两只右脚踩右板。
// size = 1 时大约 1.2 米高、身体 1.9 米长（连滑雪板约 2.2 米长）。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DachshundBuilder : MonoBehaviour
{
    // 六种围巾颜色（和小鸡的前六种一样，偏淡、低饱和）
    public enum ScarfStyle
    {
        [InspectorName("豆沙红")] DouShaRed,
        [InspectorName("雾蓝")] MistBlue,
        [InspectorName("奶黄")] CreamYellow,
        [InspectorName("灰绿")] GreyGreen,
        [InspectorName("淡紫")] LightPurple,
        [InspectorName("藕粉")] LotusPink,
    }

    [Tooltip("整体大小。1 = 大约 1.2 米高（带滑雪板）")]
    public float size = 1f;

    [Tooltip("勾上：身体、耳朵、腿用圆润平滑的网格；不勾：一面一面的低多边形")]
    public bool smoothBody = true;

    [Tooltip("围巾颜色（六种选一种）。滑雪板木板也是这个颜色。改完要重新 Build Dachshund")]
    public ScarfStyle scarfStyle = ScarfStyle.DouShaRed;

    [Tooltip("勾上：脚下踩一对和围巾同色的旧木板滑雪板，左边两只脚踩左板、右边两只脚踩右板（腊肠狗整体抬高到板子上面，板底在 y = 0）")]
    public bool addSkis = true;

    [Tooltip("勾上：每只腊肠狗 Play 时自动轮流换成六种围巾颜色里的下一种（以后做滑雪腊肠狗生成器时用）")]
    public bool uniqueScarfOnPlay = false;

    [Tooltip("黑色描边的粗细（米）。离镜头远时可以调大一点")]
    public float outlineWidth = 0.012f;

    [Tooltip("卡通着色器，会自动找 DogScout/Toon，一般不用管")]
    public Shader toonShader;

    [Tooltip("勾上：进入 Play 时如果还没有腊肠狗，就自动生成一只")]
    public bool buildOnPlayIfMissing = true;

    [Tooltip("编辑器里生成时，材质和网格存到这个文件夹（这样能做成 Prefab）")]
    public string assetFolder = "Assets/Dachshund";

    public const string RootName = "DachshundModel";
    const float BoardLift = 0.05f;   // 滑雪板厚度：有滑雪板时身体往上抬这么多
    const string ToonShaderName = "DogScout/Toon";
    static readonly Color32 OutlineColor = new Color32(60, 30, 15, 255);

    public static readonly Color32[] ScarfColors =
    {
        new Color32(196, 112, 110, 255), // 豆沙红
        new Color32(116, 148, 190, 255), // 雾蓝
        new Color32(222, 196, 124, 255), // 奶黄
        new Color32(130, 170, 132, 255), // 灰绿
        new Color32(160, 134, 186, 255), // 淡紫
        new Color32(222, 162, 178, 255), // 藕粉
    };
    static readonly string[] ScarfNames = { "豆沙红", "雾蓝", "奶黄", "灰绿", "淡紫", "藕粉" };

    enum Shape { Empty, Sphere, Capsule, Cylinder, Cube, Cone, Taper }

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

    // 颜色（从参考图里取的）。Scarf 用上面选的围巾颜色
    static readonly Dictionary<string, Color32> Colors = new Dictionary<string, Color32>
    {
        { "Navy", new Color32(48, 58, 86, 255) },
        { "Tan", new Color32(200, 132, 80, 255) },
        { "Nose", new Color32(28, 26, 32, 255) },
        { "EyeWhite", new Color32(250, 247, 238, 255) },
        { "Pupil", new Color32(24, 22, 28, 255) },
        { "BoardDark", new Color32(45, 40, 45, 255) },
        // 钉在木板上的钉子
        { "Nail", new Color32(70, 66, 64, 255) },
    };

    // 零件清单：名字, 父物体, 形状, 位置, 旋转(度), 缩放, 颜色, 要不要描边
    static readonly Part[] Parts =
    {
        // 一对滑雪板（双板）：顺着脸的方向（+Z），左右各一块。每块是两条旧木板拼起来的，前头翘起来，
        // 刷成和围巾一样的颜色，板面上露出钉子头。两只左脚踩左板，两只右脚踩右板，每只脚上压着一条深色固定带
        new Part("BoardPlankL0", "DachshundModel", Shape.Cube, new Vector3(-0.21f, 0.025f, 0.02f), new Vector3(0f, 0.8f, 0f), new Vector3(0.074f, 0.05f, 1.8f), "Scarf", true),
        new Part("BoardPlankL1", "DachshundModel", Shape.Cube, new Vector3(-0.132f, 0.025f, 0f), new Vector3(0f, -0.6f, 0f), new Vector3(0.074f, 0.05f, 1.86f), "Scarf", true),
        new Part("BoardPlankTipL0", "DachshundModel", Shape.Cube, new Vector3(-0.214f, 0.1f, 1.03f), new Vector3(-30f, 0.8f, 0f), new Vector3(0.074f, 0.05f, 0.3f), "Scarf", true),
        new Part("BoardPlankTipL1", "DachshundModel", Shape.Cube, new Vector3(-0.142f, 0.1f, 1.04f), new Vector3(-30f, -0.6f, 0f), new Vector3(0.074f, 0.05f, 0.3f), "Scarf", true),
        new Part("BoardPlankR0", "DachshundModel", Shape.Cube, new Vector3(0.132f, 0.025f, 0.01f), new Vector3(0f, 0.7f, 0f), new Vector3(0.074f, 0.05f, 1.84f), "Scarf", true),
        new Part("BoardPlankR1", "DachshundModel", Shape.Cube, new Vector3(0.21f, 0.025f, 0.03f), new Vector3(0f, -0.9f, 0f), new Vector3(0.074f, 0.05f, 1.8f), "Scarf", true),
        new Part("BoardPlankTipR0", "DachshundModel", Shape.Cube, new Vector3(0.143f, 0.1f, 1.04f), new Vector3(-30f, 0.7f, 0f), new Vector3(0.074f, 0.05f, 0.3f), "Scarf", true),
        new Part("BoardPlankTipR1", "DachshundModel", Shape.Cube, new Vector3(0.196f, 0.1f, 1.04f), new Vector3(-30f, -0.9f, 0f), new Vector3(0.074f, 0.05f, 0.3f), "Scarf", true),
        new Part("BoardNail0", "DachshundModel", Shape.Sphere, new Vector3(-0.21f, 0.05f, -0.8f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail1", "DachshundModel", Shape.Sphere, new Vector3(-0.21f, 0.05f, 0.05f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail2", "DachshundModel", Shape.Sphere, new Vector3(-0.2f, 0.05f, 0.8f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail3", "DachshundModel", Shape.Sphere, new Vector3(-0.132f, 0.05f, -0.82f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail4", "DachshundModel", Shape.Sphere, new Vector3(-0.132f, 0.05f, -0.02f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail5", "DachshundModel", Shape.Sphere, new Vector3(-0.14f, 0.05f, 0.82f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail6", "DachshundModel", Shape.Sphere, new Vector3(0.132f, 0.05f, -0.81f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail7", "DachshundModel", Shape.Sphere, new Vector3(0.132f, 0.05f, 0.03f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail8", "DachshundModel", Shape.Sphere, new Vector3(0.14f, 0.05f, 0.83f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail9", "DachshundModel", Shape.Sphere, new Vector3(0.21f, 0.05f, -0.79f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail10", "DachshundModel", Shape.Sphere, new Vector3(0.21f, 0.05f, 0.04f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BoardNail11", "DachshundModel", Shape.Sphere, new Vector3(0.2f, 0.05f, 0.81f), new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.012f, 0.022f), "Nail", false),
        new Part("BindingFL", "DachshundModel", Shape.Cube, new Vector3(-0.17f, 0.09f, 0.52f), new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.03f, 0.07f), "BoardDark", true),
        new Part("BindingFR", "DachshundModel", Shape.Cube, new Vector3(0.17f, 0.09f, 0.52f), new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.03f, 0.07f), "BoardDark", true),
        new Part("BindingBL", "DachshundModel", Shape.Cube, new Vector3(-0.17f, 0.09f, -0.5f), new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.03f, 0.07f), "BoardDark", true),
        new Part("BindingBR", "DachshundModel", Shape.Cube, new Vector3(0.17f, 0.09f, -0.5f), new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.03f, 0.07f), "BoardDark", true),
        new Part("Body", "DachshundModel", Shape.Empty, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f), null, false),
        // 长长的身体：一根横躺的胶囊，前面胸口、后面屁股各一个和身体一样粗的球
        new Part("Torso", "Body", Shape.Capsule, new Vector3(0f, 0.44f, -0.05f), new Vector3(90f, 0f, 0f), new Vector3(0.46f, 0.66f, 0.42f), "Navy", true),
        new Part("Chest", "Body", Shape.Sphere, new Vector3(0f, 0.44f, 0.48f), new Vector3(0f, 0f, 0f), new Vector3(0.46f, 0.42f, 0.46f), "Navy", true),
        new Part("Rump", "Body", Shape.Sphere, new Vector3(0f, 0.44f, -0.56f), new Vector3(0f, 0f, 0f), new Vector3(0.46f, 0.42f, 0.46f), "Navy", true),
        // 胸前一大块棕色
        new Part("ChestPatch", "Body", Shape.Sphere, new Vector3(0f, 0.38f, 0.68f), new Vector3(14f, 0f, 0f), new Vector3(0.24f, 0.26f, 0.08f), "Tan", false),
        // 四条小短腿：上粗下细，上面深蓝，下面棕色的脚
        new Part("LegFL", "Body", Shape.Taper, new Vector3(-0.15f, 0.2f, 0.48f), new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0.09f, 0.14f), "Navy", true),
        new Part("LegFR", "Body", Shape.Taper, new Vector3(0.15f, 0.2f, 0.48f), new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0.09f, 0.14f), "Navy", true),
        new Part("LegBL", "Body", Shape.Taper, new Vector3(-0.15f, 0.2f, -0.54f), new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0.09f, 0.14f), "Navy", true),
        new Part("LegBR", "Body", Shape.Taper, new Vector3(0.15f, 0.2f, -0.54f), new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0.09f, 0.14f), "Navy", true),
        new Part("SockFL", "Body", Shape.Taper, new Vector3(-0.15f, 0.1f, 0.48f), new Vector3(0f, 0f, 0f), new Vector3(0.105f, 0.045f, 0.105f), "Tan", false),
        new Part("SockFR", "Body", Shape.Taper, new Vector3(0.15f, 0.1f, 0.48f), new Vector3(0f, 0f, 0f), new Vector3(0.105f, 0.045f, 0.105f), "Tan", false),
        new Part("SockBL", "Body", Shape.Taper, new Vector3(-0.15f, 0.1f, -0.54f), new Vector3(0f, 0f, 0f), new Vector3(0.105f, 0.045f, 0.105f), "Tan", false),
        new Part("SockBR", "Body", Shape.Taper, new Vector3(0.15f, 0.1f, -0.54f), new Vector3(0f, 0f, 0f), new Vector3(0.105f, 0.045f, 0.105f), "Tan", false),
        new Part("PawFL", "Body", Shape.Sphere, new Vector3(-0.15f, 0.04f, 0.51f), new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.08f, 0.17f), "Tan", true),
        new Part("PawFR", "Body", Shape.Sphere, new Vector3(0.15f, 0.04f, 0.51f), new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.08f, 0.17f), "Tan", true),
        new Part("PawBL", "Body", Shape.Sphere, new Vector3(-0.15f, 0.04f, -0.51f), new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.08f, 0.17f), "Tan", true),
        new Part("PawBR", "Body", Shape.Sphere, new Vector3(0.15f, 0.04f, -0.51f), new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.08f, 0.17f), "Tan", true),
        // 细细的尾巴，往后上方翘
        new Part("Tail", "Body", Shape.Capsule, new Vector3(0f, 0.7f, -1f), new Vector3(-55f, 0f, 0f), new Vector3(0.05f, 0.3f, 0.05f), "Navy", true),
        // 脖子和头
        new Part("Neck", "Body", Shape.Capsule, new Vector3(0f, 0.7f, 0.66f), new Vector3(36f, 0f, 0f), new Vector3(0.28f, 0.22f, 0.28f), "Navy", true),
        new Part("Head", "Body", Shape.Sphere, new Vector3(0f, 0.92f, 0.82f), new Vector3(0f, 0f, 0f), new Vector3(0.4f, 0.38f, 0.4f), "Navy", true),
        // 棕色的长嘴巴 + 两边棕色脸颊 + 黑鼻头
        new Part("Muzzle", "Body", Shape.Sphere, new Vector3(0f, 0.84f, 1.02f), new Vector3(-6f, 0f, 0f), new Vector3(0.24f, 0.2f, 0.38f), "Tan", true),
        new Part("CheekL", "Body", Shape.Sphere, new Vector3(-0.11f, 0.82f, 0.93f), new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.13f, 0.15f), "Tan", false),
        new Part("CheekR", "Body", Shape.Sphere, new Vector3(0.11f, 0.82f, 0.93f), new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.13f, 0.15f), "Tan", false),
        new Part("Nose", "Body", Shape.Sphere, new Vector3(0f, 0.88f, 1.2f), new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.1f, 0.1f), "Nose", true),
        // 大大的圆眼睛：白眼球 + 黑眼珠
        new Part("EyeL", "Body", Shape.Sphere, new Vector3(-0.085f, 1.01f, 0.99f), new Vector3(0f, -12f, 0f), new Vector3(0.12f, 0.13f, 0.08f), "EyeWhite", true),
        new Part("EyeR", "Body", Shape.Sphere, new Vector3(0.085f, 1.01f, 0.99f), new Vector3(0f, 12f, 0f), new Vector3(0.12f, 0.13f, 0.08f), "EyeWhite", true),
        new Part("PupilL", "Body", Shape.Sphere, new Vector3(-0.08f, 1.005f, 1.03f), new Vector3(0f, -12f, 0f), new Vector3(0.06f, 0.065f, 0.025f), "Pupil", false),
        new Part("PupilR", "Body", Shape.Sphere, new Vector3(0.08f, 1.005f, 1.03f), new Vector3(0f, 12f, 0f), new Vector3(0.06f, 0.065f, 0.025f), "Pupil", false),
        // 棕色小眉毛
        new Part("BrowL", "Body", Shape.Sphere, new Vector3(-0.09f, 1.095f, 0.96f), new Vector3(-20f, 0f, 12f), new Vector3(0.07f, 0.035f, 0.05f), "Tan", false),
        new Part("BrowR", "Body", Shape.Sphere, new Vector3(0.09f, 1.095f, 0.96f), new Vector3(-20f, 0f, -12f), new Vector3(0.07f, 0.035f, 0.05f), "Tan", false),
        // 长长的耷拉耳朵，挂在头两边
        new Part("EarL", "Body", Shape.Sphere, new Vector3(-0.21f, 0.82f, 0.78f), new Vector3(8f, 0f, -10f), new Vector3(0.07f, 0.36f, 0.22f), "Navy", true),
        new Part("EarR", "Body", Shape.Sphere, new Vector3(0.21f, 0.82f, 0.78f), new Vector3(8f, 0f, 10f), new Vector3(0.07f, 0.36f, 0.22f), "Navy", true),
        // 围巾：绕在脖子上，前面偏右打个结，两条尾巴垂在胸前
        new Part("Scarf", "Body", Shape.Sphere, new Vector3(0f, 0.7f, 0.7f), new Vector3(40f, 0f, 0f), new Vector3(0.44f, 0.13f, 0.42f), "Scarf", true),
        new Part("ScarfKnot", "Body", Shape.Sphere, new Vector3(0.08f, 0.66f, 0.84f), new Vector3(0f, 0f, 0f), new Vector3(0.09f, 0.08f, 0.07f), "Scarf", true),
        new Part("ScarfEndA", "Body", Shape.Cube, new Vector3(0.06f, 0.56f, 0.85f), new Vector3(-14f, 0f, -10f), new Vector3(0.07f, 0.17f, 0.03f), "Scarf", true),
        new Part("ScarfEndB", "Body", Shape.Cube, new Vector3(0.13f, 0.58f, 0.83f), new Vector3(-14f, 0f, 20f), new Vector3(0.065f, 0.14f, 0.03f), "Scarf", true),
    };

    static int nextScarf;

    // Play 时轮流发六种围巾颜色
    public static ScarfStyle NextScarfStyle()
    {
        return (ScarfStyle)(nextScarf++ % ScarfColors.Length);
    }

    public Color ScarfColor { get { return ScarfColors[(int)scarfStyle]; } }

    void Reset() { FindShader(); }
    void OnValidate() { FindShader(); }

    void Awake()
    {
        SendLightDirection();
        if (buildOnPlayIfMissing && transform.Find(RootName) == null)
            Build();
        if (uniqueScarfOnPlay && Application.isPlaying)
            SetScarfStyle(NextScarfStyle());
        // 有滑雪板就不要小狗生成器加的两根红色雪橇
        if (addSkis && Application.isPlaying)
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

    // 换成六种围巾里的某一种（只换这一只腊肠狗）。滑雪板木板和围巾同色，一起换
    public void SetScarfStyle(ScarfStyle style)
    {
        scarfStyle = style;
        SetScarfColor(ScarfColors[(int)style]);
    }

    // 换成任意颜色（只换这一只腊肠狗，不影响别的腊肠狗）
    public void SetScarfColor(Color c)
    {
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
                mat = new Material(r.sharedMaterial) { name = "DachshundScarf" };
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            }
            r.sharedMaterial = mat;
        }
    }

    [ContextMenu("Build Dachshund（生成腊肠狗）")]
    public void Build()
    {
        Clear();
        FindShader();
        SendLightDirection();
        if (toonShader == null)
            Debug.LogWarning("没找到 DogToon.shader，腊肠狗会用普通材质（没有三渲二效果）。请确认 DogToon.shader 在 Assets 里并且没有报错。");

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
            if (boardPart && !addSkis) continue;

            var go = new GameObject(p.name);
            var t = go.transform;
            t.SetParent(made[p.parent], false);
            t.localPosition = p.pos;
            if (addSkis && !boardPart && p.parent == RootName) t.localPosition += Vector3.up * BoardLift;
            t.localEulerAngles = p.rot;
            t.localScale = p.scale;
            made[p.name] = t;

            if (p.shape == Shape.Empty) continue;

            Material mat;
            if (!mats.TryGetValue(p.color, out mat))
            {
                // 六种围巾各存一个材质（DachshundToon_Scarf_DouShaRed 这样），这样六只腊肠狗放在一起颜色不会互相覆盖
                if (p.color == "Scarf") mat = GetMaterial("Scarf_" + scarfStyle, ScarfColors[(int)scarfStyle], false);
                else mat = GetMaterial(p.color, Colors[p.color], false);
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

    [ContextMenu("Clear Dachshund（删除腊肠狗）")]
    public void Clear()
    {
        var old = transform.Find(RootName);
        if (old != null) DestroyImmediate(old.gameObject);
    }

    // 在这个物体旁边排出六只腊肠狗，每只一种围巾颜色。放在一个叫 DachshundScarfSet 的空物体下面，
    // 每只都是单独的物体（Dachshund_豆沙红、Dachshund_雾蓝……），可以直接拖进 Project 窗口做成 6 个 Prefab
    [ContextMenu("Build 6 Scarf Dachshunds（生成六只不同围巾的腊肠狗）")]
    public void BuildSixScarfDachshunds()
    {
        var old = GameObject.Find("DachshundScarfSet");
        if (old != null) DestroyImmediate(old);
        var set = new GameObject("DachshundScarfSet").transform;
        set.position = transform.position + transform.right * 1.5f * size;
        set.rotation = transform.rotation;
        for (int i = 0; i < ScarfColors.Length; i++)
        {
            var go = new GameObject("Dachshund_" + ScarfNames[i]);
            go.transform.SetParent(set, false);
            go.transform.localPosition = new Vector3(i * 1.1f * size, 0f, 0f);
            var b = go.AddComponent<DachshundBuilder>();
            b.size = size;
            b.smoothBody = smoothBody;
            b.scarfStyle = (ScarfStyle)i;
            b.addSkis = addSkis;
            b.outlineWidth = outlineWidth;
            b.toonShader = toonShader;
            b.assetFolder = assetFolder;
            b.Build();
        }
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(set.gameObject, "Build 6 Scarf Dachshunds");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    // ---------------- 材质 ----------------

    Material GetMaterial(string name, Color32 color, bool isOutline)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !string.IsNullOrEmpty(assetFolder))
        {
            string path = assetFolder + "/Materials/DachshundToon_" + name + ".mat";
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
        var m = new Material(PickShader()) { name = "DachshundToon_" + name };
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

    // 球、胶囊、圆柱可以做成平滑的；尖锥和方块（木板、围巾尾巴）一直保持棱角
    bool IsSmooth(Shape s)
    {
        return smoothBody && (s == Shape.Sphere || s == Shape.Capsule || s == Shape.Cylinder || s == Shape.Taper);
    }

    Mesh GetMesh(Shape shape, bool forOutline)
    {
        bool hiRes = IsSmooth(shape);
        bool smooth = hiRes || forOutline;   // 平滑网格可以直接拿来做描边
        string name = (hiRes ? "DachshundSmooth_" + shape : "DachshundLowPoly_" + shape + (forOutline ? "_Outline" : ""));
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
            case Shape.Taper:
                // 上粗下细的腿：顶上圆头，往下慢慢变细，底下小圆头
                Lathe(v, t, hiRes ? 24 : 8, TaperProfile);
                break;
            case Shape.Cone:
                // 六棱锥（腊肠狗暂时没用到，留着和小鸡一样）
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

    // 上粗下细的轮廓 (半径, 高度)：顶部半径 0.5，底部半径 0.3
    static readonly Vector2[] TaperProfile =
    {
        new Vector2(0f, 1f), new Vector2(0.3f, 0.96f), new Vector2(0.45f, 0.86f), new Vector2(0.5f, 0.7f),
        new Vector2(0.3f, -0.8f), new Vector2(0.22f, -0.95f), new Vector2(0f, -1f),
    };

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
