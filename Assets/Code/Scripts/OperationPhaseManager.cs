using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Operation(MasterState)配下のServantState(Move/Battle/Result)を進行させる。
/// この段階では骨格のみ。既存のCombatant.Tick/MatchManager.ProcessRequests等の
/// 実際の戦闘ロジックの移植は④で行う。
/// </summary>
public class OperationPhaseManager : ITickable
{
    #region References

    private readonly StateManager _stateManager;
    private readonly UnitManager _unitManager;
    private readonly WaveManager _waveManager;
    private readonly MatchManager _matchManager;
    private readonly FuelManager _fuelManager;
    private readonly SectionManager _sectionManager;
    private readonly System.Func<GameObject> _getTrainInstance;
    private readonly RNG _rng;

    private RamSettings _ramSettings = new();
    private OperationModifiers _modifiers;
    private readonly List<Data_Wave> _pendingBattleWaves = new();
    private EventData _currentEvent;

    private TickScheduler _scheduler;
    private TickScheduler.PauseHandle _popupPause;
    private RunStats _runStats;
    public void SetRunStats(RunStats runStats) => _runStats = runStats;

    #endregion

    #region MoveState

    private float _moveElapsed;
    private float _moveDuration;
    private float _moveDistance;
    private float _moveSpeed;
    public float MoveDistance => _moveDistance;
    public float MoveSpeed => _moveSpeed;
    public float MoveElapsed => _moveElapsed;
    public float MoveDuration => _moveDuration;
    public float MoveProgress => _moveDuration > 0f ? Mathf.Clamp01(_moveElapsed / _moveDuration) : 1f;

    #endregion

    #region Event
    public event System.Action<bool> OnAlertChanged;
    public event System.Action<Sprite, int> OnRevivePopupRequested;
    public event System.Action OnRevivePopupClosed;
    public event System.Action<int, List<UnitManager.BattleDamageEntry>> OnServantResultShown;
    public event System.Action OnServantResultHidden;
    public event System.Action<EventData> OnEventRequested;
    public event System.Action OnEventClosed;
    #endregion

    #region RevivePopupState

    private bool _isRevivePopupOpen;
    private string _pendingReviveItemId;
    private enum RamStage { Alert,Charge}
    private RamStage _ramStage;
    private float _ramTimer;
    private float _ramSpeed;
    private Vector3 _trainHome;
    private bool _trainNeedsReset;

    private int _fuelAtSectionStart;
    #endregion

    #region Setup
    public OperationPhaseManager(
        StateManager stateManager,
        UnitManager unitManager,
        WaveManager waveManager,
        MatchManager matchManager,
        FuelManager fuelManager,
        SectionManager sectionManager,
        System.Func<GameObject> getTrainInstance,
        RNG rng)
    {
        _stateManager = stateManager;
        _unitManager = unitManager;
        _waveManager = waveManager;
        _matchManager = matchManager;
        _fuelManager = fuelManager;
        _sectionManager = sectionManager;
        _getTrainInstance = getTrainInstance;
        _rng = rng;

        _unitManager.OnDeadUnitTapped += HandleDeadUnitTapped;

        _stateManager.OnOperationPhaseChanged += HandleOperationPhaseChanged;
    }
    public void SetRamSettings(RamSettings settings)
    {
        _ramSettings = settings ?? new RamSettings();
    }

