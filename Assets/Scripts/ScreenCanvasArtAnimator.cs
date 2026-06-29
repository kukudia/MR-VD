using System;
using UnityEngine;
using UnityEngine.UI;

public static class ScreenCanvasArtTheme
{
    public static readonly Color AudioPanelBase = new Color(0.04f, 0.06f, 0.09f, 0.96f);
    public static readonly Color InfoPanelBase = new Color(0.05f, 0.05f, 0.08f, 0.96f);
    public static readonly Color DeviceListBase = new Color(0.03f, 0.05f, 0.07f, 0.96f);
    public static readonly Color AudioPrimaryAccent = new Color(0.25f, 0.88f, 0.82f, 1f);
    public static readonly Color AudioSecondaryAccent = new Color(0.96f, 0.68f, 0.30f, 1f);
    public static readonly Color InfoPrimaryAccent = new Color(0.95f, 0.75f, 0.35f, 1f);
    public static readonly Color InfoSecondaryAccent = new Color(0.68f, 0.46f, 1f, 1f);

    public static void ApplyPanelArt(RectTransform root, Color baseColor, Color primaryAccent, Color secondaryAccent, bool animated)
    {
        if (root == null)
        {
            return;
        }

        Image background = Ensure<Image>(root.gameObject);
        background.color = baseColor;
        background.raycastTarget = false;

        Image topBand = EnsureBand(root, "ArtTopBand", primaryAccent, 0.45f, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), new Vector2(0f, 0f));
        Image bottomBand = EnsureBand(root, "ArtBottomBand", secondaryAccent, 0.25f, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f));
        Image leftBand = EnsureBand(root, "ArtLeftBand", primaryAccent, 0.22f, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(2f, 0f));
        Image rightBand = EnsureBand(root, "ArtRightBand", secondaryAccent, 0.14f, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-2f, 0f), new Vector2(0f, 0f));

        if (animated)
        {
            Image scanline = EnsureBand(root, "ArtScanline", primaryAccent, 0.10f, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(0f, -15f));
            ScreenCanvasArtAnimator animator = Ensure<ScreenCanvasArtAnimator>(root.gameObject);
            animator.Configure(root, scanline, topBand, bottomBand, leftBand, rightBand, primaryAccent, secondaryAccent);
        }
        else
        {
            Transform scanline = root.Find("ArtScanline");
            if (scanline != null)
            {
                DestroyObject(scanline.gameObject);
            }

            ScreenCanvasArtAnimator animator = root.GetComponent<ScreenCanvasArtAnimator>();
            if (animator != null)
            {
                DestroyObject(animator);
            }
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
            || child.name == "ArtRightBand"
            || child.name == "ArtScanline";
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
            ? new Color(0.86f, 0.91f, 0.96f, 0.78f)
            : title
                ? new Color(0.98f, 0.99f, 1f, 1f)
                : new Color(0.93f, 0.96f, 0.98f, 0.93f);

        Shadow shadow = Ensure<Shadow>(text.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.64f);
        shadow.effectDistance = title ? new Vector2(1.1f, -1.1f) : new Vector2(0.8f, -0.8f);
        shadow.useGraphicAlpha = true;

        if (title || controlLabel)
        {
            Outline outline = Ensure<Outline>(text.gameObject);
            outline.effectColor = controlLabel
                ? new Color(AudioPrimaryAccent.r, AudioPrimaryAccent.g, AudioPrimaryAccent.b, 0.55f)
                : new Color(AudioSecondaryAccent.r, AudioSecondaryAccent.g, AudioSecondaryAccent.b, 0.35f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
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
                ? new Color(0.15f, 0.21f, 0.27f, 0.95f)
                : new Color(0.16f, 0.23f, 0.29f, 0.94f);
            image.raycastTarget = true;
        }

        selectable.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.98f, 0.97f, 1f);
        colors.pressedColor = new Color(0.76f, 0.84f, 0.90f, 1f);
        colors.selectedColor = new Color(0.80f, 0.96f, 0.90f, 1f);
        colors.disabledColor = new Color(0.53f, 0.56f, 0.60f, 0.55f);
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

public sealed class ScreenCanvasArtAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private Image scanline;
    [SerializeField] private Image topBand;
    [SerializeField] private Image bottomBand;
    [SerializeField] private Image leftBand;
    [SerializeField] private Image rightBand;
    [SerializeField] private Color primaryAccent;
    [SerializeField] private Color secondaryAccent;

    public void Configure(RectTransform rootRect, Image scanlineImage, Image top, Image bottom, Image left, Image right, Color primary, Color secondary)
    {
        root = rootRect;
        scanline = scanlineImage;
        topBand = top;
        bottomBand = bottom;
        leftBand = left;
        rightBand = right;
        primaryAccent = primary;
        secondaryAccent = secondary;
    }

    private void Update()
    {
        if (root == null)
        {
            root = transform as RectTransform;
            if (root == null)
            {
                return;
            }
        }

        float t = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.25f);

        if (scanline != null)
        {
            RectTransform scanlineRect = scanline.rectTransform;
            float height = Mathf.Max(24f, root.rect.height);
            float travel = Mathf.Max(32f, height - 24f);
            float y = -12f - Mathf.Repeat(t * 28f, travel);
            scanlineRect.anchoredPosition = new Vector2(scanlineRect.anchoredPosition.x, y);
            scanline.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.04f + 0.06f * pulse);
        }

        if (topBand != null)
        {
            topBand.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.30f + 0.15f * pulse);
        }

        if (bottomBand != null)
        {
            bottomBand.color = new Color(secondaryAccent.r, secondaryAccent.g, secondaryAccent.b, 0.18f + 0.08f * (1f - pulse));
        }

        if (leftBand != null)
        {
            leftBand.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.12f + 0.08f * pulse);
        }

        if (rightBand != null)
        {
            rightBand.color = new Color(secondaryAccent.r, secondaryAccent.g, secondaryAccent.b, 0.08f + 0.05f * pulse);
        }
    }
}
