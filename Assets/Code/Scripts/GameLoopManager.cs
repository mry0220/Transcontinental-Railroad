using System.Collections.Generic;
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
    [SerializeField] private UIInputHandler uIInputHandler;
    [SerializeField] private ParallaxBackground background;
    [SerializeField] private RamSettings ramSettings = new();
    private MatchManager matchManager;
    private SectionManager sectionManager;

    //====Inspector Config
    [SerializeField] private List<StageData> stages;
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

    [SerializeField] private StageData testStageData; 

    //====Runtime State====
    private RNG rng;
    private double clock;
    private double accumulator;
    private int stateFrameCount = 0; //statemanagerテスト用

    private PrepPhaseManager prepPhaseManager;
    private OperationPhaseManager operationPhaseManager;

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

        rng = new RNG(seed);

        matchManager = new();
        waveManager?.SetMatchManager(matchManager);
        unitManager?.SetMatchManager(matchManager);
        unitManager?.SetFuelManager(fuelManager);
        uIInputHandler?.SetInputBuffer(inputBuffer);
        uIInputHandler?.SetUnitManager(unitManager);
        uIInputHandler?.SetStateManager(stateManager);
        sectionManager = new();

        prepPhaseManager = new PrepPhaseManager(stateManager,fuelManager,uIManager,sectionManager);
        operationPhaseManager = new OperationPhaseManager(
            stateManager,unitManager,waveManager,matchManager,
            fuelManager,uIManager, sectionManager, () => _trainInstance,rng);
        operationPhaseManager.SetBackground(background);
        operationPhaseManager.SetRamSettings(ramSettings);

        clock  = 0.0;
        accumulator = 0.0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        dt = (double)timestep /1000.0; // 16.6667ms
