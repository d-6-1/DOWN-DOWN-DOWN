// @role Crossfades to a different song for each possessed body (rider, dog, chicken, extras)
// @calls GhostController
// @tune volume:float:0-1:Music volume
// @tune fadeTime:float:0-5:Crossfade time in seconds
// PossessionMusic.cs —— 附身到不同动物的时候放不同的音乐
//
// 用法：在场景里建一个空物体（比如叫 Music），把这个脚本拖上去，
// 再把音乐文件拖到 Inspector 里对应的格子。不用改 GhostController 或别的脚本。
//
// 它每一帧看一眼“幽灵现在附身在谁身上”（GhostController.Current），
// 判断那是 原来的骑手 / 滑雪小狗 / 滑雪小鸡，换歌的时候慢慢淡入淡出。
using UnityEngine;

public class PossessionMusic : MonoBehaviour
{
    [Header("每种身体的音乐（空着就沿用上一首）")]
    [Tooltip("开局的主角，以及用 Ctrl+D 复制出来的骑手")]
    public AudioClip riderMusic;
    [Tooltip("附身到滑雪小狗")]
    public AudioClip dogMusic;
    [Tooltip("附身到滑雪小鸡")]
    public AudioClip chickenMusic;

    [System.Serializable]
    public class ExtraMusic
    {
        [Tooltip("身体里某个子物体名字包含这几个字就算（比如 PenguinModel 填 Penguin）")]
        public string nameContains;
        public AudioClip music;
    }
    [Header("以后加了新动物，在这里加一行")]
    public ExtraMusic[] extraAnimals = new ExtraMusic[0];

    [Header("声音")]
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("换歌时淡入淡出用几秒")]
    public float fadeTime = 1.5f;
    [Tooltip("勾上：再附身回同一种动物时，从上次停下的地方接着放；不勾：从头放")]
    public bool resumeWhereLeft = true;
    [Tooltip("勾上：左上角显示现在是哪种身体、放的哪首歌，方便检查")]
    public bool showDebug = false;

    AudioSource[] players = new AudioSource[2];
    int active;              // players[active] 是正在淡入/正在放的那个
    AudioClip currentClip;
    SkierController lastBody;
    SkierController startBody;
    string lastKind = "";
    readonly System.Collections.Generic.Dictionary<AudioClip, float> savedTime =
        new System.Collections.Generic.Dictionary<AudioClip, float>();

    void Awake()
    {
        for (int i = 0; i < 2; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;          // 2D 声音：不管镜头离多远都一样大（场景放大了 19 倍也没关系）
            src.volume = 0f;
            src.ignoreListenerPause = true;
            players[i] = src;
        }
    }

    void Update()
    {
        SkierController body = GhostController.Current;
        if (body == null) body = FindObjectOfType<SkierController>();
        if (startBody == null) startBody = body;

        if (body != lastBody)
        {
            lastBody = body;
            AudioClip want = PickMusic(body, out lastKind);
            if (want != null && want != currentClip) SwitchTo(want);
        }

        KeepPlaying();
        Fade();
    }

    // 判断现在的身体是什么动物，选对应的音乐
    AudioClip PickMusic(SkierController body, out string kind)
    {
        kind = "没有身体";
        if (body == null) return null;

        foreach (var e in extraAnimals)
        {
            if (e == null || e.music == null || string.IsNullOrEmpty(e.nameContains)) continue;
            if (HasChildNamed(body.transform, e.nameContains)) { kind = e.nameContains; return e.music; }
        }

        // 小鸡的身体里有 ChickenModel（小鸡也是从小狗生成器继承来的，所以要先判断小鸡）
        if (HasChildNamed(body.transform, "ChickenModel")) { kind = "小鸡"; return chickenMusic; }

        // 被附身的滑雪小狗，生成器会把它改名叫 "possessed dog"
        if (body != startBody && body.gameObject.name.Contains("possessed")) { kind = "小狗"; return dogMusic; }

        kind = "原来的骑手";
        return riderMusic;
    }

    static bool HasChildNamed(Transform root, string part)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains(part)) return true;
        return false;
    }

    void SwitchTo(AudioClip clip)
    {
        AudioSource old = players[active];
        if (currentClip != null && old.isPlaying) savedTime[currentClip] = old.time;

        active = 1 - active;
        AudioSource next = players[active];
        next.Stop();
        next.clip = clip;
        next.volume = 0f;
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        float t;
        resumeAt = (resumeWhereLeft && savedTime.TryGetValue(clip, out t) && t < clip.length) ? t : 0f;
        next.Play();
        currentClip = clip;
    }

    float resumeAt;

    // 音乐还没加载完时 Play 可能没生效：加载好以后再补一次，并跳到上次停下的位置
    void KeepPlaying()
    {
        AudioSource src = players[active];
        if (currentClip == null || src.clip != currentClip) return;
        if (currentClip.loadState != AudioDataLoadState.Loaded) return;
        if (!src.isPlaying) src.Play();
        if (resumeAt > 0f) { src.time = resumeAt; resumeAt = 0f; }
    }

    // 用不受慢动作影响的时间淡入淡出（附身时游戏会变慢，GAME OVER 时会暂停）
    void Fade()
    {
        float step = fadeTime <= 0f ? 1f : Time.unscaledDeltaTime / fadeTime;
        for (int i = 0; i < 2; i++)
        {
            AudioSource src = players[i];
            float goal = i == active ? volume : 0f;
            src.volume = Mathf.MoveTowards(src.volume, goal, step * Mathf.Max(volume, 0.01f));
            if (i != active && src.isPlaying && src.volume <= 0f) src.Stop();
        }
    }

    void OnGUI()
    {
        if (!showDebug) return;
        string song = currentClip != null ? currentClip.name : "（没设置音乐）";
        AudioSource src = players[active];
        var listeners = FindObjectsOfType<AudioListener>();
        int listenOn = 0;
        foreach (var l in listeners) if (l.isActiveAndEnabled) listenOn++;
        string[] lines =
        {
            "音乐：现在是 " + lastKind + "，正在放 " + song,
            "加载状态：" + (currentClip != null ? currentClip.loadState.ToString() : "-")
                + "   在播放：" + (src.isPlaying ? "是" : "否")
                + "   播放到：" + src.time.ToString("0.0") + " 秒"
                + "   音量：" + src.volume.ToString("0.00"),
            "耳朵(Audio Listener)：一共 " + listeners.Length + " 个，开着的 " + listenOn + " 个"
                + "   总音量：" + AudioListener.volume.ToString("0.00")
                + "   总暂停：" + (AudioListener.pause ? "是" : "否"),
            "声卡：" + AudioSettings.outputSampleRate + " Hz   " + AudioSettings.driverCapabilities,
        };
        var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        style.normal.textColor = Color.black;
        GUI.Box(new Rect(5, 5, 820, 30 * lines.Length + 10), "");
        for (int i = 0; i < lines.Length; i++)
            GUI.Label(new Rect(12, 10 + 30 * i, 810, 28), lines[i], style);
    }
}
