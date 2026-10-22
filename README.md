# arduinoGame - 極速躍動 3D (Rolling Leap 3D)

🎮 **《極速躍動 3D》（Rolling Leap 3D）** 是一款結合 **Unity 6 3D 物理引擎** 與 **Arduino 實體感測互動** 的疾速節奏跑酷遊戲。玩家可藉由實體三軸傾角控制器與超音波推進手勢感應，或純鍵盤滑鼠模式來操控躍動球體在空中賽道疾馳、挑戰 500 米通天天梯、收集水晶寶石並避開障礙陷阱！

---

## 🌟 遊戲核心特色與最新升級 (v0.9.7)

### 0. 🖱️ 游標鎖定與沉浸式無界體驗 (Cursor Lock & Seamless Camera Control)
- **進入遊戲游標自動鎖定與隱藏**：
  - 進入關卡或天梯攀爬模式時，滑鼠游標自動進入鎖定模式 (`CursorLockMode.Locked; Cursor.visible = false;`)。
  - 徹底解決**游標滑出視窗外或多螢幕誤點桌面**的問題，大幅提升跑酷與視角環繞時的沉浸感。
- **無死角 360 度流暢旋轉**：
  - 在游標鎖定狀態下，滑鼠相對位移持續精確輸入，視角環繞旋轉平滑無上限。
- **智慧選單與結算自動解鎖**：
  - 按下 `ESC` 暫停、開啟即時設定分頁、關卡死亡、通關結算或返回主選單時，游標自動解除鎖定並恢復顯示 (`CursorLockMode.None; Cursor.visible = true;`)，UI 點擊順暢自如。
  - 支援視窗切回 (Focus) 自動重新鎖定與畫面點擊安全回鎖機制。

---

### 1. 🏰 全新極限攀爬模式：通天天梯 (Sky Tower Mode)
- **Only Up 風格 500 米極限專注體驗**：
  - 專為考驗玩家專注力與微操設計的垂直攀爬挑戰，全程共分為六大分區：
    1. **0m ~ 80m【地表基座與入門階梯】**：寬闊新手熟悉路徑。
    2. **80m ~ 160m【機械起重與鋼骨鷹架】**：狹窄工字鋼樑與交錯斜坡。
    3. **160m ~ 260m【微風浮島與動態跳躍】**：週期性水平橫移與垂直升降動態浮島。
    4. **260m ~ 360m【空中花園與曲折圓柱】**：極限窄道、弧形階梯與旋轉機關。
    5. **360m ~ 440m【暴風雷雲與消失板】**：視野開闊高空考驗。
    6. **440m ~ 500m【天頂星環與通天終點星門】**：踏入金色光門達成登頂通關！
- **實體體感操控創新**：
  - **三軸加速度計 (MPU-6050 / ADXL345)**：裝置向前傾斜小球向前衝刺，向後傾倒退煞車，左右傾斜控制左右橫向走位。
  - **超音波推進手勢跳躍 (HC-SR04)**：手持實體裝置**往電腦螢幕方向向前推進**，距離縮短時立即感應觸發強大垂直推力跳躍！
- **360 度自由環繞視角系統 (Orbit Camera)**：
  - **滑鼠控制**：滑鼠滑動無死角水平環繞 (Yaw) 與俯仰視角 (Pitch)。
  - **俯仰角限制 (-15° ~ 75°)**：防止鏡頭穿入地面或翻轉顛倒，既能俯視下方落點，亦能仰視上方高聳塔樓。
  - **實體搖桿支援**：Arduino 雙軸類比搖桿附帶死區濾波平滑旋轉。
  - **鍵盤快捷視角**：支援鍵盤 `Q` / `E` 鍵快速旋轉視角。
  - **相機水平朝向向量聯動 (Camera Forward/Right)**：小球移動方向與鏡頭朝向完全同步，玩家視角轉向哪裡，往前傾/按 W 就朝哪裡奔馳！
- **硬派墜落機制**：
  - 無中途存檔點，失誤摔落考驗心態，看跌落至哪一層平台或從地面重新攀登。

---

