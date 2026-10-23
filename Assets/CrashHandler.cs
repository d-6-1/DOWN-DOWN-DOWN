// @role Early crash check: hitting an Obstacle logs a crash; R reloads the scene
using UnityEngine;
using UnityEngine.SceneManagement;

public class CrashHandler : MonoBehaviour
{
    private bool isCrashed = false;

    void OnCollisionEnter(Collision collision)
    {
        // 撞到标记为 Obstacle 的障碍物时触发
        if (collision.gameObject.CompareTag("Obstacle") && !isCrashed)
        {
            isCrashed = true;
            Debug.Log("摔倒了！按 R 键重新开始。");
        }
    }

    void Update()
    {
        // 随时按 R 键重置当前场景
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}