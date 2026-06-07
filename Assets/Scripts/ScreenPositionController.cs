using UnityEngine;

/// <summary>
/// Toggles whether the attached screen object follows the main camera.
/// </summary>
public class ScreenPositionController : MonoBehaviour
{
    [Tooltip("When enabled, this object follows the main camera using the configured offset.")]
    public bool isFollowedCamera;

    [Tooltip("World-space offset applied from the main camera while following.")]
    public Vector3 positionOffset = new Vector3(0, -1, 2);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isFollowedCamera = !isFollowedCamera;
        }

        if (isFollowedCamera)
        {
            if (Camera.main == null)
            {
                return;
            }

            transform.position = Vector3.Lerp(Camera.main.transform.position + positionOffset, transform.position, Time.deltaTime);
        }
    }
}
