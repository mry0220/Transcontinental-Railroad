using System.Collections.Generic;
using UnityEngine;

public class GameLoopManager : MonoBehaviour
{
    #region Types
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

    #endregion

    #region InspectorConfig
    // 本番: 60Hz固定, Inspector非表示
    //====References====
    [SerializeField] private FuelManager fuelManager;
    [SerializeField] private UIManager uIManager;
    [SerializeField] private UnitManager unitManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private UIInputHandler uIInputHandler;
    [SerializeField] private ParallaxBackground background;
    [SerializeField] private RamSettings ramSettings = new();
    [SerializeField] private BattleVisualSettings battleVisual = new();
     
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

    #endregion

    #region Runtime

    private StateManager stateManager;
    private InputBuffer inputBuffer;
    private MatchManager matchManager;
    private SectionManager sectionManager;
    private PrepPhaseManager prepPhaseManager;
    private OperationPhaseManager operationPhaseManager;
    private BattleEffects _effects;
    private TickScheduler.PauseHandle _menuPause;
    private BattleIndicators _indicators;
    private RunStats _runStats;
    private Item_Train _runTrain;
    private OperationModifiers _modifiers;
    //====Runtime State====
    private RNG rng;
    private double clock;
    private double accumulator;
    private double dt;
    private int stateFrameCount = 0; //statemanagerテスト用

    private GameObject _trainInstance;

    public TickScheduler Scheduler { get; } = new TickScheduler();

    private readonly List<TickScheduler.TickInfo> _tickInfo = new();
    #endregion

    #region UnityLifecycle

    private void Awake()
    {
        Combatant.VisualSettings = battleVisual;
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

        prepPhaseManager = new PrepPhaseManager(stateManager,fuelManager,sectionManager);
        operationPhaseManager = new OperationPhaseManager(
            stateManager,unitManager,waveManager,matchManager,
            fuelManager, sectionManager, () => _trainInstance,rng);
        operationPhaseManager.SetRamSettings(ramSettings);
        operationPhaseManager.SetScheduler(Scheduler);
        _runStats = new RunStats();
        unitManager?.SetRunStats(_runStats);
        operationPhaseManager.SetRunStats(_runStats);
        _modifiers = new OperationModifiers();
        waveManager?.SetModifiers(_modifiers);
        unitManager?.SetModifiers(_modifiers);
        operationPhaseManager.SetModifiers(_modifiers);

        Scheduler.Register(prepPhaseManager, TickPhase.Simulation, 0,
            () => stateManager != null && stateManager.Current == GameState.Prep);
        Scheduler.Register(operationPhaseManager, TickPhase.Simulation, 10,
            () => stateManager != null && stateManager.Current == GameState.Operation);
        Scheduler.Register(new ActionTickable(TickScreenView), TickPhase.Presentation, 10,null,"ScreenView");
        _effects = new BattleEffects(battleVisual);
        Scheduler.Register(_effects, TickPhase.Presentation, 0);
        System.Func<bool> isBattle = () => stateManager != null
        && stateManager.Current == GameState.Operation
        && stateManager.CurrentOperationPhase == OperationPhase.Battle;
        _indicators = new BattleIndicators(battleVisual, isBattle);
        Scheduler.Register(_indicators, TickPhase.Presentation, 5,isBattle,"BattleIndicators");
        
        clock = 0.0;
        accumulator = 0.0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        dt = (double)timestep /1000.0; // 16.6667ms
#else
        dt = 1.0 / 60.0;
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
            {
                uIManager?.SetBattleButtonsVisible(next == OperationPhase.Battle);
                if (next != OperationPhase.Battle) _indicators?.Hide();
            };
            stateManager.OnResultPhaseChanged += (prev, next) => ShowResultPhase(next);
                
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

            uIManager.OnServantResultTapped += operationPhaseManager.ConfirmServantResult;

            uIManager.OnMenuRequested += HandleMenuRequested;
            uIManager.OnMenuResumeResuested += CloseMenu;
            uIManager.OnMenuReturnRequested += HandleMenuReturn;
            operationPhaseManager.OnAlertChanged += uIManager.SetAlert;
            operationPhaseManager.OnRevivePopupRequested += uIManager.ShowRevivePopup;
            operationPhaseManager.OnRevivePopupClosed += uIManager.HideRevivePopup;
            operationPhaseManager.OnServantResultShown += uIManager.ShowServantResult;
            operationPhaseManager.OnServantResultHidden += uIManager.HideServantResult;
            prepPhaseManager.OnLoadoutChanged += uIManager.RefreshUnitSelect;
            prepPhaseManager.OnFuelSetReadyChanged += uIManager.SetFuelSetStatus;
            uIManager.OnReviveConfirmed += operationPhaseManager.ConfirmRevive;
            uIManager.OnReviveCancelled += operationPhaseManager.CancelRevive;
            uIManager.OnEventChoiceChosen += operationPhaseManager.ChooseEventOption;
            operationPhaseManager.OnEventRequested += uIManager.ShowEventPopup;
            operationPhaseManager.OnEventClosed += uIManager.HideEventPopup;
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
        if(unitManager != null)
        {
            unitManager.OnUnitDied += id => _runStats.AddDeath(id);
            unitManager.OnUnitRevived += id => _runStats.AddRevive(id);
        }

        if(stateManager == null)
        Debug.LogError("StateManager not found!");
        if (inputBuffer == null)
        Debug.LogError("InputBuffer not found!");

        Combatant.OnAnyDamaged += HandleAnyDamaged;
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
        Scheduler.Run(TickPhase.Presentation, (float)frameDt);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(logCatchUpWarnings && steps >= maxSteps && accumulator >= dt)
        {
            Debug.LogWarning($"Frame took too long: {frameDt:F4}s," +
                $"skipped {accumulator / dt:F1} steps");
        }

#endif
    }