    public void SetScheduler(TickScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    private void HandleOperationPhaseChanged(OperationPhase prev,OperationPhase next)
    {
        if (next == OperationPhase.Result)
        {
            bool hadBattle = _sectionManager.CurrentSection?.section?.type == SectionType.Battle;
            var ranking = hadBattle && _unitManager != null
                ? _unitManager.GetBattleDamageRanking()
                : new List<UnitManager.BattleDamageEntry>();

            OnServantResultShown?.Invoke(_fuelManager.CurrentFuel - _fuelAtSectionStart, ranking);
        }
        else if(prev == OperationPhase.Result)
        {
            OnServantResultHidden?.Invoke();
        }
    }

    /// <summary>運行の中断(titleへ戻るなど)で呼ばれる。ポップアップのPauseを確実に解除する</summary>
    public void ResetForNewRun()
    {
        ClosePopup();
        CloseEvent();
        _pendingBattleWaves.Clear();
        _trainNeedsReset = false;
    }

    public void SetModifiers(OperationModifiers modifiers) => _modifiers = modifiers;
    #endregion

    #region EntryAndTick

    /// <summary> GameState.Operation突入時にGameLoopManagerから呼ばれる </summary>
    public void Enter()
    {
        Debug.Log("[OperationPhaseManager] Enter: Moveから開始");
        ClosePopup();
        CloseEvent();
        _pendingBattleWaves.Clear();
        EnterMove();
    }

    /// <summary> GameLoopManagerのFixedStepから、GameState.Operation中のみ呼ばれる </summary>
    public void Tick(float fixedDt)
    {

        TickAllRevivals(fixedDt);

        switch (_stateManager.CurrentOperationPhase)
        {
            case OperationPhase.Move:
                TickMove(fixedDt);
                break;
            case OperationPhase.Battle:
                TickBattle(fixedDt);
                break;
            case OperationPhase.Ram:
                TickRam(fixedDt);
                break;
            case OperationPhase.Result:
                break;
            case OperationPhase.Event:
                break;
        }
    }
    /// <summary>Presentation用。見た目の更新だけを行い、ゲームの結果には影響させない</summary>
    private void TickAllRevivals(float fixedDt)
    {
        _unitManager?.TickRevivals(fixedDt, _stateManager.CurrentOperationPhase == OperationPhase.Move);
    }

    #endregion

    #region Input
    /// <summary> GameLoopManagerのConsumeInputから、GameState.Operation中のみ呼ばれる </summary>
    public void HandleInput(InputBuffer.InputEvent evt)
    {
        

        if (_stateManager.CurrentOperationPhase != OperationPhase.Battle) return;
        
        if(evt.type == InputBuffer.InputType.PointerMove && evt.isDragFromUI)
        {
            _unitManager?.UpdateUnitPlacementPreview(evt);
        }
        if(evt.type == InputBuffer.InputType.PointerUp && evt.isDragFromUI)
        {
            _unitManager?.DeployUnit(evt.draggedItemId);
        }

    }

    #endregion

    #region Move
    private void EnterMove()
    {
        _fuelAtSectionStart = _fuelManager.CurrentFuel;

        var entry = _sectionManager.CurrentSection;
        if(entry == null || entry.section == null)
        {
            Debug.LogError("[OperationPhaseManager] CurrentSectionがNull SectuionManger.Buildが未実行の可能性 ");
            return;
        }

        
        _moveDistance = _rng.Range(entry.distanceMin, entry.distanceMax);
        _moveSpeed = GetTrainMoveSpeed();

        _moveElapsed = 0f;
        _moveDuration = _moveSpeed > 0f ? _moveDistance / _moveSpeed : 0f;

        
    }
    private float GetTrainMoveSpeed()
    {
        var trainInstance = _getTrainInstance();
        var trainCombatant = trainInstance != null ? trainInstance.GetComponent<Combatant>() : null;
        return trainCombatant != null ? trainCombatant.MoveSpeed : 1f;
    }
    private void TickMove(float fixedDt) 
    {
        if (!TickFuel(true, fixedDt)) return;

        float step = Mathf.Min(fixedDt, Mathf.Max(0f, _moveDuration - _moveElapsed));
        _runStats?.AddDistance(MoveSpeed * step);
        _moveElapsed += fixedDt;
        if (_moveElapsed < _moveDuration) return;

        ProcessCurrentSection();
    }

    #endregion

    #region SectionProcessing
    private void ProcessCurrentSection()
    {
        ClosePopup();
        var entry = _sectionManager.CurrentSection;

        switch (entry.section.type)
        {
            case SectionType.Battle:
                _unitManager?.BeginBattleStats();
                _waveManager?.StartBattlePhase(entry.section.wave);
                foreach(var extra in _pendingBattleWaves)
                {
                    _waveManager?.StartBattlePhase(extra);
                }
                _pendingBattleWaves.Clear();
                _stateManager.TransitionOperationPhase(OperationPhase.Battle);
                break;
            case SectionType.Event:
                EnterEvent(entry.section.eventData);
                break;
            case SectionType.Goal:
                _stateManager.TransitionToResult(RunOutcome.Cleared);
                break;
        }
    }

    private void EnterEvent(EventData data)
    {
        _stateManager.TransitionOperationPhase(OperationPhase.Event);

        if (data == null || data.choices.Count == 0)
        {
            Debug.LogWarning("[OperationPhaseManager] EventDataが未設定か選択が空。何も起こさず進行");
            _stateManager.TransitionOperationPhase(OperationPhase.Result);
            return;
        }

        _currentEvent = data;
        OnEventRequested?.Invoke(data);
    }

    public void ChooseEventOption(int index)
    {
        if (_stateManager.Current != GameState.Operation) return;
        if (_stateManager.CurrentOperationPhase != OperationPhase.Event) return;
        if (_currentEvent == null || index < 0 || index >= _currentEvent.choices.Count) return;

        foreach(var effect in _currentEvent.choices[index].effects)
        {
            ApplyEventEffect(effect);
        }
        CloseEvent();

        if(_fuelManager.CurrentFuel <= 0)
        {
            GetTrainCombatant()?.ApplyDamage(int.MaxValue);
            _stateManager.TransitionToResult(RunOutcome.Failed);
            return;
        }
        _stateManager.TransitionOperationPhase(OperationPhase.Result);
    }

    private void ApplyEventEffect(EventEffect effect)
    {
        switch(effect.type)
        {
            case EventEffectType.Fuel:
                _fuelManager.ConsumeAmount(-effect.fuelAmount);
                break;
            case EventEffectType.KillUnit:
                _unitManager?.KillRandomAliveUnits(effect.count,PickIndex);
                break;
            case EventEffectType.ReviveUnit:
                _modifiers?.ApplyMultiplier(effect.target, effect.multiplier);
                break;
            case EventEffectType.ChangeBattle:
                if (effect.extraWave != null)
                {
                    Debug.Log("[OperationPhase] ChangeBattleSection");
                    _pendingBattleWaves.Add(effect.extraWave); 
                }
                break;

        }
    }

    private int PickIndex(int n) => Mathf.Min(n - 1, Mathf.FloorToInt(_rng.NextFloat() * n));

    private void CloseEvent()
    {
        _currentEvent = null;
        OnEventClosed?.Invoke();
    }

    #endregion

    #region Battle
    private void TickBattle(float fixedDt) 
    {

        foreach (var combatant in Combatant.All)
        {
            combatant.Tick(fixedDt);
        }
        _matchManager.ProcessRequests();

        if (CheckBattleClear()) return;

        TickFuel(false,fixedDt);
    }
    private bool CheckBattleClear()
    {
        bool anyEnemyAlive = Combatant.All.Any(c => c.Affiliation == Base_Item.Affiliation.Enemy && !c.IsDead);
        if (!anyEnemyAlive)
        {
            Combatant.DestroyDead(Base_Item.Affiliation.Enemy);
            _unitManager?.SettleBattle();
            _stateManager.TransitionOperationPhase(OperationPhase.Result);
            return true;
        }
        return false;
    }

    /// <summary>
    /// RETURNボタンから呼ばれる。Battle中のみ有効
    /// </summary>
    public void RequestReturnAll()
    {
        if (_stateManager.Current != GameState.Operation) return;
        if (_stateManager.CurrentOperationPhase != OperationPhase.Battle) return;

        _unitManager?.ReturnAllAliveUnits();
    }

    #endregion

    #region Ram

    /// <summary>
    /// RAMボタンから呼ばれるBattle中のみ有効
    /// </summary>
    public void RequestRam()
    {
        if (_stateManager.Current != GameState.Operation) return;
        if (_stateManager.CurrentOperationPhase != OperationPhase.Battle) return;

        _unitManager?.HideUnitPlacementPreview();

        var train = _getTrainInstance();
        _trainHome = train != null ? train.transform.position : Vector3.zero;
        _ramStage = RamStage.Alert;
        _ramTimer = 0f;
        _ramSpeed = 0f;

        _stateManager.TransitionOperationPhase(OperationPhase.Ram);
        OnAlertChanged?.Invoke(true);
        
    }
    private void TickRam(float fixedDt)
    {
        var train = _getTrainInstance();
        if(train == null)
        {
            FinishRam(null);
            return;
        }

        if(_ramStage == RamStage.Alert)
        {
            _ramTimer += fixedDt;
            if(_ramTimer >= _ramSettings.alertDuration)
            {
                OnAlertChanged?.Invoke(false);
                _ramStage = RamStage.Charge;
            }
            return;
        }

        _ramSpeed = Mathf.Min(_ramSpeed + _ramSettings.acceleration * fixedDt, _ramSettings.maxSpeed);
        train.transform.position += Vector3.right * (_ramSpeed * fixedDt);
        CrushEntitiesUpTo(train.transform.position.x + _ramSettings.frontOffset, train);


        if (train.transform.position.x > GetScreenRightEdge() + _ramSettings.exitMargin)
        {
            FinishRam(train);
        }
    }
    private void CrushEntitiesUpTo(float frontX,GameObject train)
    {
        foreach (var combatant in Combatant.All.ToList())
        {
            if (train != null && combatant.gameObject == train) continue;
            if (combatant.IsDead) continue;
            if (combatant.transform.position.x > frontX) continue;

            bool isAlly = combatant.Affiliation == Base_Item.Affiliation.Ally;
            combatant.ApplyDamage(int.MaxValue);

            if(isAlly)
            {
                if(_unitManager != null && _unitManager.CrushUnit(combatant))
                {
                    _fuelManager.ConsumeAmount(-_ramSettings.allyCrushFuelRefund);
                    _runStats?.AddCrushedUnit();
                }
            }
            else
            {
                _runStats?.AddCrushedEnemy();
            }
        }
    }
    private void FinishRam(GameObject train)
    {
        OnAlertChanged?.Invoke(false);

        CrushEntitiesUpTo(float.PositiveInfinity,train);

        Combatant.DestroyDead(Base_Item.Affiliation.Enemy);
        _matchManager.Clear();
        _unitManager?.SettleBattle();

        _trainNeedsReset = true;
        _stateManager.TransitionOperationPhase(OperationPhase.Result);
    }
    private static float GetScreenRightEdge()
    {
        var cam = Camera.main;
        if (cam == null) return 20f;
        return cam.transform.position.x + cam.orthographicSize * cam.aspect;
    }
    private void ResetTrainPosition()
    {
        if (!_trainNeedsReset) return;
        _trainNeedsReset = false;

        var train = _getTrainInstance();
        if (train != null) train.transform.position = _trainHome;
    }

    #endregion

    #region Result
    private void AdvanceAfterResult()
    {
        if(!_sectionManager.AdvanceToNextSection())
        {
            Debug.LogWarning("[OperationPhaseManager]次のSectionがない状態でResultからの進行が呼ばれた");
            return;
        }
        Combatant.DestroyDead(Base_Item.Affiliation.Enemy);
        _stateManager.TransitionOperationPhase(OperationPhase.Move);
        ResetTrainPosition();
        EnterMove();
    }

    /// <summary>
    /// servant Resultの画面タップから呼ばれる
    /// </summary>
    public void ConfirmServantResult()
    {
        if (_stateManager.Current != GameState.Operation) return;
        if (_stateManager.CurrentOperationPhase != OperationPhase.Result) return;

        AdvanceAfterResult();
    }

    #endregion

    #region Fuel
    private bool TickFuel(bool isMoving,float fixedDt)
    {
        var trainCombatant = GetTrainCombatant();
        float attackPower = trainCombatant != null ? trainCombatant.AttackPower : 0f;
        float speed = GetTrainMoveSpeed();

        if (_fuelManager.ConsumeOperationFuel(attackPower, speed, isMoving, fixedDt)) return true;

        trainCombatant?.ApplyDamage(int.MaxValue);
        _stateManager.TransitionToResult(RunOutcome.Failed);
        return false;
    }
    private Combatant GetTrainCombatant()
    {
        var trainInstance = _getTrainInstance();
        return trainInstance != null ? trainInstance.GetComponent<Combatant>() : null;
    }

    #endregion

    #region RevivePopup
    private void HandleDeadUnitTapped(string itemId)
    {
        if (_stateManager.CurrentOperationPhase != OperationPhase.Move) return;
        if (_isRevivePopupOpen) return;

        _pendingReviveItemId = itemId;
        _isRevivePopupOpen = true;
        _popupPause = _scheduler?.Pause(TickPhase.Simulation,"RevivePopup");

        OnRevivePopupRequested?.Invoke(
            _unitManager.GetUnitIcon(itemId),
            _unitManager.GetReviveFuelCost(itemId));
    }
    public void ConfirmRevive()
    {
        if (!_isRevivePopupOpen) return;
        _unitManager.TryConfirmRevive(_pendingReviveItemId);
        ClosePopup();
    }
    public void CancelRevive()
    {
        ClosePopup();
    }
    private void ClosePopup()
    {
        _popupPause?.Release();
        _popupPause = null;
        _isRevivePopupOpen = false;
        _pendingReviveItemId = null;
        OnRevivePopupClosed?.Invoke();
    }

    #endregion

}

