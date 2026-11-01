using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Jump Pad Settings")]
    public float launchForce = 13.5f;
    public float forwardBoost = 1.4f;
    public Color activeColor = new Color(0f, 1f, 0.9f);

    private float lastLaunchTime = -999f;
    private MeshRenderer meshRenderer;
    private Color originalEmission;
    private bool hasEmission = false;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && meshRenderer.material.HasProperty("_EmissionColor"))
        {
            originalEmission = meshRenderer.material.GetColor("_EmissionColor");
            if (originalEmission.maxColorComponent < 0.2f)
            {
                originalEmission = new Color(4.8f, 2.5f, 0.2f);
            }
            hasEmission = true;
        }
    }

    void Update()
    {
        if (hasEmission && meshRenderer != null)
        {
            float pulse = 0.72f + Mathf.PingPong(Time.time * 2.8f, 0.56f);
            meshRenderer.material.SetColor("_EmissionColor", originalEmission * pulse);
        }
    }

    void Awake()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.isTrigger = true;
            // 充分覆蓋左右邊緣並向上延伸感應範圍：
            // X: 1.45f (實體寬度擴展至約 2.65m，邊緣觸碰 100% 觸發)
            // Y: 10.0f (乘上 localScale.y = 0.12 後，世界高度約 1.2m，上方充足觸發空間)
            // Z: 1.45f (前後延伸防漏踩)
            bc.size = new Vector3(1.45f, 10.0f, 1.45f);
            bc.center = new Vector3(0, 5.0f, 0);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TriggerLaunch(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        TriggerLaunch(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TriggerLaunch(collision.gameObject);
    }

    private void TriggerLaunch(GameObject target)
    {
        RollingSkyBallController ball = target.GetComponent<RollingSkyBallController>() ?? target.GetComponentInParent<RollingSkyBallController>();
        if (ball == null || ball.IsDead) return;

        // 【空中跳躍保護】：若小球已在彈跳滯空狀態中 (尚未著陸)，絕不允許二段重複觸發
        if (ball.IsJumpPadAirborne) return;

        // 【空中向上飛昇哨兵】：若小球正處於空中高速向上飛行 (Y 速度 > 1.0m/s)，不可再次引爆
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null && rb.linearVelocity.y > 1.0f) return;

        // 【上方感應高度門檻】：小球在跳台上方 1.4m 範圍內均可靈敏觸發 (擴大上方感應)
        if (ball.transform.position.y > transform.position.y + 1.4f) return;

        // 【全域與局部冷卻】：跳台獨立冷卻 0.4s，小球全域跳台冷卻 0.6s
        if (Time.time - ball.LastJumpPadLaunchTime < 0.6f) return;
        if (Time.time - lastLaunchTime < 0.4f) return;

        lastLaunchTime = Time.time;
        ball.Launch(launchForce, forwardBoost);
    }
}
