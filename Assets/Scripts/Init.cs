using UnityEngine;

/// <summary>
/// Assigns the configured camera as Unity's main camera during scene initialization.
/// </summary>
public class Init : MonoBehaviour
{
    [Tooltip("Camera that should receive the MainCamera tag on startup.")]
    public Camera mainCamera;

    private void Awake()
    {
        if (mainCamera == null)
        {
            Debug.LogWarning("[Init] Main camera reference is missing.");
            return;
        }

        mainCamera.tag = "MainCamera";
    }
}
