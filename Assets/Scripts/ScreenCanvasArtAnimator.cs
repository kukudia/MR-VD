using System;
using UnityEngine;
using UnityEngine.UI;

public static class ScreenCanvasArtTheme
{
    public static readonly Color AudioPanelBase = new Color(0.018f, 0.019f, 0.021f, 0.97f);
    public static readonly Color DeviceListBase = new Color(0.035f, 0.037f, 0.040f, 0.98f);
    public static readonly Color AudioPrimaryAccent = new Color(0.86f, 0.87f, 0.88f, 1f);
    public static readonly Color AudioSecondaryAccent = new Color(0.55f, 0.56f, 0.58f, 1f);
    public static readonly Color CardBase = new Color(0.055f, 0.057f, 0.060f, 0.98f);
    public static readonly Color CardRaised = new Color(0.085f, 0.088f, 0.092f, 0.99f);
    public static readonly Color MutedText = new Color(0.64f, 0.65f, 0.67f, 0.94f);

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
