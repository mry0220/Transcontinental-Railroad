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

    private float _moveElapsed;
    private float _moveDuration;

    private bool _isRevivePopupOpen;
    private string _pendingReviveItemId;

    

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

    /// <summary> GameState.Operation突入時にGameLoopManagerから呼ばれる </summary>
    public void Enter()
    {
        Debug.Log("[OperationPhaseManager] Enter: Moveから開始");
        ClosePopup();
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
            case OperationPhase.Result:
               // TickResult(fixedDt);
                break;
        }
    }

    private void TickAllRevivals(float fixedDt)
    {
        foreach(var combatant in Combatant.All)
        {
            if (!combatant.IsDead || !combatant.IsReviving) continue;

            combatant.TickRevive(fixedDt);

            if(_stateManager.CurrentOperationPhase == OperationPhase.Move && combatant.IsReviveReady)
            {
                combatant.CompleteRevive();
            }
        }
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

        float distance = _rng.Range(entry.distanceMin,entry.distanceMax);
        float trainSpeed = GetTrainMoveSpeed();

        _moveElapsed = 0f;
        _moveDuration = trainSpeed > 0f ? distance / trainSpeed : 0f;

        
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
                _unitManager?.ResetUnitStatuses();
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
        _uIManager.UpdateFuelUI(_fuelManager.CurrentFuel, _fuelManager.MaxFuel);

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
       // if (_waveManager != null && _waveManager.HasPendingWaves) return false;

        bool anyEnemyAlive = Combatant.All.Any(c => c.Affiliation == Base_Item.Affiliation.Enemy && !c.IsDead);
        if (!anyEnemyAlive)
        {
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

        var combatant = _unitManager.GetDeployedCombatant(itemId);
        _uIManager.ShowRevivePopup(combatant != null ? combatant.ReviveFuelCost : 0);
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
}

