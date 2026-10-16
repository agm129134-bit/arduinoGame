using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Jump Pad Settings")]
    public float launchForce = 16.0f;
    public float forwardBoost = 2.0f;
    public Color activeColor = new Color(0f, 1f, 0.9f);

    private void OnTriggerEnter(Collider other)
    {
        RollingSkyBallController ball = other.GetComponent<RollingSkyBallController>();
        if (ball != null)
        {
            ball.Launch(launchForce, forwardBoost);
        }
    }
}
