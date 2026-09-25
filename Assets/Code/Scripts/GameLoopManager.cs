using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor.ShaderKeywordFilter;
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

    private enum FixedtimeStep
    {
        _30Hz = 33,
        _60Hz = 16,
        _144Hz = 6,
    }
#endif

    // 本番: 60Hz固定, Inspector非表示
    //====References====
    private StateManager stateManager;
    private InputBuffer inputBuffer;
    [SerializeField] private FuelManager fuelManager;
    [SerializeField] private UIManager uIManager;
    [SerializeField] private UnitManager unitManager;
    [SerializeField] private WaveManager waveManager;
    private MatchManager matchManager;

    //====Inspector Config
    [SerializeField] private FPS fps = FPS._60FPS;
    [SerializeField] private int seed = 12345;
    [Header("VSync"), SerializeField] private bool vSync = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private FixedtimeStep timestep = FixedtimeStep._60Hz;
#endif
    
    //====Debug / Catch-up Settings (Editor / DevBuild)====
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Catch-up Settings")]
    [SerializeField] private bool enableCatchUp = true;
    [SerializeField] private int maxCatchUpIterations = 5;
    [SerializeField,Header("Warning Message ON")] private bool logCatchUpWarnings = true;
#endif

    [SerializeField] private DataBase_Train trainDB;
    [SerializeField] private Vector3 trainSpawnPosition = Vector3.zero;
    private GameObject _trainInstance;

    //====Runtime State====
    private RNG rng;
    private double clock;
    private double accumulator;
    private int stateFrameCount = 0; //statemanagerテスト用


#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private double dt;
#else
    private double dt = 1.0 / 60.0; // 16.6667ms
    //Unit Placement

