using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class RollingSkyGameManager : MonoBehaviour
{
    [Header("Level Progress")]
    public Transform ball;
    public float trackLength = 250.0f;
    private float startZ = 0f;

    [Header("HUD References")]
    public Slider progressBar;
    public Text progressText;
    public Text gemText;
    public Text hardwareStatusText;

    [Header("Popup Panels")]
    public GameObject pausePanel;
    public GameObject gameOverPanel;
    public Text gameOverReasonText;
    public Text gameOverProgressText;
    public GameObject victoryPanel;
    public GameObject inGameSettingsPanel;

    [Header("In-Game Settings UI References")]
    private GameObject inGameDisplayPanel;
    private GameObject inGameKeyboardPanel;
    private GameObject inGameControllerPanel;
    private GameObject inGameAudioPanel;

    private Button tabDisplayBtn;
    private Button tabKeyboardBtn;
    private Button tabControllerBtn;
    private Button tabAudioBtn;

    private Text txtKeyLeft;
    private Text txtKeyRight;
    private Text txtKeyJump;
    private Slider sliderKeyboardSpeed;
    private Text txtKeyboardSpeed;
    private Slider sliderInGameMouseSens;
    private Text txtInGameMouseSens;

    private Slider sliderTiltSensitivity;
    private Text txtTiltSensitivity;
    private Slider sliderTiltDeadzone;
    private Text txtTiltDeadzone;
    private Slider sliderJumpDistance;
    private Text txtJumpDistance;
    private Slider sliderCamSensitivity;
    private Text txtCamSensitivity;
    private Text inGameHardwareStatusText;

    private Text txtCurrentBgm;
    private Button btnBgmTrack0;
    private Button btnBgmTrack1;
    private Button btnBgmTrack2;
    private Text txtDisplayStatus;

    private KeyCode currentKeyLeft = KeyCode.A;
    private KeyCode currentKeyRight = KeyCode.D;
    private KeyCode currentKeyJump = KeyCode.Space;
    private int currentBgmIndex = 0;
    private bool isRebinding = false;

    private Slider sliderBgmVolume;
    private Text txtBgmVolume;
    private Slider sliderSfxVolume;
    private Text txtSfxVolume;
    private Text txtFullscreenState;

    private int collectedGems = 0;
    private int totalGemsInLevel = 0;
    private bool isGameOver = false;
    private bool isVictory = false;
    private bool isPaused = false;
    private bool isEndlessMode = false;
    private bool isBuilderMode = false;

    private ArduinoReceiver arduino;

    void Awake()
    {
        // 確保載入關卡時時間縮放正常
        Time.timeScale = 1.0f;
    }

    void OnDisable()
    {
        // 離開場景或組件禁用時，恢復游標狀態
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        isBuilderMode = (PlayerPrefs.GetInt("IsBuilderMode", 0) == 1);
        EnsureAllUI();
        BindAllButtonsRuntime();

        if (isBuilderMode)
        {
            SetCursorLocked(false);
            // 進入自訂建造模式：隱藏常規跑酷 HUD 與預設小球
            if (ball == null)
            {
                RollingSkyBallController bc = FindAnyObjectByType<RollingSkyBallController>();
                if (bc != null) ball = bc.transform;
            }
            if (ball != null) ball.gameObject.SetActive(false);

            if (progressBar != null) progressBar.gameObject.SetActive(false);
            if (progressText != null) progressText.gameObject.SetActive(false);
            if (gemText != null) gemText.gameObject.SetActive(false);
            if (hardwareStatusText != null) hardwareStatusText.gameObject.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);

            // 確保 TrackBuilderManager 啟動
            if (FindAnyObjectByType<TrackBuilderManager>() == null)
            {
                GameObject builderObj = new GameObject("TrackBuilderManager");
                builderObj.AddComponent<TrackBuilderManager>();
            }
            return;
        }

        isEndlessMode = (PlayerPrefs.GetInt("IsEndlessMode", 0) == 1);

        if (ball == null)
        {
            RollingSkyBallController bc = FindAnyObjectByType<RollingSkyBallController>();
            if (bc != null) ball = bc.transform;
        }

        if (ball != null) startZ = ball.position.z;

        arduino = FindAnyObjectByType<ArduinoReceiver>();
        totalGemsInLevel = FindObjectsByType<GemCollectible>(FindObjectsSortMode.None).Length;

        // 【Runtime 自癒架構】：全自動補全 EventSystem、PausePanel 與 TrackGenerator
        EnsureAllUI();
        EnsureTrackGenerator();

        // 綁定所有面板按鈕事件
        BindAllButtonsRuntime();
        EnsureAllInGameButtonsTextSafe();

        // 無盡模式下隱藏左上角寶石文字（徹底消除孤立數字 0 裁切問題）
        if (gemText != null && isEndlessMode)
        {
            gemText.gameObject.SetActive(false);
        }

        UpdateGemUI();
        StartCoroutine(DelayedRefreshTotalGems());

        // 鎖定並隱藏滑鼠游標，防止游標滑出視窗並提升遊玩沉浸感
        SetCursorLocked(true);
    }

    /// <summary>
    /// 全動態刷新當前關卡/通天塔寶石總數（場景中活躍寶石 + 已採集數量）
    /// 徹底根除動態生成尚未實例化導致 8 / 7 等超額分子/分母異常問題
    /// </summary>
    public void RefreshTotalGems()
    {
        GemCollectible[] allGems = FindObjectsByType<GemCollectible>(FindObjectsSortMode.None);
        int activeCount = 0;
        foreach (var g in allGems)
        {
            if (g != null && g.gameObject.activeInHierarchy) activeCount++;
        }
        totalGemsInLevel = activeCount + collectedGems;
        if (totalGemsInLevel < collectedGems) totalGemsInLevel = collectedGems;
        UpdateGemUI();
    }

    private System.Collections.IEnumerator DelayedRefreshTotalGems()
    {
        yield return null; // 等待一幀讓所有動態地圖生成器 (SkyTower / TrackGenerator) 完成生成
        RefreshTotalGems();
    }

    void EnsureTrackGenerator()
    {
        if (FindAnyObjectByType<RollingSkyTrackGenerator>() == null)
        {
            GameObject genObj = new GameObject("RollingSkyTrackGenerator");
            genObj.AddComponent<RollingSkyTrackGenerator>();
        }
    }

    public void ResetLevelStats()
    {
        collectedGems = 0;
        totalGemsInLevel = FindObjectsByType<GemCollectible>(FindObjectsSortMode.None).Length;
        if (ball != null) startZ = ball.position.z;
        UpdateGemUI();
        StartCoroutine(DelayedRefreshTotalGems());

        // 鎖定並隱藏滑鼠游標，防止游標滑出視窗並提升遊玩沉浸感
        SetCursorLocked(true);
    }

    void Update()
    {
        // 遊戲內各選單面板的實體搖桿/鍵盤導航與 Accordion 推擠更新 (使用 unscaledDeltaTime，暫停中亦流暢運作)
        HandleInGameNavigationInput();
        UpdateInGameAccordionLayout();

        // 特殊除錯按鍵：自由增加寶石數量 (G: +50, F1: +100, F2: +500)
        if (Input.GetKeyDown(KeyCode.G)) AddDebugGems(50);
        if (Input.GetKeyDown(KeyCode.F1)) AddDebugGems(100);
        if (Input.GetKeyDown(KeyCode.F2)) AddDebugGems(500);

        if (isBuilderMode) return; // 建造模式由 TrackBuilderManager 掌管輸入

        // 正常遊戲狀態下若游標未鎖定，玩家點擊視窗時自動重新鎖定游標
        if (!isPaused && !isGameOver && !isVictory)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
                {
                    SetCursorLocked(true);
                }
            }
        }

        // 快速重開鍵
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
            return;
        }

        // ESC 鍵處理：若設定面板開著則返回暫停面板，若已結算則回主選單，遊戲中則切換暫停面板
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isRebinding) return; // 正在重新綁定按鍵時不響應 ESC

            if (inGameSettingsPanel != null && inGameSettingsPanel.activeSelf)
            {
                CloseInGameSettings();
                return;
            }

            if (isGameOver || isVictory)
            {
                ReturnToMainMenu();
            }
            else
            {
                TogglePause();
            }
            return;
        }

        // 若開啟控制器分頁，實時刷新感測器數值以供玩家校準
        if (inGameSettingsPanel != null && inGameSettingsPanel.activeSelf)
        {
            if (inGameControllerPanel != null && inGameControllerPanel.activeSelf)
            {
                UpdateInGameHardwareReadout();
            }
        }

        if (isGameOver || isVictory || isPaused) return;

        UpdateProgress();
        UpdateHardwareHUD();
    }

    public void AddDebugGems(int count)
    {
        int totalGems = PlayerPrefs.GetInt("TotalGems", 0) + count;
        PlayerPrefs.SetInt("TotalGems", totalGems);
        PlayerPrefs.Save();
        collectedGems += count;
        UpdateGemUI();
        Debug.Log($"💎 [Debug] 寶石已增加 {count}！當前累計總寶石: {totalGems}");
    }

    // --- 游標鎖定與隱藏控制 ---
    public void SetCursorLocked(bool locked)
    {
        if (isBuilderMode)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            if (!isPaused && !isGameOver && !isVictory && !isBuilderMode)
            {
                SetCursorLocked(true);
            }
        }
    }

    // --- 暫停管理 (含自動容錯建立與即時設定支援) ---
    public void TogglePause()
    {
        if (isGameOver || isVictory) return;

        if (inGameSettingsPanel != null && inGameSettingsPanel.activeSelf)
        {
            CloseInGameSettings();
            return;
        }

        if (pausePanel == null)
        {
            EnsureAllUI();
            BindAllButtonsRuntime();
        }

        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1.0f;

        if (pausePanel != null)
        {
            pausePanel.transform.SetAsLastSibling();
            pausePanel.SetActive(isPaused);
            if (isPaused)
            {
                InitPauseMenuButtons();
                activeInGameNavCol = 0;
                activeInGameNavRow = 0;
                isInGameNavActive = true;
                EnsureAllInGameButtonsTextSafe();
            }
        }

        if (!isPaused && inGameSettingsPanel != null)
        {
            inGameSettingsPanel.SetActive(false);
        }

        // 暫停時解除鎖定並顯示游標以供 UI 操作；繼續遊戲時隱藏並鎖定游標防滑出視窗
        SetCursorLocked(!isPaused);

        Debug.Log(isPaused ? "[GameManager] 遊戲已暫停 (PAUSED)" : "[GameManager] 遊戲已繼續 (RESUMED)");
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1.0f;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (inGameSettingsPanel != null) inGameSettingsPanel.SetActive(false);
        SetCursorLocked(true);
    }

    // --- 場景導航 ---
    public void RestartLevel()
    {
        Time.timeScale = 1.0f;
        SetCursorLocked(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1.0f;
        SetCursorLocked(false);
        SceneManager.LoadScene("MainMenuScene");
    }

    // --- Runtime UI 自癒與防禦性初始化 ---
    void EnsureAllUI()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        if (AudioManager.Instance == null && FindAnyObjectByType<AudioManager>() == null)
        {
            GameObject audioObj = new GameObject("AudioManager");
            audioObj.AddComponent<AudioManager>();
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject cObj = new GameObject("Canvas");
            canvas = cObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler cs = cObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (pausePanel == null)
        {
            Transform pTransform = canvas.transform.Find("PausePanel");
            if (pTransform != null)
            {
                pausePanel = pTransform.gameObject;
            }
            else
            {
                pausePanel = BuildDynamicPausePanel(canvas.transform, defaultFont);
            }
        }

        if (inGameSettingsPanel == null)
        {
            Transform sTransform = canvas.transform.Find("InGameSettingsPanel");
            if (sTransform != null)
            {
                inGameSettingsPanel = sTransform.gameObject;
            }
            else
            {
                inGameSettingsPanel = BuildDynamicInGameSettingsPanel(canvas.transform, defaultFont);
            }
        }

        if (pausePanel != null)
        {
            EnsurePausePanelButtons(pausePanel, defaultFont);
            pausePanel.transform.SetAsLastSibling();
            pausePanel.SetActive(false);
        }

        EnsureGameOverPanelButtons(gameOverPanel, defaultFont);
        EnsureVictoryPanelButtons(victoryPanel, defaultFont);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (inGameSettingsPanel != null) inGameSettingsPanel.SetActive(false);
    }

    GameObject BuildDynamicPausePanel(Transform canvasTransform, Font font)
    {
        GameObject panel = new GameObject("PausePanel");
        panel.transform.SetParent(canvasTransform, false);

        Image img = panel.AddComponent<Image>();
        img.color = new Color(0.04f, 0.06f, 0.12f, 0.92f);
        img.raycastTarget = true;

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        CreateText(panel.transform, "Title", "遊戲暫停 PAUSED", 56, new Color(0f, 1f, 0.9f), new Vector2(0, 160), new Vector2(800, 80), font, FontStyle.Bold);
        CreateText(panel.transform, "Hint", "💡 提示：再次按下 [ESC] 鍵可直接繼續遊戲", 24, new Color(0.4f, 0.9f, 1f), new Vector2(0, 105), new Vector2(800, 50), font, FontStyle.Normal);

        CreateStyledButton(panel.transform, "Btn_Resume", "▶ 繼續遊戲 (RESUME)", new Vector2(0, 45), new Vector2(440, 58), 28, font);
        CreateStyledButton(panel.transform, "Btn_Settings", "⚙ 遊戲設定 (SETTINGS)", new Vector2(0, -25), new Vector2(440, 58), 26, font);
        CreateStyledButton(panel.transform, "Btn_Restart", "🔄 重新挑戰 (RESTART)", new Vector2(0, -95), new Vector2(440, 58), 28, font);
        CreateStyledButton(panel.transform, "Btn_MainMenu", "🏠 回到主選單 (MAIN MENU)", new Vector2(0, -165), new Vector2(440, 58), 28, font);

        return panel;
    }

    GameObject BuildDynamicInGameSettingsPanel(Transform canvasTransform, Font font)
    {
        GameObject panel = new GameObject("InGameSettingsPanel");
        panel.transform.SetParent(canvasTransform, false);

        Image img = panel.AddComponent<Image>();
        img.color = new Color(0.03f, 0.06f, 0.12f, 0.96f);
        img.raycastTarget = true;

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // 頂部標題
        CreateText(panel.transform, "Title", "⚙ 遊戲設定 (SYSTEM SETTINGS)", 44, new Color(0f, 1f, 0.9f), new Vector2(0, 370), new Vector2(900, 60), font, FontStyle.Bold);

        // 頂部 4 大分頁按鈕 (Tabs)
        GameObject bTabDisp = CreateStyledButton(panel.transform, "Btn_TabDisplay", "【 畫面 DISPLAY 】", new Vector2(-375, 300), new Vector2(230, 52), 20, font);
        tabDisplayBtn = bTabDisp.GetComponent<Button>();
        tabDisplayBtn.onClick.AddListener(() => SwitchInGameSettingsTab(0));

        GameObject bTabKey = CreateStyledButton(panel.transform, "Btn_TabKeyboard", "【 鍵盤 / 滑鼠 KEYBOARD 】", new Vector2(-125, 300), new Vector2(230, 52), 19, font);
        tabKeyboardBtn = bTabKey.GetComponent<Button>();
        tabKeyboardBtn.onClick.AddListener(() => SwitchInGameSettingsTab(1));

        GameObject bTabCtrl = CreateStyledButton(panel.transform, "Btn_TabController", "【 控制器 CONTROLLER 】", new Vector2(125, 300), new Vector2(230, 52), 20, font);
        tabControllerBtn = bTabCtrl.GetComponent<Button>();
        tabControllerBtn.onClick.AddListener(() => SwitchInGameSettingsTab(2));

        GameObject bTabAudio = CreateStyledButton(panel.transform, "Btn_TabAudio", "【 音訊 AUDIO 】", new Vector2(375, 300), new Vector2(230, 52), 20, font);
        tabAudioBtn = bTabAudio.GetComponent<Button>();
        tabAudioBtn.onClick.AddListener(() => SwitchInGameSettingsTab(3));

        // ---------- 分頁 0: 畫面設定 (Display Panel) ----------
        inGameDisplayPanel = CreateSubPanel(panel.transform, "InGameDisplayPanel");
        CreateText(inGameDisplayPanel.transform, "Lbl_ScreenMode", "顯示模式 (SCREEN MODE):", 24, Color.white, new Vector2(0, 140), new Vector2(800, 40), font, FontStyle.Bold);

        GameObject btnFsObj = CreateStyledButton(inGameDisplayPanel.transform, "Btn_Fullscreen", "全螢幕模式 (FULLSCREEN)", new Vector2(-170, 80), new Vector2(300, 52), 22, font);
        btnFsObj.GetComponent<Button>().onClick.AddListener(() => SetInGameFullscreen(true));

        GameObject btnWinObj = CreateStyledButton(inGameDisplayPanel.transform, "Btn_Windowed", "視窗化模式 (WINDOWED)", new Vector2(170, 80), new Vector2(300, 52), 22, font);
        btnWinObj.GetComponent<Button>().onClick.AddListener(() => SetInGameFullscreen(false));

        CreateText(inGameDisplayPanel.transform, "Lbl_Resolution", "螢幕解析度 (RESOLUTION):", 24, Color.white, new Vector2(0, 0), new Vector2(800, 40), font, FontStyle.Bold);

        GameObject b1080 = CreateStyledButton(inGameDisplayPanel.transform, "Btn_Res1080", "1920 × 1080", new Vector2(-330, -55), new Vector2(200, 48), 20, font);
        b1080.GetComponent<Button>().onClick.AddListener(() => SetInGameResolution(1920, 1080));

        GameObject b900 = CreateStyledButton(inGameDisplayPanel.transform, "Btn_Res900", "1600 × 900", new Vector2(-110, -55), new Vector2(200, 48), 20, font);
        b900.GetComponent<Button>().onClick.AddListener(() => SetInGameResolution(1600, 900));

        GameObject b768 = CreateStyledButton(inGameDisplayPanel.transform, "Btn_Res768", "1366 × 768", new Vector2(110, -55), new Vector2(200, 48), 20, font);
        b768.GetComponent<Button>().onClick.AddListener(() => SetInGameResolution(1366, 768));

        GameObject b720 = CreateStyledButton(inGameDisplayPanel.transform, "Btn_Res720", "1280 × 720", new Vector2(330, -55), new Vector2(200, 48), 20, font);
        b720.GetComponent<Button>().onClick.AddListener(() => SetInGameResolution(1280, 720));

        txtDisplayStatus = CreateText(inGameDisplayPanel.transform, "Txt_DisplayStatus", $"★ 當前狀態: {(Screen.fullScreen ? "全螢幕" : "視窗化")} ({Screen.width} × {Screen.height})", 20, Color.yellow, new Vector2(0, -135), new Vector2(800, 40), font, FontStyle.Normal);

        // ---------- 分頁 1: 鍵盤按鍵設定 (Keyboard Panel) ----------
        inGameKeyboardPanel = CreateSubPanel(panel.transform, "InGameKeyboardPanel");

        CreateText(inGameKeyboardPanel.transform, "Lbl_Left", "往左移動 (MOVE LEFT):", 26, Color.white, new Vector2(-200, 150), new Vector2(340, 42), font, FontStyle.Bold);
        GameObject bKeyLeft = CreateStyledButton(inGameKeyboardPanel.transform, "Btn_KeyLeft", "[ A ]", new Vector2(160, 150), new Vector2(220, 48), 24, font);
        txtKeyLeft = bKeyLeft.GetComponentInChildren<Text>();
        bKeyLeft.GetComponent<Button>().onClick.AddListener(() => StartInGameRebind("Key_Left"));

        CreateText(inGameKeyboardPanel.transform, "Lbl_Right", "往右移動 (MOVE RIGHT):", 26, Color.white, new Vector2(-200, 85), new Vector2(340, 42), font, FontStyle.Bold);
        GameObject bKeyRight = CreateStyledButton(inGameKeyboardPanel.transform, "Btn_KeyRight", "[ D ]", new Vector2(160, 85), new Vector2(220, 48), 24, font);
        txtKeyRight = bKeyRight.GetComponentInChildren<Text>();
        bKeyRight.GetComponent<Button>().onClick.AddListener(() => StartInGameRebind("Key_Right"));

        CreateText(inGameKeyboardPanel.transform, "Lbl_Jump", "跳躍動作 (JUMP):", 26, Color.white, new Vector2(-200, 20), new Vector2(340, 42), font, FontStyle.Bold);
        GameObject bKeyJump = CreateStyledButton(inGameKeyboardPanel.transform, "Btn_KeyJump", "[ Space ]", new Vector2(160, 20), new Vector2(220, 48), 24, font);
        txtKeyJump = bKeyJump.GetComponentInChildren<Text>();
        bKeyJump.GetComponent<Button>().onClick.AddListener(() => StartInGameRebind("Key_Jump"));

        CreateText(inGameKeyboardPanel.transform, "Lbl_Speed", "鍵盤左右速度靈敏度:", 26, Color.white, new Vector2(-200, -45), new Vector2(340, 42), font, FontStyle.Bold);
        sliderKeyboardSpeed = CreateSlider(inGameKeyboardPanel.transform, "Slider_KeyboardSpeed", new Vector2(100, -45), new Vector2(240, 26), 5.0f, 15.0f, PlayerPrefs.GetFloat("Keyboard_Speed", 9.0f));
        txtKeyboardSpeed = CreateText(inGameKeyboardPanel.transform, "Txt_KeyboardSpeedVal", $"{sliderKeyboardSpeed.value:F1}", 26, Color.cyan, new Vector2(270, -45), new Vector2(80, 42), font, FontStyle.Bold);
        sliderKeyboardSpeed.onValueChanged.AddListener(val => {
            if (txtKeyboardSpeed != null) txtKeyboardSpeed.text = $"{val:F1}";
            PlayerPrefs.SetFloat("Keyboard_Speed", val);
            FindAnyObjectByType<RollingSkyBallController>()?.ReloadSettings();
        });

        CreateText(inGameKeyboardPanel.transform, "Lbl_MouseSens", "滑鼠視角靈敏度 (LOOK):", 26, Color.white, new Vector2(-200, -110), new Vector2(340, 42), font, FontStyle.Bold);
        sliderInGameMouseSens = CreateSlider(inGameKeyboardPanel.transform, "Slider_MouseSensitivity", new Vector2(100, -110), new Vector2(240, 26), 0.5f, 5.0f, PlayerPrefs.GetFloat("Camera_Sensitivity", 2.0f));
        txtInGameMouseSens = CreateText(inGameKeyboardPanel.transform, "Txt_MouseSensVal", $"{sliderInGameMouseSens.value:F1}x", 26, Color.cyan, new Vector2(270, -110), new Vector2(80, 42), font, FontStyle.Bold);
        sliderInGameMouseSens.onValueChanged.AddListener(val => {
            if (txtInGameMouseSens != null) txtInGameMouseSens.text = $"{val:F1}x";
            PlayerPrefs.SetFloat("Camera_Sensitivity", val);
            if (sliderCamSensitivity != null) sliderCamSensitivity.value = val;
            FindAnyObjectByType<RollingSkyCamera>()?.ReloadSettings();
        });

        CreateText(inGameKeyboardPanel.transform, "Hint_Rebind", "💡 點擊上方按鈕後，按下實體鍵盤任一鍵即可重新綁定", 20, new Color(0.3f, 0.85f, 1f), new Vector2(0, -170), new Vector2(800, 35), font, FontStyle.Normal);

        // ---------- 分頁 2: 控制器設定 (Controller Panel) ----------
        inGameControllerPanel = CreateSubPanel(panel.transform, "InGameControllerPanel");

        CreateText(inGameControllerPanel.transform, "Lbl_Tilt", "陀螺儀轉向靈敏度:", 26, Color.white, new Vector2(-200, 140), new Vector2(340, 45), font, FontStyle.Bold);
        sliderTiltSensitivity = CreateSlider(inGameControllerPanel.transform, "Slider_TiltSensitivity", new Vector2(100, 140), new Vector2(240, 26), 0.5f, 3.5f, PlayerPrefs.GetFloat("Tilt_Sensitivity", 2.6f));
        txtTiltSensitivity = CreateText(inGameControllerPanel.transform, "Txt_TiltVal", $"{sliderTiltSensitivity.value:F2}", 26, Color.cyan, new Vector2(270, 140), new Vector2(80, 45), font, FontStyle.Bold);
        sliderTiltSensitivity.onValueChanged.AddListener(val => {
            if (txtTiltSensitivity != null) txtTiltSensitivity.text = $"{val:F2}";
            PlayerPrefs.SetFloat("Tilt_Sensitivity", val);
            FindAnyObjectByType<RollingSkyBallController>()?.ReloadSettings();
        });

        CreateText(inGameControllerPanel.transform, "Lbl_Deadzone", "手抖死區過濾 (DEADZONE):", 26, Color.white, new Vector2(-200, 75), new Vector2(340, 45), font, FontStyle.Bold);
        sliderTiltDeadzone = CreateSlider(inGameControllerPanel.transform, "Slider_TiltDeadzone", new Vector2(100, 75), new Vector2(240, 26), 0.01f, 0.20f, PlayerPrefs.GetFloat("Tilt_Deadzone", 0.04f));
        txtTiltDeadzone = CreateText(inGameControllerPanel.transform, "Txt_DeadVal", $"{sliderTiltDeadzone.value:F2}", 26, Color.cyan, new Vector2(270, 75), new Vector2(80, 45), font, FontStyle.Bold);
        sliderTiltDeadzone.onValueChanged.AddListener(val => {
            if (txtTiltDeadzone != null) txtTiltDeadzone.text = $"{val:F2}";
            PlayerPrefs.SetFloat("Tilt_Deadzone", val);
            FindAnyObjectByType<RollingSkyBallController>()?.ReloadSettings();
        });

        CreateText(inGameControllerPanel.transform, "Lbl_CamSens", "視角轉向靈敏度 (LOOK):", 26, Color.white, new Vector2(-200, 10), new Vector2(340, 45), font, FontStyle.Bold);
        sliderCamSensitivity = CreateSlider(inGameControllerPanel.transform, "Slider_CamSensitivity", new Vector2(100, 10), new Vector2(240, 26), 0.5f, 5.0f, PlayerPrefs.GetFloat("Camera_Sensitivity", 2.0f));
        txtCamSensitivity = CreateText(inGameControllerPanel.transform, "Txt_CamSensVal", $"{sliderCamSensitivity.value:F1}x", 26, Color.cyan, new Vector2(270, 10), new Vector2(80, 45), font, FontStyle.Bold);
        sliderCamSensitivity.onValueChanged.AddListener(val => {
            if (txtCamSensitivity != null) txtCamSensitivity.text = $"{val:F1}x";
            PlayerPrefs.SetFloat("Camera_Sensitivity", val);
            FindAnyObjectByType<RollingSkyCamera>()?.ReloadSettings();
        });

        CreateText(inGameControllerPanel.transform, "Lbl_JumpDist", "超音波跳躍觸發距離:", 26, Color.white, new Vector2(-200, -55), new Vector2(340, 45), font, FontStyle.Bold);
        sliderJumpDistance = CreateSlider(inGameControllerPanel.transform, "Slider_JumpDistance", new Vector2(100, -55), new Vector2(240, 26), 10.0f, 40.0f, PlayerPrefs.GetFloat("Jump_Distance", 20.0f));
        txtJumpDistance = CreateText(inGameControllerPanel.transform, "Txt_JumpVal", $"{sliderJumpDistance.value:F0} cm", 26, Color.cyan, new Vector2(270, -55), new Vector2(80, 45), font, FontStyle.Bold);
        sliderJumpDistance.onValueChanged.AddListener(val => {
            if (txtJumpDistance != null) txtJumpDistance.text = $"{val:F0} cm";
            PlayerPrefs.SetFloat("Jump_Distance", val);
            // RollingSkyJumpDetector 只在 Start 讀取一次，需即時同步 (RollingSkyBallController 每幀讀 PlayerPrefs)
            RollingSkyJumpDetector jumpDet = FindAnyObjectByType<RollingSkyJumpDetector>();
            if (jumpDet != null) jumpDet.triggerDistance = val;
        });

        // 控制器監視板
        GameObject box = CreatePanel(inGameControllerPanel.transform, "SensorBox", new Color(0.06f, 0.12f, 0.24f, 0.85f));
        RectTransform boxRt = box.GetComponent<RectTransform>();
        boxRt.anchoredPosition = new Vector2(0, -145);
        boxRt.sizeDelta = new Vector2(820, 85);
        inGameHardwareStatusText = CreateText(box.transform, "SensorText", "• 實體裝置連線中 (Port: COM3)\n超音波測距: 50.0 cm\nMPU-6050 傾角: X=0.00, Y=0.00", 19, Color.green, Vector2.zero, new Vector2(800, 80), font, FontStyle.Normal);

        // ---------- 分頁 3: 音訊設定 (Audio Panel) ----------
        inGameAudioPanel = CreateSubPanel(panel.transform, "InGameAudioPanel");

        CreateText(inGameAudioPanel.transform, "Lbl_Track", "背景音樂 (BGM TRACK):", 26, Color.white, new Vector2(-220, 130), new Vector2(340, 45), font, FontStyle.Bold);
        GameObject bTr0 = CreateStyledButton(inGameAudioPanel.transform, "Btn_Track0", "Ver A 霓虹", new Vector2(-40, 130), new Vector2(150, 48), 20, font);
        btnBgmTrack0 = bTr0.GetComponent<Button>();
        btnBgmTrack0.onClick.AddListener(() => SelectInGameBgmTrack(0));

        GameObject bTr1 = CreateStyledButton(inGameAudioPanel.transform, "Btn_Track1", "Ver B 晶片", new Vector2(120, 130), new Vector2(150, 48), 20, font);
        btnBgmTrack1 = bTr1.GetComponent<Button>();
        btnBgmTrack1.onClick.AddListener(() => SelectInGameBgmTrack(1));

        GameObject bTr2 = CreateStyledButton(inGameAudioPanel.transform, "Btn_Track2", "Ver C 重力", new Vector2(280, 130), new Vector2(150, 48), 20, font);
        btnBgmTrack2 = bTr2.GetComponent<Button>();
        btnBgmTrack2.onClick.AddListener(() => SelectInGameBgmTrack(2));

        txtCurrentBgm = CreateText(inGameAudioPanel.transform, "Txt_CurrentBgm", "當前播放: Ver A: 霓虹賽博", 20, Color.cyan, new Vector2(0, 75), new Vector2(600, 35), font, FontStyle.Normal);

        CreateText(inGameAudioPanel.transform, "Lbl_Bgm", "音樂音量 (BGM):", 26, Color.white, new Vector2(-220, 15), new Vector2(340, 45), font, FontStyle.Bold);
        sliderBgmVolume = CreateSlider(inGameAudioPanel.transform, "Slider_Bgm", new Vector2(60, 15), new Vector2(240, 26), 0f, 1f, PlayerPrefs.GetFloat("BGM_Volume", 0.5f));
        txtBgmVolume = CreateText(inGameAudioPanel.transform, "Txt_BgmVal", $"{sliderBgmVolume.value * 100f:F0}%", 24, Color.cyan, new Vector2(230, 15), new Vector2(80, 45), font, FontStyle.Bold);
        sliderBgmVolume.onValueChanged.AddListener(OnInGameBgmChanged);

        CreateText(inGameAudioPanel.transform, "Lbl_Sfx", "音效音量 (SFX):", 26, Color.white, new Vector2(-220, -55), new Vector2(340, 45), font, FontStyle.Bold);
        sliderSfxVolume = CreateSlider(inGameAudioPanel.transform, "Slider_Sfx", new Vector2(60, -55), new Vector2(240, 26), 0f, 1f, PlayerPrefs.GetFloat("SFX_Volume", 0.5f));
        txtSfxVolume = CreateText(inGameAudioPanel.transform, "Txt_SfxVal", $"{sliderSfxVolume.value * 100f:F0}%", 24, Color.cyan, new Vector2(230, -55), new Vector2(80, 45), font, FontStyle.Bold);
        sliderSfxVolume.onValueChanged.AddListener(OnInGameSfxChanged);

        GameObject bTest = CreateStyledButton(inGameAudioPanel.transform, "Btn_TestSfx", "🔊 測試音效 (TEST SFX)", new Vector2(0, -135), new Vector2(260, 48), 22, font);
        bTest.GetComponent<Button>().onClick.AddListener(() => AudioManager.Instance?.PlayGem());

        // ---------- 底部操作按鈕 ----------
        GameObject bReset = CreateStyledButton(panel.transform, "Btn_ResetDefaults", "恢復控制預設 (RESET)", new Vector2(-200, -340), new Vector2(340, 58), 22, font);
        bReset.GetComponent<Button>().onClick.AddListener(ResetInGameControlsDefaults);

        GameObject bBack = CreateStyledButton(panel.transform, "Btn_BackFromSettings", "⬅ 返回暫停選單 (BACK)", new Vector2(200, -340), new Vector2(340, 58), 24, font);
        bBack.GetComponent<Button>().onClick.AddListener(CloseInGameSettings);

        panel.SetActive(false);
        return panel;
    }

    GameObject CreateSubPanel(Transform parent, string name)
    {
        GameObject subObj = new GameObject(name);
        subObj.transform.SetParent(parent, false);
        RectTransform rt = subObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -15);
        rt.sizeDelta = new Vector2(1000, 440);
        return subObj;
    }

    GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panelObj = new GameObject(name);
        panelObj.transform.SetParent(parent, false);
        Image img = panelObj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = (color.a > 0.01f);
        RectTransform rt = panelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1000, 400);
        return panelObj;
    }

    public void SwitchInGameSettingsTab(int tabIndex)
    {
        currentInGameSettingsTab = tabIndex;
        if (inGameDisplayPanel != null) inGameDisplayPanel.SetActive(tabIndex == 0);
        if (inGameKeyboardPanel != null) inGameKeyboardPanel.SetActive(tabIndex == 1);
        if (inGameControllerPanel != null) inGameControllerPanel.SetActive(tabIndex == 2);
        if (inGameAudioPanel != null) inGameAudioPanel.SetActive(tabIndex == 3);

        Color activeCol = new Color(0f, 0.5f, 0.9f);
        Color normalCol = new Color(0.1f, 0.22f, 0.38f);

        if (tabDisplayBtn != null) SetInGameBtnColor(tabDisplayBtn, tabIndex == 0 ? activeCol : normalCol);
        if (tabKeyboardBtn != null) SetInGameBtnColor(tabKeyboardBtn, tabIndex == 1 ? activeCol : normalCol);
        if (tabControllerBtn != null) SetInGameBtnColor(tabControllerBtn, tabIndex == 2 ? activeCol : normalCol);
        if (tabAudioBtn != null) SetInGameBtnColor(tabAudioBtn, tabIndex == 3 ? activeCol : normalCol);

        RebuildInGameSettingsButtons(tabIndex);
    }

    void SetInGameBtnColor(Button btn, Color c)
    {
        ColorBlock cb = btn.colors;
        cb.normalColor = c;
        btn.colors = cb;
    }

    void SetInGameFullscreen(bool isFull)
    {
        Screen.fullScreen = isFull;
        PlayerPrefs.SetInt("Screen_Fullscreen", isFull ? 1 : 0);
        PlayerPrefs.Save();
        if (txtDisplayStatus != null)
        {
            txtDisplayStatus.text = $"★ 當前狀態: {(isFull ? "全螢幕" : "視窗化")} ({Screen.width} × {Screen.height})";
        }
    }

    void SetInGameResolution(int w, int h)
    {
        Screen.SetResolution(w, h, Screen.fullScreen);
        PlayerPrefs.SetInt("Screen_Width", w);
        PlayerPrefs.SetInt("Screen_Height", h);
        PlayerPrefs.Save();
        if (txtDisplayStatus != null)
        {
            txtDisplayStatus.text = $"★ 當前狀態: {(Screen.fullScreen ? "全螢幕" : "視窗化")} ({w} × {h})";
        }
    }

    void SelectInGameBgmTrack(int index)
    {
        currentBgmIndex = index;
        AudioManager.Instance?.PlayBgmTrack(index);
        UpdateInGameBgmUI();
    }

    void UpdateInGameBgmUI()
    {
        string[] titles = new string[] { "Ver A: 霓虹賽博", "Ver B: 復古晶片", "Ver C: 深空重力" };
        if (txtCurrentBgm != null)
        {
            txtCurrentBgm.text = $"當前播放: {titles[Mathf.Clamp(currentBgmIndex, 0, 2)]}";
        }

        Color activeCol = new Color(0f, 0.6f, 0.95f);
        Color normalCol = new Color(0.1f, 0.22f, 0.38f);
        if (btnBgmTrack0 != null) SetInGameBtnColor(btnBgmTrack0, currentBgmIndex == 0 ? activeCol : normalCol);
        if (btnBgmTrack1 != null) SetInGameBtnColor(btnBgmTrack1, currentBgmIndex == 1 ? activeCol : normalCol);
        if (btnBgmTrack2 != null) SetInGameBtnColor(btnBgmTrack2, currentBgmIndex == 2 ? activeCol : normalCol);
    }

    public void StartInGameRebind(string actionKey)
    {
        if (isRebinding) return;
        StartCoroutine(InGameRebindRoutine(actionKey));
    }

    System.Collections.IEnumerator InGameRebindRoutine(string actionKey)
    {
        isRebinding = true;
        Text targetText = null;
        if (actionKey == "Key_Left") targetText = txtKeyLeft;
        else if (actionKey == "Key_Right") targetText = txtKeyRight;
        else if (actionKey == "Key_Jump") targetText = txtKeyJump;

        if (targetText != null) targetText.text = "請按鍵...";

        yield return null;

        bool keyAssigned = false;
        while (!keyAssigned)
        {
            if (Input.anyKeyDown)
            {
                foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(kc) && kc != KeyCode.Escape)
                    {
                        if (actionKey == "Key_Left") { currentKeyLeft = kc; PlayerPrefs.SetInt("Key_Left", (int)kc); }
                        else if (actionKey == "Key_Right") { currentKeyRight = kc; PlayerPrefs.SetInt("Key_Right", (int)kc); }
                        else if (actionKey == "Key_Jump") { currentKeyJump = kc; PlayerPrefs.SetInt("Key_Jump", (int)kc); }

                        keyAssigned = true;
                        break;
                    }
                }
            }
            yield return null;
        }

        PlayerPrefs.Save();
        UpdateInGameKeyTexts();
        FindAnyObjectByType<RollingSkyBallController>()?.ReloadSettings();
        isRebinding = false;
    }

    void UpdateInGameKeyTexts()
    {
        if (txtKeyLeft != null) txtKeyLeft.text = $"[ {currentKeyLeft} ]";
        if (txtKeyRight != null) txtKeyRight.text = $"[ {currentKeyRight} ]";
        if (txtKeyJump != null) txtKeyJump.text = $"[ {currentKeyJump} ]";
    }

    public void ResetInGameControlsDefaults()
    {
        currentKeyLeft = KeyCode.A;
        currentKeyRight = KeyCode.D;
        currentKeyJump = KeyCode.Space;
        PlayerPrefs.SetInt("Key_Left", (int)KeyCode.A);
        PlayerPrefs.SetInt("Key_Right", (int)KeyCode.D);
        PlayerPrefs.SetInt("Key_Jump", (int)KeyCode.Space);

        PlayerPrefs.SetFloat("Keyboard_Speed", 9.0f);
        PlayerPrefs.SetFloat("Tilt_Sensitivity", 2.6f);
        PlayerPrefs.SetFloat("Tilt_Deadzone", 0.04f);
        PlayerPrefs.SetFloat("Jump_Distance", 20.0f);
        PlayerPrefs.SetFloat("Camera_Sensitivity", 2.0f);
        PlayerPrefs.Save();

        UpdateInGameKeyTexts();
        if (sliderKeyboardSpeed != null) sliderKeyboardSpeed.value = 9.0f;
        if (sliderTiltSensitivity != null) sliderTiltSensitivity.value = 1.8f;
        if (sliderTiltDeadzone != null) sliderTiltDeadzone.value = 0.08f;
        if (sliderJumpDistance != null) sliderJumpDistance.value = 20.0f;
        if (sliderCamSensitivity != null) sliderCamSensitivity.value = 2.0f;
        if (sliderInGameMouseSens != null) sliderInGameMouseSens.value = 2.0f;

        FindAnyObjectByType<RollingSkyBallController>()?.ReloadSettings();
        FindAnyObjectByType<RollingSkyCamera>()?.ReloadSettings();
        Debug.Log("[Settings] 控制器與鍵盤設定已恢復預設！");
    }

    void UpdateInGameHardwareReadout()
    {
        if (inGameHardwareStatusText == null) return;
        if (arduino != null && arduino.distance > 0)
        {
            inGameHardwareStatusText.text = $"• 實體裝置連線中 (Port: {arduino.portName})\n超音波測距: <color=yellow>{arduino.distance:F1} cm</color>\nMPU-6050 傾角: X=<color=cyan>{arduino.acceleration.x:F2}</color>, Y=<color=cyan>{arduino.acceleration.y:F2}</color>";
        }
        else
        {
            inGameHardwareStatusText.text = "• 尚未檢測到實體裝置 (純鍵盤模擬模式)\n超音波測距: -- cm\nMPU-6050 傾角: X=0.00, Y=0.00";
        }
    }

    Slider CreateSlider(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, float def)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);
        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = min; slider.maxValue = max; slider.value = def;

        RectTransform sRect = sliderObj.GetComponent<RectTransform>();
        sRect.anchoredPosition = pos; sRect.sizeDelta = size;

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sliderObj.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.16f, 0.25f, 0.9f);
        bgImg.raycastTarget = true; // 讓整條滑軌可點擊/拖曳，而非只有 24px 的把手
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.sizeDelta = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero; faRect.anchorMax = Vector2.one; faRect.sizeDelta = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0f, 0.75f, 1f);
        fillImg.raycastTarget = false;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;
        slider.fillRect = fillRect;

        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform haRect = handleArea.AddComponent<RectTransform>();
        haRect.anchorMin = Vector2.zero; haRect.anchorMax = Vector2.one; haRect.sizeDelta = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        handleImg.raycastTarget = true;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(24, 0);
        slider.handleRect = handleRect;

        return slider;
    }

    void OnInGameBgmChanged(float val)
    {
        if (txtBgmVolume != null) txtBgmVolume.text = $"{val * 100f:F0}%";
        AudioManager.Instance?.SetBgmVolume(val);
        PlayerPrefs.SetFloat("BGM_Volume", val);
    }

    void OnInGameSfxChanged(float val)
    {
        if (txtSfxVolume != null) txtSfxVolume.text = $"{val * 100f:F0}%";
        AudioManager.Instance?.SetSfxVolume(val);
        PlayerPrefs.SetFloat("SFX_Volume", val);
    }

    public void OpenInGameSettings()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (inGameSettingsPanel != null)
        {
            inGameSettingsPanel.transform.SetAsLastSibling();
            inGameSettingsPanel.SetActive(true);
            // 設定面板導航由 HandleInGameSettingsNavigation 負責；關閉 EventSystem 內建方向鍵導航，避免滑鼠點過的 Slider 被雙重調整
            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = false;
                EventSystem.current.SetSelectedGameObject(null);
            }

            currentKeyLeft = (KeyCode)PlayerPrefs.GetInt("Key_Left", (int)KeyCode.A);
            currentKeyRight = (KeyCode)PlayerPrefs.GetInt("Key_Right", (int)KeyCode.D);
            currentKeyJump = (KeyCode)PlayerPrefs.GetInt("Key_Jump", (int)KeyCode.Space);
            UpdateInGameKeyTexts();

            if (sliderKeyboardSpeed != null) sliderKeyboardSpeed.value = PlayerPrefs.GetFloat("Keyboard_Speed", 9.0f);
            if (sliderTiltSensitivity != null) sliderTiltSensitivity.value = PlayerPrefs.GetFloat("Tilt_Sensitivity", 2.6f);
            if (sliderTiltDeadzone != null) sliderTiltDeadzone.value = PlayerPrefs.GetFloat("Tilt_Deadzone", 0.04f);
            if (sliderJumpDistance != null) sliderJumpDistance.value = PlayerPrefs.GetFloat("Jump_Distance", 20.0f);
            if (sliderCamSensitivity != null) sliderCamSensitivity.value = PlayerPrefs.GetFloat("Camera_Sensitivity", 2.0f);
            if (sliderInGameMouseSens != null) sliderInGameMouseSens.value = PlayerPrefs.GetFloat("Camera_Sensitivity", 2.0f);
            if (sliderBgmVolume != null) sliderBgmVolume.value = PlayerPrefs.GetFloat("BGM_Volume", 0.5f);
            if (sliderSfxVolume != null) sliderSfxVolume.value = PlayerPrefs.GetFloat("SFX_Volume", 0.5f);

            SwitchInGameSettingsTab(0);
            activeInGameNavCol = 0;
            activeInGameNavRow = 0;
            isInGameNavActive = true;
            EnsureAllInGameButtonsTextSafe();
        }
    }

    public void CloseInGameSettings()
    {
        if (isRebinding) return;
        if (inGameSettingsPanel != null) inGameSettingsPanel.SetActive(false);
        if (pausePanel != null)
        {
            pausePanel.transform.SetAsLastSibling();
            pausePanel.SetActive(true);
            InitPauseMenuButtons();
            activeInGameNavCol = 0;
            activeInGameNavRow = 0;
            isInGameNavActive = true;
            EnsureAllInGameButtonsTextSafe();
        }
        PlayerPrefs.Save();
        FindAnyObjectByType<RollingSkyBallController>()?.ReloadSettings();
    }

    void EnsurePausePanelButtons(GameObject panel, Font font)
    {
        if (panel == null) return;
        Transform tResume = panel.transform.Find("Btn_Resume");
        Transform tSettings = panel.transform.Find("Btn_Settings");
        Transform tRestart = panel.transform.Find("Btn_Restart");
        Transform tMainMenu = panel.transform.Find("Btn_MainMenu");

        if (tResume != null)
        {
            RectTransform rt = tResume.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0, 45); rt.sizeDelta = new Vector2(440, 58); }
        }

        if (tSettings == null)
        {
            CreateStyledButton(panel.transform, "Btn_Settings", "⚙ 遊戲設定 (SETTINGS)", new Vector2(0, -25), new Vector2(440, 58), 26, font);
        }
        else
        {
            RectTransform rt = tSettings.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0, -25); rt.sizeDelta = new Vector2(440, 58); }
        }

        if (tRestart != null)
        {
            RectTransform rt = tRestart.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0, -95); rt.sizeDelta = new Vector2(440, 58); }
        }

        if (tMainMenu != null)
        {
            RectTransform rt = tMainMenu.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0, -165); rt.sizeDelta = new Vector2(440, 58); }
        }
    }

    void EnsureGameOverPanelButtons(GameObject panel, Font font)
    {
        if (panel == null) return;

        // 重構並重置死亡面板標題與完成度座標，徹底杜絕文本與按鈕相互重疊
        Transform tReason = panel.transform.Find("Reason");
        if (tReason != null)
        {
            RectTransform rtReason = tReason.GetComponent<RectTransform>();
            if (rtReason != null)
            {
                rtReason.anchoredPosition = new Vector2(0, 140);
                rtReason.sizeDelta = new Vector2(850, 60);
            }
            Text txtReason = tReason.GetComponent<Text>();
            if (txtReason != null)
            {
                txtReason.fontSize = 44;
                txtReason.fontStyle = FontStyle.Bold;
                txtReason.alignment = TextAnchor.MiddleCenter;
                txtReason.horizontalOverflow = HorizontalWrapMode.Wrap;
                txtReason.verticalOverflow = VerticalWrapMode.Overflow;
                txtReason.color = Color.white;
            }
        }

        Transform tProg = panel.transform.Find("Progress");
        if (tProg != null)
        {
            RectTransform rtProg = tProg.GetComponent<RectTransform>();
            if (rtProg != null)
            {
                rtProg.anchoredPosition = new Vector2(0, 60);
                rtProg.sizeDelta = new Vector2(850, 50);
            }
            Text txtProg = tProg.GetComponent<Text>();
            if (txtProg != null)
            {
                txtProg.fontSize = 34;
                txtProg.fontStyle = FontStyle.Bold;
                txtProg.alignment = TextAnchor.MiddleCenter;
                txtProg.horizontalOverflow = HorizontalWrapMode.Wrap;
                txtProg.verticalOverflow = VerticalWrapMode.Overflow;
                txtProg.color = Color.yellow;
            }
        }

        Transform tHint = panel.transform.Find("Hint");
        if (tHint != null)
        {
            RectTransform rtHint = tHint.GetComponent<RectTransform>();
            if (rtHint != null)
            {
                rtHint.anchoredPosition = new Vector2(0, 5);
                rtHint.sizeDelta = new Vector2(850, 40);
            }
            Text txtHint = tHint.GetComponent<Text>();
            if (txtHint != null)
            {
                txtHint.fontSize = 22;
                txtHint.alignment = TextAnchor.MiddleCenter;
                txtHint.horizontalOverflow = HorizontalWrapMode.Wrap;
                txtHint.verticalOverflow = VerticalWrapMode.Overflow;
                txtHint.color = new Color(0.85f, 0.85f, 0.85f, 0.85f);
            }
        }

        Transform tRestart = panel.transform.Find("Btn_Restart");
        Transform tMainMenu = panel.transform.Find("Btn_MainMenu");

        if (tRestart == null)
        {
            CreateStyledButton(panel.transform, "Btn_Restart", "🔄 重新開始 (RETRY)", new Vector2(0, -65), new Vector2(440, 60), 28, font);
        }
        else
        {
            RectTransform rt = tRestart.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0, -65); rt.sizeDelta = new Vector2(440, 60); }
        }

        if (tMainMenu == null)
        {
            CreateStyledButton(panel.transform, "Btn_MainMenu", "🏠 回主選單 (MAIN MENU)", new Vector2(0, -140), new Vector2(440, 60), 28, font);
        }
        else
        {
            RectTransform rt = tMainMenu.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0, -140); rt.sizeDelta = new Vector2(440, 60); }
        }
    }

    void EnsureVictoryPanelButtons(GameObject panel, Font font)
    {
        if (panel == null) return;

        // 重構並重置標題與副標題座標，徹底杜絕按鈕與文本重疊
        Transform tTitle = panel.transform.Find("Title");
        if (tTitle != null)
        {
            RectTransform rtTitle = tTitle.GetComponent<RectTransform>();
            if (rtTitle != null)
            {
                rtTitle.anchoredPosition = new Vector2(0, 160);
                rtTitle.sizeDelta = new Vector2(900, 65);
            }
            Text txtTitle = tTitle.GetComponent<Text>();
            if (txtTitle != null)
            {
                txtTitle.fontSize = 52;
                txtTitle.fontStyle = FontStyle.Bold;
                txtTitle.alignment = TextAnchor.MiddleCenter;
                txtTitle.color = Color.yellow;
            }
        }

        Transform tSub = panel.transform.Find("Sub");
        if (tSub != null)
        {
            RectTransform rtSub = tSub.GetComponent<RectTransform>();
            if (rtSub != null)
            {
                rtSub.anchoredPosition = new Vector2(0, 80);
                rtSub.sizeDelta = new Vector2(1000, 75);
            }
            Text txtSub = tSub.GetComponent<Text>();
            if (txtSub != null)
            {
                txtSub.fontSize = 26;
                txtSub.fontStyle = FontStyle.Bold;
                txtSub.alignment = TextAnchor.MiddleCenter;
                txtSub.horizontalOverflow = HorizontalWrapMode.Wrap;
                txtSub.verticalOverflow = VerticalWrapMode.Overflow;
                txtSub.lineSpacing = 1.15f;
                txtSub.color = Color.white;
            }
        }

        Transform tHint = panel.transform.Find("Hint");
        if (tHint != null)
        {
            RectTransform rtHint = tHint.GetComponent<RectTransform>();
            if (rtHint != null)
            {
                rtHint.anchoredPosition = new Vector2(0, 20);
                rtHint.sizeDelta = new Vector2(850, 35);
            }
            Text txtHint = tHint.GetComponent<Text>();
            if (txtHint != null)
            {
                txtHint.fontSize = 20;
                txtHint.alignment = TextAnchor.MiddleCenter;
                txtHint.color = new Color(0.85f, 0.85f, 0.85f, 0.75f);
            }
        }

        Transform tNext = panel.transform.Find("Btn_NextLevel");
        Transform tRestart = panel.transform.Find("Btn_Restart");
        Transform tMainMenu = panel.transform.Find("Btn_MainMenu");

        if (tNext == null)
        {
            CreateStyledButton(panel.transform, "Btn_NextLevel", "⏩ 前往下一關 (NEXT LEVEL)", new Vector2(0, 10), new Vector2(440, 56), 26, font);
        }
        else
        {
            RectTransform rt = tNext.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0, 10); rt.sizeDelta = new Vector2(440, 56); }
        }

        if (tRestart == null)
        {
            CreateStyledButton(panel.transform, "Btn_Restart", "🔄 重新開始 (RETRY)", new Vector2(0, -55), new Vector2(440, 56), 26, font);
        }
        else
        {
            RectTransform rt = tRestart.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0, -55); rt.sizeDelta = new Vector2(440, 56); }
        }

        if (tMainMenu == null)
        {
            CreateStyledButton(panel.transform, "Btn_MainMenu", "🏠 回主選單 (MAIN MENU)", new Vector2(0, -125), new Vector2(440, 56), 26, font);
        }
        else
        {
            RectTransform rt = tMainMenu.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0, -125); rt.sizeDelta = new Vector2(440, 56); }
        }
    }

    GameObject CreateStyledButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, int fontSize, Font font)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.1f, 0.18f, 0.32f, 0.95f);
        img.raycastTarget = true;

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.1f, 0.22f, 0.38f);
        colors.highlightedColor = new Color(0f, 0.7f, 0.95f);
        colors.pressedColor = new Color(0f, 0.4f, 0.7f);
        btn.colors = colors;

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(btnObj.transform, false);
        Text t = tObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = label;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform trt = tObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        Outline outline = tObj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(2, -2);

        return btnObj;
    }

    Text CreateText(Transform parent, string name, string content, int fontSize, Color color, Vector2 pos, Vector2 size, Font font, FontStyle style)
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

    void BindAllButtonsRuntime()
    {
        if (pausePanel != null)
        {
            BindButton(pausePanel, "Btn_Resume", ResumeGame);
            BindButton(pausePanel, "Btn_Settings", OpenInGameSettings);
            BindButton(pausePanel, "Btn_Restart", RestartLevel);
            BindButton(pausePanel, "Btn_MainMenu", ReturnToMainMenu);
        }

        if (inGameSettingsPanel != null)
        {
            BindButton(inGameSettingsPanel, "Btn_BackFromSettings", CloseInGameSettings);
        }

        if (gameOverPanel != null)
        {
            BindButton(gameOverPanel, "Btn_Restart", RestartLevel);
            BindButton(gameOverPanel, "Btn_MainMenu", ReturnToMainMenu);
        }

        if (victoryPanel != null)
        {
            BindButton(victoryPanel, "Btn_NextLevel", GoToNextLevel);
            BindButton(victoryPanel, "Btn_Restart", RestartLevel);
            BindButton(victoryPanel, "Btn_MainMenu", ReturnToMainMenu);
        }
    }

    public void GoToNextLevel()
    {
        int currentLevel = PlayerPrefs.GetInt("SelectedLevel", 1);
        int nextLevel = currentLevel + 1;
        if (nextLevel > 10) nextLevel = 10;

        PlayerPrefs.SetInt("SelectedLevel", nextLevel);
        PlayerPrefs.SetInt("IsEndlessMode", 0);
        PlayerPrefs.SetInt("IsBuilderMode", 0);
        PlayerPrefs.Save();

        Time.timeScale = 1.0f;
        SetCursorLocked(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void BindButton(GameObject parent, string btnName, UnityAction action)
    {
        if (parent == null) return;
        Transform t = parent.transform.Find(btnName);
        if (t != null)
        {
            Button btn = t.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(action);
            }
        }
    }

    void UpdateProgress()
    {
        if (ball == null) return;

        bool isSkyTower = (PlayerPrefs.GetInt("IsSkyTowerMode", 0) == 1);
        if (isSkyTower)
        {
            // 通天天梯 0m - 500m 實時高度與分區判定
            float currentHeight = Mathf.Max(0f, ball.position.y);
            float maxHeight = 500.0f;
            float ratio = Mathf.Clamp01(currentHeight / maxHeight);

            int bestHeight = PlayerPrefs.GetInt("SkyTower_BestHeight", 0);
            if ((int)currentHeight > bestHeight)
            {
                bestHeight = (int)currentHeight;
                PlayerPrefs.SetInt("SkyTower_BestHeight", bestHeight);
                PlayerPrefs.Save();
            }

            string zoneName = "【Zone 1: 晨曦起點】";
            if (currentHeight >= 430f) zoneName = "【Zone 6: 凌霄天頂】";
            else if (currentHeight >= 320f) zoneName = "【Zone 5: 雲端迷蹤】";
            else if (currentHeight >= 220f) zoneName = "【Zone 4: 窄崖尖刺】";
            else if (currentHeight >= 120f) zoneName = "【Zone 3: 旋轉階梯】";
            else if (currentHeight >= 50f) zoneName = "【Zone 2: 霓虹管陣】";

            if (progressText != null)
            {
                progressText.text = $"🗼 高度: <color=lime>{currentHeight:F1}m</color> / 500m ({ratio:P0})  {zoneName}  |  🏆 最佳: <color=yellow>{bestHeight}m</color>";
            }
            if (progressBar != null) progressBar.value = ratio;
            return;
        }

        float currentDist = ball.position.z - startZ;

        if (isEndlessMode)
        {
            // 無盡模式計分：前進米數 + 水晶收集額外分數
            int score = (int)Mathf.Max(0, currentDist) + (collectedGems * 50);
            int bestScore = PlayerPrefs.GetInt("Endless_HighScore", 0);
            if (score > bestScore)
            {
                bestScore = score;
                PlayerPrefs.SetInt("Endless_HighScore", bestScore);
                PlayerPrefs.Save();
            }

            if (progressText != null)
            {
                // 徹底修復分數超過一千分被切掉問題：放寬寬度至 850 並啟用 Overflow
                progressText.horizontalOverflow = HorizontalWrapMode.Overflow;
                progressText.verticalOverflow = VerticalWrapMode.Overflow;
                RectTransform prt = progressText.GetComponent<RectTransform>();
                if (prt != null && prt.sizeDelta.x < 750f)
                {
                    prt.sizeDelta = new Vector2(850f, 60f);
                }
                progressText.text = $"⚡ 得分: {score}  |  🏆 最佳: {bestScore}";
            }
            if (progressBar != null) progressBar.value = Mathf.PingPong(currentDist / 80f, 1.0f);
        }
        else
        {
            float ratio = Mathf.Clamp01(currentDist / trackLength);
            float percent = ratio * 100f;

            if (progressBar != null) progressBar.value = ratio;
            if (progressText != null) progressText.text = $"{percent:F0}%";

            // 終點判定
            if (ratio >= 0.999f)
            {
                TriggerVictory();
            }
        }
    }

    void UpdateHardwareHUD()
    {
        if (hardwareStatusText == null) return;

        bool isSkyTower = (PlayerPrefs.GetInt("IsSkyTowerMode", 0) == 1);
        if (isSkyTower)
        {
            if (arduino != null && arduino.distance > 0)
            {
                hardwareStatusText.text = $"推進跳躍測距: <color=yellow>{arduino.distance:F1} cm</color> (前推跳躍) | 3軸前後/左右: <color=cyan>Y={arduino.acceleration.y:F2}, X={arduino.acceleration.x:F2}</color> | <color=white>[ESC: 暫停]</color>";
            }
            else
            {
                hardwareStatusText.text = "天梯操作模式: [W/S: 前進/後退] | [A/D: 左右] | [Space: 跳躍] | [R: 重試] | <color=cyan>[ESC: 暫停]</color>";
            }
            return;
        }

        RollingSkyBallController ballCtrl = ball != null ? ball.GetComponent<RollingSkyBallController>() : null;
        string swayInfo = (ballCtrl != null && ballCtrl.enableSway && !isEndlessMode) ? $" | <color=yellow>平衡風力: {ballCtrl.CurrentSwayForce:+0.0;-0.0;0.0}</color>" : "";

        if (arduino != null && arduino.distance > 0)
        {
            hardwareStatusText.text = $"超音波測距: <color=yellow>{arduino.distance:F1} cm</color> (揮手跳躍) | 傾斜感應: <color=cyan>{arduino.acceleration.x:F2}</color>{swayInfo} | <color=white>[ESC: 暫停選單]</color>";
        }
        else
        {
            hardwareStatusText.text = $"鍵盤操作模式: [A/D: 左右移] | [Space: 跳躍] | [R: 重試]{swayInfo} | <color=cyan>[ESC: 暫停 / 回到選單]</color>";
        }
    }

    public void AddGem()
    {
        collectedGems++;
        if (collectedGems > totalGemsInLevel)
        {
            totalGemsInLevel = collectedGems;
        }
        UpdateGemUI();
    }

    void UpdateGemUI()
    {
        if (gemText != null)
        {
            if (isEndlessMode)
            {
                // 無盡模式徹底隱藏左上角孤立文字，避免裁切露出的殘留數字 "0"
                gemText.gameObject.SetActive(false);
            }
            else
            {
                gemText.gameObject.SetActive(true);
                RectTransform grt = gemText.GetComponent<RectTransform>();
                if (grt != null)
                {
                    grt.anchorMin = new Vector2(0, 1);
                    grt.anchorMax = new Vector2(0, 1);
                    grt.pivot = new Vector2(0, 1);
                    grt.anchoredPosition = new Vector2(30, -30);
                    grt.sizeDelta = new Vector2(300, 45);
                }
                gemText.horizontalOverflow = HorizontalWrapMode.Overflow;
                gemText.verticalOverflow = VerticalWrapMode.Overflow;
                gemText.text = $"💎 {collectedGems} / {totalGemsInLevel}";
            }
        }
    }

    Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    public void TriggerGameOver(string reason)
    {
        if (isGameOver || isVictory) return;

        if (isBuilderMode)
        {
            // 在自訂建造模式試玩中，由 TrackBuilderManager 掌管死亡與返回編輯，不彈出常規 GameOver 面板
            TrackBuilderManager.Instance?.OnTestBallDied(reason);
            return;
        }

        isGameOver = true;
        SetCursorLocked(false);

        if (pausePanel != null) pausePanel.SetActive(false);

        float currentDist = (ball != null) ? ball.position.z - startZ : 0f;

        if (gameOverPanel != null)
        {
            EnsureGameOverPanelButtons(gameOverPanel, GetDefaultFont());
            gameOverPanel.transform.SetAsLastSibling();
            gameOverPanel.SetActive(true);
            InitGameOverButtons();
            activeInGameNavCol = 0;
            activeInGameNavRow = 0;
            isInGameNavActive = true;
            EnsureAllInGameButtonsTextSafe();
            if (gameOverReasonText != null)
            {
                gameOverReasonText.text = reason;
                gameOverReasonText.horizontalOverflow = HorizontalWrapMode.Wrap;
                gameOverReasonText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            if (isEndlessMode)
            {
                int score = (int)Mathf.Max(0, currentDist) + (collectedGems * 50);
                int bestScore = PlayerPrefs.GetInt("Endless_HighScore", 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    PlayerPrefs.SetInt("Endless_HighScore", bestScore);
                    PlayerPrefs.Save();
                }
                if (score >= bestScore && score > 0)
                {
                    AudioManager.Instance?.PlayHighscore();
                }
                if (gameOverProgressText != null)
                {
                    gameOverProgressText.text = $"無盡跑酷得分: {score} 分 (歷史最佳: {bestScore} 分)";
                    gameOverProgressText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    gameOverProgressText.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }
            else
            {
                float percent = Mathf.Clamp01(currentDist / trackLength) * 100f;
                if (gameOverProgressText != null)
                {
                    gameOverProgressText.text = $"完成度: {percent:F0}%";
                    gameOverProgressText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    gameOverProgressText.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }
        }

        Debug.Log($"[GameManager] Game Over! {reason}");
    }

    public void TriggerVictory()
    {
        if (isGameOver || isVictory) return;
        isVictory = true;
        SetCursorLocked(false);

        if (pausePanel != null) pausePanel.SetActive(false);
        if (inGameSettingsPanel != null) inGameSettingsPanel.SetActive(false);

        // 平滑凍結小球物理，杜絕撞牆卡住或衝入虛空
        if (ball != null)
        {
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        // 強制更新進度條為 100% 完美通關狀態
        if (progressBar != null) progressBar.value = 1.0f;
        if (progressText != null) progressText.text = "100%";

        // 標記本關卡已通關
        int currentLevel = PlayerPrefs.GetInt("SelectedLevel", 1);
        PlayerPrefs.SetInt($"Level_{currentLevel}_Completed", 1);
        PlayerPrefs.Save();

        if (victoryPanel != null)
        {
            EnsureVictoryPanelButtons(victoryPanel, GetDefaultFont());

            Transform subT = victoryPanel.transform.Find("Sub");
            if (subT != null)
            {
                Text t = subT.GetComponent<Text>();
                if (t != null)
                {
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    t.verticalOverflow = VerticalWrapMode.Overflow;
                    if (currentLevel >= 10)
                        t.text = "恭喜征服第 10 關！通關全系列所有關卡！\n你是真正的滾球之王！";
                    else
                        t.text = $"恭喜征服第 {currentLevel} 關！晶彩跳躍，勢不可擋！";
                }
            }

            Transform tNext = victoryPanel.transform.Find("Btn_NextLevel");
            Transform tRestart = victoryPanel.transform.Find("Btn_Restart");
            Transform tMainMenu = victoryPanel.transform.Find("Btn_MainMenu");

            bool canGoNext = (currentLevel < 10) && !isEndlessMode;
            if (tNext != null)
            {
                tNext.gameObject.SetActive(canGoNext);
                Text nextText = tNext.GetComponentInChildren<Text>();
                if (nextText != null)
                {
                    nextText.text = $"⏩ 前往第 {currentLevel + 1} 關 (NEXT LEVEL)";
                }
            }

            // 第10關或無盡模式下沒有下一關按鈕時，將按鈕向上平移置中
            if (!canGoNext)
            {
                if (tRestart != null)
                {
                    RectTransform rt = tRestart.GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(0, -20);
                }
                if (tMainMenu != null)
                {
                    RectTransform rt = tMainMenu.GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(0, -90);
                }
            }
            else
            {
                if (tRestart != null)
                {
                    RectTransform rt = tRestart.GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(0, -55);
                }
                if (tMainMenu != null)
                {
                    RectTransform rt = tMainMenu.GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(0, -125);
                }
            }

            victoryPanel.transform.SetAsLastSibling();
            victoryPanel.SetActive(true);
            InitVictoryButtons();
            activeInGameNavCol = 0;
            activeInGameNavRow = 0;
            isInGameNavActive = true;
            EnsureAllInGameButtonsTextSafe();
        }

        AudioManager.Instance?.PlayVictory();
        Debug.Log($"[GameManager] 🎉 100% 完美通關第 {currentLevel} 關！");
    }

    // ==================== 遊戲內各選單實體搖桿控制、動態推擠防重疊與文字防切安全防線 ====================
    [System.Serializable]
    public class InGameAccordionButton
    {
        public Button button;
        public Slider slider; // 非 null 時：左右鍵 / 搖桿左右直接調整數值 (設定面板控制器分頁)
        public RectTransform rectTransform;
        public Image image;
        public Text text;
        public Vector2 basePos;
        public int column; // 0: 單欄或左欄
        public int row;    // 縱向列索引
        public Color normalColor = new Color(0.1f, 0.22f, 0.38f, 0.95f);
        public Color highlightColor = new Color(0.95f, 0.65f, 0.05f, 1f); // 亮金橙高對比發光色
        public Color normalTextColor = Color.white;
        public Color highlightTextColor = new Color(1f, 1f, 0.85f, 1f);
    }

    private List<InGameAccordionButton> pauseMenuButtons = new List<InGameAccordionButton>();
    private List<InGameAccordionButton> gameOverMenuButtons = new List<InGameAccordionButton>();
    private List<InGameAccordionButton> victoryMenuButtons = new List<InGameAccordionButton>();
    private List<InGameAccordionButton> inGameSettingsButtons = new List<InGameAccordionButton>();
    private int currentInGameSettingsTab = 0;

    private int activeInGameNavCol = 0;
    private int activeInGameNavRow = 0;
    private bool isInGameNavActive = true;
    private float inGameNavCooldownTimer = 0f;
    private bool lastInGameJoyBtnState = false;

    private const float SELECTED_SCALE = 1.15f;
    private const float PUSH_OFFSET = 18f;
    private const float ANIM_SPEED = 14f;

    void InitPauseMenuButtons()
    {
        if (pausePanel == null) return;
        pauseMenuButtons.Clear();
        string[] btnNames = new string[] { "Btn_Resume", "Btn_Settings", "Btn_Restart", "Btn_MainMenu" };
        for (int r = 0; r < btnNames.Length; r++)
        {
            RegisterInGameAccordionButton(pausePanel, btnNames[r], 0, r, pauseMenuButtons, (c, row) => {
                activeInGameNavCol = c;
                activeInGameNavRow = row;
                isInGameNavActive = true;
            });
        }
    }

    void InitGameOverButtons()
    {
        if (gameOverPanel == null) return;
        gameOverMenuButtons.Clear();
        string[] btnNames = new string[] { "Btn_Restart", "Btn_MainMenu" };
        for (int r = 0; r < btnNames.Length; r++)
        {
            RegisterInGameAccordionButton(gameOverPanel, btnNames[r], 0, r, gameOverMenuButtons, (c, row) => {
                activeInGameNavCol = c;
                activeInGameNavRow = row;
                isInGameNavActive = true;
            });
        }
    }

    void InitVictoryButtons()
    {
        if (victoryPanel == null) return;
        victoryMenuButtons.Clear();
        int currentLevel = PlayerPrefs.GetInt("SelectedLevel", 1);
        bool canGoNext = (currentLevel < 10) && !isEndlessMode;

        if (canGoNext)
        {
            string[] btnNames = new string[] { "Btn_NextLevel", "Btn_Restart", "Btn_MainMenu" };
            for (int r = 0; r < btnNames.Length; r++)
            {
                RegisterInGameAccordionButton(victoryPanel, btnNames[r], 0, r, victoryMenuButtons, (c, row) => {
                    activeInGameNavCol = c;
                    activeInGameNavRow = row;
                    isInGameNavActive = true;
                });
            }
        }
        else
        {
            string[] btnNames = new string[] { "Btn_Restart", "Btn_MainMenu" };
            for (int r = 0; r < btnNames.Length; r++)
            {
                RegisterInGameAccordionButton(victoryPanel, btnNames[r], 0, r, victoryMenuButtons, (c, row) => {
                    activeInGameNavCol = c;
                    activeInGameNavRow = row;
                    isInGameNavActive = true;
                });
            }
        }
    }

    void RebuildInGameSettingsButtons(int tabIndex)
    {
        currentInGameSettingsTab = tabIndex;
        if (inGameSettingsPanel == null) return;
        inGameSettingsButtons.Clear();

        // Row 0: 頂部 4 大分頁 Tab 按鈕
        string[] tabNames = new string[] { "Btn_TabDisplay", "Btn_TabKeyboard", "Btn_TabController", "Btn_TabAudio" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            RegisterInGameAccordionButton(inGameSettingsPanel, tabNames[i], i, 0, inGameSettingsButtons, (c, r) => {
                activeInGameNavCol = c;
                activeInGameNavRow = r;
                isInGameNavActive = true;
            });
        }

        // Row 1~4: 依據當前 Tab 註冊內部選項按鈕
        if (tabIndex == 0 && inGameDisplayPanel != null)
        {
            // Row 1: 全螢幕 / 視窗化
            RegisterInGameAccordionButton(inGameDisplayPanel, "Btn_Fullscreen", 0, 1, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameDisplayPanel, "Btn_Windowed", 1, 1, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });

            // Row 2: 4 大快捷解析度
            RegisterInGameAccordionButton(inGameDisplayPanel, "Btn_Res1080", 0, 2, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameDisplayPanel, "Btn_Res900", 1, 2, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameDisplayPanel, "Btn_Res768", 2, 2, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameDisplayPanel, "Btn_Res720", 3, 2, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
        }
        else if (tabIndex == 1 && inGameKeyboardPanel != null)
        {
            // Row 1~3: 改鍵按鈕
            RegisterInGameAccordionButton(inGameKeyboardPanel, "Btn_KeyLeft", 0, 1, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameKeyboardPanel, "Btn_KeyRight", 0, 2, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameKeyboardPanel, "Btn_KeyJump", 0, 3, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
        }
        else if (tabIndex == 2 && inGameControllerPanel != null)
        {
            // Row 1~4: 控制器滑桿 (左右鍵 / 搖桿左右調整數值)
            RegisterInGameAccordionButton(inGameControllerPanel, "Slider_TiltSensitivity", 0, 1, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameControllerPanel, "Slider_TiltDeadzone", 0, 2, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameControllerPanel, "Slider_CamSensitivity", 0, 3, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameControllerPanel, "Slider_JumpDistance", 0, 4, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
        }
        else if (tabIndex == 3 && inGameAudioPanel != null)
        {
            // Row 1: 3 款 BGM 曲目
            RegisterInGameAccordionButton(inGameAudioPanel, "Btn_Track0", 0, 1, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameAudioPanel, "Btn_Track1", 1, 1, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
            RegisterInGameAccordionButton(inGameAudioPanel, "Btn_Track2", 2, 1, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });

            // Row 2: 測試音效
            RegisterInGameAccordionButton(inGameAudioPanel, "Btn_TestSfx", 0, 2, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
        }

        // Row 5: 底部操作按鈕
        RegisterInGameAccordionButton(inGameSettingsPanel, "Btn_ResetDefaults", 0, 5, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });
        RegisterInGameAccordionButton(inGameSettingsPanel, "Btn_BackFromSettings", 1, 5, inGameSettingsButtons, (c, r) => { activeInGameNavCol = c; activeInGameNavRow = r; isInGameNavActive = true; });

        EnsurePanelTextsSafe(inGameSettingsPanel);
    }

    void RegisterInGameAccordionButton(GameObject panel, string btnName, int col, int row, List<InGameAccordionButton> list, System.Action<int, int> onHover)
    {
        Transform t = panel.transform.Find(btnName);
        if (t == null) return;

        Button btn = t.GetComponent<Button>();
        Slider sld = t.GetComponent<Slider>();
        RectTransform rt = t.GetComponent<RectTransform>();
        Image img = t.GetComponent<Image>();
        Text txt = t.GetComponentInChildren<Text>();

        if ((btn == null && sld == null) || rt == null) return;

        // Slider 本身沒有 Image：改用其 Background 作為高亮目標
        if (img == null && sld != null)
        {
            Transform bg = t.Find("Background");
            if (bg != null) img = bg.GetComponent<Image>();
        }

        // 文字防切核心加固：強制溢出模式
        if (txt != null)
        {
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
        }

        InGameAccordionButton item = new InGameAccordionButton();
        item.button = btn;
        item.slider = sld;
        item.rectTransform = rt;
        item.image = img;
        item.text = txt;
        item.basePos = rt.anchoredPosition;
        item.column = col;
        item.row = row;

        if (img != null) item.normalColor = img.color;
        if (sld != null) item.normalColor = new Color(0.12f, 0.16f, 0.25f, 0.9f); // 與 CreateSlider 的 Background 原色一致 (避免抓到尚未淡出的高亮色)

        EventTrigger trigger = t.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = t.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        int capturedCol = col;
        int capturedRow = row;
        enterEntry.callback.AddListener((data) => {
            if (onHover != null) onHover(capturedCol, capturedRow);
        });
        trigger.triggers.Add(enterEntry);

        list.Add(item);
    }

    void EnsureAllInGameButtonsTextSafe()
    {
        if (pausePanel != null) EnsurePanelTextsSafe(pausePanel);
        if (gameOverPanel != null) EnsurePanelTextsSafe(gameOverPanel);
        if (victoryPanel != null) EnsurePanelTextsSafe(victoryPanel);
        if (inGameSettingsPanel != null) EnsurePanelTextsSafe(inGameSettingsPanel);
    }

    void EnsurePanelTextsSafe(GameObject panel)
    {
        if (panel == null) return;
        Text[] allTexts = panel.GetComponentsInChildren<Text>(true);
        foreach (var txt in allTexts)
        {
            if (txt == null) continue;
            // 按鈕內部文字強制設定 Overflow，並確保寬度充裕絕不被裁剪
            if (txt.transform.parent != null && txt.transform.parent.GetComponent<Button>() != null)
            {
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                RectTransform trt = txt.GetComponent<RectTransform>();
                if (trt != null)
                {
                    trt.sizeDelta = new Vector2(Mathf.Max(trt.sizeDelta.x, 240f), Mathf.Max(trt.sizeDelta.y, 40f));
                }
            }
        }
    }

    void UpdateInGameAccordionLayout()
    {
        float dt = Time.unscaledDeltaTime;

        if (inGameSettingsPanel != null && inGameSettingsPanel.activeSelf)
        {
            UpdateInGameGroupAccordion(inGameSettingsButtons, activeInGameNavCol, activeInGameNavRow, dt, allowPush: false);
        }
        else if (pausePanel != null && pausePanel.activeSelf)
        {
            UpdateInGameGroupAccordion(pauseMenuButtons, activeInGameNavCol, activeInGameNavRow, dt, allowPush: true);
        }
        else if (gameOverPanel != null && gameOverPanel.activeSelf)
        {
            UpdateInGameGroupAccordion(gameOverMenuButtons, activeInGameNavCol, activeInGameNavRow, dt, allowPush: true);
        }
        else if (victoryPanel != null && victoryPanel.activeSelf)
        {
            UpdateInGameGroupAccordion(victoryMenuButtons, activeInGameNavCol, activeInGameNavRow, dt, allowPush: true);
        }
    }

    void UpdateInGameGroupAccordion(List<InGameAccordionButton> buttonList, int currentCol, int currentRow, float dt, bool allowPush = true)
    {
        for (int i = 0; i < buttonList.Count; i++)
        {
            var item = buttonList[i];
            if (item.rectTransform == null) continue;

            Vector2 targetPos = item.basePos;
            float targetScale = 1.0f;
            Color targetColor = item.normalColor;
            Color targetTextColor = item.normalTextColor;

            if (isInGameNavActive)
            {
                if (item.row == currentRow && item.column == currentCol)
                {
                    // 當前選中按鈕：
                    // 設定選單 (allowPush == false)：採用優雅就地高光微縮放 1.07x，保持多欄對齊不移位
                    // 一般選單 (allowPush == true)：居中放大 1.15x，發光高亮
                    targetScale = allowPush ? SELECTED_SCALE : 1.07f;
                    if (item.slider != null) targetScale = 1.0f; // Slider 只變色不縮放，避免拖曳時滑軌尺寸跳動
                    targetColor = item.highlightColor;
                    targetTextColor = item.highlightTextColor;
                    targetPos = item.basePos;
                }
                else if (allowPush)
                {
                    if (item.row == currentRow && item.column != currentCol)
                    {
                        // 同一行左右相鄰按鈕：向左右推開 16px 避免重疊
                        float sign = (item.column > currentCol) ? 1f : -1f;
                        targetPos = new Vector2(item.basePos.x + sign * 16f, item.basePos.y);
                    }
                    else if (item.row < currentRow)
                    {
                        targetScale = 1.0f;
                        targetPos = new Vector2(item.basePos.x, item.basePos.y + PUSH_OFFSET);
                    }
                    else if (item.row > currentRow)
                    {
                        targetScale = 1.0f;
                        targetPos = new Vector2(item.basePos.x, item.basePos.y - PUSH_OFFSET);
                    }
                }
            }

            item.rectTransform.anchoredPosition = Vector2.Lerp(item.rectTransform.anchoredPosition, targetPos, dt * ANIM_SPEED);
            item.rectTransform.localScale = Vector3.Lerp(item.rectTransform.localScale, new Vector3(targetScale, targetScale, 1f), dt * ANIM_SPEED);

            if (item.image != null)
                item.image.color = Color.Lerp(item.image.color, targetColor, dt * ANIM_SPEED);
            if (item.text != null)
                item.text.color = Color.Lerp(item.text.color, targetTextColor, dt * ANIM_SPEED);
        }
    }

    void HandleInGameNavigationInput()
    {
        bool hasActivePanel = (pausePanel != null && pausePanel.activeSelf) ||
                              (gameOverPanel != null && gameOverPanel.activeSelf) ||
                              (victoryPanel != null && victoryPanel.activeSelf) ||
                              (inGameSettingsPanel != null && inGameSettingsPanel.activeSelf);
        if (!hasActivePanel) return;

        if (inGameNavCooldownTimer > 0f)
        {
            inGameNavCooldownTimer -= Time.unscaledDeltaTime;
        }

        bool moveUp = false;
        bool moveDown = false;
        bool moveLeft = false;
        bool moveRight = false;
        bool confirm = false;

        // 1. 鍵盤輸入
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) moveUp = true;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) moveDown = true;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) moveLeft = true;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) moveRight = true;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) confirm = true;

        // 2. Arduino 實體搖桿輸入
        if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();
        if (arduino != null)
        {
            float joyX = arduino.joystick.x;
            float joyY = arduino.joystick.y;
            bool joyBtn = arduino.isButtonPressed;

            if (joyBtn && !lastInGameJoyBtnState)
            {
                confirm = true;
            }
            lastInGameJoyBtnState = joyBtn;

            if (inGameNavCooldownTimer <= 0f)
            {
                if (joyY < 320f) moveUp = true;
                else if (joyY > 680f) moveDown = true;

                if (joyX < 320f) moveLeft = true;
                else if (joyX > 680f) moveRight = true;
            }
        }

        // 3. 各面板導航分發
        if (inGameSettingsPanel != null && inGameSettingsPanel.activeSelf)
        {
            HandleInGameSettingsNavigation(moveUp, moveDown, moveLeft, moveRight, confirm);
        }
        else if (pausePanel != null && pausePanel.activeSelf)
        {
            HandleInGameGroupNavigation(moveUp, moveDown, false, false, confirm, 1, pauseMenuButtons.Count, pauseMenuButtons);
        }
        else if (gameOverPanel != null && gameOverPanel.activeSelf)
        {
            HandleInGameGroupNavigation(moveUp, moveDown, false, false, confirm, 1, gameOverMenuButtons.Count, gameOverMenuButtons);
        }
        else if (victoryPanel != null && victoryPanel.activeSelf)
        {
            HandleInGameGroupNavigation(moveUp, moveDown, false, false, confirm, 1, victoryMenuButtons.Count, victoryMenuButtons);
        }
    }

    void HandleInGameGroupNavigation(bool moveUp, bool moveDown, bool moveLeft, bool moveRight, bool confirm, int maxCols, int maxRows, List<InGameAccordionButton> group)
    {
        if (group == null || group.Count == 0 || maxRows <= 0) return;

        if (inGameNavCooldownTimer <= 0f)
        {
            if (moveUp)
            {
                activeInGameNavRow--;
                if (activeInGameNavRow < 0) activeInGameNavRow = maxRows - 1;
                inGameNavCooldownTimer = 0.22f;
                isInGameNavActive = true;
            }
            else if (moveDown)
            {
                activeInGameNavRow++;
                if (activeInGameNavRow >= maxRows) activeInGameNavRow = 0;
                inGameNavCooldownTimer = 0.22f;
                isInGameNavActive = true;
            }
        }

        if (confirm)
        {
            TriggerSelectedInGameButton(group, activeInGameNavCol, activeInGameNavRow);
        }
    }

    void HandleInGameSettingsNavigation(bool moveUp, bool moveDown, bool moveLeft, bool moveRight, bool confirm)
    {
        if (inGameSettingsButtons == null || inGameSettingsButtons.Count == 0) return;

        // 焦點在 Slider 上：左右鍵 (按住可連續) / 搖桿左右 直接調整數值，而不是移動欄位
        var focusedItem = (activeInGameNavRow != 0) ? inGameSettingsButtons.Find(b => b.row == activeInGameNavRow && b.column == activeInGameNavCol) : null;
        if (focusedItem != null && focusedItem.slider != null)
        {
            bool holdLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool holdRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            int dir = 0;
            if (moveLeft || (holdLeft && inGameNavCooldownTimer <= 0f)) dir = -1;
            else if (moveRight || (holdRight && inGameNavCooldownTimer <= 0f)) dir = 1;
            Slider s = focusedItem.slider;
            if (dir != 0 && s.interactable)
            {
                float step = s.wholeNumbers ? 1f : (s.maxValue - s.minValue) / 20f;
                s.value = Mathf.Clamp(s.value + dir * step, s.minValue, s.maxValue); // 觸發 onValueChanged → 更新文字、寫入 PlayerPrefs、ReloadSettings
                inGameNavCooldownTimer = 0.15f;
                isInGameNavActive = true;
            }
            moveLeft = false;
            moveRight = false;
        }

        if (inGameNavCooldownTimer <= 0f)
        {
            if (moveUp)
            {
                int targetRow = activeInGameNavRow - 1;
                while (targetRow >= 0 && !inGameSettingsButtons.Exists(b => b.row == targetRow))
                {
                    targetRow--;
                }
                if (targetRow < 0) targetRow = 5; // 循環到底部按鈕
                activeInGameNavRow = targetRow;

                var inRow = inGameSettingsButtons.FindAll(b => b.row == activeInGameNavRow);
                if (inRow.Count > 0 && !inRow.Exists(b => b.column == activeInGameNavCol))
                {
                    activeInGameNavCol = inRow[0].column;
                }
                inGameNavCooldownTimer = 0.22f;
                isInGameNavActive = true;
            }
            else if (moveDown)
            {
                int targetRow = activeInGameNavRow + 1;
                while (targetRow <= 5 && !inGameSettingsButtons.Exists(b => b.row == targetRow))
                {
                    targetRow++;
                }
                if (targetRow > 5) targetRow = 0; // 循環回頂部 Tab
                activeInGameNavRow = targetRow;

                var inRow = inGameSettingsButtons.FindAll(b => b.row == activeInGameNavRow);
                if (inRow.Count > 0 && !inRow.Exists(b => b.column == activeInGameNavCol))
                {
                    activeInGameNavCol = inRow[0].column;
                }
                inGameNavCooldownTimer = 0.22f;
                isInGameNavActive = true;
            }
            else if (moveLeft)
            {
                if (activeInGameNavRow == 0)
                {
                    if (activeInGameNavCol > 0)
                    {
                        activeInGameNavCol--;
                        SwitchInGameSettingsTab(activeInGameNavCol);
                        inGameNavCooldownTimer = 0.22f;
                        isInGameNavActive = true;
                    }
                }
                else
                {
                    var inRow = inGameSettingsButtons.FindAll(b => b.row == activeInGameNavRow);
                    inRow.Sort((a, b) => a.column.CompareTo(b.column));
                    int curIdx = inRow.FindIndex(b => b.column == activeInGameNavCol);
                    if (curIdx > 0)
                    {
                        activeInGameNavCol = inRow[curIdx - 1].column;
                        inGameNavCooldownTimer = 0.22f;
                        isInGameNavActive = true;
                    }
                }
            }
            else if (moveRight)
            {
                if (activeInGameNavRow == 0)
                {
                    if (activeInGameNavCol < 3)
                    {
                        activeInGameNavCol++;
                        SwitchInGameSettingsTab(activeInGameNavCol);
                        inGameNavCooldownTimer = 0.22f;
                        isInGameNavActive = true;
                    }
                }
                else
                {
                    var inRow = inGameSettingsButtons.FindAll(b => b.row == activeInGameNavRow);
                    inRow.Sort((a, b) => a.column.CompareTo(b.column));
                    int curIdx = inRow.FindIndex(b => b.column == activeInGameNavCol);
                    if (curIdx >= 0 && curIdx < inRow.Count - 1)
                    {
                        activeInGameNavCol = inRow[curIdx + 1].column;
                        inGameNavCooldownTimer = 0.22f;
                        isInGameNavActive = true;
                    }
                }
            }
        }

        if (confirm)
        {
            TriggerSelectedInGameButton(inGameSettingsButtons, activeInGameNavCol, activeInGameNavRow);
        }
    }

    void TriggerSelectedInGameButton(List<InGameAccordionButton> group, int col, int row)
    {
        var target = group.Find(b => b.column == col && b.row == row);
        if (target == null) target = group.Find(b => b.row == row);

        if (target != null && target.button != null && target.button.interactable)
        {
            Debug.Log($"🎮 [InGameNav] 觸發按鈕: ({target.button.name})");
            target.button.onClick.Invoke();
            inGameNavCooldownTimer = 0.28f;
        }
    }
}
