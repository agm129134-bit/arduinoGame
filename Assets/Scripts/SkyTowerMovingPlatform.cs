using UnityEngine;

/// <summary>
/// 通天天梯模式專用：微幅動態浮動平臺
/// </summary>
public class SkyTowerMovingPlatform : MonoBehaviour
{
    public Vector3 moveDirection = Vector3.right;
    public float moveDistance = 2.5f;
    public float moveSpeed = 1.2f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * moveSpeed) * moveDistance;
        transform.position = startPos + moveDirection.normalized * offset;
    }
}
