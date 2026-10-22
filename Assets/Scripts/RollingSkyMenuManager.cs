using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RollingSkyMenuManager : MonoBehaviour
{
    [Header("Main Panels")]
    public GameObject mainMenuPanel;
    public GameObject levelSelectPanel;
    public GameObject settingsPanel;
    public GameObject shopPanel;

    [Header("Settings Sub-Tabs")]
    public GameObject displaySettingsPanel;
    public GameObject keyboardSettingsPanel;
    public GameObject controllerSettingsPanel;
    public GameObject audioSettingsPanel;
    public Button tabDisplayBtn;
    public Button tabKeyboardBtn;
    public Button tabControllerBtn;
    public Button tabAudioBtn;

    [Header("Display Settings UI")]
    public Button btnFullscreen;
    public Button btnWindowed;
    public Button btnResPrev;
    public Button btnResNext;
    public Text txtCurrentRes;
    public Button btnApplyRes;
    public Text txtDisplayStatus;
    public Button btnRes1080;
    public Button btnRes900;
    public Button btnRes768;
    public Button btnRes720;

    [Header("Keyboard Settings UI")]
    public Button btnKeyLeft;
    public Text txtKeyLeft;
    public Button btnKeyRight;
    public Text txtKeyRight;
    public Button btnKeyJump;
    public Text txtKeyJump;
    public Slider sliderKeyboardSpeed;
    public Text txtKeyboardSpeed;
    public Slider sliderMouseSensitivity;
    public Text txtMouseSensitivity;

    [Header("Controller Settings UI")]
    public InputField comPortInput;
    public Slider sliderTiltSensitivity;
    public Text txtTiltSensitivity;
    public Slider sliderTiltDeadzone;
    public Text txtTiltDeadzone;
    public Slider sliderJumpDistance;
    public Text txtJumpDistance;
    public Slider sliderCamSensitivity;
    public Text txtCamSensitivity;
    public Text hardwareStatusText;

    [Header("Audio Settings UI")]
    public Button btnBgmTrack0;
    public Button btnBgmTrack1;
    public Button btnBgmTrack2;
    public Text txtCurrentBgm;
    public Slider sliderBgmVolume;
    public Text txtBgmVolume;
    public Slider sliderSfxVolume;
    public Text txtSfxVolume;
    public Button btnTestSfx;

    [Header("Hardware Reference")]
    public ArduinoReceiver arduino;

    [Header("Scene Config")]
    public string gameSceneName = "RollingSkyScene";

    private GameObject currentActivePanel;
    private bool isRebinding = false;

    // 暫存鍵位
    private KeyCode currentKeyLeft = KeyCode.A;
    private KeyCode currentKeyRight = KeyCode.D;
    private KeyCode currentKeyJump = KeyCode.Space;
    private int currentBgmIndex = 0;

    [System.Serializable]
    public struct ResolutionOption
    {
        public int width;
        public int height;
        public ResolutionOption(int w, int h) { width = w; height = h; }
    }
    private List<ResolutionOption> resOptions = new List<ResolutionOption>();
    private int currentResIndex = 0;
    private int selectedWidth = 1920;
    private int selectedHeight = 1080;
    private bool isFullscreen = true;

    void Awake()
    {
        if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();
    }

    void Start()
    {
        // 確保 EventSystem 存在且擁有輸入模組
        if (EventSystem.current == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }

        // 確保 AudioManager 存在
        if (AudioManager.Instance == null && FindAnyObjectByType<AudioManager>() == null)
        {
            GameObject audioObj = new GameObject("AudioManager");
            audioObj.AddComponent<AudioManager>();
        }

        // 初始化解析度與全螢幕設定
        InitResolutionList();

        // Runtime UI 自癒補強：畫面與音訊設定分頁、排版修復、10關網格、外觀商店
        EnsureSettingsPanelsAndTabsUI();
        EnsureLevelSelect10Grid();
        EnsureMainMenuExtendedButtons();
        EnsureShopUI();

        // 動態綁定所有按鈕事件
        BindAllButtonsRuntime();

        // 載入保存的所有設定
        LoadAllSettings();

        // 初始顯示主面板
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (displaySettingsPanel != null) displaySettingsPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        currentActivePanel = mainMenuPanel;

        RefreshMainMenuStatus();
    }

    void Update()
    {
        // 特殊除錯按鍵：自由增加寶石數量 (G: +50, F1: +100, F2: +500)
        if (Input.GetKeyDown(KeyCode.G)) AddDebugGems(50);
        if (Input.GetKeyDown(KeyCode.F1)) AddDebugGems(100);
        if (Input.GetKeyDown(KeyCode.F2)) AddDebugGems(500);

        // 若在控制器設定分頁，即時刷新硬體感測器數值以供玩家校準
        if (settingsPanel != null && settingsPanel.activeInHierarchy && 
            controllerSettingsPanel != null && controllerSettingsPanel.activeInHierarchy)
        {
            UpdateHardwareReadout();
        }

        // ESC 快捷返回
        if (Input.GetKeyDown(KeyCode.Escape) && !isRebinding)
        {
            if (levelSelectPanel != null && levelSelectPanel.activeInHierarchy)
            {
                ShowPanel(mainMenuPanel);
            }
            else if (settingsPanel != null && settingsPanel.activeInHierarchy)
            {
                OnSaveSettings();
            }
            else if (shopPanel != null && shopPanel.activeInHierarchy)
            {
                ShowPanel(mainMenuPanel);
            }
        }
    }

    // ==================== 按鈕動態綁定核心 ====================
    void BindAllButtonsRuntime()
    {
        // 主面板按鈕
        BindButton(mainMenuPanel, "Btn_Start", OnStartGame);
        BindButton(mainMenuPanel, "Btn_Levels", OnOpenLevelSelect);
        BindButton(mainMenuPanel, "Btn_Endless", OnOpenEndlessMode);
        BindButton(mainMenuPanel, "Btn_SkyTower", OnOpenSkyTowerMode);
        BindButton(mainMenuPanel, "Btn_Builder", OnOpenTrackBuilder);
        BindButton(mainMenuPanel, "Btn_Shop", OnOpenShop);
        BindButton(mainMenuPanel, "Btn_Settings", OnOpenSettings);
        BindButton(mainMenuPanel, "Btn_Exit", OnExitGame);

        // 10 大關卡按鈕綁定
        if (levelSelectPanel != null)
        {
            for (int i = 1; i <= 10; i++)
            {
                int lvl = i;
                BindButton(levelSelectPanel, $"Btn_Lvl{lvl}", () => OnSelectLevel(lvl));
            }
            BindButton(levelSelectPanel, "Btn_Back", () => ShowPanel(mainMenuPanel));
        }

        // 設定面板 Tab 切換 (0: 畫面, 1: 鍵盤, 2: 控制器, 3: 音訊)
        if (tabDisplayBtn != null)
        {
            tabDisplayBtn.onClick.RemoveAllListeners();
            tabDisplayBtn.onClick.AddListener(() => SwitchSettingsTab(0));
        }
        else BindButton(settingsPanel, "Btn_TabDisplay", () => SwitchSettingsTab(0));

        if (tabKeyboardBtn != null)
        {
            tabKeyboardBtn.onClick.RemoveAllListeners();
            tabKeyboardBtn.onClick.AddListener(() => SwitchSettingsTab(1));
        }
        else BindButton(settingsPanel, "Btn_TabKeyboard", () => SwitchSettingsTab(1));

        if (tabControllerBtn != null)
        {
            tabControllerBtn.onClick.RemoveAllListeners();
            tabControllerBtn.onClick.AddListener(() => SwitchSettingsTab(2));
        }
        else BindButton(settingsPanel, "Btn_TabController", () => SwitchSettingsTab(2));

        if (tabAudioBtn != null)
        {
            tabAudioBtn.onClick.RemoveAllListeners();
            tabAudioBtn.onClick.AddListener(() => SwitchSettingsTab(3));
        }
        else BindButton(settingsPanel, "Btn_TabAudio", () => SwitchSettingsTab(3));

        // 畫面與顯示設定按鈕綁定
        if (btnFullscreen != null)
        {
            btnFullscreen.onClick.RemoveAllListeners();
            btnFullscreen.onClick.AddListener(() => SetFullscreen(true));
        }
        else BindButton(displaySettingsPanel, "Btn_Fullscreen", () => SetFullscreen(true));

        if (btnWindowed != null)
        {
            btnWindowed.onClick.RemoveAllListeners();
            btnWindowed.onClick.AddListener(() => SetFullscreen(false));
        }
        else BindButton(displaySettingsPanel, "Btn_Windowed", () => SetFullscreen(false));

        if (btnResPrev != null)
        {
            btnResPrev.onClick.RemoveAllListeners();
            btnResPrev.onClick.AddListener(OnResPrevClicked);
        }
        else BindButton(displaySettingsPanel, "Btn_ResPrev", OnResPrevClicked);

        if (btnResNext != null)
        {
            btnResNext.onClick.RemoveAllListeners();
            btnResNext.onClick.AddListener(OnResNextClicked);
        }
        else BindButton(displaySettingsPanel, "Btn_ResNext", OnResNextClicked);

        if (btnRes1080 != null)
        {
            btnRes1080.onClick.RemoveAllListeners();
            btnRes1080.onClick.AddListener(() => OnPresetClicked(1920, 1080));
        }
        else BindButton(displaySettingsPanel, "Btn_Res1080", () => OnPresetClicked(1920, 1080));

        if (btnRes900 != null)
        {
            btnRes900.onClick.RemoveAllListeners();
            btnRes900.onClick.AddListener(() => OnPresetClicked(1600, 900));
        }
        else BindButton(displaySettingsPanel, "Btn_Res900", () => OnPresetClicked(1600, 900));

        if (btnRes768 != null)
        {
            btnRes768.onClick.RemoveAllListeners();
            btnRes768.onClick.AddListener(() => OnPresetClicked(1366, 768));
        }
        else BindButton(displaySettingsPanel, "Btn_Res768", () => OnPresetClicked(1366, 768));

        if (btnRes720 != null)
        {
            btnRes720.onClick.RemoveAllListeners();
            btnRes720.onClick.AddListener(() => OnPresetClicked(1280, 720));
        }
        else BindButton(displaySettingsPanel, "Btn_Res720", () => OnPresetClicked(1280, 720));

        if (btnApplyRes != null)
        {
            btnApplyRes.onClick.RemoveAllListeners();
            btnApplyRes.onClick.AddListener(ApplyCurrentResolution);
        }
        else BindButton(displaySettingsPanel, "Btn_ApplyRes", ApplyCurrentResolution);

        // 鍵盤重新綁定按鈕
        if (btnKeyLeft != null)
        {
            btnKeyLeft.onClick.RemoveAllListeners();
            btnKeyLeft.onClick.AddListener(() => StartRebind("Key_Left"));
        }
        else BindButton(keyboardSettingsPanel, "Btn_KeyLeft", () => StartRebind("Key_Left"));

        if (btnKeyRight != null)
        {
            btnKeyRight.onClick.RemoveAllListeners();
            btnKeyRight.onClick.AddListener(() => StartRebind("Key_Right"));
        }
        else BindButton(keyboardSettingsPanel, "Btn_KeyRight", () => StartRebind("Key_Right"));

        if (btnKeyJump != null)
        {
            btnKeyJump.onClick.RemoveAllListeners();
            btnKeyJump.onClick.AddListener(() => StartRebind("Key_Jump"));
        }
        else BindButton(keyboardSettingsPanel, "Btn_KeyJump", () => StartRebind("Key_Jump"));

        // 鍵盤滑桿監聽
        if (sliderKeyboardSpeed != null)
        {
            sliderKeyboardSpeed.onValueChanged.RemoveAllListeners();
            sliderKeyboardSpeed.onValueChanged.AddListener(OnKeyboardSpeedChanged);
        }

        // 控制器滑桿監聽
        if (sliderTiltSensitivity != null)
        {
            sliderTiltSensitivity.onValueChanged.RemoveAllListeners();
            sliderTiltSensitivity.onValueChanged.AddListener(v => {
                if (txtTiltSensitivity != null) txtTiltSensitivity.text = v.ToString("F2");
            });
        }
        if (sliderTiltDeadzone != null)
        {
            sliderTiltDeadzone.onValueChanged.RemoveAllListeners();
            sliderTiltDeadzone.onValueChanged.AddListener(v => {
                if (txtTiltDeadzone != null) txtTiltDeadzone.text = v.ToString("F2");
            });
        }
        if (sliderJumpDistance != null)
        {
            sliderJumpDistance.onValueChanged.RemoveAllListeners();
            sliderJumpDistance.onValueChanged.AddListener(v => {
                if (txtJumpDistance != null) txtJumpDistance.text = $"{v:F0} cm";
            });
        }
        if (sliderCamSensitivity != null)
        {
            sliderCamSensitivity.onValueChanged.RemoveAllListeners();
            sliderCamSensitivity.onValueChanged.AddListener(v => {
                if (txtCamSensitivity != null) txtCamSensitivity.text = $"{v:F1}x";
            });
        }

        // 音訊設定按鈕與滑桿
        if (btnBgmTrack0 != null)
        {
            btnBgmTrack0.onClick.RemoveAllListeners();
            btnBgmTrack0.onClick.AddListener(() => SelectBgmTrack(0));
        }
        else BindButton(audioSettingsPanel, "Btn_BgmTrack0", () => SelectBgmTrack(0));

        if (btnBgmTrack1 != null)
        {
            btnBgmTrack1.onClick.RemoveAllListeners();
            btnBgmTrack1.onClick.AddListener(() => SelectBgmTrack(1));
        }
        else BindButton(audioSettingsPanel, "Btn_BgmTrack1", () => SelectBgmTrack(1));

        if (btnBgmTrack2 != null)
        {
            btnBgmTrack2.onClick.RemoveAllListeners();
            btnBgmTrack2.onClick.AddListener(() => SelectBgmTrack(2));
        }
        else BindButton(audioSettingsPanel, "Btn_BgmTrack2", () => SelectBgmTrack(2));

        if (sliderBgmVolume != null)
        {
            sliderBgmVolume.onValueChanged.RemoveAllListeners();
            sliderBgmVolume.onValueChanged.AddListener(OnBgmVolumeChanged);
        }
        if (sliderSfxVolume != null)
        {
            sliderSfxVolume.onValueChanged.RemoveAllListeners();
            sliderSfxVolume.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (btnTestSfx != null)
        {
            btnTestSfx.onClick.RemoveAllListeners();
            btnTestSfx.onClick.AddListener(() => AudioManager.Instance?.PlayGem());
        }
        else BindButton(audioSettingsPanel, "Btn_TestSfx", () => AudioManager.Instance?.PlayGem());

        // 設定面板底層儲存與重置按鈕
        BindButton(settingsPanel, "Btn_ResetDefaults", OnResetDefaults);
        BindButton(settingsPanel, "Btn_ResetAllSave", OnResetAllSave);
        BindButton(settingsPanel, "Btn_SaveBack", OnSaveSettings);

        // 商店返回按鈕
        if (shopPanel != null)
        {
            BindButton(shopPanel, "Btn_ShopBack", () => ShowPanel(mainMenuPanel));
        }
    }

    void BindButton(GameObject parent, string btnName, UnityEngine.Events.UnityAction action)
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

    // ==================== 10 大關卡選單排版與動態生成 ====================
    void EnsureLevelSelect10Grid()
    {
        if (levelSelectPanel == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 標題
        Transform tTitle = levelSelectPanel.transform.Find("Title");
        if (tTitle != null)
        {
            Text t = tTitle.GetComponent<Text>();
            if (t != null) { t.text = "SELECT LEVEL (10 大挑戰關卡)"; t.fontSize = 54; }
            RectTransform rt = tTitle.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, 360);
        }

        // 2 欄 × 5 列乾淨網格佈局，徹底解決按鈕重疊問題
        string[] levelNames = new string[]
        {
            "LV.1 霓虹大道 (Neon Avenue) ★",
            "LV.2 虛空躍升 (Void Leap) ★",
            "LV.3 疾速迴旋 (Speed Spiral) ★★",
            "LV.4 斷崖連跳 (Chasm Bounce) ★★",
            "LV.5 迷宮尖刺 (Spike Labyrinth) ★★★",
            "LV.6 升降律動 (Piston Pulse) ★★★",
            "LV.7 浮島穿梭 (Floating Islands) ★★★★",
            "LV.8 重力逆襲 (Gravity Shift) ★★★★",
            "LV.9 混沌風暴 (Chaos Vortex) ★★★★★",
            "LV.10 終極躍動 (Apex Transcendence) ★★★★★"
        };

        float[] yPositions = new float[] { 220, 135, 50, -35, -120 };

        for (int i = 1; i <= 10; i++)
        {
            string btnName = $"Btn_Lvl{i}";
            int colIndex = (i <= 5) ? 0 : 1;
            int rowIndex = (i <= 5) ? (i - 1) : (i - 6);
            float posX = (colIndex == 0) ? -330 : 330;
            float posY = yPositions[rowIndex];

            Transform tBtn = levelSelectPanel.transform.Find(btnName);
            bool isCompleted = (PlayerPrefs.GetInt($"Level_{i}_Completed", 0) == 1);
            string label = levelNames[i - 1] + (isCompleted ? "  [✓ CLEAR]" : "");

            if (tBtn == null)
            {
                CreateStyledButton(levelSelectPanel.transform, btnName, label, new Vector2(posX, posY), new Vector2(620, 68), 22, font);
            }
            else
            {
                RectTransform rt = tBtn.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(posX, posY);
                rt.sizeDelta = new Vector2(620, 68);
                Text txt = tBtn.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.text = label;
                    txt.fontSize = 22;
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }
        }

        // 返回按鈕
        Transform tBack = levelSelectPanel.transform.Find("Btn_Back");
        if (tBack != null)
        {
            RectTransform rt = tBack.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, -260);
            rt.sizeDelta = new Vector2(360, 65);
        }
    }

    // ==================== 主選單擴充按鈕 (無盡/自訂/商店) ====================
    void EnsureMainMenuExtendedButtons()
    {
        if (mainMenuPanel == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 重新排版主選單按鈕，分為左右兩大功能面板 (左四個、右四個完美對稱排版，高度70，無重疊)
        // 左側：四大遊玩模式 (X = -220)
        RepositionButton(mainMenuPanel, "Btn_Start", "開始第一關 (START GAME)", new Vector2(-220, 70), new Vector2(400, 70), 28, font);
        RepositionButton(mainMenuPanel, "Btn_Levels", "選擇關卡 (LEVEL SELECT)", new Vector2(-220, -15), new Vector2(400, 70), 28, font);
        RepositionButton(mainMenuPanel, "Btn_Endless", "無盡極限跑酷 (ENDLESS MODE)", new Vector2(-220, -100), new Vector2(400, 70), 26, font);
        RepositionButton(mainMenuPanel, "Btn_SkyTower", "通天天梯 (SKY TOWER)", new Vector2(-220, -185), new Vector2(400, 70), 26, font);

        // 右側：系統、創造與商店 (X = 220)
        RepositionButton(mainMenuPanel, "Btn_Shop", "球體皮膚商店 (SKIN SHOP)", new Vector2(220, 70), new Vector2(400, 70), 28, font);
        RepositionButton(mainMenuPanel, "Btn_Builder", "自訂場地編輯器 (TRACK BUILDER)", new Vector2(220, -15), new Vector2(400, 70), 24, font);
        RepositionButton(mainMenuPanel, "Btn_Settings", "系統設定 (SETTINGS)", new Vector2(220, -100), new Vector2(400, 70), 30, font);
        RepositionButton(mainMenuPanel, "Btn_Exit", "離開遊戲 (EXIT)", new Vector2(220, -185), new Vector2(400, 70), 30, font);

        // 頂部玩家資產與紀錄看板
        Transform tStatus = mainMenuPanel.transform.Find("PlayerStatusText");
        if (tStatus == null)
        {
            CreateText(mainMenuPanel.transform, "PlayerStatusText", "", 24, Color.yellow, new Vector2(0, 160), new Vector2(900, 50), font, FontStyle.Bold);
        }
    }

    void RepositionButton(GameObject panel, string name, string label, Vector2 pos, Vector2 size, int fontSize, Font font)
    {
        Transform t = panel.transform.Find(name);
        if (t == null)
        {
            CreateStyledButton(panel.transform, name, label, pos, size, fontSize, font);
        }
        else
        {
            RectTransform rt = t.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Text txt = t.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = label;
                txt.fontSize = fontSize;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }
    }

    void RefreshMainMenuStatus()
    {
        if (mainMenuPanel == null) return;
        Transform t = mainMenuPanel.transform.Find("PlayerStatusText");
        if (t != null)
        {
            Text txt = t.GetComponent<Text>();
            if (txt != null)
            {
                int gems = PlayerPrefs.GetInt("TotalGems", 0);
                int highScore = PlayerPrefs.GetInt("Endless_HighScore", 0);
                int towerHeight = PlayerPrefs.GetInt("SkyTower_BestHeight", 0);
                txt.text = $"💎 水晶: <color=cyan>{gems}</color>   |   🏆 無盡最高: <color=yellow>{highScore}</color> 分   |   🗼 天梯最高: <color=lime>{towerHeight}m</color>";
            }
        }
    }

    // ==================== 外觀皮膚商店系統 (Shop UI) ====================
    void EnsureShopUI()
    {
        if (shopPanel != null) return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        shopPanel = new GameObject("ShopPanel");
        shopPanel.transform.SetParent(canvas.transform, false);
        Image bg = shopPanel.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.12f, 0.96f);
        RectTransform rt = shopPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;

        CreateText(shopPanel.transform, "Title", "BALL SKIN SHOP (球體皮膚商店)", 56, new Color(0f, 1f, 0.9f), new Vector2(0, 370), new Vector2(900, 80), font, FontStyle.Bold);
        CreateText(shopPanel.transform, "Txt_ShopGems", "", 28, Color.yellow, new Vector2(0, 300), new Vector2(800, 50), font, FontStyle.Bold);

        string[] skinNames = new string[] { "經典電藍 (Classic Blue)", "霓虹烈焰 (Neon Flame)", "賽博矩陣 (Cyber Matrix)", "黃金至尊 (Golden Spark)", "銀河虛空 (Void Galaxy)" };
        int[] skinPrices = new int[] { 0, 20, 40, 70, 100 };
        Color[] skinColors = new Color[] { new Color(0f, 0.8f, 1f), new Color(1f, 0.3f, 0.1f), new Color(0.1f, 1f, 0.4f), new Color(1f, 0.9f, 0.2f), new Color(0.7f, 0.2f, 1f) };

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            float y = 180 - (i * 90);
            CreateText(shopPanel.transform, $"Label_Skin_{i}", $"【{skinNames[i]}】 需 {skinPrices[i]} 水晶", 24, skinColors[i], new Vector2(-150, y), new Vector2(500, 60), font, FontStyle.Bold);
            Button btn = CreateStyledButton(shopPanel.transform, $"Btn_Skin_{i}", "購買 / 裝備", new Vector2(250, y), new Vector2(240, 60), 22, font);
            btn.onClick.AddListener(() => OnSkinButtonClicked(idx, skinPrices[idx]));
        }

        CreateStyledButton(shopPanel.transform, "Btn_ShopBack", "← 返回主選單 (BACK)", new Vector2(0, -380), new Vector2(360, 65), 26, font);
        shopPanel.SetActive(false);
    }

    void RefreshShopUI()
    {
        if (shopPanel == null) return;
        int totalGems = PlayerPrefs.GetInt("TotalGems", 0);
        int equippedSkin = PlayerPrefs.GetInt("EquippedBallSkin", 0);

        Transform tGems = shopPanel.transform.Find("Txt_ShopGems");
        if (tGems != null)
        {
            Text txt = tGems.GetComponent<Text>();
            if (txt != null) txt.text = $"💎 目前持有水晶: <color=cyan>{totalGems}</color>";
        }

        int[] skinPrices = new int[] { 0, 20, 40, 70, 100 };
        for (int i = 0; i < 5; i++)
        {
            Transform tBtn = shopPanel.transform.Find($"Btn_Skin_{i}");
            if (tBtn != null)
            {
                Text txt = tBtn.GetComponentInChildren<Text>();
                bool isUnlocked = (i == 0) || (PlayerPrefs.GetInt($"Skin_Unlocked_{i}", 0) == 1);
                bool isEquipped = (equippedSkin == i);

                if (isEquipped)
                {
                    txt.text = "✓ 已裝備 (EQUIPPED)";
                    txt.color = Color.green;
                }
                else if (isUnlocked)
                {
                    txt.text = "點擊裝備 (EQUIP)";
                    txt.color = Color.white;
                }
                else
                {
                    txt.text = $"購買 ({skinPrices[i]} 💎)";
                    txt.color = (totalGems >= skinPrices[i]) ? Color.yellow : Color.gray;
                }
            }
        }
    }

    void OnSkinButtonClicked(int skinIndex, int price)
    {
        bool isUnlocked = (skinIndex == 0) || (PlayerPrefs.GetInt($"Skin_Unlocked_{skinIndex}", 0) == 1);
        int equippedSkin = PlayerPrefs.GetInt("EquippedBallSkin", 0);

        if (isUnlocked)
        {
            // 裝備此皮膚
            PlayerPrefs.SetInt("EquippedBallSkin", skinIndex);
            PlayerPrefs.Save();
            AudioManager.Instance?.PlayEquip();
            RefreshShopUI();
            return;
        }

        // 嘗試購買
        int totalGems = PlayerPrefs.GetInt("TotalGems", 0);
        if (totalGems >= price)
        {
            totalGems -= price;
            PlayerPrefs.SetInt("TotalGems", totalGems);
            PlayerPrefs.SetInt($"Skin_Unlocked_{skinIndex}", 1);
            PlayerPrefs.SetInt("EquippedBallSkin", skinIndex);
            PlayerPrefs.Save();
            AudioManager.Instance?.PlayBuy();
            RefreshShopUI();
            RefreshMainMenuStatus();
        }
        else
        {
            Debug.LogWarning("[Shop] 水晶不足，快去各關卡或無盡模式收集更多水晶！");
        }
    }

    // ==================== 設定面板排版與動態分頁生成 ====================
    void EnsureSettingsPanelsAndTabsUI()
    {
        if (settingsPanel == null) return;

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. 頂部 4 大分頁按鈕：寬度 260，間距 20，水平對稱，字體 21，開啟 Overflow 徹底避免折行！
        Transform tTabDisplay = settingsPanel.transform.Find("Btn_TabDisplay");
        Transform tTabKey = settingsPanel.transform.Find("Btn_TabKeyboard");
        Transform tTabCtrl = settingsPanel.transform.Find("Btn_TabController");
        Transform tTabAudio = settingsPanel.transform.Find("Btn_TabAudio");

        if (tTabDisplay == null)
        {
            tabDisplayBtn = CreateStyledButton(settingsPanel.transform, "Btn_TabDisplay", "【 畫面設定 DISPLAY 】", new Vector2(-420, 310), new Vector2(260, 60), 21, defaultFont);
        }
        else
        {
            RectTransform rt = tTabDisplay.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-420, 310);
            rt.sizeDelta = new Vector2(260, 60);
            Text txt = tTabDisplay.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "【 畫面設定 DISPLAY 】"; txt.fontSize = 21; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
            tabDisplayBtn = tTabDisplay.GetComponent<Button>();
        }

        if (tTabKey != null)
        {
            RectTransform rtKey = tTabKey.GetComponent<RectTransform>();
            rtKey.anchoredPosition = new Vector2(-140, 310);
            rtKey.sizeDelta = new Vector2(260, 60);
            Text txt = tTabKey.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "【 鍵盤 / 滑鼠 KEYBOARD 】"; txt.fontSize = 20; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
            tabKeyboardBtn = tTabKey.GetComponent<Button>();
        }

        if (tTabCtrl != null)
        {
            RectTransform rtCtrl = tTabCtrl.GetComponent<RectTransform>();
            rtCtrl.anchoredPosition = new Vector2(140, 310);
            rtCtrl.sizeDelta = new Vector2(260, 60);
            Text txt = tTabCtrl.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "【 控制器 CONTROLLER 】"; txt.fontSize = 21; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
            tabControllerBtn = tTabCtrl.GetComponent<Button>();
        }

        if (tTabAudio == null)
        {
            tabAudioBtn = CreateStyledButton(settingsPanel.transform, "Btn_TabAudio", "【 音訊設定 AUDIO 】", new Vector2(420, 310), new Vector2(260, 60), 21, defaultFont);
        }
        else
        {
            RectTransform rtAudio = tTabAudio.GetComponent<RectTransform>();
            rtAudio.anchoredPosition = new Vector2(420, 310);
            rtAudio.sizeDelta = new Vector2(260, 60);
            Text txt = tTabAudio.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "【 音訊設定 AUDIO 】"; txt.fontSize = 21; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
            tabAudioBtn = tTabAudio.GetComponent<Button>();
        }

        // 2. 底部操作按鈕：寬度充裕，單行排列，不重疊
        Transform tReset = settingsPanel.transform.Find("Btn_ResetDefaults");
        if (tReset != null)
        {
            RectTransform rt = tReset.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-360, -380);
            rt.sizeDelta = new Vector2(280, 65);
            Text txt = tReset.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "恢復控制預設 (RESET)"; txt.fontSize = 20; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
        }

        Transform tResetSave = settingsPanel.transform.Find("Btn_ResetAllSave");
        if (tResetSave == null)
        {
            Button btnReset = CreateStyledButton(settingsPanel.transform, "Btn_ResetAllSave", "⚠️ 重置全部存檔 (RESET ALL SAVE)", new Vector2(0, -380), new Vector2(330, 65), 18, defaultFont);
            Image img = btnReset.GetComponent<Image>();
            if (img != null) img.color = new Color(0.6f, 0.12f, 0.15f, 0.95f);
            ColorBlock cb = btnReset.colors;
            cb.normalColor = new Color(0.6f, 0.15f, 0.18f);
            cb.highlightedColor = new Color(0.9f, 0.2f, 0.25f);
            cb.pressedColor = new Color(0.4f, 0.08f, 0.1f);
            btnReset.colors = cb;
        }
        else
        {
            RectTransform rt = tResetSave.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, -380);
            rt.sizeDelta = new Vector2(330, 65);
            Text txt = tResetSave.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "⚠️ 重置全部存檔 (RESET ALL SAVE)"; txt.fontSize = 18; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
        }

        Transform tSave = settingsPanel.transform.Find("Btn_SaveBack");
        if (tSave != null)
        {
            RectTransform rt = tSave.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(360, -380);
            rt.sizeDelta = new Vector2(280, 65);
            Text txt = tSave.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "儲存並返回 (SAVE & BACK)"; txt.fontSize = 20; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
        }

        // 3. 確保 DisplaySettingsPanel 存在
        Transform tDisplayPanel = settingsPanel.transform.Find("DisplaySettingsPanel");
        if (tDisplayPanel == null)
        {
            displaySettingsPanel = BuildDynamicDisplaySettingsPanel(settingsPanel.transform, defaultFont);
        }
        else
        {
            displaySettingsPanel = tDisplayPanel.gameObject;
            HookDisplayUIReferences(displaySettingsPanel);
        }

        // 4. 確保 AudioSettingsPanel 存在
        Transform tAudioPanel = settingsPanel.transform.Find("AudioSettingsPanel");
        if (tAudioPanel == null)
        {
            audioSettingsPanel = BuildDynamicAudioSettingsPanel(settingsPanel.transform, defaultFont);
        }
        else
        {
            audioSettingsPanel = tAudioPanel.gameObject;
            HookAudioUIReferences(audioSettingsPanel);
        }

        // 5. 重新排版並確保 鍵盤/滑鼠分頁 與 控制器分頁 的靈敏度元件存在，保證完全不重疊
        RepositionAndEnsureKeyboardUI();
        RepositionAndEnsureControllerUI();
    }

    void RepositionAndEnsureKeyboardUI()
    {
        if (keyboardSettingsPanel == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 重新排版 KeyboardSettingsPanel，各行間隔 65px
        SetChildPos(keyboardSettingsPanel, "LeftLabel", new Vector2(-200, 165));
        SetChildPos(keyboardSettingsPanel, "Btn_KeyLeft", new Vector2(160, 165));

        SetChildPos(keyboardSettingsPanel, "RightLabel", new Vector2(-200, 100));
        SetChildPos(keyboardSettingsPanel, "Btn_KeyRight", new Vector2(160, 100));

        SetChildPos(keyboardSettingsPanel, "JumpLabel", new Vector2(-200, 35));
        SetChildPos(keyboardSettingsPanel, "Btn_KeyJump", new Vector2(160, 35));

        SetChildPos(keyboardSettingsPanel, "SpeedLabel", new Vector2(-200, -30));
        SetChildPos(keyboardSettingsPanel, "Slider_KeyboardSpeed", new Vector2(120, -30));
        SetChildPos(keyboardSettingsPanel, "Txt_KeySpeed", new Vector2(290, -30));

        // 確保新增「滑鼠視角靈敏度 (MOUSE LOOK)」
        Transform tMouseSens = keyboardSettingsPanel.transform.Find("Slider_MouseSens");
        if (tMouseSens != null)
        {
            sliderMouseSensitivity = tMouseSens.GetComponent<Slider>();
            SetChildPos(keyboardSettingsPanel, "Lbl_MouseSens", new Vector2(-200, -95));
            SetChildPos(keyboardSettingsPanel, "Slider_MouseSens", new Vector2(120, -95));
            SetChildPos(keyboardSettingsPanel, "Txt_MouseSensVal", new Vector2(290, -95));
            Transform tVal = keyboardSettingsPanel.transform.Find("Txt_MouseSensVal");
            if (tVal != null) txtMouseSensitivity = tVal.GetComponent<Text>();
        }
        else
        {
            CreateText(keyboardSettingsPanel.transform, "Lbl_MouseSens", "滑鼠視角靈敏度 (MOUSE LOOK):", 24, Color.white, new Vector2(-200, -95), new Vector2(360, 42), font, FontStyle.Bold);
            sliderMouseSensitivity = CreateSlider(keyboardSettingsPanel.transform, "Slider_MouseSens", new Vector2(120, -95), new Vector2(240, 26), 0.5f, 5.0f, PlayerPrefs.GetFloat("Camera_Sensitivity", 2.0f));
            txtMouseSensitivity = CreateText(keyboardSettingsPanel.transform, "Txt_MouseSensVal", $"{sliderMouseSensitivity.value:F1}x", 24, Color.cyan, new Vector2(290, -95), new Vector2(90, 42), font, FontStyle.Bold);
        }

        SetChildPos(keyboardSettingsPanel, "Hint", new Vector2(0, -165));

        if (sliderMouseSensitivity != null)
        {
            sliderMouseSensitivity.onValueChanged.RemoveAllListeners();
            sliderMouseSensitivity.onValueChanged.AddListener(v => {
                if (txtMouseSensitivity != null) txtMouseSensitivity.text = $"{v:F1}x";
                if (sliderCamSensitivity != null) sliderCamSensitivity.value = v;
                PlayerPrefs.SetFloat("Camera_Sensitivity", v);
            });
        }
    }

    void RepositionAndEnsureControllerUI()
    {
        if (controllerSettingsPanel == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 重新排版 ControllerSettingsPanel，嚴格統一行距 60px，徹底避免重疊
        SetChildPos(controllerSettingsPanel, "PortLabel", new Vector2(-200, 190));
        SetChildPos(controllerSettingsPanel, "Input_COMPort", new Vector2(150, 190));

        SetChildPos(controllerSettingsPanel, "TiltSensLabel", new Vector2(-200, 130));
        SetChildPos(controllerSettingsPanel, "Slider_TiltSens", new Vector2(120, 130));
        SetChildPos(controllerSettingsPanel, "Txt_TiltSens", new Vector2(290, 130));

        SetChildPos(controllerSettingsPanel, "DeadzoneLabel", new Vector2(-200, 70));
        SetChildPos(controllerSettingsPanel, "Slider_Deadzone", new Vector2(120, 70));
        SetChildPos(controllerSettingsPanel, "Txt_Deadzone", new Vector2(290, 70));

        // 視角轉向靈敏度放在 Y = 10
        Transform tSens = controllerSettingsPanel.transform.Find("Slider_CamSensitivity");
        if (tSens != null)
        {
            sliderCamSensitivity = tSens.GetComponent<Slider>();
            SetChildPos(controllerSettingsPanel, "Lbl_CamSensitivity", new Vector2(-200, 10));
            SetChildPos(controllerSettingsPanel, "Slider_CamSensitivity", new Vector2(120, 10));
            SetChildPos(controllerSettingsPanel, "Txt_CamSensVal", new Vector2(290, 10));
            Transform tVal = controllerSettingsPanel.transform.Find("Txt_CamSensVal");
            if (tVal != null) txtCamSensitivity = tVal.GetComponent<Text>();
        }
        else
        {
            CreateText(controllerSettingsPanel.transform, "Lbl_CamSensitivity", "視角轉向靈敏度 (LOOK SENS):", 24, Color.white, new Vector2(-200, 10), new Vector2(360, 42), font, FontStyle.Bold);
            sliderCamSensitivity = CreateSlider(controllerSettingsPanel.transform, "Slider_CamSensitivity", new Vector2(120, 10), new Vector2(240, 26), 0.5f, 5.0f, PlayerPrefs.GetFloat("Camera_Sensitivity", 2.0f));
            txtCamSensitivity = CreateText(controllerSettingsPanel.transform, "Txt_CamSensVal", $"{sliderCamSensitivity.value:F1}x", 24, Color.cyan, new Vector2(290, 10), new Vector2(90, 42), font, FontStyle.Bold);
        }

        SetChildPos(controllerSettingsPanel, "JumpDistLabel", new Vector2(-200, -50));
        SetChildPos(controllerSettingsPanel, "Slider_JumpDist", new Vector2(120, -50));
        SetChildPos(controllerSettingsPanel, "Txt_JumpDist", new Vector2(290, -50));

        Transform hwBox = controllerSettingsPanel.transform.Find("HardwareBox");
        if (hwBox != null)
        {
            RectTransform rt = hwBox.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0, -150);
                rt.sizeDelta = new Vector2(860, 95);
            }
        }

        if (sliderCamSensitivity != null)
        {
            sliderCamSensitivity.onValueChanged.RemoveAllListeners();
            sliderCamSensitivity.onValueChanged.AddListener(v => {
                if (txtCamSensitivity != null) txtCamSensitivity.text = $"{v:F1}x";
                if (sliderMouseSensitivity != null) sliderMouseSensitivity.value = v;
                PlayerPrefs.SetFloat("Camera_Sensitivity", v);
            });
        }
    }

    void SetChildPos(GameObject parent, string childName, Vector2 anchoredPos)
    {
        if (parent == null) return;
        Transform t = parent.transform.Find(childName);
        if (t != null)
        {
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = anchoredPos;
        }
    }

    void EnsureAudioSettingsUI()
    {
        EnsureSettingsPanelsAndTabsUI();
    }

    GameObject BuildDynamicDisplaySettingsPanel(Transform parent, Font font)
    {
        GameObject pDisplay = new GameObject("DisplaySettingsPanel");
        pDisplay.transform.SetParent(parent, false);
        Image img = pDisplay.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = false;

        RectTransform rt = pDisplay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;

        // 1. 全螢幕與視窗化開關
        CreateText(pDisplay.transform, "Lbl_ScreenMode", "顯示模式 (SCREEN MODE):", 26, Color.white, new Vector2(0, 205), new Vector2(800, 40), font, FontStyle.Bold);
        btnFullscreen = CreateStyledButton(pDisplay.transform, "Btn_Fullscreen", "🖥️ 全螢幕模式 (FULLSCREEN)", new Vector2(-170, 145), new Vector2(320, 60), 22, font);
        btnWindowed = CreateStyledButton(pDisplay.transform, "Btn_Windowed", "🪟 視窗化模式 (WINDOWED)", new Vector2(170, 145), new Vector2(320, 60), 22, font);

        // 2. 解析度選擇器
        CreateText(pDisplay.transform, "Lbl_Resolution", "螢幕解析度 (RESOLUTION):", 26, Color.white, new Vector2(0, 65), new Vector2(800, 40), font, FontStyle.Bold);
        btnResPrev = CreateStyledButton(pDisplay.transform, "Btn_ResPrev", "◀ 上一個", new Vector2(-240, 5), new Vector2(150, 55), 22, font);
        txtCurrentRes = CreateText(pDisplay.transform, "Txt_CurrentRes", "1920 × 1080", 28, Color.cyan, new Vector2(0, 5), new Vector2(300, 55), font, FontStyle.Bold);
        btnResNext = CreateStyledButton(pDisplay.transform, "Btn_ResNext", "下一個 ▶", new Vector2(240, 5), new Vector2(150, 55), 22, font);

        // 3. 推薦解析度快速按鈕
        btnRes1080 = CreateStyledButton(pDisplay.transform, "Btn_Res1080", "1920 × 1080", new Vector2(-315, -70), new Vector2(190, 50), 20, font);
        btnRes900 = CreateStyledButton(pDisplay.transform, "Btn_Res900", "1600 × 900", new Vector2(-105, -70), new Vector2(190, 50), 20, font);
        btnRes768 = CreateStyledButton(pDisplay.transform, "Btn_Res768", "1366 × 768", new Vector2(105, -70), new Vector2(190, 50), 20, font);
        btnRes720 = CreateStyledButton(pDisplay.transform, "Btn_Res720", "1280 × 720", new Vector2(315, -70), new Vector2(190, 50), 20, font);

        // 4. 立即套用按鈕
        btnApplyRes = CreateStyledButton(pDisplay.transform, "Btn_ApplyRes", "✔ 套用解析度設定 (APPLY RESOLUTION)", new Vector2(0, -145), new Vector2(400, 55), 22, font);
        ColorBlock cb = btnApplyRes.colors;
        cb.normalColor = new Color(0.1f, 0.45f, 0.35f);
        cb.highlightedColor = new Color(0.15f, 0.65f, 0.5f);
        cb.pressedColor = new Color(0.08f, 0.3f, 0.22f);
        btnApplyRes.colors = cb;

        // 5. 當前狀態顯示文字
        txtDisplayStatus = CreateText(pDisplay.transform, "Txt_DisplayStatus", "", 22, Color.yellow, new Vector2(0, -220), new Vector2(900, 40), font, FontStyle.Normal);

        pDisplay.SetActive(false);
        return pDisplay;
    }

    void HookDisplayUIReferences(GameObject panel)
    {
        if (panel == null) return;
        Transform tf = panel.transform.Find("Btn_Fullscreen");
        if (tf != null) btnFullscreen = tf.GetComponent<Button>();
        Transform tw = panel.transform.Find("Btn_Windowed");
        if (tw != null) btnWindowed = tw.GetComponent<Button>();

        Transform tp = panel.transform.Find("Btn_ResPrev");
        if (tp != null) btnResPrev = tp.GetComponent<Button>();
        Transform tn = panel.transform.Find("Btn_ResNext");
        if (tn != null) btnResNext = tn.GetComponent<Button>();

        Transform tc = panel.transform.Find("Txt_CurrentRes");
        if (tc != null) txtCurrentRes = tc.GetComponent<Text>();

        Transform ta = panel.transform.Find("Btn_ApplyRes");
        if (ta != null) btnApplyRes = ta.GetComponent<Button>();

        Transform ts = panel.transform.Find("Txt_DisplayStatus");
        if (ts != null) txtDisplayStatus = ts.GetComponent<Text>();

        Transform t1080 = panel.transform.Find("Btn_Res1080");
        if (t1080 != null) btnRes1080 = t1080.GetComponent<Button>();
        Transform t900 = panel.transform.Find("Btn_Res900");
        if (t900 != null) btnRes900 = t900.GetComponent<Button>();
        Transform t768 = panel.transform.Find("Btn_Res768");
        if (t768 != null) btnRes768 = t768.GetComponent<Button>();
        Transform t720 = panel.transform.Find("Btn_Res720");
        if (t720 != null) btnRes720 = t720.GetComponent<Button>();
    }

    GameObject BuildDynamicAudioSettingsPanel(Transform parent, Font font)
    {
        GameObject pAudio = new GameObject("AudioSettingsPanel");
        pAudio.transform.SetParent(parent, false);
        Image img = pAudio.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = false;

        RectTransform rt = pAudio.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;

        CreateText(pAudio.transform, "Lbl_Track", "BGM 背景音樂選擇:", 28, Color.white, new Vector2(-240, 190), new Vector2(420, 50), font, FontStyle.Bold);
        btnBgmTrack0 = CreateStyledButton(pAudio.transform, "Btn_BgmTrack0", "Ver A: 霓虹賽博 (Cyber Neon)", new Vector2(-280, 115), new Vector2(340, 60), 20, font);
        btnBgmTrack1 = CreateStyledButton(pAudio.transform, "Btn_BgmTrack1", "Ver B: 復古晶片 (Chiptune Rush)", new Vector2(70, 115), new Vector2(340, 60), 20, font);
        btnBgmTrack2 = CreateStyledButton(pAudio.transform, "Btn_BgmTrack2", "Ver C: 深空重力 (Deep Gravity)", new Vector2(420, 115), new Vector2(340, 60), 20, font);

        txtCurrentBgm = CreateText(pAudio.transform, "Txt_CurrentBgm", "當前播放: Ver A: 霓虹賽博", 24, Color.cyan, new Vector2(0, 45), new Vector2(800, 40), font, FontStyle.Normal);

        CreateText(pAudio.transform, "Lbl_BgmVol", "音樂音量 (BGM VOLUME):", 28, Color.white, new Vector2(-240, -40), new Vector2(420, 50), font, FontStyle.Bold);
        sliderBgmVolume = CreateSlider(pAudio.transform, "Slider_BgmVolume", new Vector2(100, -40), new Vector2(320, 30), 0f, 1f, 0.5f);
        txtBgmVolume = CreateText(pAudio.transform, "Txt_BgmVolVal", "50%", 26, Color.cyan, new Vector2(320, -40), new Vector2(100, 50), font, FontStyle.Bold);

        CreateText(pAudio.transform, "Lbl_SfxVol", "音效音量 (SFX VOLUME):", 28, Color.white, new Vector2(-240, -120), new Vector2(420, 50), font, FontStyle.Bold);
        sliderSfxVolume = CreateSlider(pAudio.transform, "Slider_SfxVolume", new Vector2(100, -120), new Vector2(320, 30), 0f, 1f, 0.5f);
        txtSfxVolume = CreateText(pAudio.transform, "Txt_SfxVolVal", "50%", 26, Color.cyan, new Vector2(320, -120), new Vector2(100, 50), font, FontStyle.Bold);

        btnTestSfx = CreateStyledButton(pAudio.transform, "Btn_TestSfx", "🔊 試聽音效 (TEST SFX)", new Vector2(0, -210), new Vector2(320, 55), 22, font);

        pAudio.SetActive(false);
        return pAudio;
    }

    void HookAudioUIReferences(GameObject panel)
    {
        Transform t0 = panel.transform.Find("Btn_BgmTrack0");
        if (t0 != null) btnBgmTrack0 = t0.GetComponent<Button>();
        Transform t1 = panel.transform.Find("Btn_BgmTrack1");
        if (t1 != null) btnBgmTrack1 = t1.GetComponent<Button>();
        Transform t2 = panel.transform.Find("Btn_BgmTrack2");
        if (t2 != null) btnBgmTrack2 = t2.GetComponent<Button>();

        Transform tCur = panel.transform.Find("Txt_CurrentBgm");
        if (tCur != null) txtCurrentBgm = tCur.GetComponent<Text>();

        Transform tBgmS = panel.transform.Find("Slider_BgmVolume");
        if (tBgmS != null) sliderBgmVolume = tBgmS.GetComponent<Slider>();
        Transform tBgmV = panel.transform.Find("Txt_BgmVolVal");
        if (tBgmV != null) txtBgmVolume = tBgmV.GetComponent<Text>();

        Transform tSfxS = panel.transform.Find("Slider_SfxVolume");
        if (tSfxS != null) sliderSfxVolume = tSfxS.GetComponent<Slider>();
        Transform tSfxV = panel.transform.Find("Txt_SfxVolVal");
        if (tSfxV != null) txtSfxVolume = tSfxV.GetComponent<Text>();

        Transform tTest = panel.transform.Find("Btn_TestSfx");
        if (tTest != null) btnTestSfx = tTest.GetComponent<Button>();
    }

    // ==================== 頁面導航 ====================
    public void OnStartGame()
    {
        ClearEventFocus();
        PlayerPrefs.SetInt("SelectedLevel", 1);
        PlayerPrefs.SetInt("IsSkyTowerMode", 0);
        PlayerPrefs.SetInt("IsEndlessMode", 0);
        PlayerPrefs.SetInt("IsBuilderMode", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOpenLevelSelect()
    {
        ShowPanel(levelSelectPanel);
    }

    public void OnOpenEndlessMode()
    {
        ClearEventFocus();
        PlayerPrefs.SetInt("IsEndlessMode", 1);
        PlayerPrefs.SetInt("IsSkyTowerMode", 0);
        PlayerPrefs.SetInt("IsBuilderMode", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOpenSkyTowerMode()
    {
        ClearEventFocus();
        PlayerPrefs.SetInt("IsSkyTowerMode", 1);
        PlayerPrefs.SetInt("IsEndlessMode", 0);
        PlayerPrefs.SetInt("IsBuilderMode", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOpenTrackBuilder()
    {
        ClearEventFocus();
        PlayerPrefs.SetInt("IsBuilderMode", 1);
        PlayerPrefs.SetInt("IsSkyTowerMode", 0);
        PlayerPrefs.SetInt("IsEndlessMode", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOpenShop()
    {
        ShowPanel(shopPanel);
        RefreshShopUI();
    }

    public void OnOpenSettings()
    {
        ShowPanel(settingsPanel);
        SwitchSettingsTab(0);
    }

    public void OnSelectLevel(int levelIndex)
    {
        ClearEventFocus();
        PlayerPrefs.SetInt("SelectedLevel", levelIndex);
        PlayerPrefs.SetInt("IsSkyTowerMode", 0);
        PlayerPrefs.SetInt("IsEndlessMode", 0);
        PlayerPrefs.SetInt("IsBuilderMode", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnExitGame()
    {
        Debug.Log("[Menu] 退出遊戲！");
        Application.Quit();
    }

    public void ShowPanel(GameObject panelToShow)
    {
        ClearEventFocus();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
            currentActivePanel = panelToShow;
        }

        if (panelToShow == mainMenuPanel)
        {
            RefreshMainMenuStatus();
        }
    }

    public void SwitchSettingsTab(int tabIndex)
    {
        ClearEventFocus();
        if (displaySettingsPanel != null) displaySettingsPanel.SetActive(tabIndex == 0);
        if (keyboardSettingsPanel != null) keyboardSettingsPanel.SetActive(tabIndex == 1);
        if (controllerSettingsPanel != null) controllerSettingsPanel.SetActive(tabIndex == 2);
        if (audioSettingsPanel != null) audioSettingsPanel.SetActive(tabIndex == 3);

        Color activeCol = new Color(0f, 0.5f, 0.9f);
        Color normalCol = new Color(0.1f, 0.22f, 0.38f);

        if (tabDisplayBtn != null) SetBtnNormalColor(tabDisplayBtn, (tabIndex == 0) ? activeCol : normalCol);
        if (tabKeyboardBtn != null) SetBtnNormalColor(tabKeyboardBtn, (tabIndex == 1) ? activeCol : normalCol);
        if (tabControllerBtn != null) SetBtnNormalColor(tabControllerBtn, (tabIndex == 2) ? activeCol : normalCol);
        if (tabAudioBtn != null) SetBtnNormalColor(tabAudioBtn, (tabIndex == 3) ? activeCol : normalCol);

        if (tabIndex == 0)
        {
            UpdateDisplayUI();
        }
    }

    // ==================== 音訊設定控制 ====================
    public void SelectBgmTrack(int trackIndex)
    {
        currentBgmIndex = trackIndex;
        AudioManager.Instance?.PlayBgmTrack(trackIndex);
        UpdateBgmUI();
    }

    void OnBgmVolumeChanged(float val)
    {
        if (txtBgmVolume != null) txtBgmVolume.text = $"{val * 100f:F0}%";
        AudioManager.Instance?.SetBgmVolume(val);
    }

    void OnSfxVolumeChanged(float val)
    {
        if (txtSfxVolume != null) txtSfxVolume.text = $"{val * 100f:F0}%";
        AudioManager.Instance?.SetSfxVolume(val);
    }

    void UpdateBgmUI()
    {
        string[] titles = new string[] { "Ver A: 霓虹賽博", "Ver B: 復古晶片", "Ver C: 深空重力" };
        if (txtCurrentBgm != null)
        {
            txtCurrentBgm.text = $"當前播放: {titles[Mathf.Clamp(currentBgmIndex, 0, 2)]}";
        }

        Color activeCol = new Color(0f, 0.6f, 0.95f);
        Color normalCol = new Color(0.1f, 0.22f, 0.38f);

        if (btnBgmTrack0 != null) SetBtnNormalColor(btnBgmTrack0, currentBgmIndex == 0 ? activeCol : normalCol);
        if (btnBgmTrack1 != null) SetBtnNormalColor(btnBgmTrack1, currentBgmIndex == 1 ? activeCol : normalCol);
        if (btnBgmTrack2 != null) SetBtnNormalColor(btnBgmTrack2, currentBgmIndex == 2 ? activeCol : normalCol);
    }

    void SetBtnNormalColor(Button btn, Color c)
    {
        ColorBlock cb = btn.colors;
        cb.normalColor = c;
        btn.colors = cb;
    }

    // ==================== 按鍵重新綁定 ====================
    public void StartRebind(string actionKey)
    {
        if (isRebinding) return;
        ClearEventFocus();
        StartCoroutine(RebindRoutine(actionKey));
    }

    IEnumerator RebindRoutine(string actionKey)
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
                        if (actionKey == "Key_Left") currentKeyLeft = kc;
                        else if (actionKey == "Key_Right") currentKeyRight = kc;
                        else if (actionKey == "Key_Jump") currentKeyJump = kc;

                        keyAssigned = true;
                        break;
                    }
                }
            }
            yield return null;
        }

        UpdateKeyTexts();
        isRebinding = false;
    }

    void UpdateKeyTexts()
    {
        if (txtKeyLeft != null) txtKeyLeft.text = currentKeyLeft.ToString();
        if (txtKeyRight != null) txtKeyRight.text = currentKeyRight.ToString();
        if (txtKeyJump != null) txtKeyJump.text = currentKeyJump.ToString();
    }

    void OnKeyboardSpeedChanged(float val)
    {
        if (txtKeyboardSpeed != null) txtKeyboardSpeed.text = val.ToString("F1");
    }

    // ==================== 設定存取 ====================
    public void OnSaveSettings()
    {
        ClearEventFocus();

        // 儲存畫面設定
        ApplyCurrentResolution();

        PlayerPrefs.SetInt("Key_Left", (int)currentKeyLeft);
        PlayerPrefs.SetInt("Key_Right", (int)currentKeyRight);
        PlayerPrefs.SetInt("Key_Jump", (int)currentKeyJump);
        if (sliderKeyboardSpeed != null) PlayerPrefs.SetFloat("Keyboard_Speed", sliderKeyboardSpeed.value);

        if (comPortInput != null && !string.IsNullOrEmpty(comPortInput.text))
        {
            PlayerPrefs.SetString("COM_Port", comPortInput.text.Trim());
        }
        if (sliderTiltSensitivity != null) PlayerPrefs.SetFloat("Tilt_Sensitivity", sliderTiltSensitivity.value);
        if (sliderTiltDeadzone != null) PlayerPrefs.SetFloat("Tilt_Deadzone", sliderTiltDeadzone.value);
        if (sliderJumpDistance != null) PlayerPrefs.SetFloat("Ultrasonic_Jump_Distance", sliderJumpDistance.value);
        if (sliderCamSensitivity != null) PlayerPrefs.SetFloat("Camera_Sensitivity", sliderCamSensitivity.value);

        PlayerPrefs.SetInt("BGM_Track_Index", currentBgmIndex);
        if (sliderBgmVolume != null) PlayerPrefs.SetFloat("BGM_Volume", sliderBgmVolume.value);
        if (sliderSfxVolume != null) PlayerPrefs.SetFloat("SFX_Volume", sliderSfxVolume.value);

        PlayerPrefs.Save();
        Debug.Log("🎉 所有設定已成功保存至 PlayerPrefs！");

        ShowPanel(mainMenuPanel);
    }

    public void OnResetDefaults()
    {
        ClearEventFocus();

        // 1. 重設畫面為原生解析度全螢幕
        selectedWidth = (Screen.currentResolution.width > 0) ? Screen.currentResolution.width : 1920;
        selectedHeight = (Screen.currentResolution.height > 0) ? Screen.currentResolution.height : 1080;
        isFullscreen = true;
        currentResIndex = resOptions.FindIndex(o => o.width == selectedWidth && o.height == selectedHeight);
        ApplyCurrentResolution();

        // 2. 控制預設
        currentKeyLeft = KeyCode.A;
        currentKeyRight = KeyCode.D;
        currentKeyJump = KeyCode.Space;
        UpdateKeyTexts();
        if (sliderKeyboardSpeed != null) sliderKeyboardSpeed.value = 9.0f;

        if (comPortInput != null) comPortInput.text = "COM3";
        if (sliderTiltSensitivity != null) sliderTiltSensitivity.value = 1.8f;
        if (sliderTiltDeadzone != null) sliderTiltDeadzone.value = 0.08f;
        if (sliderJumpDistance != null) sliderJumpDistance.value = 20.0f;
        if (sliderCamSensitivity != null)
        {
            sliderCamSensitivity.value = 2.0f;
            if (txtCamSensitivity != null) txtCamSensitivity.text = "2.0x";
        }
        if (sliderMouseSensitivity != null)
        {
            sliderMouseSensitivity.value = 2.0f;
            if (txtMouseSensitivity != null) txtMouseSensitivity.text = "2.0x";
        }

        // 3. 音效預設
        currentBgmIndex = 0;
        if (sliderBgmVolume != null) sliderBgmVolume.value = 0.5f;
        if (sliderSfxVolume != null) sliderSfxVolume.value = 0.5f;
        SelectBgmTrack(0);

        Debug.Log("⚙️ 所有設定已重設為出廠最佳預設值！");
    }

    public void AddDebugGems(int count)
    {
        int totalGems = PlayerPrefs.GetInt("TotalGems", 0) + count;
        PlayerPrefs.SetInt("TotalGems", totalGems);
        PlayerPrefs.Save();
        RefreshMainMenuStatus();
        RefreshShopUI();
        Debug.Log($"💎 [Debug] 寶石已增加 {count}！當前累計總寶石: {totalGems}");
    }

    public void OnResetAllSave()
    {
        ClearEventFocus();
        Debug.Log("⚠️ 正在執行全域存檔重置 (RESET ALL SAVE)...");

        // 1. 寶石歸零
        PlayerPrefs.SetInt("TotalGems", 0);

        // 2. 無盡模式最高分歸零
        PlayerPrefs.SetInt("Endless_HighScore", 0);

        // 3. 所有 10 關通關狀態歸零
        for (int i = 1; i <= 10; i++)
        {
            PlayerPrefs.SetInt($"Level_{i}_Completed", 0);
        }

        // 4. 重置皮膚解鎖 (第0套預設解鎖，其餘鎖定)
        PlayerPrefs.SetInt("Skin_Unlocked_0", 1);
        for (int i = 1; i <= 4; i++)
        {
            PlayerPrefs.SetInt($"Skin_Unlocked_{i}", 0);
        }
        PlayerPrefs.SetInt("EquippedBallSkin", 0);

        PlayerPrefs.Save();

        // 5. 立即刷新各 UI
        RefreshMainMenuStatus();
        EnsureLevelSelect10Grid();
        RefreshShopUI();

        Debug.Log("✅ 全域存檔重置完成！所有關卡通關狀態、無盡高分、皮膚解鎖與寶石已全數重置為初始狀態。");
    }

    void LoadAllSettings()
    {
        // 載入畫面顯示設定
        selectedWidth = PlayerPrefs.GetInt("Screen_Width", Screen.currentResolution.width > 0 ? Screen.currentResolution.width : 1920);
        selectedHeight = PlayerPrefs.GetInt("Screen_Height", Screen.currentResolution.height > 0 ? Screen.currentResolution.height : 1080);
        isFullscreen = PlayerPrefs.GetInt("Screen_Fullscreen", 1) == 1;
        currentResIndex = resOptions.FindIndex(o => o.width == selectedWidth && o.height == selectedHeight);
        UpdateDisplayUI();

        currentKeyLeft = (KeyCode)PlayerPrefs.GetInt("Key_Left", (int)KeyCode.A);
        currentKeyRight = (KeyCode)PlayerPrefs.GetInt("Key_Right", (int)KeyCode.D);
        currentKeyJump = (KeyCode)PlayerPrefs.GetInt("Key_Jump", (int)KeyCode.Space);
        UpdateKeyTexts();

        if (sliderKeyboardSpeed != null)
        {
            sliderKeyboardSpeed.value = PlayerPrefs.GetFloat("Keyboard_Speed", 9.0f);
            if (txtKeyboardSpeed != null) txtKeyboardSpeed.text = sliderKeyboardSpeed.value.ToString("F1");
        }

        if (comPortInput != null)
        {
            comPortInput.text = PlayerPrefs.GetString("COM_Port", "COM3");
        }
        if (sliderTiltSensitivity != null)
        {
            sliderTiltSensitivity.value = PlayerPrefs.GetFloat("Tilt_Sensitivity", 1.8f);
            if (txtTiltSensitivity != null) txtTiltSensitivity.text = sliderTiltSensitivity.value.ToString("F2");
        }
        if (sliderTiltDeadzone != null)
        {
            sliderTiltDeadzone.value = PlayerPrefs.GetFloat("Tilt_Deadzone", 0.08f);
            if (txtTiltDeadzone != null) txtTiltDeadzone.text = sliderTiltDeadzone.value.ToString("F2");
        }
        if (sliderJumpDistance != null)
        {
            sliderJumpDistance.value = PlayerPrefs.GetFloat("Ultrasonic_Jump_Distance", 20.0f);
            if (txtJumpDistance != null) txtJumpDistance.text = $"{sliderJumpDistance.value:F0} cm";
        }
        float camSens = PlayerPrefs.GetFloat("Camera_Sensitivity", 2.0f);
        if (sliderCamSensitivity != null)
        {
            sliderCamSensitivity.value = camSens;
            if (txtCamSensitivity != null) txtCamSensitivity.text = $"{camSens:F1}x";
        }
        if (sliderMouseSensitivity != null)
        {
            sliderMouseSensitivity.value = camSens;
            if (txtMouseSensitivity != null) txtMouseSensitivity.text = $"{camSens:F1}x";
        }

        currentBgmIndex = PlayerPrefs.GetInt("BGM_Track_Index", 0);
        if (sliderBgmVolume != null)
        {
            sliderBgmVolume.value = PlayerPrefs.GetFloat("BGM_Volume", 0.5f);
            if (txtBgmVolume != null) txtBgmVolume.text = $"{sliderBgmVolume.value * 100f:F0}%";
        }
        if (sliderSfxVolume != null)
        {
            sliderSfxVolume.value = PlayerPrefs.GetFloat("SFX_Volume", 0.5f);
            if (txtSfxVolume != null) txtSfxVolume.text = $"{sliderSfxVolume.value * 100f:F0}%";
        }
        UpdateBgmUI();
    }

    // ==================== 畫面顯示控制邏輯 ====================
    void InitResolutionList()
    {
        resOptions.Clear();

        if (Screen.resolutions != null && Screen.resolutions.Length > 0)
        {
            foreach (var r in Screen.resolutions)
            {
                if (r.width >= 1024 && r.height >= 600)
                {
                    if (!resOptions.Exists(o => o.width == r.width && o.height == r.height))
                    {
                        resOptions.Add(new ResolutionOption(r.width, r.height));
                    }
                }
            }
        }

        int[,] standard = new int[,] {
            { 1280, 720 },
            { 1366, 768 },
            { 1600, 900 },
            { 1920, 1080 },
            { 2560, 1440 },
            { 3840, 2160 }
        };
        for (int i = 0; i < standard.GetLength(0); i++)
        {
            int w = standard[i, 0];
            int h = standard[i, 1];
            if (!resOptions.Exists(o => o.width == w && o.height == h))
            {
                resOptions.Add(new ResolutionOption(w, h));
            }
        }

        resOptions.Sort((a, b) => {
            int cmp = a.width.CompareTo(b.width);
            return cmp != 0 ? cmp : a.height.CompareTo(b.height);
        });

        selectedWidth = PlayerPrefs.GetInt("Screen_Width", Screen.currentResolution.width > 0 ? Screen.currentResolution.width : 1920);
        selectedHeight = PlayerPrefs.GetInt("Screen_Height", Screen.currentResolution.height > 0 ? Screen.currentResolution.height : 1080);
        isFullscreen = PlayerPrefs.GetInt("Screen_Fullscreen", 1) == 1;

        currentResIndex = resOptions.FindIndex(o => o.width == selectedWidth && o.height == selectedHeight);
        if (currentResIndex < 0)
        {
            resOptions.Add(new ResolutionOption(selectedWidth, selectedHeight));
            resOptions.Sort((a, b) => a.width.CompareTo(b.width));
            currentResIndex = resOptions.FindIndex(o => o.width == selectedWidth && o.height == selectedHeight);
        }
    }

    public void SetFullscreen(bool fullscreen)
    {
        isFullscreen = fullscreen;
        Screen.SetResolution(selectedWidth, selectedHeight, isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        PlayerPrefs.SetInt("Screen_Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
        UpdateDisplayUI();
        Debug.Log($"🖥️ [Display] 切換顯示模式: {(isFullscreen ? "全螢幕 (FULLSCREEN)" : "視窗化 (WINDOWED)")}");
    }

    void OnResPrevClicked()
    {
        if (resOptions.Count == 0) return;
        currentResIndex--;
        if (currentResIndex < 0) currentResIndex = resOptions.Count - 1;
        selectedWidth = resOptions[currentResIndex].width;
        selectedHeight = resOptions[currentResIndex].height;
        UpdateResolutionText();
        UpdateDisplayUI();
    }

    void OnResNextClicked()
    {
        if (resOptions.Count == 0) return;
        currentResIndex++;
        if (currentResIndex >= resOptions.Count) currentResIndex = 0;
        selectedWidth = resOptions[currentResIndex].width;
        selectedHeight = resOptions[currentResIndex].height;
        UpdateResolutionText();
        UpdateDisplayUI();
    }

    void OnPresetClicked(int w, int h)
    {
        selectedWidth = w;
        selectedHeight = h;
        currentResIndex = resOptions.FindIndex(o => o.width == w && o.height == h);
        ApplyCurrentResolution();
    }

    public void ApplyCurrentResolution()
    {
        Screen.SetResolution(selectedWidth, selectedHeight, isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        PlayerPrefs.SetInt("Screen_Width", selectedWidth);
        PlayerPrefs.SetInt("Screen_Height", selectedHeight);
        PlayerPrefs.SetInt("Screen_Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
        UpdateDisplayUI();
        Debug.Log($"🖥️ [Display] 已套用解析度: {selectedWidth} × {selectedHeight}, 模式: {(isFullscreen ? "全螢幕" : "視窗化")}");
    }

    void UpdateDisplayUI()
    {
        UpdateResolutionText();

        Color activeCol = new Color(0f, 0.65f, 1f);
        Color normalCol = new Color(0.1f, 0.22f, 0.38f);

        if (btnFullscreen != null) SetBtnNormalColor(btnFullscreen, isFullscreen ? activeCol : normalCol);
        if (btnWindowed != null) SetBtnNormalColor(btnWindowed, !isFullscreen ? activeCol : normalCol);

        HighlightPresetBtn(btnRes1080, 1920, 1080);
        HighlightPresetBtn(btnRes900, 1600, 900);
        HighlightPresetBtn(btnRes768, 1366, 768);
        HighlightPresetBtn(btnRes720, 1280, 720);

        if (txtDisplayStatus != null)
        {
            string modeStr = isFullscreen ? "全螢幕 (FULLSCREEN)" : "視窗化 (WINDOWED)";
            txtDisplayStatus.text = $"★ 生效解析度: <color=cyan>{Screen.width} × {Screen.height}</color>  |  模式: <color=yellow>{modeStr}</color>";
        }
    }

    void HighlightPresetBtn(Button btn, int w, int h)
    {
        if (btn == null) return;
        bool isMatch = (selectedWidth == w && selectedHeight == h);
        SetBtnNormalColor(btn, isMatch ? new Color(0f, 0.65f, 1f) : new Color(0.1f, 0.22f, 0.38f));
    }

    void UpdateResolutionText()
    {
        if (txtCurrentRes != null)
        {
            txtCurrentRes.text = $"{selectedWidth} × {selectedHeight}";
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ApplySavedScreenSettingsOnBoot()
    {
        if (PlayerPrefs.HasKey("Screen_Width") && PlayerPrefs.HasKey("Screen_Height"))
        {
            int w = PlayerPrefs.GetInt("Screen_Width", 1920);
            int h = PlayerPrefs.GetInt("Screen_Height", 1080);
            bool fs = PlayerPrefs.GetInt("Screen_Fullscreen", 1) == 1;
            if (w > 0 && h > 0)
            {
                Screen.SetResolution(w, h, fs ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
        }
    }

    void UpdateHardwareReadout()
    {
        if (hardwareStatusText == null) return;
        if (arduino != null && arduino.distance > 0)
        {
            hardwareStatusText.text = $"• 實體裝置連線中 (Port: {arduino.portName})\n" +
                                      $"超音波測距: {arduino.distance:F1} cm (上下揮手測試起跳)\n" +
                                      $"MPU-6050 傾角: X={arduino.acceleration.x:F2}, Y={arduino.acceleration.y:F2}";
            hardwareStatusText.color = Color.green;
        }
        else
        {
            hardwareStatusText.text = "• 尚未偵測到實體硬體 (純鍵盤測試模式運作中)";
            hardwareStatusText.color = Color.yellow;
        }
    }

    void ClearEventFocus()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    Button CreateStyledButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, int fontSize, Font font)
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

        return btn;
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
}
