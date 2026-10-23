// @role Game state: start, playing, win at the finish, lose on hitting an obstacle
// @calls SkierController GhostController
// @tune maxAirTime:float:1-10:Seconds in the air before it counts as a win/fall
using UnityEngine;
using UnityEngine.SceneManagement;

public class Gamemode : MonoBehaviour
{
    [Tooltip("If the rider stays airborne longer than this (seconds), they have left the end of the run: WIN")]
    public float maxAirTime = 4f;

    [Tooltip("Ghost possession: press E near another skier to take control of them")]
    public bool enableGhost = true;

    // The one Gamemode in the scene (copies of rider made with Ctrl+D also carry one; only the first is used)
    public static Gamemode Instance { get; private set; }
    public bool IsPlaying => state == State.Playing;

    enum State { Intro, Playing, Over }
    State state;

    // After pressing R on the end screen, skip the intro and start right away
    static bool skipIntro;

    SkierController skier;
    float airTimer;

    // The skier the ghost is in right now
    SkierController Skier => GhostController.Current != null ? GhostController.Current : skier;

    bool won;
    string reason = "";

    // ---------- Look & feel ----------
    static readonly Color Brown      = new Color(0.40f, 0.24f, 0.12f);
    static readonly Color LightBrown = new Color(0.62f, 0.42f, 0.24f);
    static readonly Color Cream      = new Color(1.00f, 0.97f, 0.90f, 0.94f);

    Texture2D panelTex, borderTex, lineTex, buttonTex, buttonHoverTex;
    GUIStyle titleStyle, bodyStyle, smallStyle, hudStyle, decoStyle, buttonStyle;
    bool stylesReady;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        Instance = this;

        skier = GetComponent<SkierController>();
        if (skier == null) skier = FindObjectOfType<SkierController>();

