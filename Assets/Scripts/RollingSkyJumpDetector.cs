using UnityEngine;

public class RollingSkyJumpDetector : MonoBehaviour
{
    [Header("References")]
    public ArduinoReceiver arduino;
    public RollingSkyBallController ball;

    [Header("Ultrasonic Gesture Settings")]
    [Tooltip("手掌壓入此距離內視為跳躍觸發 (cm)")]
    public float triggerDistance = 20.0f;
    [Tooltip("最大感應邊界，超過視為無手掌 (cm)")]
    public float maxDetectionDistance = 45.0f;
    [Tooltip("跳躍冷卻時間，防止單次手勢重複連跳 (秒)")]
    public float jumpCooldown = 0.35f;

    private float cooldownTimer = 0f;
    private float lastDistance = 50f;
    private bool wasHandNear = false;

    private KeyCode customKeyJump = KeyCode.Space;

    void Start()
    {
        if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();
        if (ball == null) ball = GetComponent<RollingSkyBallController>();

        // 載入自訂跳躍鍵與超音波跳躍距離
        customKeyJump = (KeyCode)PlayerPrefs.GetInt("Key_Jump", (int)KeyCode.Space);
        triggerDistance = PlayerPrefs.GetFloat("Ultrasonic_JumpDist", 20.0f);
    }

    void Update()
    {
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // 1. 鍵盤測試跳躍 (自訂鍵 / Space)
        if (Input.GetKeyDown(customKeyJump) || Input.GetKeyDown(KeyCode.Space))
        {
            TriggerJump($"鍵盤 [{customKeyJump}]");
            return;
        }

        // 2. 超音波上下拍動 / 壓下跳躍檢測
        if (arduino != null && arduino.distance > 0)
        {
            float dist = arduino.distance;

            // 判斷手掌是否進入有效範圍
            if (dist < maxDetectionDistance)
            {
                // 當手掌由高處下壓進入觸發範圍 (或上下抽動)，且冷卻完畢時起跳
                if (dist <= triggerDistance && !wasHandNear && cooldownTimer <= 0)
                {
                    wasHandNear = true;
                    TriggerJump($"超音波感應 ({dist:F1} cm)");
                }
                else if (dist > triggerDistance + 5f)
                {
                    // 手掌抬起，重置手勢準備下一次拍跳
                    wasHandNear = false;
                }
            }
            else
            {
                wasHandNear = false;
            }

            lastDistance = dist;
        }
    }

    void TriggerJump(string source)
    {
        if (ball != null && ball.CanJump())
        {
            cooldownTimer = jumpCooldown;
            ball.Jump();
            Debug.Log($"[JumpDetector] 跳躍觸發！來源: {source}");
        }
    }
}
