using UnityEngine;

public class RollingSkyCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Offset Settings")]
    public Vector3 offset = new Vector3(0, 4.5f, -6.5f);
    public float smoothSpeedX = 10.0f;
    public float lookAheadDistance = 5.0f;

    private float currentX;

    void Start()
    {
        if (target != null)
        {
            currentX = target.position.x;
            transform.position = target.position + offset;
        }
    }

    void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;

        // 平滑追蹤 X 軸 (避免左右晃動過劇)
        currentX = Mathf.Lerp(currentX, target.position.x, smoothSpeedX * Time.deltaTime);

        // 嚴格跟隨 Z 軸向前衝刺
        Vector3 desiredPos = new Vector3(currentX, target.position.y + offset.y, target.position.z + offset.z);
        transform.position = desiredPos;

        // 俯角朝向小球前方賽道
        Vector3 lookTarget = target.position + Vector3.forward * lookAheadDistance;
        transform.LookAt(lookTarget);
    }
}