        if (enableGhost && FindObjectOfType<GhostController>() == null)
            new GameObject("GhostManager").AddComponent<GhostController>();
    }

    void Start()
    {
        if (Instance != this) return;
        panelTex       = MakeTex(Cream);
        borderTex      = MakeTex(Brown);
        lineTex        = MakeTex(LightBrown);
        buttonTex      = MakeTex(Brown);
        buttonHoverTex = MakeTex(LightBrown);

        if (skipIntro)
        {
            skipIntro = false;
            BeginRun();
        }
        else
        {
            // Freeze the game until the player starts
            state = State.Intro;
            Skier.canControl = false;
            Time.timeScale = 0f;
        }
    }

    void BeginRun()
    {
        state = State.Playing;
        Skier.canControl = true;
        Time.timeScale = 1f;
    }

    void Update()
    {
        switch (state)
        {
            case State.Intro:
                if (KeyInput.RestartPressed()) BeginRun();
                break;

            case State.Playing:
                // Leaving the end of the slope counts as reaching the bottom
                airTimer = Skier.IsGrounded ? 0f : airTimer + Time.deltaTime;
                if (airTimer > maxAirTime) EndGame(true, "You reached the bottom!", null);
                break;

            case State.Over:
                if (KeyInput.RestartPressed()) Restart();
                break;
        }
    }

    // Called by SkierController when the possessed skier hits something. Hitting an obstacle: LOSE
    public void SkierHit(SkierController who, Collider c)
    {
        if (state != State.Playing || who != Skier) return;
        if (GhostController.InGrace) return;   // just jumped into a new body
        Transform hit = FindTagged(c.transform, "Obstacle");
        if (hit != null)
            EndGame(false, $"You crashed into {hit.name}.", c.gameObject);
    }

    // Called by SkierController when the possessed skier passes through a trigger. Finish line: WIN
    public void SkierTrigger(SkierController who, Collider other)
    {
        if (state != State.Playing || who != Skier) return;
        if (FindTagged(other.transform, "Finish") != null)
            EndGame(true, "You reached the bottom!", null);
    }

    // Called by GhostController right after the ghost enters a new skier
    public void OnPossessed()
    {
        airTimer = 0f;
    }

    void EndGame(bool win, string why, GameObject culprit)
    {
        state = State.Over;
        won = win;
        reason = why;
        Skier.canControl = false;
        Rigidbody rb = Skier.GetComponent<Rigidbody>();
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;

        // Click this entry in the Console to select the object that was hit
        Debug.Log("[Gamemode] " + why, culprit);
    }

    void Restart()
    {
        skipIntro = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;   // never leave the game frozen
    }

    // Returns this object or the first parent that has the given tag
    Transform FindTagged(Transform t, string tag)
    {
        while (t != null)
        {
            if (t.CompareTag(tag)) return t;
            t = t.parent;
        }
        return null;
    }

    // =====================================================
    //                        UI
    // =====================================================
    void OnGUI()
    {
        if (Instance != this) return;
        if (!stylesReady) BuildStyles();

        switch (state)
        {
            case State.Intro:   DrawIntro(); break;
            case State.Playing: DrawHud();   break;
            case State.Over:    DrawEnd();   break;
        }
    }

    void DrawHud()
    {
        Rect p = new Rect(16, 16, 340, 76);
        GUI.Label(new Rect(p.x + 18, p.y + 10, p.width - 30, 26),
            $"Speed:  {Skier.CurrentSpeed:F1}", hudStyle);
        GUI.Label(new Rect(p.x + 18, p.y + 38, p.width - 30, 26),
            $"Board:  {(Skier.BoardSideways ? "Sideways (braking)" : "Straight (fast)")}", hudStyle);
    }
    void DrawIntro()
    {
        Rect p = CenterRect(600, enableGhost ? 424 : 400);
        DrawPanel(p);

        float x = p.x + 30, w = p.width - 60, y = p.y + 28;

        GUI.Label(new Rect(x, y, w, 30), "*   ~   *   ~   *", decoStyle);          y += 30;
        GUI.Label(new Rect(x, y, w, 44), "DOWN DOWN DOWN--Abby&Chloe", titleStyle);          y += 50;
        DrawLine(x + 80, y, w - 160);                                              y += 18;

        GUI.Label(new Rect(x, y, w, 60),
            "Your friend is injured!\nSki to the bottom to get help.", bodyStyle); y += 72;

        float rulesH = enableGhost ? 120 : 96;
        GUI.Label(new Rect(x, y, w, rulesH),
            "W  -  point the board downhill (speed up)\n" +
            "S  -  turn the board sideways (brake)\n" +
            "A / D  -  lead with the left / right nose\n" +
            (enableGhost ? "E  -  possess a nearby skier\n" : "") +
            "Avoid the trees and rocks!", smallStyle);                             y += rulesH + 12;

        if (GUI.Button(new Rect(p.center.x - 130, y, 260, 44), "START", buttonStyle)) BeginRun();
        y += 50;
        GUI.Label(new Rect(x, y, w, 24), "Press  R  or click START to begin", smallStyle);
    }

   

    void DrawEnd()
    {
        Rect p = CenterRect(560, won ? 330 : 290);
        DrawPanel(p);

        float x = p.x + 30, w = p.width - 60, y = p.y + 28;

        GUI.Label(new Rect(x, y, w, 30), "*   ~   *   ~   *", decoStyle);             y += 30;
        GUI.Label(new Rect(x, y, w, 44), won ? "YOU WIN!" : "GAME OVER", titleStyle); y += 50;
        DrawLine(x + 80, y, w - 160);                                                 y += 18;

        GUI.Label(new Rect(x, y, w, 30), reason, bodyStyle);                          y += 34;
        if (won)
        {
            GUI.Label(new Rect(x, y, w, 30), "You saved your friend!", bodyStyle);    y += 40;
        }
        else y += 6;

        if (GUI.Button(new Rect(p.center.x - 130, y, 260, 44), "RESTART", buttonStyle)) Restart();
        y += 50;
        GUI.Label(new Rect(x, y, w, 24), "Press  R  to play again", smallStyle);
    }

    // ---------- helpers ----------
    Rect CenterRect(float w, float h)
    {
        return new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
    }

    void DrawPanel(Rect r)
    {
        GUI.DrawTexture(r, panelTex);
        DrawFrame(r, 3f, borderTex);                                                       // thick outer border
        DrawFrame(new Rect(r.x + 7, r.y + 7, r.width - 14, r.height - 14), 1f, lineTex);  // thin inner line
    }

    void DrawFrame(Rect r, float t, Texture2D tex)
    {
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), tex);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), tex);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), tex);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), tex);
    }

    void DrawLine(float x, float y, float w)
    {
        GUI.DrawTexture(new Rect(x, y, w, 2), lineTex);
    }

    Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void BuildStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = Brown;

        bodyStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        bodyStyle.normal.textColor = Brown;

        smallStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        smallStyle.normal.textColor = LightBrown;

        hudStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        hudStyle.normal.textColor = Brown;

        decoStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        decoStyle.normal.textColor = LightBrown;

        buttonStyle = new GUIStyle(GUI.skin.button)
        { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        buttonStyle.normal.background = buttonTex;
        buttonStyle.hover.background = buttonHoverTex;
        buttonStyle.active.background = buttonHoverTex;
        buttonStyle.normal.textColor = Cream;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;

        stylesReady = true;
    }
}