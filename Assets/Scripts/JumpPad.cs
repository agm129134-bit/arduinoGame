using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Jump Pad Settings")]
    public float launchForce = 16.0f;
    public float forwardBoost = 2.0f;
    public Color activeColor = new Color(0f, 1f, 0.9f);

    private float lastLaunchTime = -999f;

    void Awake()
    {
        // 修正 Collider 為適當貼地觸發器 (高 0.45m)，徹底杜絕高空掠過誤觸發
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.isTrigger = true;
            bc.size = new Vector3(1.0f, 0.45f, 1.0f);
            bc.center = new Vector3(0, 0.22f, 0);
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

        // 【空中防誤觸哨兵】：若小球在空中向上飛昇 (Y 軸向上速度 > 1.2m/s)，絕不在半空中引爆跳台
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null && rb.linearVelocity.y > 1.2f) return;

        // 【垂直高度防護】：小球必須接近跳台表面 (高度差 < 0.85m)，在上方高空飛過時不觸發
        if (ball.transform.position.y > transform.position.y + 0.85f) return;

        // 【全域與局部彈跳冷卻】：跳台個體冷卻 0.35 秒，小球全域跳台冷卻 0.5 秒，徹底杜絕多個彈跳墊相鄰時連續疊加超高跳躍
        if (Time.time - ball.LastJumpPadLaunchTime < 0.5f) return;
        if (Time.time - lastLaunchTime < 0.35f) return;

        lastLaunchTime = Time.time;
        ball.Launch(launchForce, forwardBoost);
    }
}
