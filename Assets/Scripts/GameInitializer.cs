using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    void Awake()
    {
        // Cap frame rate to 60 FPS to prevent GPU from maxing out
        Application.targetFrameRate = 60;
    }
}