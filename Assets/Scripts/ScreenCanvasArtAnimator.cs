using System;
using UnityEngine;
using UnityEngine.UI;

public static class ScreenCanvasArtTheme
{
    public static readonly Color AudioPanelBase = new Color(0.025f, 0.027f, 0.03f, 0.94f);
    public static readonly Color DeviceListBase = new Color(0.055f, 0.058f, 0.062f, 0.92f);
    public static readonly Color AudioPrimaryAccent = new Color(0.72f, 0.74f, 0.76f, 1f);
    public static readonly Color AudioSecondaryAccent = new Color(0.42f, 0.44f, 0.46f, 1f);
    public static readonly Color CardBase = new Color(0.075f, 0.078f, 0.082f, 0.96f);
    public static readonly Color CardRaised = new Color(0.105f, 0.108f, 0.112f, 0.98f);
    public static readonly Color MutedText = new Color(0.70f, 0.71f, 0.72f, 0.90f);

    public static void ApplyPanelArt(RectTransform root, Color baseColor, Color borderColor)
    {
        if (root == null)
        {
            return;
        }

        Image background = Ensure<Image>(root.gameObject);
        background.color = baseColor;
        background.raycastTarget = false;

        EnsureBand(root, "ArtTopBand", borderColor, 0.55f, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), new Vector2(0f, 0f));
        EnsureBand(root, "ArtBottomBand", borderColor, 0.38f, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f));
        EnsureBand(root, "ArtLeftBand", borderColor, 0.38f, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f));
        EnsureBand(root, "ArtRightBand", borderColor, 0.38f, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-1f, 0f), new Vector2(0f, 0f));

        Transform scanline = root.Find("ArtScanline");
        if (scanline != null)
        {
            DestroyObject(scanline.gameObject);
        }

        SendPanelArtToBack(root);
    }

    public static int GetContentStartSiblingIndex(RectTransform root)
    {
        if (root == null)
        {
            return 0;
        }

        SendPanelArtToBack(root);
        int artCount = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            if (!IsPanelArtChild(root.GetChild(i)))
            {
                break;
            }

            artCount++;
        }

        return artCount;
    }

    public static void SendPanelArtToBack(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (IsPanelArtChild(child))
            {
                child.SetAsFirstSibling();
            }
        }
    }

    private static bool IsPanelArtChild(Transform child)
    {
        if (child == null)
        {
            return false;
        }

        return child.name == "ArtTopBand"
            || child.name == "ArtBottomBand"
            || child.name == "ArtLeftBand"
            || child.name == "ArtRightBand";
    }

    public static void StyleText(Text text, string name, int fontSize, FontStyle fontStyle)
    {
        if (text == null)
        {
            return;
        }

        bool controlLabel = text.transform.parent != null && text.transform.parent.GetComponent<Selectable>() != null;
        bool title = controlLabel
            || name.EndsWith("HeaderText", StringComparison.Ordinal)
            || name.EndsWith("Label", StringComparison.Ordinal)
            || name == "ModeText"
            || name == "LocalTimeText"
            || fontStyle == FontStyle.Bold
            || fontSize >= 12;
        bool muted = name == "NoDevicesText" || name == "DeviceText" || name == "SystemText";

        text.color = muted
            ? new Color(0.69f, 0.70f, 0.72f, 0.88f)
            : title
                ? new Color(0.95f, 0.95f, 0.96f, 1f)
                : new Color(0.84f, 0.85f, 0.86f, 0.96f);

        Shadow shadow = Ensure<Shadow>(text.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(0.7f, -0.7f);
        shadow.useGraphicAlpha = true;

        Outline outline = text.GetComponent<Outline>();
        if (outline != null)
        {
            DestroyObject(outline);
        }
    }

    public static void StyleSelectable(Selectable selectable, Image image, bool buttonLike)
    {
        if (selectable == null)
        {
            return;
        }

        if (image != null)
        {
            image.color = buttonLike
                ? new Color(0.16f, 0.165f, 0.17f, 0.98f)
                : new Color(0.13f, 0.135f, 0.14f, 0.98f);
            image.raycastTarget = true;
        }

        selectable.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.82f, 0.83f, 0.84f, 1f);
        colors.pressedColor = new Color(0.64f, 0.65f, 0.66f, 1f);
        colors.selectedColor = new Color(0.74f, 0.75f, 0.76f, 1f);
        colors.disabledColor = new Color(0.45f, 0.46f, 0.47f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.10f;
        selectable.colors = colors;

        GameObject shadowHost = image != null ? image.gameObject : selectable.gameObject;
        Shadow shadow = Ensure<Shadow>(shadowHost);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.50f);
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;
    }

    private static Image EnsureBand(RectTransform root, string name, Color accentColor, float alpha, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = EnsureChild(root, name);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        LayoutElement layout = Ensure<LayoutElement>(rect.gameObject);
        layout.ignoreLayout = true;

        Image image = Ensure<Image>(rect.gameObject);
        image.color = new Color(accentColor.r, accentColor.g, accentColor.b, alpha);
        image.raycastTarget = false;
        rect.SetAsFirstSibling();
        return image;
    }

    private static RectTransform EnsureChild(RectTransform root, string name)
    {
        Transform existing = root.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        obj.layer = root.gameObject.layer;

        RectTransform rect = obj.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = obj.AddComponent<RectTransform>();
        }

        rect.SetParent(root, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static T Ensure<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        if (component == null)
        {
            component = obj.AddComponent<T>();
        }

        return component;
    }

    private static void DestroyObject(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(obj);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}

public sealed class ScreenCanvasModuleAnimator : MonoBehaviour
{
    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private CanvasGroup bodyGroup;
    [SerializeField] private float collapsedHeight;
    [SerializeField] private float expandedHeight;

    private bool visible = true;
    private bool expanded = true;
    private float startHeight;
    private float targetHeight;
    private float startRootAlpha;
    private float targetRootAlpha;
    private float startBodyAlpha;
    private float targetBodyAlpha;
    private float elapsed;
    private float duration = 0.2f;

    public void Configure(LayoutElement layout, CanvasGroup root, CanvasGroup body, float collapsed, float expandedValue, bool initialVisible, bool initialExpanded)
    {
        layoutElement = layout;
        rootGroup = root;
        bodyGroup = body;
        collapsedHeight = Mathf.Max(0f, collapsed);
        expandedHeight = Mathf.Max(collapsedHeight, expandedValue);
        SetState(initialVisible, initialExpanded, true, 0f);
    }

    public void SetState(bool isVisible, bool isExpanded, bool immediate, float transitionDuration)
    {
        visible = isVisible;
        expanded = isExpanded;
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

    public void Configure(RectTransform targetRect, CanvasGroup group)
    {
        target = targetRect;
        canvasGroup = group;
        visibleScale = target != null ? target.localScale : Vector3.one;
    }

    public Vector3 VisibleScale => visibleScale;

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

public sealed class ScreenCanvasWeatherAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform artwork;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float speed = 1.15f;
    [SerializeField] private float scaleAmount = 0.035f;

    public void Configure(RectTransform artworkRect, CanvasGroup group)
    {
        artwork = artworkRect;
        canvasGroup = group;
    }

    private void Update()
    {
        if (artwork == null)
        {
            return;
        }

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
        float scale = 1f + scaleAmount * wave;
        artwork.localScale = new Vector3(scale, scale, 1f);
        artwork.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-1.2f, 1.2f, wave));

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Lerp(0.88f, 1f, wave);
        }
    }
}
