using UnityEngine;
using UnityEngine.UI;

public sealed class ScreenCanvasModuleAnimator : MonoBehaviour
{
    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private CanvasGroup bodyGroup;
    [SerializeField] private float collapsedHeight;
    [SerializeField] private float expandedHeight;

    private float startHeight;
    private float targetHeight;
    private float startRootAlpha;
    private float targetRootAlpha;
    private float startBodyAlpha;
    private float targetBodyAlpha;
    private float elapsed;
    private float duration = 0.2f;

    public void Configure(LayoutElement layout, CanvasGroup root, CanvasGroup body, float collapsed, float expandedValue, bool visible, bool expanded)
    {
        layoutElement = layout;
        rootGroup = root;
        bodyGroup = body;
        collapsedHeight = Mathf.Max(0f, collapsed);
        expandedHeight = Mathf.Max(collapsedHeight, expandedValue);
        SetState(visible, expanded, true, 0f);
    }

    public void SetState(bool visible, bool expanded, bool immediate, float transitionDuration)
    {
        duration = Mathf.Max(0.01f, transitionDuration);
        elapsed = 0f;
        float currentHeight = layoutElement != null ? layoutElement.preferredHeight : 0f;
        startHeight = currentHeight < 0f ? expandedHeight : currentHeight;
        targetHeight = visible ? (expanded ? expandedHeight : collapsedHeight) : 0f;
        startRootAlpha = rootGroup != null ? rootGroup.alpha : 1f;
        targetRootAlpha = visible && (expanded || collapsedHeight > 0.5f) ? 1f : 0f;
        startBodyAlpha = bodyGroup != null ? bodyGroup.alpha : 1f;
        targetBodyAlpha = visible && expanded ? 1f : 0f;

        if (rootGroup != null)
        {
            rootGroup.interactable = visible;
            rootGroup.blocksRaycasts = visible;
        }

        if (bodyGroup != null)
        {
            bodyGroup.interactable = visible && expanded;
            bodyGroup.blocksRaycasts = visible && expanded;
        }

        if (immediate)
        {
            Apply(1f);
            elapsed = duration;
        }
    }

    private void Update()
    {
        if (layoutElement == null || elapsed >= duration)
        {
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        Apply(1f - Mathf.Pow(1f - progress, 3f));
    }

    private void Apply(float progress)
    {
        if (layoutElement != null)
        {
            layoutElement.preferredHeight = Mathf.Lerp(startHeight, targetHeight, progress);
            layoutElement.minHeight = layoutElement.preferredHeight;
        }

        if (rootGroup != null)
        {
            rootGroup.alpha = Mathf.Lerp(startRootAlpha, targetRootAlpha, progress);
        }

        if (bodyGroup != null)
        {
            bodyGroup.alpha = Mathf.Lerp(startBodyAlpha, targetBodyAlpha, progress);
        }
    }
}
