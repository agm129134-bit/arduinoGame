using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public class BuildRollingSkyScene : Editor
{
    [MenuItem("Game Setup/Generate Rolling Sky Track")]
    public static void GenerateTrack()
    {
        // 1. 建立或開啟場景
        string scenePath = "Assets/Scenes/RollingSkyScene.unity";
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. 建立材質
        Material matTileDark = CreateMaterial("Mat_TileDark", new Color(0.08f, 0.1f, 0.15f), Color.black);
        Material matTileCyan = CreateMaterial("Mat_TileCyan", new Color(0.12f, 0.16f, 0.24f), new Color(0f, 0.6f, 0.8f) * 0.4f);
        Material matBall = CreateMaterial("Mat_Ball", new Color(0.1f, 0.8f, 1f), new Color(0f, 0.7f, 1f) * 0.8f);
        Material matHazard = CreateMaterial("Mat_Hazard", new Color(0.9f, 0.1f, 0.2f), new Color(1f, 0f, 0.2f) * 1.2f);
        Material matJumpPad = CreateMaterial("Mat_JumpPad", new Color(0.1f, 1f, 0.6f), new Color(0.1f, 1f, 0.6f) * 1.5f);
        Material matGem = CreateMaterial("Mat_Gem", new Color(1f, 0.85f, 0.1f), new Color(1f, 0.85f, 0.1f) * 1.2f);
        Material matGoal = CreateMaterial("Mat_Goal", new Color(1f, 0.8f, 0.2f), new Color(1f, 0.8f, 0.2f) * 1.5f);

        // 3. 建立燈光
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.9f, 0.95f, 1f);
        light.intensity = 1.3f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.15f, 0.2f, 0.3f);

        // 4. 建立軌道根物件
        GameObject trackRoot = new GameObject("--- Track Hierarchy ---");

        // 建立賽道網格 (Rolling Sky 標準 5 欄道, 寬度每欄 1.5 單位, 長度每格 2 單位)
        float colWidth = 1.4f;
        float rowLen = 2.0f;
        float trackTotalLength = 260.0f;

        // 生成關卡模組
        // Section 1: Z=0 ~ 40 (起步 5 欄平順大道)
        BuildTileSection(trackRoot.transform, 0, 20, new int[] { -2, -1, 0, 1, 2 }, colWidth, rowLen, matTileDark, matTileCyan);

        // Section 2: Z=40 ~ 48 懸崖斷層！(需要超音波上下揮動或 Space 跳躍)
        // (40~48 留空)

        // Section 3: Z=48 ~ 80 (窄橋與左右急避)
        BuildTileSection(trackRoot.transform, 24, 40, new int[] { -1, 0, 1 }, colWidth, rowLen, matTileDark, matTileCyan);

        // 放置尖刺障礙
        CreateHazard(trackRoot.transform, new Vector3(0, 0.5f, 60), matHazard);
        CreateHazard(trackRoot.transform, new Vector3(-colWidth, 0.5f, 70), matHazard);
        CreateHazard(trackRoot.transform, new Vector3(colWidth, 0.5f, 76), matHazard);

        // Section 4: Z=80 ~ 90 斷崖 + 彈射跳台！
        BuildTileSection(trackRoot.transform, 40, 44, new int[] { 0 }, colWidth, rowLen, matTileDark, matTileCyan);
        CreateJumpPad(trackRoot.transform, new Vector3(0, 0.1f, 86), matJumpPad);

        // (88 ~ 116 空中大飛躍！中間無地塊)

        // Section 5: Z=116 ~ 160 (著陸區與連續節奏跳)
        BuildTileSection(trackRoot.transform, 58, 70, new int[] { -1, 0, 1 }, colWidth, rowLen, matTileDark, matTileCyan);
        // 斷層 140 ~ 146
        BuildTileSection(trackRoot.transform, 73, 85, new int[] { -1, 0, 1 }, colWidth, rowLen, matTileDark, matTileCyan);

        CreateHazard(trackRoot.transform, new Vector3(0, 0.5f, 130), matHazard);
        CreateHazard(trackRoot.transform, new Vector3(colWidth, 0.5f, 154), matHazard);
        CreateHazard(trackRoot.transform, new Vector3(-colWidth, 0.5f, 166), matHazard);

        // Section 6: Z=170 ~ 240 (終極衝刺 5 欄道 + 障礙穿梭)
        BuildTileSection(trackRoot.transform, 85, 120, new int[] { -2, -1, 0, 1, 2 }, colWidth, rowLen, matTileDark, matTileCyan);
        
        CreateHazard(trackRoot.transform, new Vector3(-colWidth, 0.5f, 180), matHazard);
        CreateHazard(trackRoot.transform, new Vector3(colWidth, 0.5f, 180), matHazard);
        CreateHazard(trackRoot.transform, new Vector3(0, 0.5f, 195), matHazard);
        CreateHazard(trackRoot.transform, new Vector3(-colWidth, 0.5f, 210), matHazard);
        CreateHazard(trackRoot.transform, new Vector3(colWidth, 0.5f, 220), matHazard);

        // 終點傳送星門 (Z=242)
        CreateFinishGate(trackRoot.transform, new Vector3(0, 1.5f, 242), matGoal);

        // 散落收集寶石 (前三關寶石大量增加，共 22 顆)
        for (int z = 10; z < 235; z += 10)
        {
            float gx = Mathf.Sin(z * 0.2f) * colWidth;
            CreateGem(trackRoot.transform, new Vector3(gx, 1.0f, z), matGem);
        }
        CreateGem(trackRoot.transform, new Vector3(0, 4.5f, 102), matGem); // 彈跳空中寶石

        // 4.5. 添加全自動動態賽道與 10 關生成器
        GameObject genObj = new GameObject("RollingSkyTrackGenerator");
        RollingSkyTrackGenerator gen = genObj.AddComponent<RollingSkyTrackGenerator>();
        gen.matTileDark = matTileDark;
        gen.matTileCyan = matTileCyan;
        gen.matHazard = matHazard;
        gen.matJumpPad = matJumpPad;
        gen.matGem = matGem;
        gen.matGoal = matGoal;

        // 5. 建立小球主角
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "RollingBall";
        ball.transform.position = new Vector3(0, 0.7f, 2f);
        ball.GetComponent<MeshRenderer>().material = matBall;
        
        Rigidbody rb = ball.AddComponent<Rigidbody>();
        rb.mass = 1.0f;
        
        RollingSkyBallController ballCtrl = ball.AddComponent<RollingSkyBallController>();
        RollingSkyJumpDetector jumpDet = ball.AddComponent<RollingSkyJumpDetector>();
        jumpDet.ball = ballCtrl;

        // 加發光光軌
        TrailRenderer tr = ball.AddComponent<TrailRenderer>();
        tr.time = 0.4f;
        tr.startWidth = 0.6f;
        tr.endWidth = 0.05f;
        tr.material = matBall;
        tr.startColor = new Color(0f, 0.8f, 1f, 0.8f);
        tr.endColor = new Color(0f, 0.4f, 1f, 0f);

        // 6. 建立追蹤攝影機
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();
        RollingSkyCamera rsc = camObj.AddComponent<RollingSkyCamera>();
        rsc.target = ball.transform;

        // 7. 建立 ArduinoReceiver
        GameObject ardObj = new GameObject("ArduinoReceiver");
        ArduinoReceiver ard = ardObj.AddComponent<ArduinoReceiver>();
        ballCtrl.arduino = ard;
        jumpDet.arduino = ard;

        // 7.5. 建立 EventSystem (確保 UI 按鈕點擊 100% 響應)
        GameObject esObj = new GameObject("EventSystem");
        esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // 8. 建立 UI Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 進度條 UI
        GameObject pbObj = new GameObject("ProgressBar");
        pbObj.transform.SetParent(canvasObj.transform, false);
        Slider slider = pbObj.AddComponent<Slider>();
        RectTransform pbRect = pbObj.GetComponent<RectTransform>();
        pbRect.anchorMin = new Vector2(0.5f, 1f);
        pbRect.anchorMax = new Vector2(0.5f, 1f);
        pbRect.pivot = new Vector2(0.5f, 1f);
        pbRect.anchoredPosition = new Vector2(0, -30);
        pbRect.sizeDelta = new Vector2(600, 24);

        // 進度條背景與填充
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(pbObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.sizeDelta = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(pbObj.transform, false);
        RectTransform faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero; faRect.anchorMax = Vector2.one; faRect.sizeDelta = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0f, 0.8f, 1f);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;
        slider.fillRect = fillRect;

        // 進度文字
        GameObject pTextObj = new GameObject("ProgressText");
        pTextObj.transform.SetParent(canvasObj.transform, false);
        Text pText = pTextObj.AddComponent<Text>();
        pText.font = defaultFont;
        pText.fontSize = 32;
        pText.color = Color.white;
        pText.alignment = TextAnchor.MiddleCenter;
        pText.text = "0%";
        RectTransform ptRect = pTextObj.GetComponent<RectTransform>();
        ptRect.anchorMin = new Vector2(0.5f, 1f); ptRect.anchorMax = new Vector2(0.5f, 1f);
        ptRect.anchoredPosition = new Vector2(0, -70);
        ptRect.sizeDelta = new Vector2(200, 50);

        // 寶石文字
        GameObject gemTextObj = new GameObject("GemText");
        gemTextObj.transform.SetParent(canvasObj.transform, false);
        Text gText = gemTextObj.AddComponent<Text>();
        gText.font = defaultFont;
        gText.fontSize = 32;
        gText.color = new Color(1f, 0.9f, 0.2f);
        gText.alignment = TextAnchor.MiddleLeft;
        gText.text = "💎 0 / 7";
        RectTransform gtRect = gemTextObj.GetComponent<RectTransform>();
        gtRect.anchorMin = new Vector2(0, 1f); gtRect.anchorMax = new Vector2(0, 1f);
        gtRect.anchoredPosition = new Vector2(40, -40);
        gtRect.sizeDelta = new Vector2(300, 60);

        // 底部硬體感測 HUD
        GameObject hudObj = new GameObject("HardwareHUD");
        hudObj.transform.SetParent(canvasObj.transform, false);
        Text hudText = hudObj.AddComponent<Text>();
        hudText.font = defaultFont;
        hudText.fontSize = 24;
        hudText.color = Color.white;
        hudText.alignment = TextAnchor.MiddleCenter;
        hudText.text = "超音波感應就緒 | MPU-6050 陀螺儀連線中";
        RectTransform hudRect = hudObj.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0.5f, 0); hudRect.anchorMax = new Vector2(0.5f, 0);
        hudRect.anchoredPosition = new Vector2(0, 40);
        hudRect.sizeDelta = new Vector2(1000, 50);

        // 9. Pause 面板 (ESC 鍵呼叫)
        GameObject pausePanel = CreatePopupPanel(canvasObj.transform, "PausePanel", new Color(0.04f, 0.06f, 0.12f, 0.92f));
        CreateText(pausePanel.transform, "Title", "遊戲暫停 PAUSED", 56, new Color(0f, 1f, 0.9f), new Vector2(0, 160), defaultFont);
        CreateText(pausePanel.transform, "Hint", "💡 提示：再次按下 [ESC] 鍵可直接繼續遊戲", 24, new Color(0.4f, 0.9f, 1f), new Vector2(0, 95), defaultFont);

        CreateStyledButton(pausePanel.transform, "Btn_Resume", "▶ 繼續遊戲 (RESUME)", new Vector2(0, 15), new Vector2(440, 65), 30, defaultFont);
        CreateStyledButton(pausePanel.transform, "Btn_Restart", "🔄 重新挑戰 (RESTART)", new Vector2(0, -65), new Vector2(440, 65), 30, defaultFont);
        CreateStyledButton(pausePanel.transform, "Btn_MainMenu", "🏠 回到主選單 (MAIN MENU)", new Vector2(0, -145), new Vector2(440, 65), 30, defaultFont);

        // 10. GameOver 面板
        GameObject goPanel = CreatePopupPanel(canvasObj.transform, "GameOverPanel", new Color(0.35f, 0.03f, 0.06f, 0.92f));
        Text goReason = CreateText(goPanel.transform, "Reason", "墜落深淵！", 52, new Color(1f, 0.35f, 0.35f), new Vector2(0, 160), defaultFont);
        Text goProg = CreateText(goPanel.transform, "Progress", "完成度: 0%", 38, Color.yellow, new Vector2(0, 90), defaultFont);
        CreateText(goPanel.transform, "Hint", "快捷鍵：按 [R] 鍵重新挑戰 | [ESC] 回到主選單", 24, new Color(0.85f, 0.85f, 0.85f), new Vector2(0, 20), defaultFont);

        CreateStyledButton(goPanel.transform, "Btn_Restart", "🔄 重新挑戰 (RETRY)", new Vector2(0, -60), new Vector2(440, 65), 30, defaultFont);
        CreateStyledButton(goPanel.transform, "Btn_MainMenu", "🏠 回到主選單 (MAIN MENU)", new Vector2(0, -140), new Vector2(440, 65), 30, defaultFont);

        // 11. Victory 面板
        GameObject vicPanel = CreatePopupPanel(canvasObj.transform, "VictoryPanel", new Color(0.02f, 0.22f, 0.15f, 0.92f));
        CreateText(vicPanel.transform, "Title", "🎉 100% 完美通關！", 56, Color.yellow, new Vector2(0, 160), defaultFont);
        CreateText(vicPanel.transform, "Sub", "你成功征服了《極速躍動 3D (Rolling Leap)》賽博賽道！", 32, Color.white, new Vector2(0, 95), defaultFont);
        CreateText(vicPanel.transform, "Hint", "快捷鍵：按 [R] 鍵再次挑戰 | [ESC] 回到主選單", 24, new Color(0.85f, 0.85f, 0.85f), new Vector2(0, 25), defaultFont);

        CreateStyledButton(vicPanel.transform, "Btn_Restart", "🔄 再次挑戰 (REPLAY)", new Vector2(0, -55), new Vector2(440, 65), 30, defaultFont);
        CreateStyledButton(vicPanel.transform, "Btn_MainMenu", "🏠 回到主選單 (MAIN MENU)", new Vector2(0, -135), new Vector2(440, 65), 30, defaultFont);

        // 12. GameManager 配置
        GameObject gmObj = new GameObject("GameManager");
        RollingSkyGameManager gm = gmObj.AddComponent<RollingSkyGameManager>();
        gm.ball = ball.transform;
        gm.trackLength = trackTotalLength;
        gm.progressBar = slider;
        gm.progressText = pText;
        gm.gemText = gText;
        gm.hardwareStatusText = hudText;
        gm.pausePanel = pausePanel;
        gm.gameOverPanel = goPanel;
        gm.gameOverReasonText = goReason;
        gm.gameOverProgressText = goProg;
        gm.victoryPanel = vicPanel;

        // 儲存場景
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("🎉 《RollingSky3D》場景與賽道全自動建立完成！路徑: " + scenePath);
    }

    static void BuildTileSection(Transform parent, int startRow, int endRow, int[] lanes, float colW, float rowL, Material darkMat, Material cyanMat)
    {
        for (int r = startRow; r < endRow; r++)
        {
            foreach (int col in lanes)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Tile_{col}_{r}";
                tile.transform.SetParent(parent);
                tile.transform.position = new Vector3(col * colW, 0, r * rowL);
                tile.transform.localScale = new Vector3(colW * 0.96f, 0.2f, rowL * 0.96f);

                // 棋盤格交替材質
                bool isCyan = (Mathf.Abs(col) + r) % 2 == 0;
                tile.GetComponent<MeshRenderer>().material = isCyan ? cyanMat : darkMat;
            }
        }
    }

    static void CreateHazard(Transform parent, Vector3 pos, Material mat)
    {
        GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spike.name = "Hazard_Spike";
        spike.transform.SetParent(parent);
        spike.transform.position = pos;
        spike.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f);
        spike.GetComponent<MeshRenderer>().material = mat;
        spike.AddComponent<Hazard>();
    }

    static void CreateJumpPad(Transform parent, Vector3 pos, Material mat)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "JumpPad";
        pad.transform.SetParent(parent);
        pad.transform.position = pos;
        pad.transform.localScale = new Vector3(1.3f, 0.25f, 1.8f);
        pad.GetComponent<MeshRenderer>().material = mat;
        
        BoxCollider bc = pad.GetComponent<BoxCollider>();
        bc.isTrigger = true;
        pad.AddComponent<JumpPad>();
    }

    static void CreateGem(Transform parent, Vector3 pos, Material mat)
    {
        GameObject gem = new GameObject("Gem");
        gem.transform.SetParent(parent);
        gem.transform.position = pos;
        gem.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        MeshRenderer mr = gem.AddComponent<MeshRenderer>();
        mr.material = mat;
        gem.AddComponent<GemCollectible>();
    }

    static void CreateFinishGate(Transform parent, Vector3 pos, Material mat)
    {
        GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gate.name = "FinishStarGate";
        gate.transform.SetParent(parent);
        gate.transform.position = pos;
        gate.transform.localScale = new Vector3(8f, 4f, 0.5f);
        gate.GetComponent<MeshRenderer>().material = mat;
        gate.GetComponent<BoxCollider>().isTrigger = true;
    }

    static Material CreateMaterial(string name, Color albedo, Color emission)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            mat.color = albedo;
            if (emission != Color.black)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static GameObject CreatePopupPanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        img.color = color;
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        return panel;
    }

    static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, Vector2 pos, Font font)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = content;
        t.raycastTarget = false;
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(800, 80);
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
        RectTransform trt = tObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;

        Outline outline = tObj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(2, -2);

        return btn;
    }
}
