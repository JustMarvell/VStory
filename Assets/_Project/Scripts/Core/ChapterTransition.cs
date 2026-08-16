using UnityEngine;

namespace VRGame.Core
{
    public class ChapterTransition : MonoBehaviour
    {
        [SerializeField] ChapterDefinition nextChapter;
        [SerializeField] float fadeDuration = 1.5f;

        public void Trigger() => ScreenFader.Instance.FadeOutThenLoad(nextChapter, fadeDuration);
    }
}