using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Operation(MasterState)配下のServantState(Move/Battle/Result)を進行させる。
/// この段階では骨格のみ。既存のCombatant.Tick/MatchManager.ProcessRequests等の
/// 実際の戦闘ロジックの移植は④で行う。
/// </summary>
public class OperationPhaseManager
{
    private readonly StateManager _stateManager;
    private readonly UnitManager _unitManager;
    private readonly WaveManager _waveManager;
    private readonly MatchManager _matchManager;
    private readonly FuelManager _fuelManager;
    private readonly UIManager _uIManager;
    private readonly SectionManager _sectionManager;
    private readonly System.Func<GameObject> _getTrainInstance;
    private readonly RNG _rng;

    private ParallaxBackground _background;

    private float _moveElapsed;
    private float _moveDuration;

    private bool _isRevivePopupOpen;
    private string _pendingReviveItemId;

    private float _moveDistance;
    private float _moveSpeed;

    public float MoveDistance => _moveDistance;
    public float MoveSpeed => _moveSpeed;
    public float MoveElapsed => _moveElapsed;
    public float MoveDuration => _moveDuration;
    public float MoveProgress => _moveDuration > 0f ? Mathf.Clamp01(_moveElapsed / _moveDuration) : 1f;

    private enum RamStage { Alert,Charge}

    private RamSettings _ramSettings = new();
    private RamStage _ramStage;
    private float _ramTimer;
    private float _ramSpeed;
    private Vector3 _trainHome;
    private bool _trainNeedsReset;

    

    public OperationPhaseManager(
        StateManager stateManager,
        UnitManager unitManager,
        WaveManager waveManager,
        MatchManager matchManager,
        FuelManager fuelManager,
        UIManager uIManager,
        SectionManager sectionManager,
        System.Func<GameObject> getTrainInstance,
        RNG rng)
    {
        _stateManager = stateManager;
        _unitManager = unitManager;
        _uIManager = uIManager;
        _waveManager = waveManager;
        _matchManager = matchManager;
        _fuelManager = fuelManager;
        _sectionManager = sectionManager;
        _getTrainInstance = getTrainInstance;
        _rng = rng;

        _unitManager.OnDeadUnitTapped += HandleDeadUnitTapped;
        _uIManager.OnReviveConfirmed += ConfirmRevive;
        _uIManager.OnReviveCancelled += CancelRevive;
    }

    public void SetRamSettings(RamSettings settings)
    {
        _ramSettings = settings ?? new RamSettings();
    }

    /// <summary> GameState.Operation突入時にGameLoopManagerから呼ばれる </summary>
    public void Enter()
    {
        Debug.Log("[OperationPhaseManager] Enter: Moveから開始");
        _background?.ResetScroll();
        ClosePopup();
        EnterMove();
    }

    private void UpdateRouteBar()
    {
        float leg = _stateManager.CurrentOperationPhase == OperationPhase.Move ? MoveProgress : 1f;
        _uIManager.UpdateRouteProgress(_sectionManager.CurrentIndex, _sectionManager.SectionCount, leg);
    }

