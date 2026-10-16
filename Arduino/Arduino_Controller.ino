#include <Wire.h>
#include <Adafruit_Sensor.h>
#include <Adafruit_ADXL345_U.h>

/*
 * 《極速躍動 3D》（Rolling Leap 3D）/ Arduino Game
 * 實體硬體控制感測器腳位設定
 */

// 1. HC-SR04 超音波距離感測器 (揮手感應起跳)
#define TRIG_PIN 9
#define ECHO_PIN 10

// 2. 雙軸類比搖桿 (Joystick)
#define JOY_X A0
#define JOY_Y A1
#define JOY_BTN 2 // 搖桿按鈕 (按鍵備援)

// 3. ADXL345 三軸加速度計 (傾角重力左右平衡)
// I2C 接線: A4 -> SDA, A5 -> SCL, VCC -> 3.3V/5V, GND -> GND
Adafruit_ADXL345_Unified accel = Adafruit_ADXL345_Unified(12345);

void setup() {
  Serial.begin(9600); // 與 Unity ArduinoReceiver.cs 通訊之鮑率

  // 初始化超音波腳位
  pinMode(TRIG_PIN, OUTPUT);
  pinMode(ECHO_PIN, INPUT);

  // 初始化搖桿按鈕 (內部上拉電阻)
  pinMode(JOY_BTN, INPUT_PULLUP);

  // 初始化 ADXL345
  if (!accel.begin()) {
    Serial.println("Ooops, no ADXL345 detected ... Check wiring!");
    while (1);
  }
  accel.setRange(ADXL345_RANGE_16_G);
}

void loop() {
  // 1. 讀取超音波感測距離 (cm)
  digitalWrite(TRIG_PIN, LOW);
  delayMicroseconds(2);
  digitalWrite(TRIG_PIN, HIGH);
  delayMicroseconds(10);
  digitalWrite(TRIG_PIN, LOW);
  long duration = pulseIn(ECHO_PIN, HIGH, 30000); // 30ms 超時保護
  float distance = (duration / 2.0) * 0.0343;     // 換算距離 (公分)
  if (distance == 0) distance = 999;              // 超出量測範圍

  // 2. 讀取三軸加速度 (m/s^2)
  sensors_event_t event;
  accel.getEvent(&event);
  float ax = event.acceleration.x;
  float ay = event.acceleration.y;
  float az = event.acceleration.z;

  // 3. 讀取搖桿 X/Y 及按鈕
  int jx = analogRead(JOY_X);
  int jy = analogRead(JOY_Y);
  // 按鈕預設 HIGH，按下為 LOW，取反後 1 代表按下
  int btn = (digitalRead(JOY_BTN) == LOW) ? 1 : 0;

  // 4. 打包為 Unity ArduinoReceiver 專用傳輸字串
  // 格式: D:20.5|AX:1.2|AY:0.5|AZ:9.8|JX:512|JY:512|B:0
  Serial.print("D:");   Serial.print(distance);
  Serial.print("|AX:"); Serial.print(ax);
  Serial.print("|AY:"); Serial.print(ay);
  Serial.print("|AZ:"); Serial.print(az);
  Serial.print("|JX:"); Serial.print(jx);
  Serial.print("|JY:"); Serial.print(jy);
  Serial.print("|B:");  Serial.println(btn);

  // 控制發送頻率 (約 20Hz ~ 50ms)
  delay(50);
}
