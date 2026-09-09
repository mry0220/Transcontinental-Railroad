using UnityEngine;

public class GameLoopManager : MonoBehaviour
{
    private enum ms
    {
        _30fps = 30,
        _60fps = 60,
        _144fps = 144,
    }

    [SerializeField] private ms Ms = ms._60fps;

    private void Awake()
    {
        Application.targetFrameRate = (int)ms._60fps;
    }

    private void Update()
    {
        
    }
}
