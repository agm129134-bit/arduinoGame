using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

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

    void Start()
    {
        isBuilderMode = (PlayerPrefs.GetInt("IsBuilderMode", 0) == 1);
        EnsureAllUI();
        BindAllButtonsRuntime();

        if (isBuilderMode)
        {
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

        UpdateGemUI();
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
    }

    void Update()
    {
        // 特殊除錯按鍵：自由增加寶石數量 (G: +50, F1: +100, F2: +500)
        if (Input.GetKeyDown(KeyCode.G)) AddDebugGems(50);
        if (Input.GetKeyDown(KeyCode.F1)) AddDebugGems(100);
        if (Input.GetKeyDown(KeyCode.F2)) AddDebugGems(500);

        if (isBuilderMode) return; // 建造模式由 TrackBuilderManager 掌管輸入

        // 快速重開鍵
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
            return;
        }

        // ESC 鍵處理：若設定面板開著則返回暫停面板，若已結算則回主選單，遊戲中則切換暫停面板
        if (Input.GetKeyDown(KeyCode.Escape))
        {
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
        }

        if (!isPaused && inGameSettingsPanel != null)
        {
            inGameSettingsPanel.SetActive(false);
        }

        Debug.Log(isPaused ? "[GameManager] 遊戲已暫停 (PAUSED)" : "[GameManager] 遊戲已繼續 (RESUMED)");
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1.0f;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (inGameSettingsPanel != null) inGameSettingsPanel.SetActive(false);
    }

    // --- 場景導航 ---
    public void RestartLevel()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1.0f;
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

        // 標題
        CreateText(panel.transform, "Title", "⚙ 遊戲設定 (SETTINGS)", 50, new Color(0f, 1f, 0.9f), new Vector2(0, 200), new Vector2(800, 70), font, FontStyle.Bold);
        CreateText(panel.transform, "Hint", "💡 即時調整遊戲音量與視窗顯示模式", 22, new Color(0.4f, 0.9f, 1f), new Vector2(0, 150), new Vector2(800, 40), font, FontStyle.Normal);

        // --- 音樂音量 ---
        CreateText(panel.transform, "Lbl_Bgm", "音樂音量 (BGM):", 24, Color.white, new Vector2(-220, 80), new Vector2(320, 45), font, FontStyle.Bold);
        sliderBgmVolume = CreateSlider(panel.transform, "Slider_Bgm", new Vector2(70, 80), new Vector2(240, 26), 0f, 1f, PlayerPrefs.GetFloat("BGM_Volume", 0.5f));
        txtBgmVolume = CreateText(panel.transform, "Txt_BgmVal", $"{sliderBgmVolume.value * 100f:F0}%", 24, Color.cyan, new Vector2(250, 80), new Vector2(90, 45), font, FontStyle.Bold);
        sliderBgmVolume.onValueChanged.AddListener(OnInGameBgmChanged);

        // --- 音效音量 ---
        CreateText(panel.transform, "Lbl_Sfx", "音效音量 (SFX):", 24, Color.white, new Vector2(-220, 15), new Vector2(320, 45), font, FontStyle.Bold);
        sliderSfxVolume = CreateSlider(panel.transform, "Slider_Sfx", new Vector2(70, 15), new Vector2(240, 26), 0f, 1f, PlayerPrefs.GetFloat("SFX_Volume", 0.5f));
        txtSfxVolume = CreateText(panel.transform, "Txt_SfxVal", $"{sliderSfxVolume.value * 100f:F0}%", 24, Color.cyan, new Vector2(250, 15), new Vector2(90, 45), font, FontStyle.Bold);
        sliderSfxVolume.onValueChanged.AddListener(OnInGameSfxChanged);

        // --- 全螢幕切換 ---
        CreateText(panel.transform, "Lbl_Screen", "顯示模式 (DISPLAY):", 24, Color.white, new Vector2(-220, -50), new Vector2(320, 45), font, FontStyle.Bold);
        bool isFull = Screen.fullScreen;
        GameObject btnFsObj = CreateStyledButton(panel.transform, "Btn_ToggleFullscreen", isFull ? "全螢幕 [開啟 ON]" : "全螢幕 [視窗 WINDOW]", new Vector2(160, -50), new Vector2(270, 50), 22, font);
        Button btnFs = btnFsObj.GetComponent<Button>();
        txtFullscreenState = btnFsObj.GetComponentInChildren<Text>();
        btnFs.onClick.AddListener(ToggleInGameFullscreen);

        // --- 返回按鈕 ---
        CreateStyledButton(panel.transform, "Btn_BackFromSettings", "⬅ 返回暫停選單 (BACK)", new Vector2(0, -145), new Vector2(400, 58), 26, font);

        panel.SetActive(false);
        return panel;
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
        bgImg.raycastTarget = false;
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

    void ToggleInGameFullscreen()
    {
        bool newFull = !Screen.fullScreen;
        Screen.fullScreen = newFull;
        PlayerPrefs.SetInt("Screen_Fullscreen", newFull ? 1 : 0);
        PlayerPrefs.Save();
        if (txtFullscreenState != null)
        {
            txtFullscreenState.text = newFull ? "全螢幕 [開啟 ON]" : "全螢幕 [視窗 WINDOW]";
        }
    }

    public void OpenInGameSettings()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (inGameSettingsPanel != null)
        {
            if (sliderBgmVolume != null) sliderBgmVolume.value = PlayerPrefs.GetFloat("BGM_Volume", 0.5f);
            if (sliderSfxVolume != null) sliderSfxVolume.value = PlayerPrefs.GetFloat("SFX_Volume", 0.5f);
            if (txtFullscreenState != null) txtFullscreenState.text = Screen.fullScreen ? "全螢幕 [開啟 ON]" : "全螢幕 [視窗 WINDOW]";
            inGameSettingsPanel.transform.SetAsLastSibling();
            inGameSettingsPanel.SetActive(true);
        }
    }

    public void CloseInGameSettings()
    {
        if (inGameSettingsPanel != null) inGameSettingsPanel.SetActive(false);
        if (pausePanel != null)
        {
            pausePanel.transform.SetAsLastSibling();
            pausePanel.SetActive(true);
        }
        PlayerPrefs.Save();
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

            if (progressText != null) progressText.text = $"⚡ 得分: {score}  |  🏆 最佳: {bestScore}";
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

        if (arduino != null && arduino.distance > 0)
        {
            hardwareStatusText.text = $"超音波測距: <color=yellow>{arduino.distance:F1} cm</color> (揮手跳躍) | 傾斜感應: <color=cyan>{arduino.acceleration.x:F2}</color> | <color=white>[ESC: 暫停選單]</color>";
        }
        else
        {
            hardwareStatusText.text = "鍵盤操作模式: [A/D: 左右移] | [Space: 跳躍] | [R: 重試] | <color=cyan>[ESC: 暫停 / 回到選單]</color>";
        }
    }

    public void AddGem()
    {
        collectedGems++;
        UpdateGemUI();
    }

    void UpdateGemUI()
    {
        if (gemText != null)
        {
            if (isEndlessMode)
            {
                gemText.text = $"💎 已收集: {collectedGems}";
            }
            else
            {
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

        if (pausePanel != null) pausePanel.SetActive(false);

        float currentDist = (ball != null) ? ball.position.z - startZ : 0f;

        if (gameOverPanel != null)
        {
            EnsureGameOverPanelButtons(gameOverPanel, GetDefaultFont());
            gameOverPanel.transform.SetAsLastSibling();
            gameOverPanel.SetActive(true);
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
        }

        AudioManager.Instance?.PlayVictory();
        Debug.Log($"[GameManager] 🎉 100% 完美通關第 {currentLevel} 關！");
    }
}