    private void OnDestroy()
    {
        Combatant.OnAnyDamaged -= HandleAnyDamaged;
    }

    #endregion

    #region FixedStepLoop
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
        Scheduler.Run(TickPhase.Simulation, fixedDt);
    }
    private void ConsumeInput(List<InputBuffer.InputEvent> eventsList)
    {
        if (stateManager == null) return;

        foreach (var evt in eventsList)
        {
            switch(stateManager.Current)
            {
                
              
                case GameState.Operation:
                    operationPhaseManager.HandleInput(evt);
                    break;
                case GameState.Result:
                    HandleInputResult(evt);
                    break;
               
            }
        }
    }
    private void HandleInputResult(InputBuffer.InputEvent evt)
    {
        if (evt.type != InputBuffer.InputType.PointerDown) return;

        if (stateManager.CurrentResultPhase == ResultPhase.UnitResult)
            stateManager.TransitionResultPhase(ResultPhase.TrainResult);
        else
            stateManager.transitionTo(GameState.Prep);
    }

    #endregion

    #region stateTransitions
    private void OnGameStateChanged(GameState prev, GameState next)
    {
        Debug.Log($"[Gameloop]State transition: {prev} -> {next}");
        CloseMenu();
        _indicators?.Clear();
        uIManager?.ShowTitleScreen(next == GameState.Title);
        if (next != GameState.Prep) uIManager?.HidePrepScreens();
        uIManager?.SetRouteBarVisible(next == GameState.Operation);
        uIManager?.SetBattleButtonsVisible(false);
        uIManager?.SetAlert(false);
        if (next != GameState.Operation)
        {
            uIManager?.HideServantResult();
            uIManager?.HideEventPopup();
        }

        stateFrameCount = 0;

        switch(next)
        {
            case GameState.Title:
                if (prev == GameState.Prep)
                    ResetForNewRun();
                if(inputBuffer != null)
                {
                    inputBuffer.Clear();
                    
                    inputBuffer.RecordMode = false;
                    inputBuffer.PlaybackMode = false;
                }
                unitManager?.HideUnitPlacementPreview();
            break;

            case GameState.Prep:
                if(prev == GameState.Result || prev == GameState.Operation)
                {
                    ResetForNewRun();
                }

                prepPhaseManager.Enter();
                uIManager?.ShowPrepScreen(stateManager.CurrentPrepPhase);
                
                break;

            case GameState.Operation:
                background?.ResetScroll();
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
                ShowResultPhase(stateManager.CurrentResultPhase);
            break;
        }
    }
    private void OnPrepPhaseChanged(PrepPhase prev,PrepPhase next)
    {
        uIManager?.ShowPrepScreen(next);
    }
    private void ResetForNewRun()
    {
        _modifiers.Reset();
        operationPhaseManager.ResetForNewRun();

        _effects.Clear();
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

    #endregion

    #region UIEventHandlers
    private void HandleTitleTapped()
    {
        if (stateManager == null || stateManager.Current != GameState.Title) return;
        stateManager.transitionTo(GameState.Prep);
    }
    private void HandleStageChosen(int index)
    {
        if (stages == null || index < 0 || index >= stages.Count) return;
        prepPhaseManager.SelectStage(stages[index]);
    }
    private void HandleLoadoutConfirmed(Loadout loadout)
    {
        _runTrain = loadout.Train;
        _runStats.Reset();
        var ids = new List<string>();
        foreach(var unit in loadout.GetSelectedUnits())
        {
            _runStats.RegisterUnit(unit.id);
            ids.Add(unit.id);
        }
        unitManager?.SetRoster(ids);

        SpawnTrain(loadout.Train);
        uIManager?.BuildUnitIcons(loadout.GetSelectedUnits());
        uIManager?.SetFuelSetTrain(loadout.Train);
    }

    private void HandleAnyDamaged(Combatant combatant,int amount)
    {
        var color = combatant.Affiliation == Base_Item.Affiliation.Enemy
            ? battleVisual.damageToEnemyColor
            : battleVisual.damageToAllyColor;
        _effects.SpawnDamage(combatant.transform.position, amount, color);
    }

    private void HandleMenuRequested()
    {
        if (uIManager == null || stateManager == null) return;
        if (uIManager.IsMenuVisible) CloseMenu();
        else OpenMenu();
    }

    private void OpenMenu()
    {
        var state = stateManager.Current;
        string returnLabel = state switch
        {
            GameState.Prep => "タイトルへ戻る",
            GameState.Operation => "準備画面へ戻る",
            _ => null
        };

        if(state == GameState.Operation && _menuPause == null)
        {
            _menuPause = Scheduler.Pause(TickPhase.Simulation, "Menu");
        }
        uIManager.ShowMenu(true, returnLabel);
    }

    private void CloseMenu()
    {
        _menuPause?.Release();
        _menuPause = null;
        uIManager?.ShowMenu(false, null);
    }

    private void HandleMenuReturn()
    {
        CloseMenu();
        switch (stateManager.Current)
        {
            case GameState.Prep:
                stateManager.transitionTo(GameState.Title);
                break;
            case GameState.Operation:
                stateManager.transitionTo(GameState.Prep);
                break;
        }

    }
    /// <summary>Presentation用。画面に出す値を、Logicから読み取って反映</summary>
    private void TickScreenView(float dt)
    {
        if (uIManager == null || stateManager == null) return;

        switch(stateManager.Current)
        {
            case GameState.Prep:
                if(stateManager.CurrentPrepPhase == PrepPhase.FuelSet)
                {
                    uIManager.UpdateFuelUI(fuelManager.CurrentFuel, fuelManager.MaxFuel);
                }
                break;
            case GameState.Operation:
                TickOperationView(dt);
                break;
        }
    }

    private void TickOperationView(float dt)
    {
        bool isMove = stateManager.CurrentOperationPhase == OperationPhase.Move;

        if (isMove && !Scheduler.IsPaused(TickPhase.Simulation))
        {
            background?.Scroll(operationPhaseManager.MoveSpeed * dt);
        }
        uIManager.UpdateFuelUI(fuelManager.CurrentFuel, fuelManager.MaxFuel);
        float leg = isMove ? operationPhaseManager.MoveProgress : 1f;
        uIManager.UpdateRouteProgress(sectionManager.CurrentIndex, sectionManager.SectionCount, leg);
        
    }

    private void ShowResultPhase(ResultPhase phase)
    {
        if (uIManager == null) return;
        var outcome = stateManager.CurrentOutcome;

        switch(phase)
        {
            case ResultPhase.UnitResult:
                var rows = new List<UIManager.UnitResultRow>();
                foreach(var record in _runStats.Units)
                {
                    rows.Add(new UIManager.UnitResultRow
                    {
                        icon = unitManager != null ? unitManager.GetUnitIcon(record.itemId) : null,
                        dealt = record.dealt,
                        taken = record.taken,
                        revives = record.revives,
                        deaths = record.deaths
                    });
                }
                uIManager.ShowUnitResult(outcome, rows);
                break;

            case ResultPhase.TrainResult:
                bool cleared = outcome == RunOutcome.Cleared;
                uIManager.ShowTrainResult(outcome, new UIManager.TrainResultData
                {
                    trainIcon = _runTrain != null ? _runTrain.icon : null,
                    totalDistance = _runStats.TotalDistance,
                    sectionsPassed = cleared ? sectionManager.SectionCount : sectionManager.CurrentIndex,
                    sectionCount = sectionManager.SectionCount,
                    fuel = fuelManager.CurrentFuel,
                    maxFuel = fuelManager.MaxFuel,
                    crushedUnits = _runStats.CrushedUnits,
                    crushedEnemies = _runStats.CrushedEnemies
                });
                break;
        }
    }

    #endregion

    #region Train
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

    #endregion

    #region Debug

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 350, 100));
        var pauses = Scheduler.ActiveRauses;
        GUILayout.Label($"Pause:{pauses.Count}");
        for(int i = 0;i<pauses.Count;i++)
            GUILayout.Label($"{pauses[i].Phase} : {pauses[i].Reason}");
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(370, 10, 420, 320));
        Scheduler.CollectInfo(_tickInfo);
        for(int p = 0;p < 2;p++)
        {
            var phase = (TickPhase)p;
            GUILayout.Label($"=={phase}{(Scheduler.IsPaused(phase) ? "(PAUSED)" : "")}==");
            for (int i = 0; i < _tickInfo.Count; i++)
            {
                var t = _tickInfo[i];
                if (t.Phase != phase) continue;
                string err = t.ErrorCount > 0 ? $" ERR x{t.ErrorCount}" : "";
                GUILayout.Label($"[{t.Order}]{(t.Active ? "ON " : "off")}{t.Name}{err}");
            }
        }
        GUILayout.EndArea();

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

    #endregion
}
