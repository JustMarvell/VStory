using UnityEngine;

namespace VRGame.Core
{
    public class LevelCompleteManager : MonoBehaviour
    {
        [SerializeField] GameObject panelRoot;

        public void Show()
        {
            panelRoot.SetActive(true);
            QuestManager.SetFlag("tutorial_completed");
        }

        public void Replay() => GameManager.Instance.LoadTestLevel();
        public void BackToMainMenu() => GameManager.Instance.LoadMainMenu();
    }
}