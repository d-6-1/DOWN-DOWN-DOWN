// @role On a crash: slow motion, snow burst, fade to black, then back to the opening
// @calls Gamemode OpeningScene ClimberBuilder GhostController SkierController
// @tune slowMotion:float:0.05-1:Time scale right after the crash
// @tune snowChunks:int:0-60:Snow chunks thrown out
// @tune shake:float:0-1:Camera shake strength
// @tune blackAfter:float:0.2-3:Seconds before fading to black
// @tune fadeTime:float:0.1-2:Fade duration
// CrashTransition.cs —— 撞车失败时的小动画：慢动作 + 碰撞特效（雪块四溅、黄色冲击星、镜头抖动），然后黑屏，直接回到开场剧情
// 不再显示 GAME OVER 界面，也不用按 R。到达终点（赢了）还是原来的 YOU WIN 界面。
//
// 用法：挂在"开场"那个物体上（和 OpeningScene 放一起），Add Component → CrashTransition。
// 需要同一个文件夹里的 OpeningScene.cs 和 ClimberBuilder.cs。不改 Gamemode、GhostController 这些原来的脚本。

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using CPart = ClimberBuilder.Part;
using CShape = ClimberBuilder.Shape;

[DefaultExecutionOrder(1000)]   // 在 GhostController 摆好镜头之后再抖镜头
public class CrashTransition : MonoBehaviour
{
    [Header("慢动作")]
    [Tooltip("撞上那一刻的慢动作：越小越慢")]
    [Range(0.05f, 1f)] public float slowMotion = 0.15f;

    [Header("特效")]
    [Tooltip("飞出去的雪块有几块")]
    public int snowChunks = 18;
    [Tooltip("特效大小（身高的几倍）")]
    public float effectSize = 1f;
    [Tooltip("镜头抖动的幅度（身高的几倍）。0 = 不抖")]
    public float shake = 0.25f;

    [Header("黑屏")]
    [Tooltip("撞上以后过几秒开始变黑")]
    public float blackAfter = 0.7f;
    [Tooltip("变黑要几秒")]
    public float fadeTime = 0.5f;
    [Tooltip("全黑停几秒再回到开场")]
    public float holdBlack = 0.4f;

    static readonly Dictionary<string, Color32> FxColors = new Dictionary<string, Color32>
    {
        { "Snow", new Color32(248, 250, 252, 255) },
        { "Star", new Color32(250, 214, 92, 255) },
        { "StarHot", new Color32(240, 140, 72, 255) },
        { "Flash", new Color32(255, 250, 230, 255) },
    };

    class Chunk { public Transform t; public Vector3 vel, spin; public float size; }

    FieldInfo wonField;
    bool wasPlaying, crashing;
    float black;
    float shakeAmp;
    Camera cam;

    void Start()
    {
        wonField = typeof(Gamemode).GetField("won", BindingFlags.NonPublic | BindingFlags.Instance);
        if (wonField == null) Debug.LogWarning("CrashTransition：Gamemode 里找不到 won，赢了也会播撞车动画。");
    }

    void Update()
    {
        var gm = Gamemode.Instance;
        if (gm == null || crashing) return;
        bool playing = gm.IsPlaying;
        if (wasPlaying && !playing && !Won(gm)) StartCoroutine(CrashRoutine(gm));
        wasPlaying = playing;
    }

    bool Won(Gamemode gm)
    {
        return wonField != null && (bool)wonField.GetValue(gm);
    }

