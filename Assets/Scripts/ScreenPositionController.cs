using UnityEngine;

public class ScreenPositionController : MonoBehaviour
{
    public bool isFollowedCamera;
    public Vector3 positionOffset = new Vector3(0, -1, 2);
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isFollowedCamera = !isFollowedCamera;
        }

        if (isFollowedCamera)
        {
            transform.position = Vector3.Lerp(Camera.main.transform.position + positionOffset, transform.position, Time.deltaTime * 1);
        }
    }
}
