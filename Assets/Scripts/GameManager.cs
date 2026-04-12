using UnityEngine;

namespace WW2_Commander
{
    /// <summary>
    /// 游戏主管理器 - 控制游戏状态、流程
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("游戏设置")]
        [SerializeField] private bool isPaused = false;
        [SerializeField] private float gameSpeed = 1.0f;

        public bool IsPaused => isPaused;
        public float GameSpeed => gameSpeed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Debug.Log("WW2_Commander 游戏启动");
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void Pause()
        {
            isPaused = true;
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void Resume()
        {
            isPaused = false;
            Time.timeScale = gameSpeed;
        }

        /// <summary>
        /// 设置游戏速度
        /// </summary>
        public void SetGameSpeed(float speed)
        {
            gameSpeed = Mathf.Clamp(speed, 0.25f, 3.0f);
            if (!isPaused)
            {
                Time.timeScale = gameSpeed;
            }
        }
    }
}
