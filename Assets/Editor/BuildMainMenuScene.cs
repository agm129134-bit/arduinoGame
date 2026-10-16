using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

public class BuildMainMenuScene
{
    [MenuItem("Game Setup/Generate Rolling Leap Main Menu")]
    public static void GenerateScene()
    {
        string scenePath = "Assets/Scenes/MainMenuScene.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. 建立方向光
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.8f, 0.9f, 1.0f);
        light.intensity = 1.0f;
        lightObj.transform.rotation = Quaternion.Euler(45, -30, 0);

        // 2. 建立 EventSystem
        GameObject esObj = new GameObject("EventSystem");
        esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // 3. 建立 3D 裝飾展示小球
        GameObject ballObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballObj.name = "MenuDecorBall";
        ballObj.transform.position = new Vector3(0, 0, 1.5f);
        ballObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        
        Material matBall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Ball.mat");
        if (matBall != null) ballObj.GetComponent<MeshRenderer>().material = matBall;

        // 4. 建立相機
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.06f, 0.12f);
        camObj.transform.position = new Vector3(0, 0, -3.5f);

        // 5. 建立 UI Canvas (1920x1080)
        GameObject canvasObj = new GameObject("MainMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // 6. 建立 ArduinoReceiver
        GameObject ardObj = new GameObject("ArduinoReceiver");
        ArduinoReceiver ard = ardObj.AddComponent<ArduinoReceiver>();

        // 7. 添加 MenuManager
        RollingSkyMenuManager mm = canvasObj.AddComponent<RollingSkyMenuManager>();
        mm.arduino = ard;

        // 8. 四大主要面板
        GameObject pMain = CreatePanel(canvasObj.transform, "MainMenuPanel", new Color(0.04f, 0.06f, 0.12f, 0.88f));
        GameObject pLevel = CreatePanel(canvasObj.transform, "LevelSelectPanel", new Color(0.04f, 0.06f, 0.12f, 0.96f));
        GameObject pSettings = CreatePanel(canvasObj.transform, "SettingsPanel", new Color(0.04f, 0.06f, 0.12f, 0.96f));
        GameObject pShop = CreatePanel(canvasObj.transform, "ShopPanel", new Color(0.04f, 0.06f, 0.12f, 0.96f));

        mm.mainMenuPanel = pMain;
        mm.levelSelectPanel = pLevel;
        mm.settingsPanel = pSettings;
        mm.shopPanel = pShop;

        // ==================== 主面板配置 (雙欄精準佈局) ====================
        CreateText(pMain.transform, "Title", "ROLLING LEAP 3D", 76, new Color(0f, 1f, 0.9f), new Vector2(0, 330), new Vector2(900, 100), defaultFont, FontStyle.Bold);
        CreateText(pMain.transform, "Subtitle", "【極速躍動 3D × Arduino 體感互動跑酷】", 28, new Color(1f, 0.85f, 0.2f), new Vector2(0, 250), new Vector2(900, 50), defaultFont, FontStyle.Normal);
        CreateText(pMain.transform, "PlayerStatusText", "💎 持有水晶: 0    |    🏆 無盡最高紀錄: 0 分", 24, Color.yellow, new Vector2(0, 170), new Vector2(900, 50), defaultFont, FontStyle.Bold);

        // 左欄：核心遊玩模式
        CreateStyledButton(pMain.transform, "Btn_Start", "開始第一關 (START GAME)", new Vector2(-220, 70), new Vector2(400, 70), 28, defaultFont);
        CreateStyledButton(pMain.transform, "Btn_Levels", "選擇關卡 (LEVEL SELECT)", new Vector2(-220, -15), new Vector2(400, 70), 28, defaultFont);
        CreateStyledButton(pMain.transform, "Btn_Endless", "無盡極限跑酷 (ENDLESS MODE)", new Vector2(-220, -100), new Vector2(400, 70), 26, defaultFont);
        CreateStyledButton(pMain.transform, "Btn_Builder", "自訂場地編輯器 (TRACK BUILDER)", new Vector2(-220, -185), new Vector2(400, 70), 24, defaultFont);

        // 右欄：系統、商店與退出
        CreateStyledButton(pMain.transform, "Btn_Shop", "球體皮膚商店 (SKIN SHOP)", new Vector2(220, 70), new Vector2(400, 70), 28, defaultFont);
        CreateStyledButton(pMain.transform, "Btn_Settings", "系統設定 (SETTINGS)", new Vector2(220, -15), new Vector2(400, 70), 28, defaultFont);
        CreateStyledButton(pMain.transform, "Btn_Exit", "離開遊戲 (EXIT)", new Vector2(220, -100), new Vector2(400, 70), 30, defaultFont);

        // ==================== 關卡選擇面板配置 (2x5 網格無重疊) ====================
        CreateText(pLevel.transform, "Title", "SELECT LEVEL (10 大挑戰關卡)", 54, new Color(0f, 1f, 0.9f), new Vector2(0, 360), new Vector2(900, 90), defaultFont, FontStyle.Bold);

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
            int col = (i <= 5) ? 0 : 1;
            int row = (i <= 5) ? (i - 1) : (i - 6);
            float px = (col == 0) ? -330 : 330;
            float py = yPositions[row];
            CreateStyledButton(pLevel.transform, $"Btn_Lvl{i}", levelNames[i - 1], new Vector2(px, py), new Vector2(620, 68), 22, defaultFont);
        }

        CreateStyledButton(pLevel.transform, "Btn_Back", "← 返回主選單 (BACK)", new Vector2(0, -260), new Vector2(360, 65), 26, defaultFont);

        // ==================== 設定面板配置 (排版修復單行顯示) ====================
        CreateText(pSettings.transform, "Title", "SYSTEM SETTINGS", 58, new Color(0f, 1f, 0.9f), new Vector2(0, 390), new Vector2(900, 80), defaultFont, FontStyle.Bold);

        // 頂部分頁按鈕 (Tab Buttons) - 寬度 340，字體 22，絕不折行
        Button btnTabKey = CreateStyledButton(pSettings.transform, "Btn_TabKeyboard", "【 鍵盤 KEYBOARD 】", new Vector2(-360, 310), new Vector2(340, 60), 22, defaultFont);
        Button btnTabCtrl = CreateStyledButton(pSettings.transform, "Btn_TabController", "【 控制器 CONTROLLER 】", new Vector2(0, 310), new Vector2(340, 60), 22, defaultFont);
        Button btnTabAudio = CreateStyledButton(pSettings.transform, "Btn_TabAudio", "【 音訊設定 AUDIO 】", new Vector2(360, 310), new Vector2(340, 60), 22, defaultFont);
        mm.tabKeyboardBtn = btnTabKey;
        mm.tabControllerBtn = btnTabCtrl;
        mm.tabAudioBtn = btnTabAudio;

        // ---------- 分頁 1: 鍵盤設定 ----------
        GameObject pKey = CreatePanel(pSettings.transform, "KeyboardSettingsPanel", Color.clear);
        RectTransform rKey = pKey.GetComponent<RectTransform>();
        rKey.anchorMin = new Vector2(0.5f, 0.5f); rKey.anchorMax = new Vector2(0.5f, 0.5f);
        rKey.anchoredPosition = new Vector2(0, -10);
        rKey.sizeDelta = new Vector2(1000, 500);
        mm.keyboardSettingsPanel = pKey;

        CreateText(pKey.transform, "LeftLabel", "往左移動 (MOVE LEFT):", 28, Color.white, new Vector2(-200, 160), new Vector2(360, 60), defaultFont, FontStyle.Bold);
        Button bLeft = CreateStyledButton(pKey.transform, "Btn_KeyLeft", "[ A ]", new Vector2(160, 160), new Vector2(200, 60), 30, defaultFont);
        mm.btnKeyLeft = bLeft;
        mm.txtKeyLeft = bLeft.GetComponentInChildren<Text>();

        CreateText(pKey.transform, "RightLabel", "往右移動 (MOVE RIGHT):", 28, Color.white, new Vector2(-200, 75), new Vector2(360, 60), defaultFont, FontStyle.Bold);
        Button bRight = CreateStyledButton(pKey.transform, "Btn_KeyRight", "[ D ]", new Vector2(160, 75), new Vector2(200, 60), 30, defaultFont);
        mm.btnKeyRight = bRight;
        mm.txtKeyRight = bRight.GetComponentInChildren<Text>();

        CreateText(pKey.transform, "JumpLabel", "跳躍動作 (JUMP):", 28, Color.white, new Vector2(-200, -10), new Vector2(360, 60), defaultFont, FontStyle.Bold);
        Button bJump = CreateStyledButton(pKey.transform, "Btn_KeyJump", "[ Space ]", new Vector2(160, -10), new Vector2(200, 60), 28, defaultFont);
        mm.btnKeyJump = bJump;
        mm.txtKeyJump = bJump.GetComponentInChildren<Text>();

        CreateText(pKey.transform, "SpeedLabel", "鍵盤左右速度靈敏度:", 28, Color.white, new Vector2(-200, -95), new Vector2(360, 60), defaultFont, FontStyle.Bold);
        Slider sSpeed = CreateSlider(pKey.transform, "Slider_KeyboardSpeed", new Vector2(120, -95), new Vector2(260, 30), 5.0f, 15.0f, 9.0f);
        mm.sliderKeyboardSpeed = sSpeed;
        mm.txtKeyboardSpeed = CreateText(pKey.transform, "Txt_KeyboardSpeedVal", "9.0", 28, Color.cyan, new Vector2(300, -95), new Vector2(80, 50), defaultFont, FontStyle.Bold);

        CreateText(pKey.transform, "TipText", "💡 點擊上方按鍵按鈕後，按下實體鍵盤任一鍵即可重新綁定", 22, new Color(0.3f, 0.85f, 1f), new Vector2(0, -180), new Vector2(800, 50), defaultFont, FontStyle.Normal);

        // ---------- 分頁 2: 控制器設定 ----------
        GameObject pCtrl = CreatePanel(pSettings.transform, "ControllerSettingsPanel", Color.clear);
        RectTransform rCtrl = pCtrl.GetComponent<RectTransform>();
        rCtrl.anchorMin = new Vector2(0.5f, 0.5f); rCtrl.anchorMax = new Vector2(0.5f, 0.5f);
        rCtrl.anchoredPosition = new Vector2(0, -10);
        rCtrl.sizeDelta = new Vector2(1000, 500);
        mm.controllerSettingsPanel = pCtrl;

        CreateText(pCtrl.transform, "ComLabel", "SERIAL COM PORT:", 28, Color.white, new Vector2(-180, 165), new Vector2(360, 50), defaultFont, FontStyle.Bold);
        InputField inCom = CreateInputField(pCtrl.transform, "Input_ComPort", "COM3", new Vector2(140, 165), new Vector2(220, 55), defaultFont);
        mm.comPortInput = inCom;

        CreateText(pCtrl.transform, "TiltLabel", "陀螺儀轉向靈敏度:", 28, Color.white, new Vector2(-180, 95), new Vector2(360, 50), defaultFont, FontStyle.Bold);
        Slider sTilt = CreateSlider(pCtrl.transform, "Slider_TiltSensitivity", new Vector2(120, 95), new Vector2(240, 30), 0.5f, 3.5f, 1.8f);
        mm.sliderTiltSensitivity = sTilt;
        mm.txtTiltSensitivity = CreateText(pCtrl.transform, "Txt_TiltVal", "1.80", 26, Color.cyan, new Vector2(290, 95), new Vector2(80, 50), defaultFont, FontStyle.Bold);

        CreateText(pCtrl.transform, "DeadLabel", "手抖死區過濾 (DEADZONE):", 28, Color.white, new Vector2(-180, 25), new Vector2(380, 50), defaultFont, FontStyle.Bold);
        Slider sDead = CreateSlider(pCtrl.transform, "Slider_TiltDeadzone", new Vector2(120, 25), new Vector2(240, 30), 0.01f, 0.20f, 0.08f);
        mm.sliderTiltDeadzone = sDead;
        mm.txtTiltDeadzone = CreateText(pCtrl.transform, "Txt_DeadVal", "0.08", 26, Color.cyan, new Vector2(290, 25), new Vector2(80, 50), defaultFont, FontStyle.Bold);

        CreateText(pCtrl.transform, "JumpLabel", "超音波跳躍觸發距離:", 28, Color.white, new Vector2(-180, -45), new Vector2(360, 50), defaultFont, FontStyle.Bold);
        Slider sJump = CreateSlider(pCtrl.transform, "Slider_JumpDistance", new Vector2(120, -45), new Vector2(240, 30), 10.0f, 40.0f, 20.0f);
        mm.sliderJumpDistance = sJump;
        mm.txtJumpDistance = CreateText(pCtrl.transform, "Txt_JumpVal", "20 cm", 26, Color.cyan, new Vector2(290, -45), new Vector2(90, 50), defaultFont, FontStyle.Bold);

        // 控制器感測器狀態監視板
        GameObject boxObj = CreatePanel(pCtrl.transform, "SensorBox", new Color(0.06f, 0.12f, 0.24f, 0.85f));
        RectTransform boxRt = boxObj.GetComponent<RectTransform>();
        boxRt.anchoredPosition = new Vector2(0, -165);
        boxRt.sizeDelta = new Vector2(820, 115);
        mm.hardwareStatusText = CreateText(boxObj.transform, "HardwareStatusText", "• 實體裝置連線中 (Port: COM3)\n超音波測距: 50.0 cm\nMPU-6050 傾角: X=0.00, Y=0.00", 22, Color.green, Vector2.zero, new Vector2(800, 100), defaultFont, FontStyle.Normal);

        // ---------- 分頁 3: 音訊設定 ----------
        GameObject pAudio = CreatePanel(pSettings.transform, "AudioSettingsPanel", Color.clear);
        RectTransform rAudio = pAudio.GetComponent<RectTransform>();
        rAudio.anchorMin = new Vector2(0.5f, 0.5f); rAudio.anchorMax = new Vector2(0.5f, 0.5f);
        rAudio.anchoredPosition = new Vector2(0, -10);
        rAudio.sizeDelta = new Vector2(1000, 500);
        mm.audioSettingsPanel = pAudio;

        CreateText(pAudio.transform, "Lbl_Track", "BGM 背景音樂選擇:", 28, Color.white, new Vector2(-240, 190), new Vector2(420, 50), defaultFont, FontStyle.Bold);
        mm.btnBgmTrack0 = CreateStyledButton(pAudio.transform, "Btn_BgmTrack0", "Ver A: 霓虹賽博 (Cyber Neon)", new Vector2(-280, 115), new Vector2(340, 60), 20, defaultFont);
        mm.btnBgmTrack1 = CreateStyledButton(pAudio.transform, "Btn_BgmTrack1", "Ver B: 復古晶片 (Chiptune Rush)", new Vector2(70, 115), new Vector2(340, 60), 20, defaultFont);
        mm.btnBgmTrack2 = CreateStyledButton(pAudio.transform, "Btn_BgmTrack2", "Ver C: 深空重力 (Deep Gravity)", new Vector2(420, 115), new Vector2(340, 60), 20, defaultFont);

        mm.txtCurrentBgm = CreateText(pAudio.transform, "Txt_CurrentBgm", "當前播放: Ver A: 霓虹賽博", 24, Color.cyan, new Vector2(0, 45), new Vector2(800, 40), defaultFont, FontStyle.Normal);

        CreateText(pAudio.transform, "Lbl_BgmVol", "音樂音量 (BGM VOLUME):", 28, Color.white, new Vector2(-240, -40), new Vector2(420, 50), defaultFont, FontStyle.Bold);
        mm.sliderBgmVolume = CreateSlider(pAudio.transform, "Slider_BgmVolume", new Vector2(100, -40), new Vector2(320, 30), 0f, 1f, 0.7f);
        mm.txtBgmVolume = CreateText(pAudio.transform, "Txt_BgmVolVal", "70%", 26, Color.cyan, new Vector2(320, -40), new Vector2(100, 50), defaultFont, FontStyle.Bold);

        CreateText(pAudio.transform, "Lbl_SfxVol", "音效音量 (SFX VOLUME):", 28, Color.white, new Vector2(-240, -120), new Vector2(420, 50), defaultFont, FontStyle.Bold);
        mm.sliderSfxVolume = CreateSlider(pAudio.transform, "Slider_SfxVolume", new Vector2(100, -120), new Vector2(320, 30), 0f, 1f, 0.8f);
        mm.txtSfxVolume = CreateText(pAudio.transform, "Txt_SfxVolVal", "80%", 26, Color.cyan, new Vector2(320, -120), new Vector2(100, 50), defaultFont, FontStyle.Bold);

        mm.btnTestSfx = CreateStyledButton(pAudio.transform, "Btn_TestSfx", "🔊 試聽音效 (TEST SFX)", new Vector2(0, -210), new Vector2(320, 55), 22, defaultFont);

        // ---------- 設定面板底部操作按鈕 (寬度充裕單行排列) ----------
        CreateStyledButton(pSettings.transform, "Btn_ResetDefaults", "重置預設 (RESET)", new Vector2(-220, -380), new Vector2(300, 65), 24, defaultFont);
        CreateStyledButton(pSettings.transform, "Btn_SaveBack", "儲存並返回 (SAVE & BACK)", new Vector2(220, -380), new Vector2(420, 65), 24, defaultFont);

        // ==================== 商店面板配置 ====================
        CreateText(pShop.transform, "Title", "BALL SKIN SHOP (球體皮膚商店)", 56, new Color(0f, 1f, 0.9f), new Vector2(0, 370), new Vector2(900, 80), defaultFont, FontStyle.Bold);
        CreateText(pShop.transform, "Txt_ShopGems", "💎 目前持有水晶: 0", 28, Color.yellow, new Vector2(0, 300), new Vector2(800, 50), defaultFont, FontStyle.Bold);

        string[] skinNames = new string[] { "經典電藍 (Classic Blue)", "霓虹烈焰 (Neon Flame)", "賽博矩陣 (Cyber Matrix)", "黃金至尊 (Golden Spark)", "銀河虛空 (Void Galaxy)" };
        int[] skinPrices = new int[] { 0, 20, 40, 70, 100 };
        Color[] skinColors = new Color[] { new Color(0f, 0.8f, 1f), new Color(1f, 0.3f, 0.1f), new Color(0.1f, 1f, 0.4f), new Color(1f, 0.9f, 0.2f), new Color(0.7f, 0.2f, 1f) };

        for (int i = 0; i < 5; i++)
        {
            float y = 180 - (i * 90);
            CreateText(pShop.transform, $"Label_Skin_{i}", $"【{skinNames[i]}】 需 {skinPrices[i]} 水晶", 24, skinColors[i], new Vector2(-150, y), new Vector2(500, 60), defaultFont, FontStyle.Bold);
            CreateStyledButton(pShop.transform, $"Btn_Skin_{i}", (i == 0) ? "已擁有" : $"購買 ({skinPrices[i]} 💎)", new Vector2(250, y), new Vector2(240, 60), 22, defaultFont);
        }

        CreateStyledButton(pShop.transform, "Btn_ShopBack", "← 返回主選單 (BACK)", new Vector2(0, -380), new Vector2(360, 65), 26, defaultFont);

        // 初始隱藏二級面板
        pLevel.SetActive(false);
        pSettings.SetActive(false);
        pShop.SetActive(false);
        pKey.SetActive(true);
        pCtrl.SetActive(false);
        pAudio.SetActive(false);

        // 儲存場景
        EditorSceneManager.SaveScene(scene, scenePath);

        // 更新 Build Settings 順序
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenuScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/RollingSkyScene.unity", true)
        };
        EditorBuildSettings.scenes = scenes;

        Debug.Log("🎉 《Rolling Leap 3D》10關階梯、外觀商店與排版修復主選單場景已完美生成！路徑: " + scenePath);
    }

    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        return panel;
    }

    static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, Vector2 pos, Vector2 size, Font font, FontStyle style)
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

    static Button CreateStyledButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, int fontSize, Font font)
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
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;

        Outline outline = tObj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(2, -2);

        return btn;
    }

    static InputField CreateInputField(Transform parent, string name, string defaultVal, Vector2 pos, Vector2 size, Font font)
    {
        GameObject inputObj = new GameObject(name);
        inputObj.transform.SetParent(parent, false);
        Image bg = inputObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.2f, 0.9f);
        bg.raycastTarget = true;

        RectTransform rt = inputObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        InputField input = inputObj.AddComponent<InputField>();

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(inputObj.transform, false);
        Text t = tObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 24;
        t.color = Color.cyan;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = defaultVal;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform trt = tObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;

        input.textComponent = t;
        input.text = defaultVal;

        return input;
    }

    static Slider CreateSlider(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, float def)
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
