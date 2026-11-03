// @role Crossfades to each possessed animal's own music loop (hero, chicken, giraffe, cat, rabbit, elephant, dachshund, dog, extras), switches to campfire music near the ghost campfire, and fades in a wind sound while it snows
// @calls GhostController SkierController SlopeFork SnowWeatherManager
// @tune volume:float:0-1:Music volume
// @tune fadeTime:float:0-5:Crossfade time in seconds
// @tune campfireRange:float:5-200:How many meters before/after the campfire its music plays
// @tune windVolume:float:0-1:Wind sound volume while it snows
// @tune windFadeTime:float:0-10:Seconds for the wind to fade in/out
// PossessionMusic.cs —— 附身到不同动物放不同的音乐 + 篝火音乐 + 下雪时的风声（第二版，2026-11-03）
//
// 用法：在场景里建一个空物体（比如叫 Music），把这个脚本拖上去，
// 再把音乐文件拖到 Inspector 里对应的格子。不用改 GhostController、SlopeFork、SnowWeatherManager 或别的脚本。
//
// 它每一帧看一眼：
//   1. 幽灵现在附身在谁身上（GhostController.Current）→ 选那只动物的音乐，慢慢淡入淡出地换歌；
//   2. 主角是不是在左边那条道、离篝火很近（SlopeFork），或者正在篝火边对话 → 换成篝火音乐；
//   3. 现在是不是在下雪刮风（SnowWeatherManager 开着并且 isSnowing）→ 风声慢慢变大，不下雪了就慢慢变小。
//      风声是另外一层，和音乐一起放，不会把音乐换掉。
// 第一版里拖好的 Rider / Dog / Chicken 三首歌，换成这一版以后还在原来的格子里。
using System.Collections.Generic;
using UnityEngine;

public class PossessionMusic : MonoBehaviour
{
    [Header("每只动物的音乐 loop（空着就沿用上一首）")]
    [Tooltip("开局的主角（还没附身到动物时）")]
    public AudioClip riderMusic;
    [Tooltip("附身到鸡")]
    public AudioClip chickenMusic;
    [Tooltip("附身到长颈鹿")]
    public AudioClip giraffeMusic;
    [Tooltip("附身到猫")]
    public AudioClip catMusic;
    [Tooltip("附身到兔子")]
    public AudioClip rabbitMusic;
    [Tooltip("附身到大象")]
    public AudioClip elephantMusic;
    [Tooltip("附身到腊肠")]
    public AudioClip dachshundMusic;
    [Tooltip("附身到普通的滑雪小狗（现在的名单里没有它了，可以空着）")]
    public AudioClip dogMusic;

    [System.Serializable]
    public class ExtraMusic
    {
        [Tooltip("身体里某个子物体名字包含这几个字就算（比如 PenguinModel 填 Penguin）")]
        public string nameContains;
        public AudioClip music;
    }
    [Header("以后加了新动物，在这里加一行")]
    public ExtraMusic[] extraAnimals = new ExtraMusic[0];

    [Header("篝火（幽灵聚集的地方）")]
    [Tooltip("在左边那条道、离篝火这么近的时候放这首（篝火边对话的时候也放这首）")]
    public AudioClip campfireMusic;
    [Tooltip("离篝火多少米以内开始放篝火音乐（往前、往后都算）")]
    public float campfireRange = 60f;

    [Header("刮风下雪")]
    [Tooltip("下雪刮风的时候放的风声（会一直循环，直到雪停）")]
    public AudioClip windSound;
    [Range(0f, 1f)] public float windVolume = 0.5f;
    [Tooltip("风声淡入淡出用几秒")]
    public float windFadeTime = 3f;