### 2. ⚙️ 專業級四大分頁設定選單 (Settings System)
- **主選單與遊戲內按 ESC 暫停雙端一致體驗**：
  - **【 畫面 DISPLAY 】**：支援全螢幕 / 視窗化切換與 4 種主流解析度（1080p / 900p / 768p / 720p）一鍵套用。
  - **【 鍵盤 / 滑鼠 KEYBOARD 】**：支援自訂鍵位重綁、鍵盤橫移速度調節，並新增 **滑鼠視角靈敏度 (0.5x ~ 5.0x)** 滑桿！
  - **【 控制器 CONTROLLER 】**：支援 COM 埠配置、陀螺儀轉向靈敏度、手抖死區過濾、超音波跳躍觸發距離，以及 **搖桿視角靈敏度 (0.5x ~ 5.0x)** 滑桿，附帶即時硬體訊號監視看板！
  - **【 音訊 AUDIO 】**：3 首原創主題背景音樂隨選即播（霓虹賽博、晶片狂潮、重力深淵），音樂與音效獨立音量調節與一鍵試聽。
- **全新防重疊排版**：全面修正選項文字與滑桿間距，以標準 60px/65px 間隔排列，清爽美觀。
- **雙向靈敏度同步**：無論在鍵盤或控制器分頁修改視角靈敏度，皆自動即時同步並儲存於 `PlayerPrefs`。

---

### 3. 🎮 更多經典遊玩模式
- **10 大特色漸進關卡 (10 Handcrafted Levels)**：
  - 從 LV.1「霓虹大道」到 LV.10「終極躍動」，難度星級階梯式上升，包含高速衝刺、空中大跳躍、尖刺迷宮與極限鋼索道。
- **⚡ 無盡極限跑酷模式 (Endless Mode)**：
  - 動態隨機生成路況，速度隨里程持續飆升，挑戰個人極限與最高分紀錄！
- **🛠️ 自訂場地編輯器 (Track Builder Mode)**：
  - 上帝俯視視角網格編輯，隨意鋪設賽道地磚、彈跳墊、尖刺陷阱與水晶，支援即時試玩與本地 JSON 儲存。
- **🛍️ 球體外觀商店 (Skin Shop)**：
  - 5 款炫彩光效皮膚（經典電藍、霓虹烈焰、賽博矩陣、黃金至尊、銀河虛空），以關卡中收集的水晶解鎖並裝備！
- **💎 璀璨 3D 多面體水晶寶石 (3D Crystal Diamond)**：
  - 程序化生成立體八面體鑽石 Mesh，高反光自發光霓虹材質，旋轉浮空閃耀。
- **🖥️ 主選單「左四右四」完美對稱佈局**：
  - 介面全新升級，左側 4 個核心玩法按鈕、右側 4 個系統與周邊按鈕，對稱美觀。

---

## 🛠️ 硬體電路與接線配置 (Hardware Wiring)

使用 Arduino Uno / Nano 連接以下感測模組：

| 感測器 / 零件 | 感測器腳位 | Arduino 腳位 | 說明 |
| :--- | :--- | :--- | :--- |
| **HC-SR04 超音波** | VCC | 5V | 電源 |
| | GND | GND | 接地 |
| | TRIG | D9 | 觸發訊號 |
| | ECHO | D10 | 回波量測訊號 (推進/揮手跳躍) |
| **ADXL345 加速度計** | VCC | 3.3V / 5V | 電源 |
| | GND | GND | 接地 |
| | SDA | A4 (SDA) | I2C 資料線 (前後左右傾角控制) |
| | SCL | A5 (SCL) | I2C 時脈線 |
| **雙軸搖桿 (Joystick)** | VCC | 5V | 電源 |
| | GND | GND | 接地 |
| | VRX | A0 | X 軸類比輸入 (視角環繞) |
| | VRY | A1 | Y 軸類比輸入 (視角俯仰) |
| | SW | D2 | 搖桿按鍵 (INPUT_PULLUP) |

> 📌 Arduino 程式碼位於：`Arduino/Arduino_Controller.ino`，燒錄鮑率為 `9600 bps`。

