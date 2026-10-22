using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Showcase-only opt-in HDR pipeline scope. The production pipeline has HDR disabled; this scene
/// temporarily overrides the active quality pipeline and restores it on disable/Play exit.
/// Never executes in Edit Mode and does not persist GraphicsSettings or quality assets.
/// </summary>
public sealed class AudioShowcasePipelineScope : MonoBehaviour
{
    [SerializeField] private RenderPipelineAsset pipeline;
    private RenderPipelineAsset previous;
    private bool applied;

    private void OnEnable()
    {
        if (pipeline == null)
        {
            return;
        }
        previous = QualitySettings.renderPipeline;
        QualitySettings.renderPipeline = pipeline;
        applied = true;
    }

    private void OnDisable()
    {
        // Do not overwrite a later explicit switch by another owner.
        if (applied && QualitySettings.renderPipeline == pipeline)
        {
            QualitySettings.renderPipeline = previous;
        }
        applied = false;
    }
}