#endif
    private GameObject currentPreview;

    private void Awake()
    {
        stateManager = GetComponent<StateManager>();
        inputBuffer = GetComponent<InputBuffer>();

        matchManager = new();
        waveManager?.SetMatchManager(matchManager);
        unitManager?.SetMatchManager(matchManager);

        rng = new RNG(seed);

        clock  = 0.0;
        accumulator = 0.0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        dt = (double)timestep /1000.0; // 16.6667ms
#else
        dt = fixedDt; // 16.6667ms
#endif

        Application.targetFrameRate = (int)fps;
        QualitySettings.vSyncCount = vSync ? 1 : 0;    

    }

    private void Start()
    {
        if (stateManager != null)
        {
            stateManager.OnStateChanged += OnGameStateChanged;
            
        }

        if(unitManager != null && uIManager != null)
        {
            unitManager.OnUnitDeployed += uIManager.HandleUnitDeployed;
            unitManager.OnUnitReturned += uIManager.HandleUnitReturned;
        }

        if(stateManager == null)
        Debug.LogError("StateManager not found!");
        if (inputBuffer == null)
        Debug.LogError("InputBuffer not found!");
    }

    private void OnGameStateChanged(GameState prev, GameState next)
    {
        Debug.Log($"[Gameloop]State transition: {prev} -> {next}");

        stateFrameCount = 0;

        switch(next)
        {
            case GameState.Title:
                if(inputBuffer != null)
                {
                    inputBuffer.Clear();
                    
                    inputBuffer.RecordMode = false;
                    inputBuffer.PlaybackMode = false;
                }
                unitManager?.HideUnitPlacementPreview();
            break;

            case GameState.Prep:

                if(_trainInstance == null && trainDB != null && trainDB.train.prefab)
                {
                    _trainInstance = Instantiate(trainDB.train.prefab, trainSpawnPosition, Quaternion.identity);
                    var trainCombatant = _trainInstance.GetComponent<Combatant>();
                    trainCombatant?.Initialize(trainDB.train,matchManager);
                    if(trainCombatant != null)
                    {
                        trainCombatant.OnDamageTaken += dmg => fuelManager.ConsumeAmount(dmg);
                    }
                }

            break;

            case GameState.Battle:
                if(inputBuffer != null)
                {
                    inputBuffer.Clear();
                    inputBuffer.RecordMode = true;
                    inputBuffer.PlaybackMode = false;
                }
                unitManager?.ResetUnitStatuses();
                waveManager?.StartBattlePhase();
            break;

            case GameState.Result:
                if(inputBuffer != null)
                {
                    inputBuffer.RecordMode = false;
                    inputBuffer.PlaybackMode = false;
                }
                Combatant.DestroyDead();
            break;

            case GameState.GameOver:
                if (inputBuffer != null)
                {
                    inputBuffer.RecordMode = false;
                    inputBuffer.PlaybackMode = false;
                }
                Combatant.DestroyDead();
            break;
        }
    }

    private void Update()
    {
        double frameDt = Time.deltaTime;
        accumulator += frameDt;

        int steps = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int maxSteps = enableCatchUp ? maxCatchUpIterations : 1;
#else
    const int maxSteps = 1;
#endif

        while (steps < maxSteps)
        {
            FixedStep((float)dt);
      
            accumulator -= dt;
            clock += dt;
            steps++;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(logCatchUpWarnings && steps >= maxSteps && accumulator >= dt)
        {
            Debug.LogWarning($"Frame took too long: {frameDt:F4}s," +
                $"skipped {accumulator / dt:F1} steps");
        }

#endif
    }
    //FixedStep event 
    private void FixedStep(float fixedDt)
    {
        stateFrameCount++;

        var eventsThisStep = inputBuffer != null ? inputBuffer.DequeueAll() : null;
        if (eventsThisStep != null && eventsThisStep.Count > 0)
        {
            ConsumeInput(eventsThisStep);
        }

        UpdateFixed(fixedDt);
    }

    private void UpdateFixed(float fixedDt)
    {
        if (stateManager == null) return;

        switch(stateManager.Current)
        {
            case GameState.Title:
                UpdateTitle(fixedDt);
                break;
            case GameState.Prep:
                UpdatePrep(fixedDt);
                break;
            case GameState.Battle:
                UpdateBattle(fixedDt);
                break;
            case GameState.Result:
                UpdateResult(fixedDt);
                break;
            case GameState.GameOver:
                UpdateGameOver(fixedDt);
                break;
        }
    }

    private void UpdateTitle(float fixedDt)
    {
        Debug.Log("TitleMode");
    }

    private void UpdatePrep(float fixedDt)
    {
        uIManager.UpdateFuelUI(fuelManager.CurrentFuel,fuelManager.MaxFuel);
        if(fuelManager.InitializingFuel())
        {
            stateManager?.transitionTo(GameState.Battle);
        }
    }

    private void UpdateBattle(float fixedDt)
    {
        Debug.Log("BattleMode");
        uIManager.UpdateFuelUI(fuelManager.CurrentFuel, fuelManager.MaxFuel);
        
        foreach(var combatant in Combatant.All)
        {
            combatant.Tick(fixedDt);
        }
        matchManager.ProcessRequests();
        
        if (!fuelManager.ConsumingFuel())
        {
            var trainCombatant = _trainInstance != null ? _trainInstance.GetComponent<Combatant>() : null;
            trainCombatant?.ApplyDamage(int.MaxValue);
            stateManager?.transitionTo(GameState.GameOver);
        }
    }

    private void UpdateResult(float fixedDt)
    {
        Debug.Log("ResultMode");
    }

    private void UpdateGameOver(float fixedDt)
    {
        Debug.Log("GameOverMode");
    }


    private void ConsumeInput(List<InputBuffer.InputEvent> eventsList)
    {
        if (stateManager == null) return;

        foreach (var evt in eventsList)
        {
            switch(stateManager.Current)
            {
                case GameState.Title:
                    HandleInputTitle(evt);
                    break;
                case GameState.Prep:
                    HandleInputPrep(evt);
                    break;
                case GameState.Battle:
                    HandleInputBattle(evt);
                    break;
                case GameState.Result:
                    HandleInputResult(evt);
                    break;
                case GameState.GameOver:
                    HandleInputGameOver(evt);
                    break;
            }
        }
    }
   
    private void HandleInputTitle(InputBuffer.InputEvent evt)
    {
        if(evt.type == InputBuffer.InputType.PointerDown)
        {
            stateManager?.transitionTo(GameState.Prep);
        }
    }

    private void HandleInputPrep(InputBuffer.InputEvent evt)
    {
       
    }

    private void HandleInputBattle(InputBuffer.InputEvent evt)
    {
        if(evt.type == InputBuffer.InputType.PointerMove && evt.isDragFromUI)
        {
            unitManager?.UpdateUnitPlacementPreview(evt);
        }

        if (evt.type == InputBuffer.InputType.PointerUp && evt.isDragFromUI)
        {
            unitManager?.DeployUnit(evt.draggedItemId);
        }

    }

    private void HandleInputResult(InputBuffer.InputEvent evt)
    {
        if (evt.type == InputBuffer.InputType.PointerDown)
        {
            stateManager?.transitionTo(GameState.Title);
        }
    }

    private void HandleInputGameOver(InputBuffer.InputEvent evt)
    {
        if (evt.type == InputBuffer.InputType.PointerDown)
        {
            stateManager?.transitionTo(GameState.Title);
        }
    }

   



}
