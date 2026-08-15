using UnityEngine;

namespace VRGame.Core
{
    public class MenuButtonActions : MonoBehaviour
    {
        public void PlayButton() => GameManager.Instance.LoadLevelSelect();
        public void SelectChapter(ChapterDefinition chapter) => GameManager.Instance.LoadChapter(chapter);
        public void TestLevelButton() => GameManager.Instance.LoadTestLevel();
    }
}