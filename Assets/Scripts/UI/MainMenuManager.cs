// MainMenuManager.cs — 主菜单控制器
// 挂在 MainMenu 场景的 Canvas 上
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SWO1.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("场景")]
        [Tooltip("游戏场景名称")]
        public string GameSceneName = "GameScene";

        private GameObject _tutorialPanel;

        void Start()
        {
            // 查找弹窗（默认隐藏）
            _tutorialPanel = GameObject.Find("TutorialPanel");
            if (_tutorialPanel != null)
                _tutorialPanel.SetActive(false);

            // 绑定按钮
            BindButton("BtnStart", OnStartGame);
            BindButton("BtnTutorial", OnShowTutorial);
            BindButton("BtnCloseTutorial", OnCloseTutorial);
        }

        void BindButton(string btnName, UnityEngine.Events.UnityAction action)
        {
            var go = GameObject.Find(btnName);
            if (go != null)
            {
                var btn = go.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(action);
            }
        }

        void OnStartGame()
        {
            Debug.Log("[MainMenu] 开始游戏");
            SceneManager.LoadScene(GameSceneName);
        }

        void OnShowTutorial()
        {
            Debug.Log("[MainMenu] 打开教程");
            if (_tutorialPanel != null)
                _tutorialPanel.SetActive(true);
        }

        void OnCloseTutorial()
        {
            Debug.Log("[MainMenu] 关闭教程");
            if (_tutorialPanel != null)
                _tutorialPanel.SetActive(false);
        }
    }
}