    [Header("声音")]
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("换歌时淡入淡出用几秒")]
    public float fadeTime = 1.5f;
    [Tooltip("勾上：再换回同一首歌时，从上次停下的地方接着放；不勾：从头放")]
    public bool resumeWhereLeft = true;
    [Tooltip("勾上：左上角显示现在是哪种身体、放的哪首歌、有没有风声，方便检查")]
    public bool showDebug = false;

    AudioSource[] players = new AudioSource[2];
    AudioSource windPlayer;
    int active;              // players[active] 是正在淡入/正在放的那个
    AudioClip currentClip;
    SkierController lastBody;
    SkierController startBody;
    AudioClip bodyClip;      // 现在这个身体的歌
    string lastKind = "";
    bool atCampfire;
    bool windy;
    SnowWeatherManager weather;
    float nextWeatherLook;
    readonly Dictionary<AudioClip, float> savedTime = new Dictionary<AudioClip, float>();

    void Awake()
    {
        for (int i = 0; i < 2; i++) players[i] = MakeSource(true);
        windPlayer = MakeSource(true);
    }

    AudioSource MakeSource(bool loop)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;          // 2D 声音：不管镜头离多远都一样大（场景放大了 19 倍也没关系）
        src.volume = 0f;
        src.ignoreListenerPause = true;
        return src;
    }

    void Update()
    {
        SkierController body = GhostController.Current;
        if (body == null) body = FindObjectOfType<SkierController>();
        if (startBody == null) startBody = body;

        if (body != lastBody)
        {
            lastBody = body;
            bodyClip = PickMusic(body, out lastKind);
        }

        atCampfire = campfireMusic != null && NearCampfire(body);
        AudioClip want = atCampfire ? campfireMusic : bodyClip;
        if (want != null && want != currentClip) SwitchTo(want);

        KeepPlaying();
        Fade();
        Wind();
    }

    // 判断现在的身体是什么动物，选对应的音乐
    AudioClip PickMusic(SkierController body, out string kind)
    {
        kind = "没有身体";
        if (body == null) return null;

        if (body != startBody)
        {
            foreach (var e in extraAnimals)
            {
                if (e == null || e.music == null || string.IsNullOrEmpty(e.nameContains)) continue;
                if (HasChildNamed(body.transform, e.nameContains)) { kind = e.nameContains; return e.music; }
            }

            // 每只动物的模型里都有一个叫 XxxModel 的零件（复制出来的外壳会改名叫 DogModel，所以先认具体的动物）
            if (HasChildNamed(body.transform, "ChickenModel")) { kind = "鸡"; return chickenMusic; }
            if (HasChildNamed(body.transform, "GiraffeModel")) { kind = "长颈鹿"; return giraffeMusic; }
            if (HasChildNamed(body.transform, "CatModel")) { kind = "猫"; return catMusic; }
            if (HasChildNamed(body.transform, "RabbitModel")) { kind = "兔子"; return rabbitMusic; }
            if (HasChildNamed(body.transform, "ElephantModel")) { kind = "大象"; return elephantMusic; }
            if (HasChildNamed(body.transform, "DachshundModel")) { kind = "腊肠"; return dachshundMusic; }

            // 被附身的滑雪小狗，生成器会把它改名叫 "possessed dog"
            if (body.gameObject.name.Contains("possessed")) { kind = "小狗"; return dogMusic; }
        }

        kind = "主角";
        return riderMusic;
    }

    static bool HasChildNamed(Transform root, string part)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains(part)) return true;
        return false;
    }

    // 在左边那条道上、离篝火 campfireRange 米以内，或者篝火边正在对话
    bool NearCampfire(SkierController body)
    {
        SlopeFork fork = SlopeFork.Instance;
        if (fork == null || !fork.Ready) return false;
        if (fork.Talking) return true;
        if (body == null) return false;
        float meters;
        if (!fork.InLeftLane(body.FeetPos, out meters)) return false;
        float range = atCampfire ? campfireRange * 1.2f : campfireRange;   // 出去时多留一点，免得在边上来回换歌
        return Mathf.Abs(meters - fork.CampfireMeters) <= range;
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

    // 下雪刮风时风声淡入，雪停了淡出（前两趟 MountainMood 会把下雪关掉，那时也没有风声）
    void Wind()
    {
        if (weather == null && Time.unscaledTime >= nextWeatherLook)
        {
            weather = FindObjectOfType<SnowWeatherManager>();
            nextWeatherLook = Time.unscaledTime + 1f;
        }
        windy = weather != null && weather.isActiveAndEnabled && weather.isSnowing;

        if (windSound == null) { windPlayer.Stop(); return; }
        if (windPlayer.clip != windSound) { windPlayer.Stop(); windPlayer.clip = windSound; }
        if (windSound.loadState != AudioDataLoadState.Loaded) windSound.LoadAudioData();

        float goal = windy ? windVolume : 0f;
        float step = windFadeTime <= 0f ? 1f : Time.unscaledDeltaTime / windFadeTime;
        windPlayer.volume = Mathf.MoveTowards(windPlayer.volume, goal, step * Mathf.Max(windVolume, 0.01f));

        if (goal > 0f && !windPlayer.isPlaying && windSound.loadState == AudioDataLoadState.Loaded) windPlayer.Play();
        if (goal <= 0f && windPlayer.isPlaying && windPlayer.volume <= 0f) windPlayer.Stop();
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
            "音乐：现在是 " + lastKind + (atCampfire ? "（在篝火边）" : "") + "，正在放 " + song,
            "加载状态：" + (currentClip != null ? currentClip.loadState.ToString() : "-")
                + "   在播放：" + (src.isPlaying ? "是" : "否")
                + "   播放到：" + src.time.ToString("0.0") + " 秒"
                + "   音量：" + src.volume.ToString("0.00"),
            "风声：" + (windSound != null ? windSound.name : "（没设置）")
                + "   在下雪：" + (windy ? "是" : "否")
                + "   风声音量：" + windPlayer.volume.ToString("0.00")
                + (weather == null ? "   （场景里没找到 SnowWeatherManager）" : ""),
            "耳朵(Audio Listener)：一共 " + listeners.Length + " 个，开着的 " + listenOn + " 个"
                + "   总音量：" + AudioListener.volume.ToString("0.00")
                + "   总暂停：" + (AudioListener.pause ? "是" : "否"),
        };
        var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        style.normal.textColor = Color.black;
        GUI.Box(new Rect(5, 5, 860, 30 * lines.Length + 10), "");
        for (int i = 0; i < lines.Length; i++)
            GUI.Label(new Rect(12, 10 + 30 * i, 850, 28), lines[i], style);
    }
}
