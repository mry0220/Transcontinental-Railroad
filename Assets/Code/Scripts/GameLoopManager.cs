using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class GameLoopManager : MonoBehaviour
{
    private enum FPS
    {
        _30FPS = 30,
        _60FPS = 60,
        _144FPS = 144,
    }

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        // デバッグ機能

    private enum Hz
    {
        _30Hz = 1000 / 30,
        _60Hz = 1000 / 60,
        _144Hz = 1000 / 144,
    }

    [SerializeField] private Hz hz = Hz._60Hz;
#else
    // 本番: 60Hz固定, Inspector非表示

    private double dt = 1.0 / 60.0; // 16.6667ms
#endif

    [SerializeField] private FPS fps = FPS._60FPS;
    [SerializeField] private int seed = 12345;
    [SerializeField] private InputBuffer inputBuffer;
    [Header("VSync"), SerializeField] private bool vSync = false;

    [Header("Catch-up Settings")]
    [SerializeField] private int maxCatchUpIterations = 5;
    [SerializeField,Header("Warning Message ON")] private bool logCatchUpWarnings = true;

    private RNG rng;
    private double clock;
    private double accumulator;
    private double dt = (double)Hz._60Hz;
    //private const int maxCatchUp = 5;
    

    private void Awake()
    {
        Application.targetFrameRate = (int)fps;
        rng = new RNG(seed);
        QualitySettings.vSyncCount = vSync ? 1 : 0;    

        clock  = 0.0;
        accumulator = 0.0;
    }

    private void Update()
    {
        double frameDt = Time.deltaTime;
        accumulator += frameDt;

        int steps = 0;

        while(accumulator >= dt && steps < maxCatchUpIterations)
        {
            FixedStep((float)dt);

            accumulator -= dt;
            clock += dt;
            steps++;
        }
    }
    //FixedStep event 
    private void FixedStep(float fixedDt)
    {
        var eventsThisStep = inputBuffer != null ? inputBuffer.DequeueAll() : null;
        if (eventsThisStep != null && eventsThisStep.Count > 0)
        {
            ConsumeInput(eventsThisStep);
        }

        UpdateFixed(fixedDt);
    }

    private void UpdateFixed(float fixedDt)
    {
       
    }
    private void ConsumeInput(List<InputBuffer.InputEvent> eventsList)
    {

    }


    
}
