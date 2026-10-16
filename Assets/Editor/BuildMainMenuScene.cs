using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.IO;

[InitializeOnLoad]
public class BuildMainMenuScene : Editor
{
    static BuildMainMenuScene()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists("Assets/Scenes/MainMenuScene.unity"))
            {
                GenerateMainMenu();
            }
        };
    }

    [MenuItem("Game Setup/Generate Main Menu Scene")]
    public static void GenerateMainMenu()
    {
        string scenePath = "Assets/Scenes/MainMenuScene.unity";
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 1. 建立實體 EventSystem (確保 UI 射線點擊 100% 響應)
        GameObject esObj = new GameObject("EventSystem");
        esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // 2. 建立燈光
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.9f, 0.95f, 1f);
        light.intensity = 1.0f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.08f, 0.12f, 0.2f);

        // 3. 建立裝飾用 3D 旋轉展示球
        GameObject ballObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballObj.name = "ShowcaseBall";
        ballObj.transform.position = new Vector3(0, 0, 5f);
        ballObj.transform.localScale = Vector3.one * 2.2f;
        
        Material matBall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Ball.mat");
        if (matBall != null) ballObj.GetComponent<MeshRenderer>().material = matBall;

        // 4. 建立相機
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.07f, 0.12f);
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

        // 6. 建立 ArduinoReceiver (供主選單即時監聽硬體校準)
        GameObject ardObj = new GameObject("ArduinoReceiver");
        ArduinoReceiver ard = ardObj.AddComponent<ArduinoReceiver>();

        // 7. 添加 MenuManager
        RollingSkyMenuManager mm = canvasObj.AddComponent<RollingSkyMenuManager>();
        mm.arduino = ard;

        // 8. 三大主要面板
        GameObject pMain = CreatePanel(canvasObj.transform, "MainMenuPanel", new Color(0.04f, 0.06f, 0.12f, 0.85f));
        GameObject pLevel = CreatePanel(canvasObj.transform, "LevelSelectPanel", new Color(0.04f, 0.06f, 0.12f, 0.95f));
        GameObject pSettings = CreatePanel(canvasObj.transform, "SettingsPanel", new Color(0.04f, 0.06f, 0.12f, 0.96f));

        mm.mainMenuPanel = pMain;
        mm.levelSelectPanel = pLevel;
        mm.settingsPanel = pSettings;

        // ==================== 主面板配置 ====================
        CreateText(pMain.transform, "Title", "ROLLING LEAP 3D", 76, new Color(0f, 1f, 0.9f), new Vector2(0, 320), new Vector2(900, 100), defaultFont, FontStyle.Bold);
        CreateText(pMain.transform, "Subtitle", "【3D 體感平衡球 × 超音波跳躍】", 30, new Color(1f, 0.85f, 0.2f), new Vector2(0, 240), new Vector2(800, 60), defaultFont, FontStyle.Normal);

        CreateStyledButton(pMain.transform, "Btn_Start", "START GAME", new Vector2(0, 80), new Vector2(420, 80), 38, defaultFont);
        CreateStyledButton(pMain.transform, "Btn_Levels", "LEVEL SELECT", new Vector2(0, -20), new Vector2(420, 80), 38, defaultFont);
        CreateStyledButton(pMain.transform, "Btn_Settings", "SETTINGS (設定)", new Vector2(0, -120), new Vector2(420, 80), 34, defaultFont);
        CreateStyledButton(pMain.transform, "Btn_Exit", "EXIT", new Vector2(0, -220), new Vector2(420, 80), 38, defaultFont);

        // ==================== 關卡選擇面板配置 ====================
        CreateText(pLevel.transform, "Title", "SELECT LEVEL", 64, new Color(0f, 1f, 0.9f), new Vector2(0, 340), new Vector2(800, 100), defaultFont, FontStyle.Bold);

        CreateStyledButton(pLevel.transform, "Btn_Lvl1", "LEVEL 1 : 霓虹初試 (Neon Avenue)", new Vector2(0, 140), new Vector2(650, 75), 32, defaultFont);
        CreateStyledButton(pLevel.transform, "Btn_Lvl2", "LEVEL 2 : 虛空躍升 (Void Leap)", new Vector2(0, 45), new Vector2(650, 75), 32, defaultFont);
        CreateStyledButton(pLevel.transform, "Btn_Lvl3", "LEVEL 3 : 疾速迴旋 (Speed Spiral)", new Vector2(0, -50), new Vector2(650, 75), 32, defaultFont);

        CreateStyledButton(pLevel.transform, "Btn_Back", "← BACK", new Vector2(0, -260), new Vector2(360, 65), 32, defaultFont);

        // ==================== 設定面板配置 (雙分頁架構) ====================
        CreateText(pSettings.transform, "Title", "SYSTEM SETTINGS", 60, new Color(0f, 1f, 0.9f), new Vector2(0, 390), new Vector2(900, 80), defaultFont, FontStyle.Bold);

        // 頂部分頁按鈕 (Tab Buttons) - 三大分頁
        Button btnTabKey = CreateStyledButton(pSettings.transform, "Btn_TabKeyboard", "【 鍵盤 KEYBOARD 】", new Vector2(-330, 310), new Vector2(300, 60), 26, defaultFont);
        Button btnTabCtrl = CreateStyledButton(pSettings.transform, "Btn_TabController", "【 控制器 CONTROLLER 】", new Vector2(0, 310), new Vector2(300, 60), 26, defaultFont);
        Button btnTabAudio = CreateStyledButton(pSettings.transform, "Btn_TabAudio", "【 音訊設定 AUDIO 】", new Vector2(330, 310), new Vector2(300, 60), 26, defaultFont);
        mm.tabKeyboardBtn = btnTabKey;
        mm.tabControllerBtn = btnTabCtrl;
        mm.tabAudioBtn = btnTabAudio;

        // ---------- 分頁 1: 鍵盤設定 (Keyboard Tab) ----------
        GameObject pKey = CreatePanel(pSettings.transform, "KeyboardSettingsPanel", Color.clear);
        RectTransform rKey = pKey.GetComponent<RectTransform>();
        rKey.anchorMin = new Vector2(0.5f, 0.5f); rKey.anchorMax = new Vector2(0.5f, 0.5f);
        rKey.anchoredPosition = new Vector2(0, -10);
        rKey.sizeDelta = new Vector2(1000, 500);
        mm.keyboardSettingsPanel = pKey;

        // 往左按鍵
        CreateText(pKey.transform, "LeftLabel", "往左移動 (MOVE LEFT):", 30, Color.white, new Vector2(-200, 160), new Vector2(360, 60), defaultFont, FontStyle.Bold);
        Button bLeft = CreateStyledButton(pKey.transform, "Btn_KeyLeft", "[ A ]", new Vector2(160, 160), new Vector2(200, 60), 32, defaultFont);
        mm.btnKeyLeft = bLeft;
        mm.txtKeyLeft = bLeft.GetComponentInChildren<Text>();

        // 往右按鍵
        CreateText(pKey.transform, "RightLabel", "往右移動 (MOVE RIGHT):", 30, Color.white, new Vector2(-200, 75), new Vector2(360, 60), defaultFont, FontStyle.Bold);
        Button bRight = CreateStyledButton(pKey.transform, "Btn_KeyRight", "[ D ]", new Vector2(160, 75), new Vector2(200, 60), 32, defaultFont);
        mm.btnKeyRight = bRight;
        mm.txtKeyRight = bRight.GetComponentInChildren<Text>();

        // 跳躍按鍵
        CreateText(pKey.transform, "JumpLabel", "跳躍動作 (JUMP):", 30, Color.white, new Vector2(-200, -10), new Vector2(360, 60), defaultFont, FontStyle.Bold);
        Button bJump = CreateStyledButton(pKey.transform, "Btn_KeyJump", "[ Space ]", new Vector2(160, -10), new Vector2(200, 60), 32, defaultFont);
        mm.btnKeyJump = bJump;
        mm.txtKeyJump = bJump.GetComponentInChildren<Text>();

        // 橫移速度滑桿
        CreateText(pKey.transform, "SpeedLabel", "鍵盤橫移速度 (SPEED):", 30, Color.white, new Vector2(-200, -95), new Vector2(360, 60), defaultFont, FontStyle.Bold);
        Slider sKeySpeed = CreateSlider(pKey.transform, "Slider_KeyboardSpeed", new Vector2(120, -95), new Vector2(240, 30), 5f, 18f, 9f);
        Text tKeySpeed = CreateText(pKey.transform, "Txt_KeySpeed", "9.0", 28, Color.yellow, new Vector2(290, -95), new Vector2(80, 50), defaultFont, FontStyle.Bold);
        mm.sliderKeyboardSpeed = sKeySpeed;
        mm.txtKeyboardSpeed = tKeySpeed;

        CreateText(pKey.transform, "Hint", "💡 提示：點擊按鍵按鈕後，直接按下您想綁定的按鍵即可即時更換！", 24, new Color(0.4f, 0.9f, 1f), new Vector2(0, -180), new Vector2(800, 50), defaultFont, FontStyle.Normal);

        // ---------- 分頁 2: 控制器/硬體設定 (Controller Tab) ----------
        GameObject pCtrl = CreatePanel(pSettings.transform, "ControllerSettingsPanel", Color.clear);
        RectTransform rCtrl = pCtrl.GetComponent<RectTransform>();
        rCtrl.anchorMin = new Vector2(0.5f, 0.5f); rCtrl.anchorMax = new Vector2(0.5f, 0.5f);
        rCtrl.anchoredPosition = new Vector2(0, -10);
        rCtrl.sizeDelta = new Vector2(1000, 500);
        mm.controllerSettingsPanel = pCtrl;

        // COM Port
        CreateText(pCtrl.transform, "PortLabel", "SERIAL COM PORT:", 28, Color.white, new Vector2(-200, 180), new Vector2(360, 50), defaultFont, FontStyle.Bold);
        InputField portInput = CreateInputField(pCtrl.transform, "Input_COMPort", "COM3", new Vector2(150, 180), new Vector2(220, 50), defaultFont);
        mm.comPortInput = portInput;

        // 陀螺儀轉向靈敏度
        CreateText(pCtrl.transform, "TiltSensLabel", "陀螺儀轉向靈敏度:", 28, Color.white, new Vector2(-200, 115), new Vector2(360, 50), defaultFont, FontStyle.Bold);
        Slider sTiltSens = CreateSlider(pCtrl.transform, "Slider_TiltSens", new Vector2(120, 115), new Vector2(240, 30), 0.5f, 3.5f, 1.8f);
        Text tTiltSens = CreateText(pCtrl.transform, "Txt_TiltSens", "1.80", 26, Color.cyan, new Vector2(290, 115), new Vector2(80, 50), defaultFont, FontStyle.Bold);
        mm.sliderTiltSensitivity = sTiltSens;
        mm.txtTiltSensitivity = tTiltSens;

        // 陀螺儀死區
        CreateText(pCtrl.transform, "DeadzoneLabel", "手抖死區過濾 (DEADZONE):", 28, Color.white, new Vector2(-200, 50), new Vector2(360, 50), defaultFont, FontStyle.Bold);
        Slider sDeadzone = CreateSlider(pCtrl.transform, "Slider_Deadzone", new Vector2(120, 50), new Vector2(240, 30), 0.01f, 0.20f, 0.08f);
        Text tDeadzone = CreateText(pCtrl.transform, "Txt_Deadzone", "0.08", 26, Color.cyan, new Vector2(290, 50), new Vector2(80, 50), defaultFont, FontStyle.Bold);
        mm.sliderTiltDeadzone = sDeadzone;
        mm.txtTiltDeadzone = tDeadzone;

        // 超音波跳躍觸發距離
        CreateText(pCtrl.transform, "JumpDistLabel", "超音波跳躍觸發距離:", 28, Color.white, new Vector2(-200, -15), new Vector2(360, 50), defaultFont, FontStyle.Bold);
        Slider sJumpDist = CreateSlider(pCtrl.transform, "Slider_JumpDist", new Vector2(120, -15), new Vector2(240, 30), 10f, 35f, 20f);
        Text tJumpDist = CreateText(pCtrl.transform, "Txt_JumpDist", "20 cm", 26, Color.yellow, new Vector2(290, -15), new Vector2(90, 50), defaultFont, FontStyle.Bold);
        mm.sliderJumpDistance = sJumpDist;
        mm.txtJumpDistance = tJumpDist;

        // 即時硬體校準框
        GameObject hwBox = CreatePanel(pCtrl.transform, "HardwareBox", new Color(0.08f, 0.12f, 0.2f, 0.95f));
        RectTransform hwRect = hwBox.GetComponent<RectTransform>();
        hwRect.anchorMin = new Vector2(0.5f, 0.5f); hwRect.anchorMax = new Vector2(0.5f, 0.5f);
        hwRect.anchoredPosition = new Vector2(0, -135);
        hwRect.sizeDelta = new Vector2(860, 130);

        Text hwText = CreateText(hwBox.transform, "HardwareStatus", "硬體感測器即時監控載入中...", 24, Color.yellow, Vector2.zero, new Vector2(820, 110), defaultFont, FontStyle.Normal);
        hwText.lineSpacing = 1.3f;
        mm.hardwareStatusText = hwText;

        // ---------- 分頁 3: 音訊設定 (Audio Tab) ----------
        GameObject pAudio = CreatePanel(pSettings.transform, "AudioSettingsPanel", Color.clear);
        RectTransform rAudio = pAudio.GetComponent<RectTransform>();
        rAudio.anchorMin = new Vector2(0.5f, 0.5f); rAudio.anchorMax = new Vector2(0.5f, 0.5f);
        rAudio.anchoredPosition = new Vector2(0, -10);
        rAudio.sizeDelta = new Vector2(1000, 500);
        mm.audioSettingsPanel = pAudio;

        // BGM 曲目選擇標題與即時指示
        CreateText(pAudio.transform, "BgmHeader", "背景音樂曲目選擇 (SELECT BGM TRACK):", 28, new Color(0f, 1f, 0.9f), new Vector2(0, 185), new Vector2(800, 45), defaultFont, FontStyle.Bold);
        Text tCurBgm = CreateText(pAudio.transform, "Txt_CurrentBgm", "⚡ 播放中：賽博霓虹脈衝 (Cyber Neon - 130 BPM)", 24, Color.yellow, new Vector2(0, 140), new Vector2(860, 40), defaultFont, FontStyle.Bold);
        mm.txtCurrentBgm = tCurBgm;

        // 三首曲目切換按鈕
        Button bBgm0 = CreateStyledButton(pAudio.transform, "Btn_Bgm0", "⚡ 賽博霓虹 (Cyber Neon)", new Vector2(-310, 75), new Vector2(290, 55), 22, defaultFont);
        Button bBgm1 = CreateStyledButton(pAudio.transform, "Btn_Bgm1", "🕹️ 復古像素 (Chiptune)", new Vector2(0, 75), new Vector2(290, 55), 22, defaultFont);
        Button bBgm2 = CreateStyledButton(pAudio.transform, "Btn_Bgm2", "🌌 深空重力 (Deep Space)", new Vector2(310, 75), new Vector2(290, 55), 22, defaultFont);
        mm.btnBgmTrack0 = bBgm0;
        mm.btnBgmTrack1 = bBgm1;
        mm.btnBgmTrack2 = bBgm2;

        // BGM 音量滑桿
        CreateText(pAudio.transform, "BgmVolLabel", "音樂音量 (BGM VOLUME):", 28, Color.white, new Vector2(-200, -5), new Vector2(360, 50), defaultFont, FontStyle.Bold);
        Slider sBgmVol = CreateSlider(pAudio.transform, "Slider_BgmVolume", new Vector2(120, -5), new Vector2(240, 30), 0f, 1f, 0.65f);
        Text tBgmVol = CreateText(pAudio.transform, "Txt_BgmVolume", "65%", 26, Color.cyan, new Vector2(290, -5), new Vector2(80, 50), defaultFont, FontStyle.Bold);
        mm.sliderBgmVolume = sBgmVol;
        mm.txtBgmVolume = tBgmVol;

        // SFX 音量滑桿
        CreateText(pAudio.transform, "SfxVolLabel", "音效音量 (SFX VOLUME):", 28, Color.white, new Vector2(-200, -75), new Vector2(360, 50), defaultFont, FontStyle.Bold);
        Slider sSfxVol = CreateSlider(pAudio.transform, "Slider_SfxVolume", new Vector2(120, -75), new Vector2(240, 30), 0f, 1f, 0.85f);
        Text tSfxVol = CreateText(pAudio.transform, "Txt_SfxVolume", "85%", 26, Color.cyan, new Vector2(290, -75), new Vector2(80, 50), defaultFont, FontStyle.Bold);
        mm.sliderSfxVolume = sSfxVol;
        mm.txtSfxVolume = tSfxVol;

        // 測試音效按鈕
        Button bTestSfx = CreateStyledButton(pAudio.transform, "Btn_TestSfx", "🔊 測試音效播放 (TEST SFX)", new Vector2(0, -145), new Vector2(380, 50), 24, defaultFont);
        mm.btnTestSfx = bTestSfx;

        // 提示文字
        CreateText(pAudio.transform, "AudioHint", "💡 提示：點擊上方曲目可即時試聽切換，遊戲關卡將自動同步您選定的音樂！", 22, new Color(0.4f, 0.9f, 1f), new Vector2(0, -210), new Vector2(880, 45), defaultFont, FontStyle.Normal);

        // ---------- 設定面板底部操作按鈕 ----------
        CreateStyledButton(pSettings.transform, "Btn_ResetDefaults", "重置預設 (RESET)", new Vector2(-200, -380), new Vector2(280, 65), 28, defaultFont);
        CreateStyledButton(pSettings.transform, "Btn_SaveBack", "儲存並返回 (SAVE & BACK)", new Vector2(200, -380), new Vector2(340, 65), 28, defaultFont);

        // 初始隱藏二級面板
        pLevel.SetActive(false);
        pSettings.SetActive(false);
        pMain.SetActive(true);

        // 儲存場景
        EditorSceneManager.SaveScene(scene, scenePath);

        // 更新 Build Settings 順序
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenuScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/RollingSkyScene.unity", true)
        };
        EditorBuildSettings.scenes = scenes;

        Debug.Log("🎉 《RollingSky3D》雙分頁主選單場景已完美生成！路徑: " + scenePath);
    }

    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false; // 防止全螢幕背景阻擋按鈕點擊
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
        t.raycastTarget = false; // 防止純文字阻擋點擊
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
        img.raycastTarget = true; // 按鈕接收射線

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

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputObj.transform, false);
        Text t = textObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 28;
        t.color = Color.cyan;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        RectTransform trt = textObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;

        InputField input = inputObj.AddComponent<InputField>();
        input.textComponent = t;
        input.text = defaultVal;
        return input;
    }

    static Slider CreateSlider(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, float def)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);
        Slider slider = sliderObj.AddComponent<Slider>();

        RectTransform rt = sliderObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sliderObj.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.15f, 0.25f);
        bgImg.raycastTarget = true;
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.sizeDelta = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one; faRt.sizeDelta = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0f, 0.8f, 1f);
        fillImg.raycastTarget = false;
        RectTransform fRt = fill.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.sizeDelta = Vector2.zero;
        slider.fillRect = fRt;

        slider.minValue = min;
        slider.maxValue = max;
        slider.value = def;
        return slider;
    }
}
