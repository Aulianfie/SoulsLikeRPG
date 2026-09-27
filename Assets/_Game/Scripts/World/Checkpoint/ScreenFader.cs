using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class ScreenFader : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _fadeDuration = 0.45f;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    public IEnumerator FadeOut()
    {
        yield return FadeTo(1f);
    }

    public IEnumerator FadeIn()
    {
        yield return FadeTo(0f);
    }

    public void Clear()
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        _canvasGroup.blocksRaycasts = true;
        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha,
                Mathf.Clamp01(elapsed / _fadeDuration));
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        _canvasGroup.blocksRaycasts = targetAlpha > 0f;
    }
}
