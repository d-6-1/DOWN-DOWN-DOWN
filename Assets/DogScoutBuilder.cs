// @role Builds the player dog model in code (low poly, toon shading, outline)
// @tune size:float:0.5-30:Dog size
// @tune outlineWidth:float:0-0.05:Outline thickness
// DogScoutBuilder.cs —— 三渲二 版本（身体圆润平滑，尖毛和爪子保留棱角）
// 用代码生成低面数的球、胶囊、圆柱、方块、尖锥，拼出一只围青色围巾、毛茸茸带爪子的小狗，
// 并加上卡通明暗和深色描边。
// 需要同一文件夹里还有 DogToon.shader（卡通着色器）。
// 用法：把这个脚本挂到任意物体上 → 组件右上角 ⋮ →「Build Dog（生成小狗）」。
// 生成的小狗叫 DogModel，脚底在 y = 0，脸朝 +Z（蓝色箭头方向），零件都没有碰撞体。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DogScoutBuilder : MonoBehaviour
{
    [Tooltip("整体大小。1 = 大约 1.5 米高")]
    public float size = 1f;

    [Tooltip("勾上：身体、头、四肢用圆润平滑的网格；不勾：一面一面的低多边形")]
    public bool smoothBody = true;

    [Tooltip("勾上：脚下加一块青色单板（小狗整体抬高到板子上面，板底在 y = 0）")]
    public bool addSnowboard = false;

    [Tooltip("黑色描边的粗细（米）。离镜头远时可以调大一点")]
    public float outlineWidth = 0.012f;

    [Tooltip("卡通着色器，会自动找 DogScout/Toon，一般不用管")]
    public Shader toonShader;

    [Tooltip("勾上：进入 Play 时如果还没有小狗，就自动生成一只")]
    public bool buildOnPlayIfMissing = true;

    [Tooltip("编辑器里生成时，材质和网格存到这个文件夹（这样能做成 Prefab）")]
    public string assetFolder = "Assets/DogScout";

    const string RootName = "DogModel";
    const float BoardLift = 0.10f;   // 雪板厚度：有雪板时身体和头往上抬这么多
    const string ToonShaderName = "DogScout/Toon";
    static readonly Color32 OutlineColor = new Color32(40, 25, 15, 255);

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

    // 颜色（从参考图里取的）
    static readonly Dictionary<string, Color32> Colors = new Dictionary<string, Color32>
    {
        { "Fur", new Color32(213, 167, 84, 255) },
        { "FurDark", new Color32(188, 128, 64, 255) },
        { "Muzzle", new Color32(254, 215, 135, 255) },
        { "Black", new Color32(30, 22, 16, 255) },
        { "White", new Color32(250, 250, 245, 255) },
        { "Pad", new Color32(222, 145, 109, 255) },
        { "Scarf", new Color32(32, 178, 170, 255) },
        { "Claw", new Color32(248, 240, 222, 255) },
        { "Board", new Color32(40, 196, 188, 255) },
        { "BoardDark", new Color32(18, 112, 108, 255) },
    };

    // 零件清单：名字, 父物体, 形状, 位置, 旋转(度), 缩放, 颜色, 要不要描边
    static readonly Part[] Parts =
    {
        new Part("Board", "DogModel", Shape.Capsule, new Vector3(0f, 0.05f, 0.04f), new Vector3(0f, 0f, 90f), new Vector3(0.1f, 0.72f, 0.4f), "Board", true),
        new Part("BindingL", "DogModel", Shape.Cube, new Vector3(-0.18f, 0.175f, 0.06f), new Vector3(0f, -10f, 0f), new Vector3(0.2f, 0.045f, 0.08f), "BoardDark", true),
        new Part("BindingR", "DogModel", Shape.Cube, new Vector3(0.18f, 0.175f, 0.06f), new Vector3(0f, 10f, 0f), new Vector3(0.2f, 0.045f, 0.08f), "BoardDark", true),
        new Part("Body", "DogModel", Shape.Empty, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f), null, false),
        new Part("Head", "DogModel", Shape.Empty, new Vector3(0f, 1.12f, 0.02f), new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f), null, false),
        new Part("HeadBall", "Head", Shape.Sphere, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.62f, 0.56f, 0.52f), "Fur", true),
        new Part("Muzzle", "Head", Shape.Sphere, new Vector3(0f, -0.1f, 0.19f), new Vector3(0f, 0f, 0f), new Vector3(0.28f, 0.2f, 0.2f), "Muzzle", true),
        new Part("Nose", "Head", Shape.Sphere, new Vector3(0f, -0.045f, 0.285f), new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.085f, 0.09f), "Black", false),
        new Part("Mouth", "Head", Shape.Capsule, new Vector3(0.015f, -0.165f, 0.265f), new Vector3(0f, 0f, 90f), new Vector3(0.025f, 0.05f, 0.025f), "Black", false),
        new Part("HeadTuft0", "Head", Shape.Cone, new Vector3(-0.06f, 0.29f, -0.02f), new Vector3(-10f, 0f, 20f), new Vector3(0.08f, 0.06f, 0.08f), "Fur", true),
        new Part("HeadTuft1", "Head", Shape.Cone, new Vector3(0f, 0.29f, -0.02f), new Vector3(-10f, 0f, 0f), new Vector3(0.08f, 0.06f, 0.08f), "Fur", true),
        new Part("HeadTuft2", "Head", Shape.Cone, new Vector3(0.06f, 0.29f, -0.02f), new Vector3(-10f, 0f, -20f), new Vector3(0.08f, 0.06f, 0.08f), "Fur", true),
        new Part("EyeWhiteL", "Head", Shape.Sphere, new Vector3(-0.13f, 0.05f, 0.205f), new Vector3(0f, -20f, 0f), new Vector3(0.15f, 0.16f, 0.07f), "White", true),
        new Part("PupilL", "Head", Shape.Sphere, new Vector3(-0.138f, 0.045f, 0.236f), new Vector3(0f, -20f, 0f), new Vector3(0.09f, 0.11f, 0.035f), "Black", false),
        new Part("BrowL", "Head", Shape.Sphere, new Vector3(-0.11f, 0.175f, 0.195f), new Vector3(0f, -20f, 20f), new Vector3(0.11f, 0.05f, 0.04f), "FurDark", false),
        new Part("EarL", "Head", Shape.Sphere, new Vector3(-0.33f, -0.04f, -0.02f), new Vector3(0f, 0f, -15f), new Vector3(0.11f, 0.38f, 0.24f), "FurDark", true),
        new Part("CheekFluffAL", "Head", Shape.Cone, new Vector3(-0.29f, -0.12f, 0.06f), new Vector3(0f, 0f, 75f), new Vector3(0.08f, 0.055f, 0.08f), "Fur", true),
        new Part("CheekFluffBL", "Head", Shape.Cone, new Vector3(-0.27f, -0.19f, 0.04f), new Vector3(0f, 0f, 110f), new Vector3(0.07f, 0.05f, 0.07f), "Fur", true),
        new Part("EyeWhiteR", "Head", Shape.Sphere, new Vector3(0.13f, 0.05f, 0.205f), new Vector3(0f, 20f, 0f), new Vector3(0.15f, 0.16f, 0.07f), "White", true),
        new Part("PupilR", "Head", Shape.Sphere, new Vector3(0.138f, 0.045f, 0.236f), new Vector3(0f, 20f, 0f), new Vector3(0.09f, 0.11f, 0.035f), "Black", false),
        new Part("BrowR", "Head", Shape.Sphere, new Vector3(0.11f, 0.175f, 0.195f), new Vector3(0f, 20f, -20f), new Vector3(0.11f, 0.05f, 0.04f), "FurDark", false),
        new Part("EarR", "Head", Shape.Sphere, new Vector3(0.33f, -0.04f, -0.02f), new Vector3(0f, 0f, 15f), new Vector3(0.11f, 0.38f, 0.24f), "FurDark", true),
        new Part("CheekFluffAR", "Head", Shape.Cone, new Vector3(0.29f, -0.12f, 0.06f), new Vector3(0f, 0f, -75f), new Vector3(0.08f, 0.055f, 0.08f), "Fur", true),
        new Part("CheekFluffBR", "Head", Shape.Cone, new Vector3(0.27f, -0.19f, 0.04f), new Vector3(0f, 0f, -110f), new Vector3(0.07f, 0.05f, 0.07f), "Fur", true),
        new Part("Torso", "Body", Shape.Capsule, new Vector3(0f, 0.585f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.4f, 0.3f, 0.3f), "Fur", true),
        new Part("Belly", "Body", Shape.Sphere, new Vector3(0f, 0.57f, 0.075f), new Vector3(0f, 0f, 0f), new Vector3(0.25f, 0.36f, 0.17f), "Muzzle", true),
        new Part("ChestFluff0", "Body", Shape.Cone, new Vector3(-0.07f, 0.77f, 0.13f), new Vector3(150f, 0f, 0f), new Vector3(0.075f, 0.05f, 0.075f), "Muzzle", false),
        new Part("ChestFluff1", "Body", Shape.Cone, new Vector3(0f, 0.77f, 0.14f), new Vector3(150f, 0f, 0f), new Vector3(0.075f, 0.05f, 0.075f), "Muzzle", false),
        new Part("ChestFluff2", "Body", Shape.Cone, new Vector3(0.07f, 0.77f, 0.13f), new Vector3(150f, 0f, 0f), new Vector3(0.075f, 0.05f, 0.075f), "Muzzle", false),
        new Part("Scarf", "Body", Shape.Cylinder, new Vector3(0f, 0.86f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.05f, 0.28f), "Scarf", true),
        new Part("ScarfKnot", "Body", Shape.Sphere, new Vector3(0.1f, 0.85f, 0.13f), new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.09f, 0.07f), "Scarf", true),
        new Part("ScarfEndA", "Body", Shape.Cube, new Vector3(0.08f, 0.74f, 0.14f), new Vector3(-12f, 0f, -12f), new Vector3(0.085f, 0.19f, 0.03f), "Scarf", true),
        new Part("ScarfEndB", "Body", Shape.Cube, new Vector3(0.15f, 0.76f, 0.12f), new Vector3(-12f, 0f, 18f), new Vector3(0.075f, 0.16f, 0.03f), "Scarf", true),
        new Part("LegL", "Body", Shape.Capsule, new Vector3(-0.135f, 0.22f, 0f), new Vector3(0f, 0f, -6f), new Vector3(0.145f, 0.17f, 0.15f), "Fur", true),
        new Part("FootL", "Body", Shape.Sphere, new Vector3(-0.18f, 0.05f, 0.05f), new Vector3(0f, -10f, 0f), new Vector3(0.15f, 0.1f, 0.24f), "Muzzle", true),
        new Part("ToeClawL0", "Body", Shape.Cone, new Vector3(-0.245f, 0.035f, 0.175f), new Vector3(90f, -10f, 0f), new Vector3(0.03f, 0.022f, 0.03f), "Claw", false),
        new Part("ToeClawL1", "Body", Shape.Cone, new Vector3(-0.2f, 0.035f, 0.175f), new Vector3(90f, -10f, 0f), new Vector3(0.03f, 0.022f, 0.03f), "Claw", false),
        new Part("ToeClawL2", "Body", Shape.Cone, new Vector3(-0.155f, 0.035f, 0.175f), new Vector3(90f, -10f, 0f), new Vector3(0.03f, 0.022f, 0.03f), "Claw", false),
        new Part("ShoulderL", "Body", Shape.Sphere, new Vector3(-0.2f, 0.78f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.15f, 0.16f), "Fur", true),
        new Part("ShoulderFluffL", "Body", Shape.Cone, new Vector3(-0.27f, 0.8f, -0.02f), new Vector3(0f, 0f, 70f), new Vector3(0.07f, 0.05f, 0.07f), "Fur", true),
        new Part("ArmL", "Body", Shape.Capsule, new Vector3(-0.33f, 0.7f, 0.03f), new Vector3(0f, 0f, -60f), new Vector3(0.11f, 0.13f, 0.11f), "Fur", true),
        new Part("PawL", "Body", Shape.Sphere, new Vector3(-0.48f, 0.62f, 0.05f), new Vector3(0f, 0f, 0f), new Vector3(0.17f, 0.16f, 0.12f), "FurDark", true),
        new Part("PawPadL", "Body", Shape.Sphere, new Vector3(-0.48f, 0.61f, 0.108f), new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.06f, 0.03f), "Pad", false),
        new Part("ClawL0", "Body", Shape.Cone, new Vector3(-0.565f, 0.67f, 0.07f), new Vector3(0f, 0f, 65f), new Vector3(0.032f, 0.025f, 0.032f), "Claw", false),
        new Part("ClawL1", "Body", Shape.Cone, new Vector3(-0.565f, 0.62f, 0.07f), new Vector3(0f, 0f, 90f), new Vector3(0.032f, 0.025f, 0.032f), "Claw", false),
        new Part("ClawL2", "Body", Shape.Cone, new Vector3(-0.565f, 0.57f, 0.07f), new Vector3(0f, 0f, 115f), new Vector3(0.032f, 0.025f, 0.032f), "Claw", false),
        new Part("LegR", "Body", Shape.Capsule, new Vector3(0.135f, 0.22f, 0f), new Vector3(0f, 0f, 6f), new Vector3(0.145f, 0.17f, 0.15f), "Fur", true),
        new Part("FootR", "Body", Shape.Sphere, new Vector3(0.18f, 0.05f, 0.05f), new Vector3(0f, 10f, 0f), new Vector3(0.15f, 0.1f, 0.24f), "Muzzle", true),
        new Part("ToeClawR0", "Body", Shape.Cone, new Vector3(0.155f, 0.035f, 0.175f), new Vector3(90f, 10f, 0f), new Vector3(0.03f, 0.022f, 0.03f), "Claw", false),
        new Part("ToeClawR1", "Body", Shape.Cone, new Vector3(0.2f, 0.035f, 0.175f), new Vector3(90f, 10f, 0f), new Vector3(0.03f, 0.022f, 0.03f), "Claw", false),
        new Part("ToeClawR2", "Body", Shape.Cone, new Vector3(0.245f, 0.035f, 0.175f), new Vector3(90f, 10f, 0f), new Vector3(0.03f, 0.022f, 0.03f), "Claw", false),
        new Part("ShoulderR", "Body", Shape.Sphere, new Vector3(0.2f, 0.78f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.15f, 0.16f), "Fur", true),
        new Part("ShoulderFluffR", "Body", Shape.Cone, new Vector3(0.27f, 0.8f, -0.02f), new Vector3(0f, 0f, -70f), new Vector3(0.07f, 0.05f, 0.07f), "Fur", true),
        new Part("ArmR", "Body", Shape.Capsule, new Vector3(0.33f, 0.7f, 0.03f), new Vector3(0f, 0f, 60f), new Vector3(0.11f, 0.13f, 0.11f), "Fur", true),
        new Part("PawR", "Body", Shape.Sphere, new Vector3(0.48f, 0.62f, 0.05f), new Vector3(0f, 0f, 0f), new Vector3(0.17f, 0.16f, 0.12f), "FurDark", true),
        new Part("PawPadR", "Body", Shape.Sphere, new Vector3(0.48f, 0.61f, 0.108f), new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.06f, 0.03f), "Pad", false),
        new Part("ClawR0", "Body", Shape.Cone, new Vector3(0.565f, 0.67f, 0.07f), new Vector3(0f, 0f, -65f), new Vector3(0.032f, 0.025f, 0.032f), "Claw", false),
        new Part("ClawR1", "Body", Shape.Cone, new Vector3(0.565f, 0.62f, 0.07f), new Vector3(0f, 0f, -90f), new Vector3(0.032f, 0.025f, 0.032f), "Claw", false),
        new Part("ClawR2", "Body", Shape.Cone, new Vector3(0.565f, 0.57f, 0.07f), new Vector3(0f, 0f, -115f), new Vector3(0.032f, 0.025f, 0.032f), "Claw", false),
        new Part("Tail", "Body", Shape.Capsule, new Vector3(0.06f, 0.46f, -0.2f), new Vector3(-35f, 0f, -20f), new Vector3(0.1f, 0.14f, 0.1f), "FurDark", true),
        new Part("TailTip", "Body", Shape.Sphere, new Vector3(0.105f, 0.565f, -0.275f), new Vector3(-35f, 0f, -20f), new Vector3(0.13f, 0.13f, 0.13f), "Muzzle", true),
    };

    void Reset() { FindShader(); }
    void OnValidate() { FindShader(); }

    void Awake()
    {
        SendLightDirection();
        if (buildOnPlayIfMissing && transform.Find(RootName) == null)
            Build();
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

    [ContextMenu("Build Dog（生成小狗）")]
    public void Build()
    {
        Clear();
        FindShader();
        SendLightDirection();
        if (toonShader == null)
            Debug.LogWarning("没找到 DogToon.shader，小狗会用普通材质（没有三渲二效果）。请确认 DogToon.shader 在 Assets 里并且没有报错。");

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
                mat = GetMaterial(p.color, Colors[p.color], false);
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

    [ContextMenu("Clear Dog（删除小狗）")]
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
            string path = assetFolder + "/Materials/DogToon_" + name + ".mat";
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
        var m = new Material(PickShader()) { name = "DogToon_" + name };
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

    // 球、胶囊、圆柱可以做成平滑的；尖锥（毛、爪子）和方块（围巾尾巴）一直保持棱角
    bool IsSmooth(Shape s)
    {
        return smoothBody && (s == Shape.Sphere || s == Shape.Capsule || s == Shape.Cylinder);
    }

    Mesh GetMesh(Shape shape, bool forOutline)
    {
        bool hiRes = IsSmooth(shape);
        bool smooth = hiRes || forOutline;   // 平滑网格可以直接拿来做描边
        string name = (hiRes ? "DogSmooth_" + shape : "DogLowPoly_" + shape + (forOutline ? "_Outline" : ""));
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
                // 六棱锥：用来做尖尖的毛和爪子
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
