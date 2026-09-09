using UnityEngine;
using System;

public sealed class GameLoopController : MonoBehaviour
{
    // Clock

    private float _dt = 1f / 60f;
    private float _LastTime;
    private float _deltaTime;
    private float _unscaledTime;
    private bool _isPaused;

    //Accumulator

    private float _acc;
    private const int CatchUpCap = 5;

    //External dependencies

    public RNG RNG { get; private set; }
    private StateManager _stateManager;
    public bool IsPaused => _isPaused;
    public float DeltaTime => _deltaTime;
    
    //Initialize Clock

    public void Initialize(double? dt = null)
    {
        _dt = dt.HasValue ? Mathf.Max(0.0001f, (float)dt.Value) : 1f / 60f;

        _LastTime = Time.unscaledTime;
    }

    //Dependency Injection

    public void SetDependencies(RNG rng,StateManager stateManager)
    {
        RNG = rng;
        _stateManager = stateManager;
    }

    public void SetRNG(RNG rng) { rng = rng; }
    public void SetStateManager(StateManager stateManager) { _stateManager = stateManager; }
    public void Pause()
    {

    }

}

