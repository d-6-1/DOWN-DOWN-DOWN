// @role Old camera that follows the rider with a fixed offset (GhostController turns it off and runs the camera)
// @tune smoothSpeed:float:1-20:Follow smoothing
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;       // 在 Inspector 中拖入 rider
    public float smoothSpeed = 5f;

    private Vector3 offset;        // 自动计算相对位置
    private Quaternion rotationOffset; // 自动计算相对角度

    void Start()
    {
        if (target != null)
        {
            // 游戏启动瞬间，自动记录当前相机相对于 rider 的偏移量和旋转
            offset = transform.position - target.position;
            rotationOffset = transform.rotation;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 保持启动时的相对视角跟着 rider 移动
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // 保持对齐时的相机角度
        transform.rotation = Quaternion.Slerp(transform.rotation, rotationOffset, smoothSpeed * Time.deltaTime);
    }
}