    void LateUpdate()
    {
        if (cam == null || shakeAmp <= 0f) return;
        float t = Time.unscaledTime * 40f;
        cam.transform.position += cam.transform.right * (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * shakeAmp
                                + cam.transform.up * (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * shakeAmp;
    }

    IEnumerator CrashRoutine(Gamemode gm)
    {
        crashing = true;
        gm.enabled = false;   // 不显示 GAME OVER 界面
        cam = Camera.main;

        SkierController who = GhostController.Current != null ? GhostController.Current : FindObjectOfType<SkierController>();
        float h = who != null ? Mathf.Max(0.1f, who.BodyHeight) : 1.6f;
        Vector3 hit = who != null ? who.BodyCenter : (cam != null ? cam.transform.position + cam.transform.forward * 10f : Vector3.zero);
        h *= effectSize;

        float fixedBase = Time.fixedDeltaTime;
        Time.timeScale = slowMotion;
        Time.fixedDeltaTime = fixedBase * slowMotion;

        var fx = new GameObject("CrashFX").transform;
        List<Chunk> chunks = MakeChunks(fx, hit, h);
        Transform star = MakeStar(fx, hit, h);

        float total = blackAfter + fadeTime + holdBlack;
        for (float e = 0f; e < total; e += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            // 雪块：往外飞、往下掉、转着圈、越来越小
            foreach (var c in chunks)
            {
                c.vel += Vector3.down * 9f * h * dt;
                c.t.position += c.vel * dt;
                c.t.Rotate(c.spin * dt, Space.Self);
                c.t.localScale = Vector3.one * c.size * Mathf.Clamp01(1.2f - e / 1.1f);
            }
            // 冲击星：一下子弹开，再缩没
            float k = e / 0.45f;
            float s = k < 0.25f ? Mathf.Lerp(0.2f, 1.15f, k / 0.25f) : Mathf.Lerp(1.15f, 0f, (k - 0.25f) / 0.75f);
            star.localScale = Vector3.one * h * Mathf.Max(0f, s);
            if (cam != null) star.rotation = Quaternion.LookRotation(star.position - cam.transform.position, cam.transform.up);

            shakeAmp = shake * h * Mathf.Clamp01(1f - e / 0.6f);
            black = Mathf.Clamp01((e - blackAfter) / Mathf.Max(0.01f, fadeTime));
            yield return null;
        }

        // 回到开场：重新加载场景，跳过开始界面，一定播开场
        shakeAmp = 0f;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = fixedBase;
        OpeningScene.forceNext = true;
        var skip = typeof(Gamemode).GetField("skipIntro", BindingFlags.NonPublic | BindingFlags.Static);
        if (skip != null) skip.SetValue(null, true);
        else Debug.LogWarning("CrashTransition：Gamemode 里找不到 skipIntro，回来时会先显示开始界面。");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    List<Chunk> MakeChunks(Transform fx, Vector3 at, float h)
    {
        var list = new List<Chunk>();
        for (int i = 0; i < snowChunks; i++)
        {
            float size = Random.Range(0.15f, 0.4f) * h;
            var holder = new GameObject("Chunk").transform;
            holder.SetParent(fx, false);
            holder.position = at + Random.insideUnitSphere * 0.3f * h;
            holder.localScale = Vector3.one * size;   // 先放大再生成，描边粗细才对
            var parts = new[] { new CPart("Snow", "Chunk", CShape.Sphere, Vector3.zero, Random.insideUnitSphere * 180f, new Vector3(1f, 0.75f, 0.9f), "Snow", true) };
            Transform root = ClimberBuilder.BuildParts(holder, "Chunk", parts, FxColors, 1f, 0.06f, false, null);
            root.localPosition = Vector3.zero;
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.3f;   // 大多往上、往外溅
            list.Add(new Chunk { t = holder, vel = dir.normalized * Random.Range(3f, 7f) * h, spin = Random.insideUnitSphere * 720f, size = size });
        }
        return list;
    }

    // 漫画里那种黄色冲击星：中间一个白色亮球，一圈尖角往外指
    Transform MakeStar(Transform fx, Vector3 at, float h)
    {
        var holder = new GameObject("ImpactStar").transform;
        holder.SetParent(fx, false);
        holder.position = at;
        holder.localScale = Vector3.one * h;   // 先放大再生成，描边粗细才对
        var parts = new List<CPart>
        {
            new CPart("Core", "Star", CShape.Sphere, Vector3.zero, Vector3.zero, new Vector3(0.7f, 0.7f, 0.2f), "Flash", true),
        };
        int spikes = 10;
        for (int i = 0; i < spikes; i++)
        {
            float a = i * 360f / spikes;
            float len = i % 2 == 0 ? 0.75f : 0.5f;
            Vector3 d = Quaternion.Euler(0f, 0f, a) * Vector3.up;
            // 尖锥默认尖头朝 +Y：绕 Z 转 a 度就指向那个方向
            parts.Add(new CPart("Spike" + i, "Star", CShape.Cone, d * (0.3f + len * 0.5f), new Vector3(0f, 0f, a), new Vector3(0.28f, len * 0.5f, 0.08f), i % 2 == 0 ? "Star" : "StarHot", true));
        }
        ClimberBuilder.BuildParts(holder, "Star", parts.ToArray(), FxColors, 1f, 0.03f, false, null);
        holder.localScale = Vector3.zero;
        return holder;
    }

    void OnGUI()
    {
        if (black <= 0f) return;
        GUI.depth = -110;
        GUI.color = new Color(0f, 0f, 0f, black);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
