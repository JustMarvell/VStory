using System;
using System.Collections;
using UnityEngine;

namespace VRGame.Core
{
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] Renderer fadeQuadRenderer;
        [SerializeField] float autoFadeInDuration = 1f;

        Material mat;
        Coroutine active;

        void Awake()
        {
            Instance = this;
            mat = fadeQuadRenderer.material;
            SetAlpha(1f);
        }

        void Start() => FadeTo(0f, autoFadeInDuration);

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void FadeOutThenLoad(ChapterDefinition next, float duration) =>
            FadeTo(1f, duration, () => GameManager.Instance.LoadChapter(next));

        public void FadeTo(float target, float duration, Action onComplete = null)
        {
            if (active != null) StopCoroutine(active);
            active = StartCoroutine(FadeRoutine(target, duration, onComplete));
        }

        IEnumerator FadeRoutine(float target, float duration, Action onComplete)
        {
            float start = mat.color.a, t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(start, target, t / duration));
                yield return null;
            }
            SetAlpha(target);
            onComplete?.Invoke();
        }

        void SetAlpha(float a)
        {
            var c = mat.color;
            c.a = a;
            mat.color = c;
            fadeQuadRenderer.gameObject.SetActive(a > 0.001f);
        }
    }
}