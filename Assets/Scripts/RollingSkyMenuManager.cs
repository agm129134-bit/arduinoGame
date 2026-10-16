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
    private bool isTransitioning = false;
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

        // 確保 AudioManager 存在 (主選單即開始播放背景音樂)
        if (AudioManager.Instance == null && FindAnyObjectByType<AudioManager>() == null)
        {
            GameObject audioObj = new GameObject("AudioManager");
            audioObj.AddComponent<AudioManager>();
        }

        // 0.5. 確保音訊設定介面與三分頁佈局存在 (具備 Runtime 自癒)
        EnsureAudioSettingsUI();

        // 1. 動態綁定所有按鈕事件 (徹底解決 Editor 保存遺失 onClick 監聽器的問題)
        BindAllButtonsRuntime();

        // 2. 載入保存的所有鍵盤、控制器與音訊設定
        LoadAllSettings();

        // 3. 初始顯示主面板
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        currentActivePanel = mainMenuPanel;
        SwitchSettingsTab(0); // 預設打開鍵盤設定分頁
        ClearEventFocus();
    }

    void Update()
    {
        // 若在控制器設定分頁，即時刷新硬體感測器數值以供玩家校準
        if (settingsPanel != null && settingsPanel.activeInHierarchy && 
            controllerSettingsPanel != null && controllerSettingsPanel.activeInHierarchy)
        {
            UpdateHardwareReadout();
        }

        // 快捷返回鍵 (若未在綁定按鍵狀態)
        if (Input.GetKeyDown(KeyCode.Escape) && !isRebinding)
        {
            if (levelSelectPanel != null && levelSelectPanel.activeInHierarchy)
            {
                ShowPanel(mainMenuPanel);
            }
            else if (settingsPanel != null && settingsPanel.activeInHierarchy)
            {
                OnSaveSettings();
                ShowPanel(mainMenuPanel);
            }
        }
    }

    // --- 動態綁定按鈕監聽 (防止 Unity 儲存丟失事件) ---
    void BindAllButtonsRuntime()
    {
        // 主面板按鈕
        BindButton(mainMenuPanel, "Btn_Start", OnStartGame);
        BindButton(mainMenuPanel, "Btn_Levels", OnOpenLevelSelect);
        BindButton(mainMenuPanel, "Btn_Settings", OnOpenSettings);
        BindButton(mainMenuPanel, "Btn_Exit", OnExitGame);

        // 關卡面板按鈕
        BindButton(levelSelectPanel, "Btn_Lvl1", () => OnSelectLevel(1));
        BindButton(levelSelectPanel, "Btn_Lvl2", () => OnSelectLevel(2));
        BindButton(levelSelectPanel, "Btn_Lvl3", () => OnSelectLevel(3));
        BindButton(levelSelectPanel, "Btn_Back", () => ShowPanel(mainMenuPanel));

        // 設定面板 Tab 切換按鈕
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

        // 鍵位綁定按鈕
        if (btnKeyLeft != null) btnKeyLeft.onClick.AddListener(() => StartRebind("Key_Left", txtKeyLeft, k => currentKeyLeft = k));
        if (btnKeyRight != null) btnKeyRight.onClick.AddListener(() => StartRebind("Key_Right", txtKeyRight, k => currentKeyRight = k));
        if (btnKeyJump != null) btnKeyJump.onClick.AddListener(() => StartRebind("Key_Jump", txtKeyJump, k => currentKeyJump = k));

        // 滑桿數值變更監聽 (鍵盤與控制器)
        if (sliderKeyboardSpeed != null)
        {
            sliderKeyboardSpeed.onValueChanged.AddListener(v => {
                if (txtKeyboardSpeed != null) txtKeyboardSpeed.text = $"{v:F1}";
            });
        }

        if (sliderTiltSensitivity != null)
        {
            sliderTiltSensitivity.onValueChanged.AddListener(v => {
                if (txtTiltSensitivity != null) txtTiltSensitivity.text = $"{v:F2}";
            });
        }

        if (sliderTiltDeadzone != null)
        {
            sliderTiltDeadzone.onValueChanged.AddListener(v => {
                if (txtTiltDeadzone != null) txtTiltDeadzone.text = $"{v:F2}";
            });
        }

        if (sliderJumpDistance != null)
        {
            sliderJumpDistance.onValueChanged.AddListener(v => {
                if (txtJumpDistance != null) txtJumpDistance.text = $"{v:F0} cm";
            });
        }

        // 音訊設定按鈕與滑桿監聽
        if (btnBgmTrack0 != null)
        {
            btnBgmTrack0.onClick.RemoveAllListeners();
            btnBgmTrack0.onClick.AddListener(() => SelectBGMTrack(0));
        }
        else BindButton(audioSettingsPanel, "Btn_Bgm0", () => SelectBGMTrack(0));

        if (btnBgmTrack1 != null)
        {
            btnBgmTrack1.onClick.RemoveAllListeners();
            btnBgmTrack1.onClick.AddListener(() => SelectBGMTrack(1));
        }
        else BindButton(audioSettingsPanel, "Btn_Bgm1", () => SelectBGMTrack(1));

        if (btnBgmTrack2 != null)
        {
            btnBgmTrack2.onClick.RemoveAllListeners();
            btnBgmTrack2.onClick.AddListener(() => SelectBGMTrack(2));
        }
        else BindButton(audioSettingsPanel, "Btn_Bgm2", () => SelectBGMTrack(2));

        if (sliderBgmVolume != null)
        {
            sliderBgmVolume.onValueChanged.RemoveAllListeners();
            sliderBgmVolume.onValueChanged.AddListener(v => {
                if (txtBgmVolume != null) txtBgmVolume.text = $"{v * 100f:F0}%";
                AudioManager.Instance?.SetBGMVolume(v);
            });
        }

        if (sliderSfxVolume != null)
        {
            sliderSfxVolume.onValueChanged.RemoveAllListeners();
            sliderSfxVolume.onValueChanged.AddListener(v => {
                if (txtSfxVolume != null) txtSfxVolume.text = $"{v * 100f:F0}%";
                AudioManager.Instance?.SetSFXVolume(v);
            });
        }

        if (btnTestSfx != null)
        {
            btnTestSfx.onClick.RemoveAllListeners();
            btnTestSfx.onClick.AddListener(() => AudioManager.Instance?.PlayGem());
        }
        else BindButton(audioSettingsPanel, "Btn_TestSfx", () => AudioManager.Instance?.PlayGem());

        // 設定面板底層儲存與重置按鈕
        BindButton(settingsPanel, "Btn_ResetDefaults", OnResetDefaults);
        BindButton(settingsPanel, "Btn_SaveBack", OnSaveSettings);
    }

    void BindButton(GameObject parent, string btnName, UnityEngine.Events.UnityAction action)
    {
        if (parent == null) return;
        Transform t = parent.transform.Find(btnName);
        if (t != null)
        {
            Button b = t.GetComponent<Button>();
            if (b != null)
            {
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(action);
            }
        }
    }

    // --- 設定資料讀取與儲存 ---
    public void LoadAllSettings()
    {
        // 鍵盤設定
        currentKeyLeft = (KeyCode)PlayerPrefs.GetInt("Key_Left", (int)KeyCode.A);
        currentKeyRight = (KeyCode)PlayerPrefs.GetInt("Key_Right", (int)KeyCode.D);
        currentKeyJump = (KeyCode)PlayerPrefs.GetInt("Key_Jump", (int)KeyCode.Space);

        if (txtKeyLeft != null) txtKeyLeft.text = currentKeyLeft.ToString();
        if (txtKeyRight != null) txtKeyRight.text = currentKeyRight.ToString();
        if (txtKeyJump != null) txtKeyJump.text = currentKeyJump.ToString();

        float kSpeed = PlayerPrefs.GetFloat("Keyboard_Speed", 9.0f);
        if (sliderKeyboardSpeed != null) sliderKeyboardSpeed.value = kSpeed;
        if (txtKeyboardSpeed != null) txtKeyboardSpeed.text = $"{kSpeed:F1}";

        // 控制器設定
        if (comPortInput != null) comPortInput.text = PlayerPrefs.GetString("COMPort", "COM3");

        float tSens = PlayerPrefs.GetFloat("Tilt_Sensitivity", 1.8f);
        if (sliderTiltSensitivity != null) sliderTiltSensitivity.value = tSens;
        if (txtTiltSensitivity != null) txtTiltSensitivity.text = $"{tSens:F2}";

        float tDead = PlayerPrefs.GetFloat("Tilt_Deadzone", 0.08f);
        if (sliderTiltDeadzone != null) sliderTiltDeadzone.value = tDead;
        if (txtTiltDeadzone != null) txtTiltDeadzone.text = $"{tDead:F2}";

        float jDist = PlayerPrefs.GetFloat("Ultrasonic_JumpDist", 20.0f);
        if (sliderJumpDistance != null) sliderJumpDistance.value = jDist;
        if (txtJumpDistance != null) txtJumpDistance.text = $"{jDist:F0} cm";

        // 音訊設定
        currentBgmIndex = PlayerPrefs.GetInt("SelectedBGM", 0);
        UpdateBgmButtonStyles(currentBgmIndex);

        float bgmVol = PlayerPrefs.GetFloat("BGM_Volume", 0.65f);
        if (sliderBgmVolume != null) sliderBgmVolume.value = bgmVol;
        if (txtBgmVolume != null) txtBgmVolume.text = $"{bgmVol * 100f:F0}%";

        float sfxVol = PlayerPrefs.GetFloat("SFX_Volume", 0.85f);
        if (sliderSfxVolume != null) sliderSfxVolume.value = sfxVol;
        if (txtSfxVolume != null) txtSfxVolume.text = $"{sfxVol * 100f:F0}%";
    }

    public void OnSaveSettings()
    {
        // 保存鍵盤
        PlayerPrefs.SetInt("Key_Left", (int)currentKeyLeft);
        PlayerPrefs.SetInt("Key_Right", (int)currentKeyRight);
        PlayerPrefs.SetInt("Key_Jump", (int)currentKeyJump);

        if (sliderKeyboardSpeed != null)
            PlayerPrefs.SetFloat("Keyboard_Speed", sliderKeyboardSpeed.value);

        // 保存控制器
        if (comPortInput != null)
        {
            string port = comPortInput.text.Trim();
            PlayerPrefs.SetString("COMPort", port);
            if (arduino != null) arduino.portName = port;
        }

        if (sliderTiltSensitivity != null)
            PlayerPrefs.SetFloat("Tilt_Sensitivity", sliderTiltSensitivity.value);

        if (sliderTiltDeadzone != null)
            PlayerPrefs.SetFloat("Tilt_Deadzone", sliderTiltDeadzone.value);

        if (sliderJumpDistance != null)
            PlayerPrefs.SetFloat("Ultrasonic_JumpDist", sliderJumpDistance.value);

        // 保存音訊
        PlayerPrefs.SetInt("SelectedBGM", currentBgmIndex);
        if (sliderBgmVolume != null) PlayerPrefs.SetFloat("BGM_Volume", sliderBgmVolume.value);
        if (sliderSfxVolume != null) PlayerPrefs.SetFloat("SFX_Volume", sliderSfxVolume.value);

        PlayerPrefs.Save();
        Debug.Log("[Settings] 鍵盤、控制器與音訊設定已成功儲存至 PlayerPrefs！");
        ShowPanel(mainMenuPanel);
    }

    public void OnResetDefaults()
    {
        currentKeyLeft = KeyCode.A;
        currentKeyRight = KeyCode.D;
        currentKeyJump = KeyCode.Space;

        if (txtKeyLeft != null) txtKeyLeft.text = "A";
        if (txtKeyRight != null) txtKeyRight.text = "D";
        if (txtKeyJump != null) txtKeyJump.text = "Space";

        if (sliderKeyboardSpeed != null) sliderKeyboardSpeed.value = 9.0f;
        if (sliderTiltSensitivity != null) sliderTiltSensitivity.value = 1.8f;
        if (sliderTiltDeadzone != null) sliderTiltDeadzone.value = 0.08f;
        if (sliderJumpDistance != null) sliderJumpDistance.value = 20.0f;
        if (comPortInput != null) comPortInput.text = "COM3";

        // 重置音訊
        SelectBGMTrack(0);
        if (sliderBgmVolume != null) sliderBgmVolume.value = 0.65f;
        if (txtBgmVolume != null) txtBgmVolume.text = "65%";
        if (sliderSfxVolume != null) sliderSfxVolume.value = 0.85f;
        if (txtSfxVolume != null) txtSfxVolume.text = "85%";
        AudioManager.Instance?.SetBGMVolume(0.65f);
        AudioManager.Instance?.SetSFXVolume(0.85f);
    }

    // --- 音訊曲目切換與按鈕高亮 ---
    public void SelectBGMTrack(int index)
    {
        ClearEventFocus();
        currentBgmIndex = Mathf.Clamp(index, 0, 2);
        PlayerPrefs.SetInt("SelectedBGM", currentBgmIndex);
        PlayerPrefs.Save();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SwitchBGM(currentBgmIndex);
        }

        UpdateBgmButtonStyles(currentBgmIndex);
    }

    void UpdateBgmButtonStyles(int selectedIndex)
    {
        Color selectedCol = new Color(0f, 0.85f, 0.95f);
        Color normalCol = new Color(0.1f, 0.18f, 0.32f);

        string[] names = {
            "⚡ 播放中：賽博霓虹脈衝 (Cyber Neon - 130 BPM)",
            "🕹️ 播放中：復古極速像素 (Chiptune Rush - 142 BPM)",
            "🌌 播放中：深空失重重力 (Deep Space Gravity - 126 BPM)"
        };

        if (txtCurrentBgm != null && selectedIndex >= 0 && selectedIndex < names.Length)
        {
            txtCurrentBgm.text = names[selectedIndex];
        }

        UpdateButtonColor(btnBgmTrack0, selectedIndex == 0 ? selectedCol : normalCol);
        UpdateButtonColor(btnBgmTrack1, selectedIndex == 1 ? selectedCol : normalCol);
        UpdateButtonColor(btnBgmTrack2, selectedIndex == 2 ? selectedCol : normalCol);
    }

    void UpdateButtonColor(Button btn, Color col)
    {
        if (btn == null) return;
        ColorBlock cb = btn.colors;
        cb.normalColor = col;
        btn.colors = cb;
    }

    // --- 鍵位綁定協程 ---
    public void StartRebind(string keyPrefName, Text displayTxt, System.Action<KeyCode> onSet)
    {
        if (isRebinding) return;
        StartCoroutine(RebindRoutine(keyPrefName, displayTxt, onSet));
    }

    IEnumerator RebindRoutine(string keyPrefName, Text displayTxt, System.Action<KeyCode> onSet)
    {
        isRebinding = true;
        if (displayTxt != null) displayTxt.text = "<color=yellow>請按鍵...</color>";

        yield return new WaitForSecondsRealtime(0.15f);

        KeyCode pressedKey = KeyCode.None;
        while (pressedKey == KeyCode.None)
        {
            if (Input.anyKeyDown)
            {
                foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(k) && k != KeyCode.Escape && k != KeyCode.Mouse0 && k != KeyCode.Mouse1)
                    {
                        pressedKey = k;
                        break;
                    }
                }
            }
            yield return null;
        }

        if (displayTxt != null) displayTxt.text = pressedKey.ToString();
        onSet?.Invoke(pressedKey);
        ClearEventFocus();
        isRebinding = false;
        Debug.Log($"[Rebind] 鍵位已更新: {keyPrefName} -> {pressedKey}");
    }

    // --- 切換設定分頁 (0: 鍵盤, 1: 控制器, 2: 音訊) ---
    public void SwitchSettingsTab(int tabIndex)
    {
        ClearEventFocus();
        if (keyboardSettingsPanel != null) keyboardSettingsPanel.SetActive(tabIndex == 0);
        if (controllerSettingsPanel != null) controllerSettingsPanel.SetActive(tabIndex == 1);
        if (audioSettingsPanel != null) audioSettingsPanel.SetActive(tabIndex == 2);

        Color activeCol = new Color(0f, 0.75f, 1f);
        Color normalCol = new Color(0.12f, 0.18f, 0.28f);

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

    // --- 面板流暢切換 ---
    public void ShowPanel(GameObject targetPanel)
    {
        if (targetPanel == null || targetPanel == currentActivePanel || isTransitioning) return;
        StartCoroutine(TransitionPanelRoutine(currentActivePanel, targetPanel));
    }

    IEnumerator TransitionPanelRoutine(GameObject fromPanel, GameObject toPanel)
    {
        isTransitioning = true;
        ClearEventFocus();

        float fadeSpeed = 7.0f;

        if (fromPanel != null)
        {
            CanvasGroup cgFrom = fromPanel.GetComponent<CanvasGroup>();
            if (cgFrom == null) cgFrom = fromPanel.AddComponent<CanvasGroup>();

            while (cgFrom.alpha > 0)
            {
                cgFrom.alpha -= Time.deltaTime * fadeSpeed;
                yield return null;
            }
            fromPanel.SetActive(false);
            cgFrom.alpha = 1f;
        }

        if (toPanel != null)
        {
            toPanel.SetActive(true);
            CanvasGroup cgTo = toPanel.GetComponent<CanvasGroup>();
            if (cgTo == null) cgTo = toPanel.AddComponent<CanvasGroup>();
            cgTo.alpha = 0f;

            while (cgTo.alpha < 1f)
            {
                cgTo.alpha += Time.deltaTime * fadeSpeed;
                yield return null;
            }
            cgTo.alpha = 1f;
        }

        currentActivePanel = toPanel;
        ClearEventFocus();
        isTransitioning = false;
    }

    void ClearEventFocus()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void UpdateHardwareReadout()
    {
        if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();

        if (arduino != null && arduino.distance > 0)
        {
            hardwareStatusText.text = $"<color=lime>● 實體裝置連線中 (Port: {arduino.portName})</color>\n" +
                                      $"超音波測距: <color=yellow>{arduino.distance:F1} cm</color> (上下揮手測試起跳)\n" +
                                      $"MPU-6050 傾角: X=<color=cyan>{arduino.acceleration.x:F2}</color>, Y=<color=cyan>{arduino.acceleration.y:F2}</color>";
        }
        else
        {
            hardwareStatusText.text = "<color=orange>○ 未檢測到實體硬體 (純鍵盤測試模式已啟用)</color>\n" +
                                      "鍵盤操作: [自訂左/右移] | [自訂跳躍鍵] | [R 鍵重試]";
        }
    }

    public void OnStartGame()
    {
        ClearEventFocus();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOpenLevelSelect()
    {
        ShowPanel(levelSelectPanel);
    }

    public void OnOpenSettings()
    {
        LoadAllSettings();
        ShowPanel(settingsPanel);
    }

    public void OnSelectLevel(int levelIndex)
    {
        ClearEventFocus();
        PlayerPrefs.SetInt("SelectedLevel", levelIndex);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnExitGame()
    {
        Application.Quit();
    }

    // --- Runtime 音訊設定面板自癒與補全 ---
    void EnsureAudioSettingsUI()
    {
        if (settingsPanel == null) return;

        // 1. 調整頂部分頁按鈕為三分頁佈局
        Transform tTabKey = settingsPanel.transform.Find("Btn_TabKeyboard");
        Transform tTabCtrl = settingsPanel.transform.Find("Btn_TabController");
        Transform tTabAudio = settingsPanel.transform.Find("Btn_TabAudio");

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (tTabKey != null)
        {
            RectTransform rtKey = tTabKey.GetComponent<RectTransform>();
            rtKey.anchoredPosition = new Vector2(-330, 310);
            rtKey.sizeDelta = new Vector2(300, 60);
            Text txt = tTabKey.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "【 鍵盤 KEYBOARD 】"; txt.fontSize = 26; }
            tabKeyboardBtn = tTabKey.GetComponent<Button>();
        }

        if (tTabCtrl != null)
        {
            RectTransform rtCtrl = tTabCtrl.GetComponent<RectTransform>();
            rtCtrl.anchoredPosition = new Vector2(0, 310);
            rtCtrl.sizeDelta = new Vector2(300, 60);
            Text txt = tTabCtrl.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = "【 控制器 CONTROLLER 】"; txt.fontSize = 26; }
            tabControllerBtn = tTabCtrl.GetComponent<Button>();
        }

        if (tTabAudio == null)
        {
            Button btnAudio = CreateStyledButton(settingsPanel.transform, "Btn_TabAudio", "【 音訊設定 AUDIO 】", new Vector2(330, 310), new Vector2(300, 60), 26, defaultFont);
            tabAudioBtn = btnAudio;
        }
        else
        {
            tabAudioBtn = tTabAudio.GetComponent<Button>();
        }

        // 2. 確保 AudioSettingsPanel 存在
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

        RectTransform rAudio = pAudio.GetComponent<RectTransform>();
        rAudio.anchorMin = new Vector2(0.5f, 0.5f); rAudio.anchorMax = new Vector2(0.5f, 0.5f);
        rAudio.anchoredPosition = new Vector2(0, -10);
        rAudio.sizeDelta = new Vector2(1000, 500);

        CreateText(pAudio.transform, "BgmHeader", "背景音樂曲目選擇 (SELECT BGM TRACK):", 28, new Color(0f, 1f, 0.9f), new Vector2(0, 185), new Vector2(800, 45), font, FontStyle.Bold);
        txtCurrentBgm = CreateText(pAudio.transform, "Txt_CurrentBgm", "⚡ 播放中：賽博霓虹脈衝 (Cyber Neon - 130 BPM)", 24, Color.yellow, new Vector2(0, 140), new Vector2(860, 40), font, FontStyle.Bold);

        btnBgmTrack0 = CreateStyledButton(pAudio.transform, "Btn_Bgm0", "⚡ 賽博霓虹 (Cyber Neon)", new Vector2(-310, 75), new Vector2(290, 55), 22, font);
        btnBgmTrack1 = CreateStyledButton(pAudio.transform, "Btn_Bgm1", "🕹️ 復古像素 (Chiptune)", new Vector2(0, 75), new Vector2(290, 55), 22, font);
        btnBgmTrack2 = CreateStyledButton(pAudio.transform, "Btn_Bgm2", "🌌 深空重力 (Deep Space)", new Vector2(310, 75), new Vector2(290, 55), 22, font);

        CreateText(pAudio.transform, "BgmVolLabel", "音樂音量 (BGM VOLUME):", 28, Color.white, new Vector2(-200, -5), new Vector2(360, 50), font, FontStyle.Bold);
        sliderBgmVolume = CreateSlider(pAudio.transform, "Slider_BgmVolume", new Vector2(120, -5), new Vector2(240, 30), 0f, 1f, 0.65f);
        txtBgmVolume = CreateText(pAudio.transform, "Txt_BgmVolume", "65%", 26, Color.cyan, new Vector2(290, -5), new Vector2(80, 50), font, FontStyle.Bold);

        CreateText(pAudio.transform, "SfxVolLabel", "音效音量 (SFX VOLUME):", 28, Color.white, new Vector2(-200, -75), new Vector2(360, 50), font, FontStyle.Bold);
        sliderSfxVolume = CreateSlider(pAudio.transform, "Slider_SfxVolume", new Vector2(120, -75), new Vector2(240, 30), 0f, 1f, 0.85f);
        txtSfxVolume = CreateText(pAudio.transform, "Txt_SfxVolume", "85%", 26, Color.cyan, new Vector2(290, -75), new Vector2(80, 50), font, FontStyle.Bold);

        btnTestSfx = CreateStyledButton(pAudio.transform, "Btn_TestSfx", "🔊 測試音效播放 (TEST SFX)", new Vector2(0, -145), new Vector2(380, 50), 24, font);

        CreateText(pAudio.transform, "AudioHint", "💡 提示：點擊上方曲目可即時試聽切換，遊戲關卡將自動同步您選定的音樂！", 22, new Color(0.4f, 0.9f, 1f), new Vector2(0, -210), new Vector2(880, 45), font, FontStyle.Normal);

        pAudio.SetActive(false);
        return pAudio;
    }

    void HookAudioUIReferences(GameObject pAudio)
    {
        if (pAudio == null) return;
        if (txtCurrentBgm == null) txtCurrentBgm = pAudio.transform.Find("Txt_CurrentBgm")?.GetComponent<Text>();
        if (btnBgmTrack0 == null) btnBgmTrack0 = pAudio.transform.Find("Btn_Bgm0")?.GetComponent<Button>();
        if (btnBgmTrack1 == null) btnBgmTrack1 = pAudio.transform.Find("Btn_Bgm1")?.GetComponent<Button>();
        if (btnBgmTrack2 == null) btnBgmTrack2 = pAudio.transform.Find("Btn_Bgm2")?.GetComponent<Button>();
        if (sliderBgmVolume == null) sliderBgmVolume = pAudio.transform.Find("Slider_BgmVolume")?.GetComponent<Slider>();
        if (txtBgmVolume == null) txtBgmVolume = pAudio.transform.Find("Txt_BgmVolume")?.GetComponent<Text>();
        if (sliderSfxVolume == null) sliderSfxVolume = pAudio.transform.Find("Slider_SfxVolume")?.GetComponent<Slider>();
        if (txtSfxVolume == null) txtSfxVolume = pAudio.transform.Find("Txt_SfxVolume")?.GetComponent<Text>();
        if (btnTestSfx == null) btnTestSfx = pAudio.transform.Find("Btn_TestSfx")?.GetComponent<Button>();
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
        RectTransform trt = tObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;

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
        Image hImg = handle.AddComponent<Image>();
        hImg.color = Color.white;
        hImg.raycastTarget = true;
        RectTransform hRect = handle.GetComponent<RectTransform>();
        hRect.sizeDelta = new Vector2(24, 0);
        slider.handleRect = hRect;
        slider.targetGraphic = hImg;

        return slider;
    }
}
