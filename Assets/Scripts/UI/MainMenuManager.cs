// MainMenuManager.cs — 主菜单控制器
// 同一场景内切换：主菜单 UI ↔ 游戏 UI
using UnityEngine;
using UnityEngine.UI;

namespace SWO1.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("主菜单 UI（点击开始后隐藏）")]
        [Tooltip("主菜单的 Canvas 或根节点")]
        public GameObject MainMenuRoot;

        [Header("游戏 UI（点击开始后显示）")]
        [Tooltip("游戏界面的 Canvas 或根节点")]
        public GameObject GameUIRoot;

        private GameObject _tutorialPanel;

        void Start()
        {
            // 查找弹窗
            _tutorialPanel = transform.Find("TutorialPanel")?.gameObject;
            if (_tutorialPanel == null)
                _tutorialPanel = GameObject.Find("TutorialPanel");
            if (_tutorialPanel != null)
                _tutorialPanel.SetActive(false);

            // 如果没手动指定，自动查找
            if (MainMenuRoot == null)
                MainMenuRoot = gameObject; // 默认就是挂载此脚本的 Canvas

            if (GameUIRoot == null)
                GameUIRoot = GameObject.Find("Canvas"); // 尝试找游戏 Canvas

            // 绑定按钮
            BindButton("BtnStart", OnStartGame);
            BindButton("BtnTutorial", OnShowTutorial);
            BindButton("BtnCloseTutorial", OnCloseTutorial);
        }

        void BindButton(string btnName, UnityEngine.Events.UnityAction action)
        {
            // 先在子物体中找
            var btn = FindButtonInChildren(btnName, transform);
            if (btn != null)
            {
                btn.onClick.AddListener(action);
                return;
            }

            // 再全局找
            var go = GameObject.Find(btnName);
            if (go != null)
            {
                btn = go.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(action);
            }
        }

        Button FindButtonInChildren(string name, Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                {
                    var btn = child.GetComponent<Button>();
                    if (btn != null) return btn;
                }
                var found = FindButtonInChildren(name, child);
                if (found != null) return found;
            }
            return null;
        }

        void OnStartGame()
        {
            Debug.Log("[MainMenu] 开始游戏");

            // 隐藏主菜单
            if (MainMenuRoot != null)
                MainMenuRoot.SetActive(false);

            // 显示游戏 UI
            if (GameUIRoot != null && GameUIRoot != MainMenuRoot)
                GameUIRoot.SetActive(true);
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
