using UnityEngine;

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