#endif

        Application.targetFrameRate = (int)fps;
        QualitySettings.vSyncCount = vSync ? 1 : 0;    

    }

    private void Start()
    {
        if (stateManager != null)
        {
            stateManager.OnStateChanged += OnGameStateChanged;
            stateManager.OnPrepPhaseChanged += OnPrepPhaseChanged;
            stateManager.OnOperationPhaseChanged += (prev, next) =>
                uIManager?.SetBattleButtonsVisible(next == OperationPhase.Battle);
        }

        if(uIManager != null)
        {
            uIManager.SetStageList(stages);

            uIManager.SetUnitSelectCandidates(trainDB != null ? trainDB.trains : null);

            uIManager.OnTrainChosen += prepPhaseManager.ChooseTrain;
            uIManager.OnUnitCandidateChosen += prepPhaseManager.ToggleUnit;
            uIManager.OnUnitSlotChosen += prepPhaseManager.ClearUnitSlot;
            uIManager.OnUnitSelectConfirmed += prepPhaseManager.ConfirmUnitSelect;
            uIManager.OnUnitSelectBack += prepPhaseManager.BackToStageSelect;

            uIManager.OnStageChosen += HandleStageChosen;
            uIManager.OnTitleTapped += HandleTitleTapped;
            uIManager.OnFuelSetTapped += prepPhaseManager.ConfirmFuelSet;

            uIManager.OnRamRequested += operationPhaseManager.RequestRam;
            uIManager.OnReturnAllRequested += operationPhaseManager.RequestReturnAll;

            uIManager.ShowTitleScreen(stateManager != null && stateManager.Current == GameState.Title);
        }
        unitManager?.SetCrushedReviveCostMultiplier(ramSettings.crushedReviveCostMultiplier);

        prepPhaseManager.OnLoadoutConfirmed += HandleLoadoutConfirmed;

        if(unitManager != null && uIManager != null)
        {
            unitManager.OnUnitDeployed += uIManager.HandleUnitDeployed;
            unitManager.OnUnitReturned += uIManager.HandleUnitReturned;
            unitManager.OnUnitDied += uIManager.HandleUnitDied;
            unitManager.OnUnitRevived += uIManager.HandleUnitRevived;
            unitManager.OnReviveStarted += uIManager.HandleReviveStarted;
            unitManager.OnReviveProgress += uIManager.HandleReviveProgress;
        }

        if(stateManager == null)
        Debug.LogError("StateManager not found!");
        if (inputBuffer == null)
        Debug.LogError("InputBuffer not found!");
    }

    private void HandleLoadoutConfirmed(Loadout loadout)
    {
        SpawnTrain(loadout.Train);
        uIManager?.BuildUnitIcons(loadout.GetSelectedUnits());
        uIManager?.SetFuelSetTrain(loadout.Train);
    }

    private void SpawnTrain(Item_Train train)
    {
        if (_trainInstance != null) Destroy(_trainInstance);
        if (train == null || train.prefab == null) return;

        _trainInstance = Instantiate(train.prefab, trainSpawnPosition, Quaternion.identity);
        var trainConbatant = _trainInstance.GetComponent < Combatant>();
        trainConbatant?.Initialize(train, matchManager);
        if(trainConbatant != null)
        {
            trainConbatant.OnDamageTaken += dmg => fuelManager.ConsumeAmount(dmg);
        }
        fuelManager.SetMaxFuel(train.maxHP);
    }

    private void OnGameStateChanged(GameState prev, GameState next)
    {
        Debug.Log($"[Gameloop]State transition: {prev} -> {next}");
        uIManager?.ShowTitleScreen(next == GameState.Title);
        if (next != GameState.Prep) uIManager?.HidePrepScreens();
        uIManager?.SetRouteBarVisible(next == GameState.Operation);
        uIManager?.SetBattleButtonsVisible(false);
        uIManager?.SetAlert(false);

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
                if(prev == GameState.Result)
                {
                    ResetForNewRun();
                }

                prepPhaseManager.Enter();
                uIManager?.ShowPrepScreen(stateManager.CurrentPrepPhase);
                // 既存の列車生成処理は④でPrepPhaseManager.TickFuelSetへ移植予定、現時点ではここに残す
                //if (_trainInstance == null && trainDB != null && trainDB.train.prefab)
                //{
                //    _trainInstance = Instantiate(trainDB.trains., trainSpawnPosition, Quaternion.identity);
                //    var trainCombatant = _trainInstance.GetComponent<Combatant>();
                //    trainCombatant?.Initialize(trainDB.train, matchManager);
                //    if (trainCombatant != null)
                //    {
                //        trainCombatant.OnDamageTaken += dmg => fuelManager.ConsumeAmount(dmg);
                //    }
                //    fuelManager.SetMaxFuel(trainDB.train.maxHP);
                //}
                break;

            case GameState.Operation:
                uIManager?.BuildRouteBar(sectionManager.GetSectionTypes());
                operationPhaseManager.Enter();
                break;

            case GameState.Result:
                if(inputBuffer != null)
                {
                    inputBuffer.RecordMode = false;
                    inputBuffer.PlaybackMode = false;
                }
                Combatant.DestroyDead();
                uIManager?.ShowResult(stateManager.CurrentOutcome);
            break;
        }
    }

    private void ResetForNewRun()
    {
        inputBuffer?.Clear();

        unitManager?.ResetForNewRun();
        waveManager?.ResetForNewRun();
        matchManager.Clear();

        Combatant.DestroyAll();
        _trainInstance = null;

        fuelManager.ResetFuel();
        uIManager?.HideResult();
        uIManager?.ResetUnitIcons();
        uIManager?.UpdateFuelUI(0,fuelManager.MaxFuel);
    }

    private void HandleTitleTapped()
    {
        if (stateManager == null || stateManager.Current != GameState.Title) return;
        stateManager.transitionTo(GameState.Prep);
    }

    private void Update()
    {
        double frameDt = Time.deltaTime;
        accumulator += frameDt;

        int steps = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int maxSteps = enableCatchUp ? maxCatchUpIterations : 1;
#else
    const int maxSteps = 5;
#endif

        while (accumulator >= dt && steps < maxSteps)
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
                prepPhaseManager.Tick(fixedDt);
                break;
            case GameState.Operation:
                operationPhaseManager.Tick(fixedDt);
                break;
            case GameState.Result:
                UpdateResult(fixedDt);
                break;
            
        }
    }

    private void UpdateTitle(float fixedDt)
    {
    }

    private void OnPrepPhaseChanged(PrepPhase prev,PrepPhase next)
    {
        uIManager?.ShowPrepScreen(next);
    }

    private void HandleStageChosen(int index)
    {
        if (stages == null || index < 0 || index >= stages.Count) return;
        prepPhaseManager.SelectStage(stages[index]);
    }

    

   

    private void UpdateResult(float fixedDt)
    {
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
                    prepPhaseManager.HandleInput(evt);
                    break;
                case GameState.Operation:
                    operationPhaseManager.HandleInput(evt);
                    break;
                case GameState.Result:
                    HandleInputResult(evt);
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

   

   

    private void HandleInputResult(InputBuffer.InputEvent evt)
    {
        if (evt.type == InputBuffer.InputType.PointerDown)
        {
            stateManager?.transitionTo(GameState.Prep);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (stateManager == null || stateManager.Current != GameState.Operation) return;

        var entry = sectionManager.CurrentSection;
        string typeText = (entry != null && entry.section != null) ? entry.section.type.ToString() : "-";
        string rangeText = entry != null ? entry.distanceMin.ToString("F1") + " - " + entry.distanceMax.ToString("F1") : "-";

        GUILayout.BeginArea(new Rect(10, 480, 350, 220));
        GUILayout.Label("=== Move ===");
        GUILayout.Label($"Phase: {stateManager.CurrentOperationPhase}");
        GUILayout.Label($"Section: {sectionManager.CurrentIndex + 1} / {sectionManager.SectionCount} ({typeText})");
        GUILayout.Label($"Distance Range: {rangeText}");
        GUILayout.Label($"Distance (RNG): {operationPhaseManager.MoveDistance:F2}");
        GUILayout.Label($"Train Speed: {operationPhaseManager.MoveSpeed:F2}");
        GUILayout.Label($"Elapsed / Duration: {operationPhaseManager.MoveElapsed:F2} / {operationPhaseManager.MoveDuration:F2} s");
        GUILayout.Label($"Progress: {operationPhaseManager.MoveProgress * 100f:F0} %");
        GUILayout.EndArea();
    }
#endif





}