    /// <summary> GameLoopManagerのFixedStepから、GameState.Operation中のみ呼ばれる </summary>
    public void Tick(float fixedDt)
    {
        if (_isRevivePopupOpen) return;

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
               // TickResult(fixedDt);
                break;
        }
        UpdateRouteBar();
        _uIManager.UpdateFuelUI(_fuelManager.CurrentFuel, _fuelManager.MaxFuel); ;
    }

    private void TickAllRevivals(float fixedDt)
    {
        _unitManager?.TickRevivals(fixedDt, _stateManager.CurrentOperationPhase == OperationPhase.Move);
    }

    /// <summary> GameLoopManagerのConsumeInputから、GameState.Operation中のみ呼ばれる </summary>
    public void HandleInput(InputBuffer.InputEvent evt)
    {
        if(_stateManager.CurrentOperationPhase == OperationPhase.Result)
        {
            if(evt.type == InputBuffer.InputType.PointerDown)
            {
                AdvanceAfterResult();
            }
        }

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

    private void AdvanceAfterResult()
    {
        if(!_sectionManager.AdvanceToNextSection())
        {
            Debug.LogWarning("[OperationPhaseManager]次のSectionがない状態でResultからの進行が呼ばれた");
            return;
        }
        Combatant.DestroyDead(Base_Item.Affiliation.Enemy);
        _stateManager.TransitionOperationPhase(OperationPhase.Move);
        EnterMove();
    }

    private void EnterMove()
    {
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

        _background?.Scroll(_moveSpeed * fixedDt);

        _moveElapsed += fixedDt;
        if (_moveElapsed < _moveDuration) return;

        ProcessCurrentSection();
    }

    private void ProcessCurrentSection()
    {
        ClosePopup();
        var entry = _sectionManager.CurrentSection;

        switch (entry.section.type)
        {
            case SectionType.Battle:
                _waveManager?.StartBattlePhase(entry.section.wave);
                _stateManager.TransitionOperationPhase(OperationPhase.Battle); ;
                break;
            case SectionType.Event:
                ApplyEventEffect(entry.section);
                _stateManager.TransitionOperationPhase(OperationPhase.Result);
                break;
            case SectionType.Goal:
                _stateManager.TransitionToResult(RunOutcome.Cleared);
                break;
        }
    }
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

    private void TickResult(float fixedDt) { /* ④で実装 */ }

    private void ApplyEventEffect(SectionData section)
    {
        _fuelManager.ConsumeAmount(-section.fuelRestoreAmount);
    }

    private void HandleDeadUnitTapped(string itemId)
    {
        if (_stateManager.CurrentOperationPhase != OperationPhase.Move) return;
        if (_isRevivePopupOpen) return;

        _pendingReviveItemId = itemId;
        _isRevivePopupOpen = true;

        _uIManager.ShowRevivePopup(_unitManager.GetUnitIcon(itemId),_unitManager.GetReviveFuelCost(itemId));
    }

    private void ConfirmRevive()
    {
        if (!_isRevivePopupOpen) return;
        _unitManager.TryConfirmRevive(_pendingReviveItemId);
        ClosePopup();
    }

    private void CancelRevive()
    {
        ClosePopup();
    }

    private void ClosePopup()
    {
        _isRevivePopupOpen = false;
        _pendingReviveItemId = null;
        _uIManager.HideRevivePopup();
    }

    public void SetBackground(ParallaxBackground background)
    {
        _background = background;
    }

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
        _uIManager.SetAlert(true);
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

    private void TickRam(float fixedDt)
    {
        var train = _getTrainInstance();
        if(train = null)
        {
            FinishRam(null);
            return;
        }

        if(_ramStage == RamStage.Alert)
        {
            _ramTimer += fixedDt;
            if(_ramTimer >= _ramSettings.alertDuration)
            {
                _uIManager.SetAlert(false);
                _ramStage = RamStage.Charge;
            }
            return;
        }

        _ramSpeed = Mathf.Min(_ramSpeed + _ramSettings.alertDuration * fixedDt, _ramSettings.maxSpeed);
        train.transform.position += Vector3.right * (_ramSpeed * fixedDt);
        CrushEntitiesUpTo(train.transform.position.x + _ramSettings.frontOffset, train);


        if (train.transform.position.x > GetScreenRightEdge() + _ramSettings.exitMargin)
        {
            FinishRam(train);
        }
    }

    private void CrushEntitiesUpTo(float frontX,GameObject train)
    {
        foreach (var combatant in Combatant.All)
        {
            if (train != null && combatant.gameObject == train) continue;
            if (combatant.IsDead) continue;
            if (combatant.transform.position.x > frontX) continue;

            bool isAlly = combatant.Affiliation == Base_Item.Affiliation.Ally;
            combatant.ApplyDamage(int.MaxValue);

            if (isAlly && _unitManager != null && _unitManager.CrushUnit(combatant))
            {
                _fuelManager.ConsumeAmount(-_ramSettings.allyCrushFuelRefund);
            }
        }
    }

    private void FinishRam(GameObject train)
    {
        _uIManager.SetAlert(false);

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
}

