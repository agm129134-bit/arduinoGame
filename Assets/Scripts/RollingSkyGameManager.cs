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

    private int collectedGems = 0;
    private int totalGemsInLevel = 0;
    private bool isGameOver = false;
    private bool isVictory = false;
    private bool isPaused = false;

    private ArduinoReceiver arduino;

    void Awake()
    {
        // 確保載入關卡時時間縮放正常
        Time.timeScale = 1.0f;
    }

    void Start()
    {
        if (ball == null)
        {
            RollingSkyBallController bc = FindAnyObjectByType<RollingSkyBallController>();
            if (bc != null) ball = bc.transform;
        }

        if (ball != null) startZ = ball.position.z;

        arduino = FindAnyObjectByType<ArduinoReceiver>();
        totalGemsInLevel = FindObjectsByType<GemCollectible>(FindObjectsSortMode.None).Length;

        // 【Runtime 自癒架構】：即使玩家沒有透過 Editor 重新生成場景，程式碼也會全自動補全 EventSystem 與 PausePanel
        EnsureAllUI();

        // 綁定所有面板按鈕事件
        BindAllButtonsRuntime();

        UpdateGemUI();
    }

    void Update()
    {
        // 快速重開鍵
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
            return;
        }

        // ESC 鍵處理：若已結算則直接回主選單，遊戲中則切換暫停面板
        if (Input.GetKeyDown(KeyCode.Escape))
        {
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

    // --- 暫停管理 (含自動容錯建立) ---
    public void TogglePause()
    {
        if (isGameOver || isVictory) return;

        // 若尚未獲取 pausePanel 則進行即時自癒建立
        if (pausePanel == null)
        {
            EnsureAllUI();
            BindAllButtonsRuntime();
        }

        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1.0f;

        if (pausePanel != null)
        {
            pausePanel.transform.SetAsLastSibling(); // 確保置於 UI 最頂層
            pausePanel.SetActive(isPaused);
        }

        Debug.Log(isPaused ? "[GameManager] 遊戲已暫停 (PAUSED)" : "[GameManager] 遊戲已繼續 (RESUMED)");
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1.0f;
        if (pausePanel != null) pausePanel.SetActive(false);
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
        // 1. 確保實體 EventSystem 存在 (保證 UI 點擊射線能正常傳遞)
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 1.5. 確保 AudioManager 存在 (全域管理背景音樂與動作音效)
        if (AudioManager.Instance == null && FindAnyObjectByType<AudioManager>() == null)
        {
            GameObject audioObj = new GameObject("AudioManager");
            audioObj.AddComponent<AudioManager>();
        }

        // 2. 尋找或建立 Canvas
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

        // 3. 尋找或動態建立 PausePanel
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

        if (pausePanel != null)
        {
            pausePanel.transform.SetAsLastSibling();
            pausePanel.SetActive(false);
        }

        // 4. 補強 GameOverPanel 與 VictoryPanel 按鈕
        EnsurePanelButtons(gameOverPanel, defaultFont);
        EnsurePanelButtons(victoryPanel, defaultFont);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    GameObject BuildDynamicPausePanel(Transform canvasTransform, Font font)
    {
        GameObject panel = new GameObject("PausePanel");
        panel.transform.SetParent(canvasTransform, false);

        Image img = panel.AddComponent<Image>();
        img.color = new Color(0.04f, 0.06f, 0.12f, 0.92f);
        img.raycastTarget = true; // 阻擋下方穿透

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // 標題與提示文字
        CreateText(panel.transform, "Title", "遊戲暫停 PAUSED", 56, new Color(0f, 1f, 0.9f), new Vector2(0, 160), new Vector2(800, 80), font, FontStyle.Bold);
        CreateText(panel.transform, "Hint", "💡 提示：再次按下 [ESC] 鍵可直接繼續遊戲", 24, new Color(0.4f, 0.9f, 1f), new Vector2(0, 95), new Vector2(800, 60), font, FontStyle.Normal);

        // 三大按鈕
        CreateStyledButton(panel.transform, "Btn_Resume", "▶ 繼續遊戲 (RESUME)", new Vector2(0, 15), new Vector2(440, 65), 30, font);
        CreateStyledButton(panel.transform, "Btn_Restart", "🔄 重新挑戰 (RESTART)", new Vector2(0, -65), new Vector2(440, 65), 30, font);
        CreateStyledButton(panel.transform, "Btn_MainMenu", "🏠 回到主選單 (MAIN MENU)", new Vector2(0, -145), new Vector2(440, 65), 30, font);

        return panel;
    }

    void EnsurePanelButtons(GameObject panel, Font font)
    {
        if (panel == null) return;

        if (panel.transform.Find("Btn_Restart") == null)
        {
            CreateStyledButton(panel.transform, "Btn_Restart", "🔄 重新挑戰 (RETRY)", new Vector2(0, -60), new Vector2(440, 65), 30, font);
        }

        if (panel.transform.Find("Btn_MainMenu") == null)
        {
            CreateStyledButton(panel.transform, "Btn_MainMenu", "🏠 回到主選單 (MAIN MENU)", new Vector2(0, -140), new Vector2(440, 65), 30, font);
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

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(2, -2);

        return t;
    }

    // --- 動態按鈕綁定 ---
    void BindAllButtonsRuntime()
    {
        if (pausePanel != null)
        {
            BindButton(pausePanel, "Btn_Resume", ResumeGame);
            BindButton(pausePanel, "Btn_Restart", RestartLevel);
            BindButton(pausePanel, "Btn_MainMenu", ReturnToMainMenu);
        }

        if (gameOverPanel != null)
        {
            BindButton(gameOverPanel, "Btn_Restart", RestartLevel);
            BindButton(gameOverPanel, "Btn_MainMenu", ReturnToMainMenu);
        }

        if (victoryPanel != null)
        {
            BindButton(victoryPanel, "Btn_Restart", RestartLevel);
            BindButton(victoryPanel, "Btn_MainMenu", ReturnToMainMenu);
        }
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

    void UpdateHardwareHUD()
    {
        if (hardwareStatusText == null) return;

        if (arduino != null && arduino.distance > 0)
        {
            hardwareStatusText.text = $"超音波測距: <color=yellow>{arduino.distance:F1} cm</color> (上下揮動跳躍) | 傾斜感應: <color=cyan>{arduino.acceleration.x:F2}</color> | <color=white>[ESC: 暫停選單]</color>";
        }
        else
        {
            hardwareStatusText.text = "鍵盤測試模式: [A/D: 左右移] | [Space: 跳躍] | [R: 重試] | <color=cyan>[ESC: 暫停 / 回到選單]</color>";
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
            gemText.text = $"💎 {collectedGems} / {totalGemsInLevel}";
        }
    }

    public void TriggerGameOver(string reason)
    {
        if (isGameOver || isVictory) return;
        isGameOver = true;

        if (pausePanel != null) pausePanel.SetActive(false);

        float currentDist = (ball != null) ? ball.position.z - startZ : 0f;
        float percent = Mathf.Clamp01(currentDist / trackLength) * 100f;

        if (gameOverPanel != null)
        {
            gameOverPanel.transform.SetAsLastSibling();
            gameOverPanel.SetActive(true);
            if (gameOverReasonText != null) gameOverReasonText.text = reason;
            if (gameOverProgressText != null) gameOverProgressText.text = $"完成度: {percent:F0}%";
        }

        Debug.Log($"[GameManager] Game Over! {reason} (完成度: {percent:F0}%)");
    }

    public void TriggerVictory()
    {
        if (isGameOver || isVictory) return;
        isVictory = true;

        if (pausePanel != null) pausePanel.SetActive(false);

        if (victoryPanel != null)
        {
            // 動態更新勝利面板文字（將「滾動的天空」換成新原創遊戲名稱）
            Transform subT = victoryPanel.transform.Find("Sub");
            if (subT != null)
            {
                Text t = subT.GetComponent<Text>();
                if (t != null) t.text = "你成功征服了《極速躍動 3D (Rolling Leap)》賽博賽道！";
            }

            victoryPanel.transform.SetAsLastSibling();
            victoryPanel.SetActive(true);
        }

        AudioManager.Instance?.PlayVictory();
        Debug.Log("[GameManager] 🎉 100% 完美通關《極速躍動 3D (Rolling Leap)》賽博賽道！");
    }
}
