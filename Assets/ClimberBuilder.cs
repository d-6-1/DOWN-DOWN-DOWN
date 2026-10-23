// @role Builds the climber corpse (your own body) for the opening
// @tune size:float:0.5-3:Corpse size
// @tune outlineWidth:float:0-0.05:Outline thickness
// ClimberBuilder.cs —— 三渲二 登山者的尸体（开场剧情里"你自己的身体"）
// 和小狗、小鸡、巴哥一样：用代码拼出低面数的球、胶囊、圆柱、尖锥、方块，再加卡通明暗和深色描边。
// 仰面躺在雪地上：橙红色羽绒服、白头盔、推到额头上的护目镜、XX 眼、奶黄围巾、登山靴带冰爪，
// 旁边倒着一个登山包（绑着一卷绳子），右手边一把冰镐斜插在雪里。
// 需要 DogToon.shader（卡通着色器，和小狗共用）已经在 Assets 里。
//
// 一般不用手动挂：OpeningScene（开场剧情）会在平台上自动生成一个。
// 想在编辑器里先看看样子：把这个脚本挂到任意空物体上 → 组件右上角 ⋮ →「Build Climber（生成登山者）」。
// 生成的模型叫 ClimberModel，躺在 y = 0 的地面上，头朝 +Z（蓝色箭头方向），零件都没有碰撞体。
// size = 1 时大约 1.9 米长（从头盔到靴子）。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ClimberBuilder : MonoBehaviour
{
    [Tooltip("整体大小。1 = 大约 1.9 米长")]
    public float size = 1f;

    [Tooltip("勾上：身体、头用圆润平滑的网格；不勾：一面一面的低多边形")]
    public bool smoothBody = true;

    [Tooltip("黑色描边的粗细（按 size = 1 的模型算，米）。离镜头远时可以调大一点")]
    public float outlineWidth = 0.012f;

    [Tooltip("卡通着色器，会自动找 DogScout/Toon，一般不用管")]
    public Shader toonShader;

    [Tooltip("勾上：进入 Play 时如果还没有模型，就自动生成一个")]
    public bool buildOnPlayIfMissing = true;

    public const string RootName = "ClimberModel";
    const string ToonShaderName = "DogScout/Toon";
    static readonly Color32 OutlineColor = new Color32(60, 30, 15, 255);

    public enum Shape { Empty, Sphere, Capsule, Cylinder, Cube, Cone }

    public struct Part
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

    // 颜色（偏淡、低饱和，和小狗小鸡的雪地画面统一）
    static readonly Dictionary<string, Color32> Colors = new Dictionary<string, Color32>
    {
        { "Jacket", new Color32(214, 104, 78, 255) },
        { "JacketDark", new Color32(178, 78, 62, 255) },
        { "Pants", new Color32(72, 80, 108, 255) },
        { "Boot", new Color32(122, 88, 64, 255) },
        { "BootCuff", new Color32(222, 196, 124, 255) },
        { "Metal", new Color32(178, 184, 192, 255) },
        { "Mitten", new Color32(96, 108, 142, 255) },
        { "Skin", new Color32(246, 214, 190, 255) },
        { "Nose", new Color32(232, 150, 140, 255) },
        { "Cheek", new Color32(238, 168, 168, 255) },
        { "Tongue", new Color32(222, 112, 122, 255) },
        { "Helmet", new Color32(244, 242, 236, 255) },
        { "HelmetDark", new Color32(200, 200, 196, 255) },
        { "Lens", new Color32(152, 190, 214, 255) },
        { "Dark", new Color32(52, 44, 48, 255) },
        { "Scarf", new Color32(222, 196, 124, 255) },
        { "Pack", new Color32(116, 148, 190, 255) },
        { "PackDark", new Color32(92, 120, 160, 255) },
        { "Rope", new Color32(130, 170, 132, 255) },
        { "RopeDark", new Color32(104, 140, 108, 255) },
        { "Wood", new Color32(160, 116, 76, 255) },
        { "Snow", new Color32(248, 250, 252, 255) },
    };

    // 零件清单：名字, 父物体, 形状, 位置, 旋转(度), 缩放, 颜色, 要不要描边
    static readonly Part[] Parts =
    {
        new Part("Body", "ClimberModel", Shape.Empty, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f), null, false),
        // 雪：被风吹来的雪堆埋住了一点左腿，头盔上也积了一小撮
        new Part("SnowDrift0", "ClimberModel", Shape.Sphere, new Vector3(-0.2f, 0f, -0.66f), new Vector3(0f, 15f, 0f), new Vector3(0.52f, 0.2f, 0.44f), "Snow", true),
        new Part("SnowDrift1", "ClimberModel", Shape.Sphere, new Vector3(0.36f, 0f, 0.1f), new Vector3(0f, -20f, 0f), new Vector3(0.34f, 0.14f, 0.5f), "Snow", true),
        // 身体：橙红色羽绒服（一圈一圈鼓鼓的），深蓝裤子，仰面躺着，头朝 +Z
        new Part("Hips", "Body", Shape.Sphere, new Vector3(0f, 0.15f, -0.14f), new Vector3(0f, 0f, 0f), new Vector3(0.44f, 0.28f, 0.36f), "Pants", true),
        new Part("Torso", "Body", Shape.Capsule, new Vector3(0f, 0.18f, 0.18f), new Vector3(90f, 0f, 0f), new Vector3(0.5f, 0.3f, 0.36f), "Jacket", true),
        new Part("PuffRing0", "Body", Shape.Cylinder, new Vector3(0f, 0.18f, 0.03f), new Vector3(90f, 0f, 0f), new Vector3(0.54f, 0.035f, 0.4f), "JacketDark", true),
        new Part("PuffRing1", "Body", Shape.Cylinder, new Vector3(0f, 0.18f, 0.24f), new Vector3(90f, 0f, 0f), new Vector3(0.54f, 0.035f, 0.4f), "JacketDark", true),
        new Part("Zipper", "Body", Shape.Cube, new Vector3(0f, 0.36f, 0.17f), new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.012f, 0.5f), "Dark", false),
        new Part("Hood", "Body", Shape.Sphere, new Vector3(0f, 0.1f, 0.5f), new Vector3(0f, 0f, 0f), new Vector3(0.46f, 0.2f, 0.18f), "JacketDark", true),
        // 围巾：奶黄色，一条尾巴甩在雪地上
        new Part("Scarf", "Body", Shape.Cylinder, new Vector3(0f, 0.19f, 0.5f), new Vector3(90f, 0f, 0f), new Vector3(0.36f, 0.05f, 0.3f), "Scarf", true),
        new Part("ScarfKnot", "Body", Shape.Sphere, new Vector3(0.11f, 0.31f, 0.47f), new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.09f, 0.1f), "Scarf", true),
        new Part("ScarfEnd", "Body", Shape.Cube, new Vector3(0.3f, 0.06f, 0.42f), new Vector3(0f, 70f, -25f), new Vector3(0.1f, 0.025f, 0.34f), "Scarf", true),
        new Part("ScarfStripe", "Body", Shape.Cube, new Vector3(0.4f, 0.04f, 0.4f), new Vector3(0f, 70f, -25f), new Vector3(0.104f, 0.027f, 0.05f), "Pants", false),
        // 腿：左腿伸直（脚尖朝天往外倒），右腿屈膝立着
        new Part("ThighL", "Body", Shape.Capsule, new Vector3(-0.15f, 0.125f, -0.385f), new Vector3(91.708f, -169.695f, 0f), new Vector3(0.21f, 0.273f, 0.21f), "Pants", true),
        new Part("ShinL", "Body", Shape.Capsule, new Vector3(-0.21f, 0.115f, -0.705f), new Vector3(91.814f, -169.046f, 0f), new Vector3(0.18f, 0.248f, 0.18f), "Pants", true),
        new Part("ThighR", "Body", Shape.Capsule, new Vector3(0.17f, 0.275f, -0.35f), new Vector3(43.848f, 158.962f, 0f), new Vector3(0.21f, 0.306f, 0.21f), "Pants", true),
        new Part("ShinR", "Body", Shape.Capsule, new Vector3(0.24f, 0.27f, -0.61f), new Vector3(138.754f, 171.254f, 0f), new Vector3(0.18f, 0.289f, 0.18f), "Pants", true),
        // 登山靴：棕色大头靴，鞋底有一圈冰爪的尖齿
        new Part("BootL", "Body", Shape.Capsule, new Vector3(-0.305f, 0.185f, -0.935f), new Vector3(33.013f, -95.194f, 0f), new Vector3(0.19f, 0.196f, 0.19f), "Boot", true),
        new Part("CuffL", "Body", Shape.Cylinder, new Vector3(-0.247f, 0.11f, -0.88f), new Vector3(90f, -172.875f, 0f), new Vector3(0.2f, 0.02f, 0.2f), "BootCuff", true),
        new Part("BootR", "Body", Shape.Capsule, new Vector3(0.28f, 0.09f, -0.88f), new Vector3(90f, 168.69f, 0f), new Vector3(0.19f, 0.197f, 0.19f), "Boot", true),
        new Part("CuffR", "Body", Shape.Cylinder, new Vector3(0.261f, 0.13f, -0.75f), new Vector3(134.964f, 177.138f, 0f), new Vector3(0.2f, 0.028f, 0.2f), "BootCuff", true),
        new Part("CramponL0", "Body", Shape.Cone, new Vector3(-0.25f, 0.08f, -1.025f), new Vector3(90f, 180f, 0f), new Vector3(0.05f, 0.035f, 0.05f), "Metal", false),
        new Part("CramponL1", "Body", Shape.Cone, new Vector3(-0.305f, 0.165f, -1.025f), new Vector3(90f, 180f, 0f), new Vector3(0.05f, 0.035f, 0.05f), "Metal", false),
        new Part("CramponL2", "Body", Shape.Cone, new Vector3(-0.36f, 0.25f, -1.025f), new Vector3(90f, 180f, 0f), new Vector3(0.05f, 0.035f, 0.05f), "Metal", false),
        new Part("CramponR0a", "Body", Shape.Cone, new Vector3(0.2f, -0.01f, -0.8f), new Vector3(180f, 0f, 0f), new Vector3(0.044f, 0.03f, 0.044f), "Metal", false),
        new Part("CramponR0b", "Body", Shape.Cone, new Vector3(0.32f, -0.01f, -0.8f), new Vector3(180f, 0f, 0f), new Vector3(0.044f, 0.03f, 0.044f), "Metal", false),
        new Part("CramponR1a", "Body", Shape.Cone, new Vector3(0.22f, -0.01f, -0.89f), new Vector3(180f, 0f, 0f), new Vector3(0.044f, 0.03f, 0.044f), "Metal", false),
        new Part("CramponR1b", "Body", Shape.Cone, new Vector3(0.34f, -0.01f, -0.89f), new Vector3(180f, 0f, 0f), new Vector3(0.044f, 0.03f, 0.044f), "Metal", false),
        new Part("CramponR2a", "Body", Shape.Cone, new Vector3(0.24f, -0.01f, -0.98f), new Vector3(180f, 0f, 0f), new Vector3(0.044f, 0.03f, 0.044f), "Metal", false),
        new Part("CramponR2b", "Body", Shape.Cone, new Vector3(0.36f, -0.01f, -0.98f), new Vector3(180f, 0f, 0f), new Vector3(0.044f, 0.03f, 0.044f), "Metal", false),
        // 手臂：左手甩到头顶外边，右手伸向冰镐
        new Part("UpperArmL", "Body", Shape.Capsule, new Vector3(-0.37f, 0.16f, 0.43f), new Vector3(105.158f, -61.699f, 0f), new Vector3(0.18f, 0.243f, 0.18f), "Jacket", true),
        new Part("ForeArmL", "Body", Shape.Capsule, new Vector3(-0.6f, 0.11f, 0.61f), new Vector3(93.848f, -42.274f, 0f), new Vector3(0.16f, 0.229f, 0.16f), "Jacket", true),
        new Part("MittenL", "Body", Shape.Sphere, new Vector3(-0.77f, 0.09f, 0.8f), new Vector3(0f, -40f, 0f), new Vector3(0.15f, 0.12f, 0.17f), "Mitten", true),
        new Part("ThumbL", "Body", Shape.Sphere, new Vector3(-0.7f, 0.1f, 0.84f), new Vector3(0f, -40f, 0f), new Vector3(0.06f, 0.06f, 0.08f), "Mitten", true),
        new Part("UpperArmR", "Body", Shape.Capsule, new Vector3(0.355f, 0.155f, 0.29f), new Vector3(108.482f, 121.329f, 0f), new Vector3(0.18f, 0.232f, 0.18f), "Jacket", true),
        new Part("ForeArmR", "Body", Shape.Capsule, new Vector3(0.545f, 0.1f, 0.32f), new Vector3(94.574f, 36.87f, 0f), new Vector3(0.16f, 0.205f, 0.16f), "Jacket", true),
        new Part("MittenR", "Body", Shape.Sphere, new Vector3(0.67f, 0.08f, 0.5f), new Vector3(0f, 35f, 0f), new Vector3(0.15f, 0.12f, 0.17f), "Mitten", true),
        new Part("ThumbR", "Body", Shape.Sphere, new Vector3(0.61f, 0.09f, 0.55f), new Vector3(0f, 35f, 0f), new Vector3(0.06f, 0.06f, 0.08f), "Mitten", true),
        new Part("Head", "Body", Shape.Empty, new Vector3(0f, 0.19f, 0.72f), new Vector3(-90f, -18f, 180f), new Vector3(1f, 1f, 1f), null, false),
        // 头：白头盔、推到额头上的护目镜（一片裂了）、XX 眼、冻红的鼻子、吐出来一点的舌头
        new Part("HeadBall", "Head", Shape.Sphere, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.36f, 0.36f, 0.34f), "Skin", true),
        new Part("EarL", "Head", Shape.Sphere, new Vector3(-0.18f, -0.01f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.08f, 0.06f), "Skin", true),
        new Part("EarR", "Head", Shape.Sphere, new Vector3(0.18f, -0.01f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.08f, 0.06f), "Skin", true),
        new Part("Helmet", "Head", Shape.Sphere, new Vector3(0f, 0.09f, -0.03f), new Vector3(0f, 0f, 0f), new Vector3(0.41f, 0.3f, 0.41f), "Helmet", true),
        new Part("HelmetRidge", "Head", Shape.Capsule, new Vector3(0f, 0.23f, -0.04f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.13f, 0.05f), "HelmetDark", true),
        new Part("HelmetSnow", "Head", Shape.Sphere, new Vector3(0.05f, 0.22f, -0.13f), new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.08f, 0.11f), "Snow", true),
        new Part("GoggleStrap", "Head", Shape.Cylinder, new Vector3(0f, 0.07f, -0.03f), new Vector3(-14f, 0f, 0f), new Vector3(0.425f, 0.026f, 0.425f), "Dark", true),
        new Part("GoggleL", "Head", Shape.Sphere, new Vector3(-0.065f, 0.11f, 0.16f), new Vector3(-30f, -18f, 0f), new Vector3(0.12f, 0.085f, 0.05f), "Lens", true),
        new Part("GoggleR", "Head", Shape.Sphere, new Vector3(0.065f, 0.11f, 0.16f), new Vector3(-30f, 18f, 0f), new Vector3(0.12f, 0.085f, 0.05f), "Lens", true),
        new Part("GoggleCrack", "Head", Shape.Cube, new Vector3(0.07f, 0.12f, 0.183f), new Vector3(-30f, 18f, 50f), new Vector3(0.075f, 0.008f, 0.008f), "Dark", false),
        new Part("EyeXL0", "Head", Shape.Cube, new Vector3(-0.068f, -0.02f, 0.163f), new Vector3(0f, -20f, 45f), new Vector3(0.065f, 0.018f, 0.016f), "Dark", false),
        new Part("EyeXL1", "Head", Shape.Cube, new Vector3(-0.068f, -0.02f, 0.163f), new Vector3(0f, -20f, -45f), new Vector3(0.065f, 0.018f, 0.016f), "Dark", false),
        new Part("EyeXR0", "Head", Shape.Cube, new Vector3(0.068f, -0.02f, 0.163f), new Vector3(0f, 20f, 45f), new Vector3(0.065f, 0.018f, 0.016f), "Dark", false),
        new Part("EyeXR1", "Head", Shape.Cube, new Vector3(0.068f, -0.02f, 0.163f), new Vector3(0f, 20f, -45f), new Vector3(0.065f, 0.018f, 0.016f), "Dark", false),
        new Part("Nose", "Head", Shape.Sphere, new Vector3(0f, -0.065f, 0.18f), new Vector3(0f, 0f, 0f), new Vector3(0.065f, 0.055f, 0.055f), "Nose", false),
        new Part("CheekL", "Head", Shape.Sphere, new Vector3(-0.105f, -0.085f, 0.13f), new Vector3(0f, -35f, 0f), new Vector3(0.07f, 0.04f, 0.03f), "Cheek", false),
        new Part("CheekR", "Head", Shape.Sphere, new Vector3(0.105f, -0.085f, 0.13f), new Vector3(0f, 35f, 0f), new Vector3(0.07f, 0.04f, 0.03f), "Cheek", false),
        new Part("Mouth", "Head", Shape.Cube, new Vector3(0f, -0.115f, 0.152f), new Vector3(12f, 0f, 0f), new Vector3(0.07f, 0.016f, 0.016f), "Dark", false),
        new Part("Tongue", "Head", Shape.Sphere, new Vector3(0.025f, -0.13f, 0.152f), new Vector3(12f, 0f, 0f), new Vector3(0.04f, 0.05f, 0.025f), "Tongue", false),
        // 装备：左边倒着一个雾蓝色登山包（上面绑一卷绳子），右手边一把冰镐斜插在雪里
        new Part("Pack", "ClimberModel", Shape.Empty, new Vector3(-0.72f, 0f, -0.18f), new Vector3(0f, 28f, 0f), new Vector3(1f, 1f, 1f), null, false),
        new Part("PackBody", "Pack", Shape.Cube, new Vector3(0f, 0.13f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.4f, 0.26f, 0.56f), "Pack", true),
        new Part("PackLid", "Pack", Shape.Sphere, new Vector3(0f, 0.15f, 0.28f), new Vector3(0f, 0f, 0f), new Vector3(0.42f, 0.24f, 0.2f), "PackDark", true),
        new Part("PackPocket", "Pack", Shape.Cube, new Vector3(0f, 0.27f, -0.06f), new Vector3(0f, 0f, 0f), new Vector3(0.28f, 0.06f, 0.26f), "PackDark", true),
        new Part("PackStrapA", "Pack", Shape.Cube, new Vector3(0f, 0.265f, 0.1f), new Vector3(0f, 0f, 0f), new Vector3(0.42f, 0.012f, 0.04f), "Dark", false),
        new Part("Rope", "Pack", Shape.Cylinder, new Vector3(0f, 0.33f, 0.04f), new Vector3(0f, 0f, 0f), new Vector3(0.32f, 0.04f, 0.32f), "Rope", true),
        new Part("RopeLoop", "Pack", Shape.Cylinder, new Vector3(0f, 0.375f, 0.04f), new Vector3(0f, 0f, 0f), new Vector3(0.25f, 0.012f, 0.25f), "RopeDark", false),
        new Part("RopeHole", "Pack", Shape.Cylinder, new Vector3(0f, 0.38f, 0.04f), new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.012f, 0.12f), "Dark", false),
        new Part("AxeShaft", "ClimberModel", Shape.Cylinder, new Vector3(0.8f, 0.285f, 0.84f), new Vector3(12.148f, 56.31f, 0f), new Vector3(0.056f, 0.343f, 0.056f), "Wood", true),
        new Part("AxePick", "ClimberModel", Shape.Cylinder, new Vector3(0.865f, 0.615f, 0.88f), new Vector3(95.185f, 86.532f, 0f), new Vector3(0.06f, 0.166f, 0.06f), "Metal", true),
        new Part("AxeTip", "ClimberModel", Shape.Cone, new Vector3(0.635f, 0.59f, 0.867f), new Vector3(125.999f, -92.603f, 0f), new Vector3(0.06f, 0.068f, 0.06f), "Metal", true),
        new Part("AxeAdze", "ClimberModel", Shape.Cube, new Vector3(1.05f, 0.6f, 0.89f), new Vector3(0f, 0f, -8f), new Vector3(0.05f, 0.09f, 0.07f), "Metal", true),
        new Part("AxeStrap", "ClimberModel", Shape.Cube, new Vector3(0.83f, 0.45f, 0.86f), new Vector3(0f, 0f, -10f), new Vector3(0.09f, 0.1f, 0.012f), "Scarf", false),
    };

    void Reset() { FindShader(); }
    void OnValidate() { FindShader(); }

    void Awake()
    {
        if (buildOnPlayIfMissing && Application.isPlaying && transform.Find(RootName) == null)
            Build();
    }

    void FindShader()
    {
        if (toonShader == null) toonShader = Shader.Find(ToonShaderName);
    }

    [ContextMenu("Build Climber（生成登山者）")]
    public void Build()
    {
        Clear();
        FindShader();
        // 模型可能放在放大了的物体下面：描边粗细会按实际大小自动算
        BuildParts(transform, RootName, Parts, Colors, size, outlineWidth, smoothBody, toonShader);
#if UNITY_EDITOR
        if (!Application.isPlaying) EditorSceneManager_MarkDirty(gameObject);
#endif
    }

    [ContextMenu("Clear Climber（删除登山者）")]
    public void Clear()
    {
        var old = transform.Find(RootName);
        if (old != null) DestroyImmediate(old.gameObject);
    }