---

## 📁 專案架構導覽

```text
arduinoGame/
├── .gitignore                      # 專業 Unity 版本控制忽略檔 (排除 Library/Temp 等快取)
├── README.md                       # 專案詳細說明文檔
├── Arduino/
│   └── Arduino_Controller.ino      # 實體 Arduino 韌體程式 (感測器資料打包 20Hz 輸出)
├── Assets/
│   ├── Audio/                      # 遊戲 44.1kHz WAV 音樂音效 (Cyber Neon, Chiptune Rush, Deep Gravity, SFX 等)
│   ├── Materials/                  # 賽道、霓虹、寶石、球體物理材質
│   ├── Scenes/
│   │   ├── MainMenuScene.unity     # 主選單、關卡選擇、天梯模式、商店與設定場景
│   │   └── RollingSkyScene.unity   # 3D 極速賽道主關卡、無盡跑酷與天梯主場景
│   └── Scripts/
│       ├── ArduinoReceiver.cs      # 序列埠通訊解析核心 (超音波、傾角、搖桿)
│       ├── AudioManager.cs         # 跨場景音訊管理器 (多版本 BGM、音效、音量持久化)
│       ├── GemCollectible.cs       # 立體璀璨八面體水晶鑽石 Mesh 生成與發光收集機制
│       ├── Hazard.cs               # 障礙物碰撞與失敗判定
│       ├── JumpPad.cs              # 彈跳墊推力加速器
│       ├── RollingSkyBallController.cs # 3D 物理小球運動、相機朝向向量聯動、鍵盤與傾角雙模融合控制
│       ├── RollingSkyCamera.cs     # 360 度自由環繞與俯仰相機 (Orbit Camera，通天天梯專用)
│       ├── RollingSkyGameManager.cs# 遊戲核心邏輯 (進度、計分、無盡模式、ESC 暫停設定)
│       ├── RollingSkyJumpDetector.cs   # 超音波手勢推進跳躍辨識演算法
│       ├── RollingSkyMenuManager.cs# 主選單「左四右四」排版、關卡網格、商店、設定分頁邏輯
│       ├── RollingSkyTrackGenerator.cs # 10 大特色關卡動態生成器與無盡跑酷動態區塊系統
│       ├── SkyTowerMapGenerator.cs # 500 米通天天梯六大分區動態生成器
│       ├── SkyTowerMovingPlatform.cs # 天梯動態橫移/升降浮島物理運動控制器
│       └── TrackBuilderManager.cs  # 自訂場地編輯器 (網格編輯/存檔/試玩)
├── Packages/
│   └── manifest.json               # Unity 套件清單
└── ProjectSettings/                # 專案全域設定 (Input, Tags, Physics, Quality)
```

---

## 🚀 快速上手 (Quick Start)

### 1. 軟體環境
- **Unity 6 (6000.3.7f1)** 或更高版本。
- **Arduino IDE** (用於上傳感測器程式碼至板端)。

### 2. 執行步驟
1. 將本倉庫 Clone 至本機：
   ```bash
   git clone https://github.com/agm129134-bit/arduinoGame.git
   ```
2. 使用 **Unity Hub** 點選 **Add project from disk** 並選擇專案資料夾。
3. 若有實體 Arduino 裝置：
   - 使用 Arduino IDE 打開 `Arduino/Arduino_Controller.ino`。
   - 安裝 `Adafruit ADXL345` 函式庫後燒錄至開發板。
   - 在 Unity 主選單「設定」->「控制器」分頁中確認 COM Port 與序列埠即時數值。
4. 若無實體硬體：
   - 專案支援**自動防呆降級**，直接開啟 `Assets/Scenes/MainMenuScene.unity`，點擊 **Play**：
     - 常規關卡：`A` / `D` 或 `←` / `→` 左右橫移，`Space` 跳躍。
     - 通天天梯：滑鼠自由轉動視角，`W` / `S` 前後移動，`A` / `D` 左右移動，`Space` 跳躍，`Q` / `E` 輔助轉視角，`ESC` 隨時暫停與調節靈敏度！
