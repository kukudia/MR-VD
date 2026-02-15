using UnityEngine;

public class Init : MonoBehaviour
{
    public Camera mainCamera;

    private void Awake()
    {
        mainCamera.tag = "MainCamera";
    }
}
