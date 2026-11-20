using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 新手教學：由 RollingSkyGameManager 在 IsTutorialMode == 1 時於執行期 AddComponent。
/// 以程式碼生成一條短而溫和的教學賽道，逐步教導所有操作，玩家真的做出動作才進入下一步。
/// 掉落/碰撞障礙不會死亡，只會回到目前步驟的檢查點。
/// PlayerPrefs：Tutorial_Completed (0/1)、IsTutorialMode (0/1，由主選單設定並於回到主選單時歸零)。
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public const string PrefCompleted = "Tutorial_Completed";
    public const string PrefMode = "IsTutorialMode";

    // 重新開始 (R / 暫停選單重來 / 遊戲結束重試) 會重載場景，以此靜態值接續目前步驟。
    // 主選單載入時會呼叫 ResetResume() 歸零，避免下次從選單進入時誤接續。
    private static int resumeStep = -1;
    public static void ResetResume() { resumeStep = -1; }

    // ---- 步驟定義 ----
    private const int STEP_INTRO = 0;
    private const int STEP_LEFT = 1;
    private const int STEP_RIGHT = 2;
    private const int STEP_JUMP = 3;
    private const int STEP_GEM = 4;
    private const int STEP_HAZARD = 5;
    private const int STEP_PAD = 6;
    private const int STEP_PAUSE = 7;
    private const int STEP_RESTART = 8;
    private const int STEP_COUNT = 9;      // 9 = 完成畫面

    private const float ZONE_LEN = 60f;
    private const float TRACK_HALF_WIDTH = 5f;
    private const float FLOOR_TOP = 0.2f;
    private const float TUTORIAL_SPEED = 7f;

    private struct StepInfo
    {
        public string title;
        public string body;
        public string hint;
        public StepInfo(string t, string b, string h) { title = t; body = b; hint = h; }
    }

    private StepInfo[] steps = new StepInfo[]
    {
        new StepInfo("歡迎來到新手教學！",
            "小球會自動向前衝刺，你只需要控制「左右移動」與「跳躍」。\n每個步驟要真的做出動作才會進入下一步；掉下去也沒關係，會回到檢查點。",
            "約 5 秒後開始第一步"),
        new StepInfo("步驟 1 / 8：向左移動",
            "鍵盤：按住 A 或 ←　　|　　Arduino 控制器：向左傾斜",
            "把小球移到左側的發光區域"),
        new StepInfo("步驟 2 / 8：向右移動",
            "鍵盤：按住 D 或 →　　|　　Arduino 控制器：向右傾斜",
            "把小球移到右側的發光區域"),
        new StepInfo("步驟 3 / 8：跳躍",
            "鍵盤：按下 Space　　|　　Arduino 控制器：手掌快速靠近超音波感測器（推進手勢）",
            "現在就跳一下吧！"),
        new StepInfo("步驟 4 / 8：收集寶石",
            "操控小球碰到金色寶石 ◆（鍵盤 A / D、← / →，或傾斜控制器對準寶石）",
            "撿到任意一顆寶石即可"),
        new StepInfo("步驟 5 / 8：閃避障礙",
            "紅色方塊是障礙物！向旁邊移開（A / D、← / →、傾斜控制器）\n或跳過它（Space / 手掌靠近感測器）",
            "撞到也不會失敗，只會回到檢查點重來"),
        new StepInfo("步驟 6 / 8：使用彈跳墊",
            "壓上綠色彈跳墊，小球會被高高彈起，可飛越大缺口",
            "直接滾過綠色彈跳墊即可"),
        new StepInfo("步驟 7 / 8：暫停遊戲",
            "按下鍵盤 ESC 開啟暫停選單，再按一次 ESC 繼續遊戲\n（使用 Arduino 的玩家請用鍵盤 ESC 暫停）",
            "先暫停，再繼續"),
        new StepInfo("步驟 8 / 8：重新開始",
            "按下鍵盤 R 可隨時重新開始關卡\n（教學會自動接續，不會從頭來過）",
            "現在按一下 R 試試看"),
    };

    private RollingSkyGameManager gm;
    private RollingSkyBallController ctrl;
    private Rigidbody ballRb;
    private Transform ball;
    private ArduinoReceiver arduino;

    private int step = 0;
    private bool built = false;
    private bool finale = false;
    private bool isReplay = false;       // 開始教學前已完成過 → 重玩模式 (顯示跳過提示、跳過後回主選單)
    private bool enteredZone = true;
    private float stepTimer = 0f;
    private bool pausedSeen = false;
    private float padLaunchBaseline = -999f;
    private bool leaving = false;

    private Transform root;
    private readonly List<GameObject> stepGems = new List<GameObject>();
    private Vector3 hazardPos;

    private Material matDark, matCyan, matHazard, matPad, matGold, matMarkL, matMarkR;
    private PhysicsMaterial tilePhys;

    // ---- UI ----
    private Font font;
    private Transform canvasTr;
    private GameObject promptPanel;
    private Text txtTag, txtTitle, txtBody, txtHint;
    private Text txtBanner;
    private float bannerTimer = 0f;
    private Text txtSkip;
    private GameObject finalePanel;
    private Image[] finaleBtnImages = new Image[2];
    private int finaleSel = 0;
    private float finaleNavCooldown = 0f;
    private bool lastJoyBtn = false;

    private static readonly Color BtnNormal = new Color(0.1f, 0.22f, 0.38f, 0.95f);
    private static readonly Color BtnHighlight = new Color(0.95f, 0.65f, 0.05f, 1f);

    float ZoneStart(int s) { return s * ZONE_LEN; }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        gm = FindAnyObjectByType<RollingSkyGameManager>();
        isReplay = PlayerPrefs.GetInt(PrefCompleted, 0) == 1;

        step = 0;
        if (resumeStep >= 0 && resumeStep <= STEP_COUNT) step = resumeStep;
        resumeStep = step;

        // 防止 GameManager 以 trackLength 判定通關 (教學由本腳本自行結束)
        if (gm != null) gm.trackLength = 1000000f;

        StartCoroutine(Boot());
    }

    IEnumerator Boot()
    {
        // 等待場景內 RollingSkyTrackGenerator 完成它預設的第 1 關生成，再整批清除
        yield return null;
        yield return null;

        gm = FindAnyObjectByType<RollingSkyGameManager>();
        ctrl = FindAnyObjectByType<RollingSkyBallController>();
        if (gm == null || ctrl == null)
        {
            Debug.LogWarning("[Tutorial] 找不到 GameManager 或球體控制器，教學中止。");
            yield break;
        }
        ball = ctrl.transform;
        ballRb = ball.GetComponent<Rigidbody>();
        arduino = FindAnyObjectByType<ArduinoReceiver>();

        CleanupGeneratedLevel();
        yield return null; // 讓 Destroy 生效，避免舊賽道碰撞體與新賽道重疊

        CacheMaterials();
        BuildTrack();

        ctrl.forwardSpeed = TUTORIAL_SPEED;
        padLaunchBaseline = ctrl.LastJumpPadLaunchTime;
        gm.trackLength = 1000000f;

        // 隱藏一般關卡的進度條 (教學沒有百分比)
        if (gm.progressBar != null) gm.progressBar.gameObject.SetActive(false);
        if (gm.progressText != null) gm.progressText.gameObject.SetActive(false);

        BuildUI();
        if (isReplay) AddSkipButtonToPausePanel(); // 第一次遊玩不提供跳過

        gm.ResetLevelStats(); // 會重新計算寶石總數並鎖定游標，需在完成畫面解鎖游標之前呼叫

        if (step >= STEP_COUNT)
        {
            PlaceBall(ZoneStart(STEP_COUNT));
            BeginFinale();
        }
        else
        {
            PlaceBall(ZoneStart(step));
            enteredZone = true;
            RefreshPromptUI();
        }

        built = true;
    }

    void CleanupGeneratedLevel()
    {
        GameObject tr = GameObject.Find("TrackRoot");
        if (tr != null)
        {
            for (int i = tr.transform.childCount - 1; i >= 0; i--)
                Destroy(tr.transform.GetChild(i).gameObject);
        }
        foreach (var g in FindObjectsByType<GemCollectible>(FindObjectsSortMode.None)) Destroy(g.gameObject);
        foreach (var p in FindObjectsByType<JumpPad>(FindObjectsSortMode.None)) Destroy(p.gameObject);
        foreach (var h in FindObjectsByType<Hazard>(FindObjectsSortMode.None)) Destroy(h.gameObject);
        foreach (var f in FindObjectsByType<FinishTrigger>(FindObjectsSortMode.None)) Destroy(f.gameObject);
        GameObject gate = GameObject.Find("FinishStarGate");
        if (gate != null) Destroy(gate);
        GameObject legacy = GameObject.Find("--- Track Hierarchy ---");
        if (legacy != null) Destroy(legacy);
    }

    // ==================== 賽道建構 ====================
    void CacheMaterials()
    {
        var tg = RollingSkyTrackGenerator.Instance;
        if (tg != null)
        {
            matDark = tg.matTileDark;
            matCyan = tg.matTileCyan;
            matHazard = tg.matHazard;
            matPad = tg.matJumpPad;
        }
        if (matDark == null) matDark = MakeMat(new Color(0.08f, 0.12f, 0.22f));
        if (matCyan == null) matCyan = MakeMat(new Color(0f, 0.75f, 1f));
        if (matHazard == null) matHazard = MakeMat(new Color(1f, 0.15f, 0.15f));
        if (matPad == null) matPad = MakeMat(new Color(0.1f, 1f, 0.4f));
        matGold = MakeMat(new Color(1f, 0.85f, 0.1f));
        matMarkL = MakeMat(new Color(0.2f, 1f, 0.9f));
        matMarkR = MakeMat(new Color(1f, 0.5f, 0.9f));

        tilePhys = new PhysicsMaterial("TutorialTile");
        tilePhys.bounciness = 0f;
        tilePhys.bounceCombine = PhysicsMaterialCombine.Minimum;
        tilePhys.dynamicFriction = 0.05f;
        tilePhys.staticFriction = 0.05f;
        tilePhys.frictionCombine = PhysicsMaterialCombine.Minimum;
    }

    static Material MakeMat(Color c)
    {
        Material m = new Material(Shader.Find("Standard"));
        m.color = c;
        m.SetFloat("_Glossiness", 0.6f);
        return m;
    }

    GameObject MakeBlock(string name, Vector3 pos, Vector3 scale, Material mat, bool collider)
    {
        GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name = name;
        o.transform.SetParent(root, false);
        o.transform.position = pos;
        o.transform.localScale = scale;
        o.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Collider c = o.GetComponent<Collider>();
        if (!collider) { if (c != null) Destroy(c); }
        else if (c != null) c.sharedMaterial = tilePhys;
        return o;
    }

    void BuildTrack()
    {
        root = new GameObject("TutorialTrack").transform;

        // 地板：每 6m 一塊，棋盤色交替；總長涵蓋所有步驟區 + 完成區
        const float seg = 6f;
        float endZ = ZoneStart(STEP_COUNT) + 50f;
        int idx = 0;
        for (float z = -12f; z < endZ; z += seg, idx++)
        {
            MakeBlock("Floor", new Vector3(0f, 0f, z + seg * 0.5f), new Vector3(TRACK_HALF_WIDTH * 2f, 0.4f, seg),
                (idx % 2 == 0) ? matDark : matCyan, true);
        }

        // 每個步驟區起點的金色檢查點線
        for (int s = 0; s <= STEP_COUNT; s++)
        {
            MakeBlock("Checkpoint_" + s, new Vector3(0f, 0.005f, ZoneStart(s)), new Vector3(TRACK_HALF_WIDTH * 2f, 0.42f, 0.5f), matGold, false);
        }

        // 步驟 1/2：左右發光目標區
        float zl = ZoneStart(STEP_LEFT) + 28f;
        MakeBlock("Marker_Left", new Vector3(-3.6f, FLOOR_TOP + 0.01f, zl), new Vector3(2.2f, 0.02f, 14f), matMarkL, false);
        float zr = ZoneStart(STEP_RIGHT) + 28f;
        MakeBlock("Marker_Right", new Vector3(3.6f, FLOOR_TOP + 0.01f, zr), new Vector3(2.2f, 0.02f, 14f), matMarkR, false);

        // 步驟 4：三顆寶石 (左、右、中)，需要左右移動才撿得到
        float gz = ZoneStart(STEP_GEM);
        stepGems.Add(MakeGem(new Vector3(-2.5f, 1.15f, gz + 22f)));
        stepGems.Add(MakeGem(new Vector3(2.5f, 1.15f, gz + 30f)));
        stepGems.Add(MakeGem(new Vector3(0f, 1.15f, gz + 38f)));

        // 步驟 5：中央障礙 (自訂軟性判定：撞到只回檢查點，不會致死)
        hazardPos = new Vector3(0f, FLOOR_TOP + 0.5f, ZoneStart(STEP_HAZARD) + 30f);
        MakeBlock("TutorialHazard", hazardPos, new Vector3(2.6f, 1.0f, 1.4f), matHazard, false);

        // 步驟 6：彈跳墊 (JumpPad 於 Awake 內自行設定 BoxCollider 為 Trigger)
        GameObject pad = MakeBlock("TutorialJumpPad", new Vector3(0f, FLOOR_TOP + 0.05f, ZoneStart(STEP_PAD) + 26f), new Vector3(2.4f, 0.12f, 2.4f), matPad, true);
        pad.AddComponent<JumpPad>();
    }

    GameObject MakeGem(Vector3 pos)
    {
        GameObject gem = new GameObject("TutorialGem");
        gem.transform.SetParent(root, false);
        gem.transform.position = pos;
        // GemCollectible 需要先掛 Collider (RequireComponent)
        SphereCollider sc = gem.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.55f;
        gem.AddComponent<GemCollectible>();
        return gem;
    }

    void PlaceBall(float z)
    {
        Vector3 p = new Vector3(0f, 1.0f, z);
        if (ballRb != null)
        {
            ballRb.isKinematic = false;
            ballRb.position = p;
            ballRb.linearVelocity = Vector3.zero;
        }
        ball.position = p;
        Physics.SyncTransforms();
        TrailRenderer tr = ball.GetComponent<TrailRenderer>();
        if (tr != null) tr.Clear();
    }

    void Respawn(string msg)
    {
        PlaceBall(ZoneStart(step));
        enteredZone = true;
        ShowBanner(msg, new Color(1f, 0.7f, 0.2f));
    }

    // ==================== 主迴圈 ====================
    void Update()
    {
        if (!built || gm == null || ctrl == null || leaving) return;

        // R：重新開始 (GameManager 會重載場景)；在「重新開始」步驟按 R 視為完成，重載後直接進入完成畫面
        if (Input.GetKeyDown(KeyCode.R))
        {
            resumeStep = (step == STEP_RESTART) ? STEP_COUNT : step;
            return;
        }

        if (finale)
        {
            UpdateFinale();
            return;
        }

        // 重玩模式：Tab 跳過教學
        if (isReplay && Input.GetKeyDown(KeyCode.Tab))
        {
            SkipTutorial();
            return;
        }

        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.unscaledDeltaTime;
            if (bannerTimer <= 0f && txtBanner != null) txtBanner.gameObject.SetActive(false);
        }

        // 暫停中：只記錄「已看到暫停」
        if (Time.timeScale == 0f)
        {
            if (step == STEP_PAUSE) pausedSeen = true;
            return;
        }

        stepTimer += Time.deltaTime;

        // 柔性掉落：在 Die (y < -5) 觸發之前，先送回目前步驟檢查點
        if (ball.position.y < -2.5f)
        {
            Respawn("掉下去了？沒關係，回到檢查點再試一次");
            return;
        }

        // 進入本步驟區時，把小球橫向歸中，讓每一步都有一致的起點
        if (!enteredZone && ball.position.z >= ZoneStart(step))
        {
            enteredZone = true;
            Vector3 p = ballRb != null ? ballRb.position : ball.position;
            p.x = 0f;
            if (ballRb != null) ballRb.position = p;
            ball.position = p;
        }

        // 跑出本步驟區仍未完成 → 回到本區起點，從容再試
        if (ball.position.z > ZoneStart(step + 1))
        {
            Respawn("再試一次：照著畫面提示操作");
            return;
        }

        if (step == STEP_HAZARD) CheckHazardHit();

        if (CheckStepDone()) CompleteStep();
    }

    void CheckHazardHit()
    {
        Vector3 p = ball.position;
        if (Mathf.Abs(p.z - hazardPos.z) < 1.2f && Mathf.Abs(p.x - hazardPos.x) < 1.8f && p.y < FLOOR_TOP + 1.5f)
        {
            Respawn("撞到障礙了！沒關係，換個方式再試一次");
        }
    }

    bool CheckStepDone()
    {
        Vector3 p = ball.position;
        switch (step)
        {
            case STEP_INTRO:
                return stepTimer >= 5f;
            case STEP_LEFT:
                return enteredZone && p.x < -2.2f;
            case STEP_RIGHT:
                return enteredZone && p.x > 2.2f;
            case STEP_JUMP:
                return enteredZone && ballRb != null && ballRb.linearVelocity.y > 4f;
            case STEP_GEM:
                if (!enteredZone) return false;
                foreach (var g in stepGems)
                    if (g == null || !g.activeSelf) return true;
                return false;
            case STEP_HAZARD:
                return enteredZone && p.z > hazardPos.z + 1.8f;
            case STEP_PAD:
                // IsJumpPadAirborne 會在發射後下一幀的 CheckGrounded 被清掉 (球尚在地面感測範圍內)，輪詢會漏接；
                // 改比對「最後一次彈跳墊發射時間」是否比進入教學時更新 (該值只在 Launch 時寫入，不會被重置)
                return enteredZone && ctrl.LastJumpPadLaunchTime > padLaunchBaseline;
            case STEP_PAUSE:
                return pausedSeen && Time.timeScale > 0f;
            default:
                return false; // STEP_RESTART 由 R 鍵處理
        }
    }

    void CompleteStep()
    {
        AudioManager.Instance?.PlayGem();
        step++;
        resumeStep = step;
        stepTimer = 0f;
        pausedSeen = false;
        enteredZone = ball.position.z >= ZoneStart(Mathf.Min(step, STEP_COUNT));

        if (step >= STEP_COUNT)
        {
            BeginFinale();
            return;
        }
        ShowBanner("✔ 做得好！", new Color(0.4f, 1f, 0.5f));
        RefreshPromptUI();
    }

    // ==================== 完成畫面 ====================
    void BeginFinale()
    {
        finale = true;
        PlayerPrefs.SetInt(PrefCompleted, 1);
        PlayerPrefs.Save();
        resumeStep = STEP_COUNT;

        if (ballRb != null)
        {
            ballRb.linearVelocity = Vector3.zero;
            ballRb.isKinematic = true;
        }
        if (promptPanel != null) promptPanel.SetActive(false);
        if (txtSkip != null) txtSkip.gameObject.SetActive(false);
        if (txtBanner != null) txtBanner.gameObject.SetActive(false);
        if (finalePanel != null)
        {
            finalePanel.transform.SetAsLastSibling();
            finalePanel.SetActive(true);
        }
        finaleSel = 0;
        UpdateFinaleHighlight();
        gm.SetCursorLocked(false);
        MarkGameManagerFinished();
        AudioManager.Instance?.PlayVictory();
    }

    /// <summary>
    /// GameManager.Update 在「未暫停/未結算」時偵測到滑鼠按下會重新鎖定游標，導致完成畫面按鈕無法點擊；
    /// 這裡讓它進入「已結算」狀態。LateUpdate 每幀強制解鎖作為備援。
    /// </summary>
    void MarkGameManagerFinished()
    {
        if (gm != null) gm.MarkTutorialFinished();
    }

    void LateUpdate()
    {
        // 備援：完成畫面期間確保游標可見且未鎖定 (晚於 GameManager.Update 執行)
        if (finale && !leaving)
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }
    }

    void UpdateFinale()
    {
        if (Time.timeScale == 0f) return; // 暫停選單開啟時，不處理完成畫面輸入

        float dt = Time.unscaledDeltaTime;
        if (finaleNavCooldown > 0f) finaleNavCooldown -= dt;

        bool up = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);

        if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();
        if (arduino != null)
        {
            bool joyBtn = arduino.isButtonPressed;
            if (joyBtn && !lastJoyBtn) confirm = true;
            lastJoyBtn = joyBtn;
            if (finaleNavCooldown <= 0f)
            {
                if (arduino.joystick.y < 320f) up = true;
                else if (arduino.joystick.y > 680f) down = true;
            }
        }

        if (up || down)
        {
            if (finaleNavCooldown <= 0f || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                finaleSel = (finaleSel + 1) % 2;
                finaleNavCooldown = 0.22f;
                UpdateFinaleHighlight();
            }
        }

        if (confirm)
        {
            if (finaleSel == 0) GoToLevel1(); else GoToMenu();
        }
    }

    void UpdateFinaleHighlight()
    {
        for (int i = 0; i < finaleBtnImages.Length; i++)
        {
            if (finaleBtnImages[i] != null)
            {
                finaleBtnImages[i].color = (i == finaleSel) ? BtnHighlight : BtnNormal;
                finaleBtnImages[i].rectTransform.localScale = (i == finaleSel) ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
            }
        }
    }

    // ==================== 離開教學 ====================
    public void GoToLevel1()
    {
        if (leaving) return;
        leaving = true;
        resumeStep = -1;
        PlayerPrefs.SetInt(PrefCompleted, 1);
        PlayerPrefs.SetInt(PrefMode, 0);
        PlayerPrefs.SetInt("SelectedLevel", 1);
        PlayerPrefs.SetInt("IsEndlessMode", 0);
        PlayerPrefs.SetInt("IsSkyTowerMode", 0);
        PlayerPrefs.SetInt("IsBuilderMode", 0);
        PlayerPrefs.Save();
        Time.timeScale = 1.0f;
        if (gm != null) gm.SetCursorLocked(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMenu()
    {
        if (leaving) return;
        leaving = true;
        resumeStep = -1;
        PlayerPrefs.SetInt(PrefCompleted, 1);
        PlayerPrefs.SetInt(PrefMode, 0);
        PlayerPrefs.Save();
        if (gm != null) gm.ReturnToMainMenu();
        else { Time.timeScale = 1.0f; SceneManager.LoadScene("MainMenuScene"); }
    }

    /// <summary>跳過教學：標記為已完成；重玩者回主選單，首次玩家直接前往第 1 關。</summary>
    public void SkipTutorial()
    {
        if (isReplay) GoToMenu(); else GoToLevel1();
    }

    // ==================== UI ====================
    void BuildUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject cObj = new GameObject("TutorialCanvas");
            canvas = cObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler cs = cObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cObj.AddComponent<GraphicRaycaster>();
        }
        canvasTr = canvas.transform;

        // 提示面板 (上方置中)
        promptPanel = new GameObject("TutorialPrompt");
        promptPanel.transform.SetParent(canvasTr, false);
        Image bg = promptPanel.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.06f, 0.12f, 0.82f);
        bg.raycastTarget = false;
        RectTransform prt = promptPanel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 1f);
        prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0f, -110f);
        prt.sizeDelta = new Vector2(1500f, 230f);

        txtTag = MakeText(promptPanel.transform, "Tag", "", 22, new Color(0.6f, 0.8f, 1f), new Vector2(0, 98), new Vector2(1400, 34), FontStyle.Normal);
        txtTitle = MakeText(promptPanel.transform, "Title", "", 40, new Color(0f, 1f, 0.9f), new Vector2(0, 55), new Vector2(1400, 56), FontStyle.Bold);
        txtBody = MakeText(promptPanel.transform, "Body", "", 26, Color.white, new Vector2(0, -8), new Vector2(1400, 80), FontStyle.Normal);
        txtHint = MakeText(promptPanel.transform, "Hint", "", 22, new Color(1f, 0.92f, 0.3f), new Vector2(0, -80), new Vector2(1400, 36), FontStyle.Normal);
        txtBody.horizontalOverflow = HorizontalWrapMode.Wrap;

        // 中央橫幅訊息 (完成 / 重試)
        txtBanner = MakeText(canvasTr, "TutorialBanner", "", 54, new Color(0.4f, 1f, 0.5f), new Vector2(0, -40), new Vector2(1500, 80), FontStyle.Bold);
        txtBanner.gameObject.SetActive(false);

        // 重玩模式：右下角跳過提示
        txtSkip = MakeText(canvasTr, "TutorialSkipHint", "[Tab] 跳過教學 (SKIP)", 26, new Color(1f, 0.92f, 0.3f), new Vector2(-30, 30), new Vector2(520, 44), FontStyle.Bold);
        RectTransform srt = txtSkip.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(1f, 0f);
        srt.anchorMax = new Vector2(1f, 0f);
        srt.pivot = new Vector2(1f, 0f);
        srt.anchoredPosition = new Vector2(-30f, 30f);
        txtSkip.alignment = TextAnchor.MiddleRight;
        txtSkip.gameObject.SetActive(isReplay);

        BuildFinalePanel();
    }

    void BuildFinalePanel()
    {
        finalePanel = new GameObject("TutorialFinalePanel");
        finalePanel.transform.SetParent(canvasTr, false);
        Image bg = finalePanel.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.06f, 0.12f, 0.9f);
        bg.raycastTarget = true;
        RectTransform rt = finalePanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1300f, 800f);

        MakeText(finalePanel.transform, "Title", "★ 恭喜完成新手教學！ ★", 56, new Color(0f, 1f, 0.9f), new Vector2(0, 320), new Vector2(1200, 80), FontStyle.Bold);
        MakeText(finalePanel.transform, "Sub",
            "你已學會：左右移動、跳躍、收集寶石、閃避障礙、彈跳墊、暫停與重新開始。\n準備好了就出發挑戰第一關吧！",
            28, Color.white, new Vector2(0, 225), new Vector2(1200, 100), FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Wrap;

        // 通天天梯操作小卡
        GameObject card = new GameObject("SkyTowerCard");
        card.transform.SetParent(finalePanel.transform, false);
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.08f, 0.14f, 0.26f, 0.95f);
        cardBg.raycastTarget = false;
        RectTransform crt = card.GetComponent<RectTransform>();
        crt.anchoredPosition = new Vector2(0, 40);
        crt.sizeDelta = new Vector2(1150f, 230f);
        MakeText(card.transform, "CardTitle", "▲ 小提示：通天天梯 (SKY TOWER) 操作", 30, new Color(1f, 0.85f, 0.2f), new Vector2(0, 80), new Vector2(1100, 44), FontStyle.Bold);
        Text cardBody = MakeText(card.transform, "CardBody",
            "鍵盤：W / A / S / D 前後左右移動　|　滑鼠：轉動視角\nQ / E：輔助轉向　|　Space：跳躍　|　ESC：暫停\nArduino：前後左右傾斜移動，搖桿轉視角，手掌靠近感測器跳躍",
            24, Color.white, new Vector2(0, -15), new Vector2(1100, 130), FontStyle.Normal);
        cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;

        finaleBtnImages[0] = MakeButton(finalePanel.transform, "Btn_TutorialLevel1", "⏩ 前往第一關 (LEVEL 1)", new Vector2(0, -150), GoToLevel1, 0);
        finaleBtnImages[1] = MakeButton(finalePanel.transform, "Btn_TutorialMenu", "⌂ 回到主選單 (MAIN MENU)", new Vector2(0, -235), GoToMenu, 1);

        MakeText(finalePanel.transform, "Hint", "↑ ↓ / W S 或搖桿選擇　|　Enter / Space / 搖桿按鍵 確認", 22, new Color(0.6f, 0.8f, 1f), new Vector2(0, -330), new Vector2(1200, 36), FontStyle.Normal);

        finalePanel.SetActive(false);
    }

    Image MakeButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction action, int index)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = BtnNormal;
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(520f, 66f);
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        Text t = MakeText(btnObj.transform, "Text", label, 28, Color.white, Vector2.zero, new Vector2(520, 66), FontStyle.Bold);
        RectTransform trt = t.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;
        return img;
    }

    Text MakeText(Transform parent, string name, string content, int fontSize, Color color, Vector2 pos, Vector2 size, FontStyle style)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = content;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(2, -2);
        return t;
    }

    void RefreshPromptUI()
    {
        if (promptPanel == null || step < 0 || step >= steps.Length) return;
        promptPanel.SetActive(true);
        StepInfo s = steps[step];
        txtTag.text = (step == STEP_INTRO) ? "新手教學 TUTORIAL" : $"新手教學 TUTORIAL　{step} / 8";
        txtTitle.text = s.title;
        txtBody.text = s.body;
        txtHint.text = s.hint;
    }

    void ShowBanner(string msg, Color color)
    {
        if (txtBanner == null) return;
        txtBanner.text = msg;
        txtBanner.color = color;
        txtBanner.gameObject.SetActive(true);
        txtBanner.transform.SetAsLastSibling();
        bannerTimer = 1.6f;
    }

    /// <summary>在暫停選單加入「跳過教學」按鈕 (複製「回主選單」按鈕樣式)，GameManager 的搖桿導航會一併收錄。</summary>
    void AddSkipButtonToPausePanel()
    {
        if (gm == null || gm.pausePanel == null) return;
        Transform existing = gm.pausePanel.transform.Find("Btn_SkipTutorial");
        if (existing != null) return;

        Transform src = gm.pausePanel.transform.Find("Btn_MainMenu");
        if (src == null) return;

        GameObject clone = Instantiate(src.gameObject, gm.pausePanel.transform);
        clone.name = "Btn_SkipTutorial";
        RectTransform rt = clone.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = new Vector2(0, -235);

        Text txt = clone.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.text = "» 跳過教學 (SKIP TUTORIAL)";
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
        }
        Button btn = clone.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(SkipTutorial);
        }
        clone.SetActive(true);
    }
}
