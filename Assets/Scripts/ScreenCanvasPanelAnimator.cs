using UnityEngine;

public sealed class ScreenCanvasPanelAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Vector3 visibleScale;

    private Vector3 startScale = Vector3.one;
    private Vector3 targetScale = Vector3.one;
    private float startAlpha = 1f;
    private float targetAlpha = 1f;
    private float elapsed;
    private float duration = 0.2f;

    public Vector3 VisibleScale => visibleScale;

    public void Configure(RectTransform targetRect, CanvasGroup group)
    {
        target = targetRect;
        canvasGroup = group;
        visibleScale = target != null ? target.localScale : Vector3.one;
    }

    public void SetVisible(bool visible, bool immediate, float transitionDuration)
    {
        if (target == null)
        {
            target = transform as RectTransform;
        }

        if (target != null && visibleScale.sqrMagnitude < 0.000001f)
        {
            visibleScale = target.localScale;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        duration = Mathf.Max(0.01f, transitionDuration);
        elapsed = 0f;
        startScale = target != null ? target.localScale : Vector3.one;
        targetScale = visible ? visibleScale : visibleScale * 0.96f;
        startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        targetAlpha = visible ? 1f : 0f;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (immediate)
        {
            Apply(1f);
            elapsed = duration;
        }
    }

    private void Update()
    {
        if (elapsed >= duration)
        {
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        Apply(1f - Mathf.Pow(1f - progress, 3f));
    }

    private void Apply(float progress)
    {
        if (target != null)
        {
            target.localScale = Vector3.LerpUnclamped(startScale, targetScale, progress);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
        }
    }
}
