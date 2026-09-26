using Oculus.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Keeps ISDK canvas pointers active when the desktop UI module owns EventSystem.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EventSystem), typeof(PointableCanvasModule))]
[DefaultExecutionOrder(1000)]
public sealed class HybridCanvasInputModule : MonoBehaviour
{
    [SerializeField] private Canvas desktopCanvas;

    private EventSystem eventSystem;
    private PointableCanvasModule pointableModule;
    private Camera desktopEventCamera;

    private void Awake()
    {
        eventSystem = GetComponent<EventSystem>();
        pointableModule = GetComponent<PointableCanvasModule>();
        if (desktopCanvas != null) desktopEventCamera = desktopCanvas.worldCamera;
    }

    private void LateUpdate()
    {
        if (eventSystem.isActiveAndEnabled && pointableModule.isActiveAndEnabled
            && eventSystem.currentInputModule != pointableModule)
        {
            try
            {
                pointableModule.Process();
            }
            finally
            {
                // ISDK assigns its pointer camera to world-space canvases while processing.
                // The desktop module needs the authored camera for its next mouse raycast.
                if (desktopCanvas != null) desktopCanvas.worldCamera = desktopEventCamera;
            }
        }
    }
}