#if UNITY_EDITOR
    static void EditorSceneManager_MarkDirty(GameObject go)
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
    }
#endif

    // ================= 通用的拼零件工具（开场平台也用它） =================

    // 按零件清单拼出一个模型：root 叫 rootName，挂在 parent 下面，整体缩放 scale。
    // outlineWidth：按缩放 1 算的描边粗细，会自动乘上实际大小
    public static Transform BuildParts(Transform parent, string rootName, Part[] parts, Dictionary<string, Color32> colors,
                                       float scale, float outlineWidth, bool smooth, Shader shader)
    {
        SendLightDirection();
        if (shader == null) shader = Shader.Find(ToonShaderName);
        if (shader == null)
            Debug.LogWarning("没找到 DogToon.shader，模型会用普通材质（没有三渲二效果）。请确认 DogToon.shader 在 Assets 里并且没有报错。");

        var made = new Dictionary<string, Transform>();
        var root = new GameObject(rootName).transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * scale;
        made[rootName] = root;

        var mats = new Dictionary<string, Material>();
        Material outlineMat = NewMaterial(shader, "Outline", OutlineColor, true, outlineWidth * root.lossyScale.x);

        foreach (var p in parts)
        {
            var go = new GameObject(p.name);
            var t = go.transform;
            t.SetParent(made[p.parent], false);
            t.localPosition = p.pos;
            t.localEulerAngles = p.rot;
            t.localScale = p.scale;
            made[p.name] = t;

            if (p.shape == Shape.Empty) continue;

            Material mat;
            if (!mats.TryGetValue(p.color, out mat))
            {
                mat = NewMaterial(shader, p.color, colors[p.color], false, 0f);
                mats[p.color] = mat;
            }
            go.AddComponent<MeshFilter>().sharedMesh = GetMesh(p.shape, smooth, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;

            // 描边：同样形状的平滑网格，用描边材质往外撑一圈
            if (p.outline && shader != null)
            {
                var ol = new GameObject("Outline");
                ol.transform.SetParent(t, false);
                ol.AddComponent<MeshFilter>().sharedMesh = GetMesh(p.shape, smooth, true);
                var r = ol.AddComponent<MeshRenderer>();
                r.sharedMaterial = outlineMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
        return root;
    }

    // 改描边粗细（世界里的米）
    public static void SetOutlineWidth(Transform root, float worldWidth)
    {
        var done = new HashSet<Material>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.gameObject.name != "Outline") continue;
            Material m = r.sharedMaterial;
            if (m == null || !done.Add(m) || !m.HasProperty("_OutlineWidth")) continue;
            m.SetFloat("_OutlineWidth", worldWidth);
        }
    }

    // 把场景里平行光（Directional Light）的方向告诉卡通着色器
    public static void SendLightDirection()
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

    static Material NewMaterial(Shader toon, string name, Color color, bool isOutline, float outlineWorld)
    {
        Shader s = toon;
        if (s == null && GraphicsSettings.currentRenderPipeline != null) s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        var m = new Material(s) { name = "ClimberToon_" + name };
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        if (m.HasProperty("_OutlineWidth"))
        {
            m.SetFloat("_OutlineWidth", isOutline ? outlineWorld : 0f);
            m.SetFloat("_Cull", isOutline ? (float)CullMode.Front : (float)CullMode.Back);
        }
        return m;
    }

    // ---------------- 网格（低多边形） ----------------

    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    // 球、胶囊、圆柱可以做成平滑的；尖锥和方块一直保持棱角
    static Mesh GetMesh(Shape shape, bool smoothBody, bool forOutline)
    {
        bool hiRes = smoothBody && (shape == Shape.Sphere || shape == Shape.Capsule || shape == Shape.Cylinder);
        bool smooth = hiRes || forOutline;   // 平滑网格可以直接拿来做描边
        string name = hiRes ? "ClimberSmooth_" + shape : "ClimberLowPoly_" + shape + (forOutline ? "_Outline" : "");
        Mesh m;
        if (meshCache.TryGetValue(name, out m) && m != null) return m;
        m = BuildMesh(shape, hiRes, smooth, name);
        meshCache[name] = m;
        return m;
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
                // 六棱锥：冰爪的尖齿、冰镐的尖头
                Lathe(v, t, 6, new[] { new Vector2(0, 1), new Vector2(0.5f, -1), new Vector2(0, -1) });
                break;
            default: Cube(v, t); break;
        }
        FaceOutward(v, t);

        var mesh = new Mesh { name = name };
        if (smooth)
        {
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
}
