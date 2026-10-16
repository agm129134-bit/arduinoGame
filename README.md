# arduinoGame - 極速躍動 3D (Rolling Leap 3D)

🎮 **《極速躍動 3D》（Rolling Leap 3D）** 是一款結合 **Unity 6 3D 物理引擎** 與 **Arduino 實體感測互動** 的疾速節奏跑酷遊戲。玩家可藉由實體三軸傾角控制器與超音波手勢感應，或純鍵盤模式來操控躍動球體在空中賽道疾馳、收集水晶寶石並避開障礙陷阱！

---

## 🌟 遊戲核心特色

- **雙模態操控體系 (Dual Control Modes)**：
  - 🕹️ **Arduino 實體感測模式**：
    - **ADXL345 三軸加速度計**：傾斜手把即時左右微調球體重心。
    - **HC-SR04 超音波距離感測器**：手掌上方懸空揮動隔空感應觸發大跳躍。
    - **雙軸類比搖桿**：方向與實體按鍵備援操控。
  - ⌨️ **純鍵盤遊戲模式**：支援完整鍵位自訂（A/D 或 方向鍵左右轉向，Space 起跳，ESC 呼叫暫停選單）。
- **豐富完整的多功能設定選單 (Settings Menu)**：
  - **[ 鍵盤設定 KEYBOARD ]**：自訂左右移動按鍵、靈敏度滑桿（0.5x ~ 3.0x）即時存檔。
  - **[ 控制器設定 CONTROLLER ]**：COM Port 序號選擇、傾角靈敏度、死區死角過濾、超音波起跳高度設定、實體訊號即時監控面板（Live Sensor Readout）。
  - **[ 音訊設定 AUDIO ]**：3 種原創賽博龐克與復古晶片風背景音樂（Cyber Neon / Chiptune Rush / Deep Gravity）隨選即播、BGM 音量與 SFX 音效音量獨立調節。
- **動態關卡與即時反饋**：
  - 動態進度百分比儀表板、寶石收集進度、彈跳墊（Jump Pad）、致命障礙物（Hazard）、終點勝利結算。
  - ESC 即時呼叫全域暫停選單（繼續遊戲、重新開始、返回主選單）。

---

## 🛠️ 硬體電路與接線配置 (Hardware Wiring)

使用 Arduino Uno / Nano 連接以下感測模組：

| 感測器 / 零件 | 感測器腳位 | Arduino 腳位 | 說明 |
| :--- | :--- | :--- | :--- |
| **HC-SR04 超音波** | VCC | 5V | 電源 |
| | GND | GND | 接地 |
| | TRIG | D9 | 觸發訊號 |
| | ECHO | D10 | 回波量測訊號 (手掌下壓起跳) |
| **ADXL345 加速度計** | VCC | 3.3V / 5V | 電源 |
| | GND | GND | 接地 |
| | SDA | A4 (SDA) | I2C 資料線 (傾角控制) |
| | SCL | A5 (SCL) | I2C 時脈線 |
| **雙軸搖桿 (Joystick)** | VCC | 5V | 電源 |
| | GND | GND | 接地 |
| | VRX | A0 | X 軸類比輸入 |
| | VRY | A1 | Y 軸類比輸入 |
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
│   ├── Audio/                      # 遊戲 44.1kHz WAV 音樂音效 (Cyber Neon, Chiptune Rush, Deep Gravity 等)
│   ├── Materials/                  # 賽道、霓虹、寶石、球體物理材質
│   ├── Scenes/
│   │   ├── MainMenuScene.unity     # 主選單與自訂控制/音訊設定介面場景
│   │   └── RollingSkyScene.unity   # 3D 極速賽道主關卡場景
│   └── Scripts/
│       ├── ArduinoReceiver.cs      # 執行緒安全之序列埠通訊解析核心
│       ├── AudioManager.cs         # 跨場景音訊管理器 (多版本 BGM、音效、音量持久化)
│       ├── BuildMainMenuScene.cs   # 自動建置主選單與設定面板之編輯器工具
│       ├── BuildRollingSkyScene.cs # 自動化關卡賽道生成工具
│       ├── GemCollectible.cs       # 旋轉發光水晶收集機制
│       ├── Hazard.cs               # 障礙物碰撞與失敗判定
│       ├── JumpPad.cs              # 彈跳墊推力加速器
│       ├── RollingSkyBallController.cs # 3D 物理球體運動、鍵盤與傾角雙模融合控制
│       ├── RollingSkyCamera.cs     # 第三人生平滑追蹤相機
│       ├── RollingSkyGameManager.cs# 遊戲核心邏輯 (百分比進度、寶石計數、ESC 暫停)
│       ├── RollingSkyJumpDetector.cs   # 超音波手勢跳躍辨識演算法
│       └── RollingSkyMenuManager.cs# 三分頁設定選單、即時鍵位監聽、音訊選單邏輯
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
   - 專案支援**自動防呆降級**，直接開啟 `Assets/Scenes/MainMenuScene.unity`，點擊 **Play** 即可使用鍵盤（`A` / `D` 或 `←` / `→` 移動，`Space` 跳躍，`ESC` 暫停）暢玩全關卡！
