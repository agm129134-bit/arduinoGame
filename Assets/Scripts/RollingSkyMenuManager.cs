using System.Collections;
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
    public GameObject keyboardSettingsPanel;
    public GameObject controllerSettingsPanel;
    public GameObject audioSettingsPanel;
    public Button tabKeyboardBtn;
    public Button tabControllerBtn;
    public Button tabAudioBtn;

    [Header("Keyboard Settings UI")]
    public Button btnKeyLeft;
    public Text txtKeyLeft;
    public Button btnKeyRight;
    public Text txtKeyRight;
    public Button btnKeyJump;
    public Text txtKeyJump;
    public Slider sliderKeyboardSpeed;
    public Text txtKeyboardSpeed;

    [Header("Controller Settings UI")]
    public InputField comPortInput;
    public Slider sliderTiltSensitivity;
    public Text txtTiltSensitivity;
    public Slider sliderTiltDeadzone;
    public Text txtTiltDeadzone;
    public Slider sliderJumpDistance;
    public Text txtJumpDistance;
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

        // Runtime UI 自癒補強：排版修復、10關網格、外觀商店
        EnsureAudioSettingsUI();
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

        // 設定面板 Tab 切換
        if (tabKeyboardBtn != null)
        {
            tabKeyboardBtn.onClick.RemoveAllListeners();
            tabKeyboardBtn.onClick.AddListener(() => SwitchSettingsTab(0));
        }
        else BindButton(settingsPanel, "Btn_TabKeyboard", () => SwitchSettingsTab(0));

        if (tabControllerBtn != null)
        {
            tabControllerBtn.onClick.RemoveAllListeners();
            tabControllerBtn.onClick.AddListener(() => SwitchSettingsTab(1));
        }
        else BindButton(settingsPanel, "Btn_TabController", () => SwitchSettingsTab(1));

        if (tabAudioBtn != null)
        {
            tabAudioBtn.onClick.RemoveAllListeners();
            tabAudioBtn.onClick.AddListener(() => SwitchSettingsTab(2));
        }
        else BindButton(settingsPanel, "Btn_TabAudio", () => SwitchSettingsTab(2));

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

        // 重新排版主選單按鈕，分為左右兩大功能面板，互不干涉重疊
        // 左側：模式選擇
        RepositionButton(mainMenuPanel, "Btn_Start", "開始第一關 (START GAME)", new Vector2(-220, 70), new Vector2(400, 70), 30, font);
        RepositionButton(mainMenuPanel, "Btn_Levels", "選擇關卡 (LEVEL SELECT)", new Vector2(-220, -15), new Vector2(400, 70), 30, font);
        RepositionButton(mainMenuPanel, "Btn_Endless", "無盡極限跑酷 (ENDLESS MODE)", new Vector2(-220, -100), new Vector2(400, 70), 26, font);
        RepositionButton(mainMenuPanel, "Btn_Builder", "自訂場地編輯器 (TRACK BUILDER)", new Vector2(-220, -185), new Vector2(400, 70), 24, font);

        // 右側：系統與商店
        RepositionButton(mainMenuPanel, "Btn_Shop", "球體皮膚商店 (SKIN SHOP)", new Vector2(220, 70), new Vector2(400, 70), 28, font);
        RepositionButton(mainMenuPanel, "Btn_Settings", "系統設定 (SETTINGS)", new Vector2(220, -15), new Vector2(400, 70), 30, font);
        RepositionButton(mainMenuPanel, "Btn_Exit", "離開遊戲 (EXIT)", new Vector2(220, -100), new Vector2(400, 70), 30, font);

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
                txt.text = $"💎 持有水晶: <color=cyan>{gems}</color>    |    🏆 無盡最高紀錄: <color=yellow>{highScore}</color> 分";
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

    // ==================== 設定面板排版修復 (解決兩行折行問題) ====================
    void EnsureAudioSettingsUI()
    {
        if (settingsPanel == null) return;

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. 頂部分頁按鈕：寬度放寬至 340，字體微調為 22，設定 Overflow 徹底避免換行！
        Transform tTabKey = settingsPanel.transform.Find("Btn_TabKeyboard");
        Transform tTabCtrl = settingsPanel.transform.Find("Btn_TabController");
        Transform tTabAudio = settingsPanel.transform.Find("Btn_TabAudio");

        if (tTabKey != null)
        {
            RectTransform rtKey = tTabKey.GetComponent<RectTransform>();
            rtKey.anchoredPosition = new Vector2(-360, 310);
            rtKey.sizeDelta = new Vector2(340, 60);
            Text txt = tTabKey.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "【 鍵盤 KEYBOARD 】"; txt.fontSize = 22; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
            tabKeyboardBtn = tTabKey.GetComponent<Button>();
        }

        if (tTabCtrl != null)
        {
            RectTransform rtCtrl = tTabCtrl.GetComponent<RectTransform>();
            rtCtrl.anchoredPosition = new Vector2(0, 310);
            rtCtrl.sizeDelta = new Vector2(340, 60);
            Text txt = tTabCtrl.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "【 控制器 CONTROLLER 】"; txt.fontSize = 22; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
            tabControllerBtn = tTabCtrl.GetComponent<Button>();
        }

        if (tTabAudio == null)
        {
            Button btnAudio = CreateStyledButton(settingsPanel.transform, "Btn_TabAudio", "【 音訊設定 AUDIO 】", new Vector2(360, 310), new Vector2(340, 60), 22, defaultFont);
            tabAudioBtn = btnAudio;
        }
        else
        {
            RectTransform rtAudio = tTabAudio.GetComponent<RectTransform>();
            rtAudio.anchoredPosition = new Vector2(360, 310);
            rtAudio.sizeDelta = new Vector2(340, 60);
            Text txt = tTabAudio.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "【 音訊設定 AUDIO 】"; txt.fontSize = 22; txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; }
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
            GameObject bObj = CreateStyledButton(settingsPanel.transform, "Btn_ResetAllSave", "⚠️ 重置全部存檔 (RESET ALL SAVE)", new Vector2(0, -380), new Vector2(330, 65), 18, defaultFont);
            Image img = bObj.GetComponent<Image>();
            if (img != null) img.color = new Color(0.6f, 0.12f, 0.15f, 0.95f);
            Button b = bObj.GetComponent<Button>();
            if (b != null)
            {
                ColorBlock cb = b.colors;
                cb.normalColor = new Color(0.6f, 0.15f, 0.18f);
                cb.highlightedColor = new Color(0.9f, 0.2f, 0.25f);
                cb.pressedColor = new Color(0.4f, 0.08f, 0.1f);
                b.colors = cb;
            }
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

        // 3. 確保 AudioSettingsPanel 存在
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
        sliderBgmVolume = CreateSlider(pAudio.transform, "Slider_BgmVolume", new Vector2(100, -40), new Vector2(320, 30), 0f, 1f, 0.7f);
        txtBgmVolume = CreateText(pAudio.transform, "Txt_BgmVolVal", "70%", 26, Color.cyan, new Vector2(320, -40), new Vector2(100, 50), font, FontStyle.Bold);

        CreateText(pAudio.transform, "Lbl_SfxVol", "音效音量 (SFX VOLUME):", 28, Color.white, new Vector2(-240, -120), new Vector2(420, 50), font, FontStyle.Bold);
        sliderSfxVolume = CreateSlider(pAudio.transform, "Slider_SfxVolume", new Vector2(100, -120), new Vector2(320, 30), 0f, 1f, 0.8f);
        txtSfxVolume = CreateText(pAudio.transform, "Txt_SfxVolVal", "80%", 26, Color.cyan, new Vector2(320, -120), new Vector2(100, 50), font, FontStyle.Bold);

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
        PlayerPrefs.SetInt("IsBuilderMode", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOpenTrackBuilder()
    {
        ClearEventFocus();
        PlayerPrefs.SetInt("IsBuilderMode", 1);
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
        if (keyboardSettingsPanel != null) keyboardSettingsPanel.SetActive(tabIndex == 0);
        if (controllerSettingsPanel != null) controllerSettingsPanel.SetActive(tabIndex == 1);
        if (audioSettingsPanel != null) audioSettingsPanel.SetActive(tabIndex == 2);

        Color activeCol = new Color(0f, 0.5f, 0.9f);
        Color normalCol = new Color(0.1f, 0.22f, 0.38f);

        if (tabKeyboardBtn != null)
        {
            ColorBlock cb = tabKeyboardBtn.colors;
            cb.normalColor = (tabIndex == 0) ? activeCol : normalCol;
            tabKeyboardBtn.colors = cb;
        }

        if (tabControllerBtn != null)
        {
            ColorBlock cb = tabControllerBtn.colors;
            cb.normalColor = (tabIndex == 1) ? activeCol : normalCol;
            tabControllerBtn.colors = cb;
        }

        if (tabAudioBtn != null)
        {
            ColorBlock cb = tabAudioBtn.colors;
            cb.normalColor = (tabIndex == 2) ? activeCol : normalCol;
            tabAudioBtn.colors = cb;
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

        currentKeyLeft = KeyCode.A;
        currentKeyRight = KeyCode.D;
        currentKeyJump = KeyCode.Space;
        UpdateKeyTexts();
        if (sliderKeyboardSpeed != null) sliderKeyboardSpeed.value = 9.0f;

        if (comPortInput != null) comPortInput.text = "COM3";
        if (sliderTiltSensitivity != null) sliderTiltSensitivity.value = 1.8f;
        if (sliderTiltDeadzone != null) sliderTiltDeadzone.value = 0.08f;
        if (sliderJumpDistance != null) sliderJumpDistance.value = 20.0f;

        currentBgmIndex = 0;
        if (sliderBgmVolume != null) sliderBgmVolume.value = 0.7f;
        if (sliderSfxVolume != null) sliderSfxVolume.value = 0.8f;
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

        currentBgmIndex = PlayerPrefs.GetInt("BGM_Track_Index", 0);
        if (sliderBgmVolume != null)
        {
            sliderBgmVolume.value = PlayerPrefs.GetFloat("BGM_Volume", 0.7f);
            if (txtBgmVolume != null) txtBgmVolume.text = $"{sliderBgmVolume.value * 100f:F0}%";
        }
        if (sliderSfxVolume != null)
        {
            sliderSfxVolume.value = PlayerPrefs.GetFloat("SFX_Volume", 0.8f);
            if (txtSfxVolume != null) txtSfxVolume.text = $"{sliderSfxVolume.value * 100f:F0}%";
        }
        UpdateBgmUI();
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
