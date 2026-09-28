using UnityEngine;

/// <summary>
/// Prep(MasterState)配下のServantState(StageSelect/UnitSelect/FuelSet)を進行させる。
/// この段階では骨格のみ。実際のStageSelect/UnitSelect/FuelSetの中身は③で実装する。
/// </summary>
public class PrepPhaseManager
{
    private readonly StateManager _stateManager;
    private readonly FuelManager _fuelManager;
    private readonly UIManager _uIManager;
    private readonly SectionManager _sectionManager;
    private readonly StageData _stageData;

    private bool _fuelReady;

    public PrepPhaseManager(StateManager stateManager,FuelManager fuelManager,UIManager uIManager,SectionManager sectionManager,StageData stageData)
    {
        _stateManager = stateManager;
        _fuelManager = fuelManager;
        _uIManager = uIManager;
        _sectionManager = sectionManager;
        _stageData = stageData;
    }

    /// <summary> GameState.Prep突入時にGameLoopManagerから呼ばれる </summary>
    public void Enter()
    {
        _fuelReady = false;
        Debug.Log("[PrepPhaseManager] Enter: StageSelectから開始");
    }

    /// <summary> GameLoopManagerのFixedStepから、GameState.Prep中のみ呼ばれる </summary>
    public void Tick(float fixedDt)
    {
        switch (_stateManager.CurrentPrepPhase)
        {
            case PrepPhase.StageSelect:
               // TickStageSelect(fixedDt);
                break;
            case PrepPhase.UnitSelect:
               // TickUnitSelect(fixedDt);
                break;
            case PrepPhase.FuelSet:
                TickFuelSet(fixedDt);
                break;
        }
    }

    /// <summary> GameLoopManagerのConsumeInputから、GameState.Prep中のみ呼ばれる </summary>
    public void HandleInput(InputBuffer.InputEvent evt)
    {
        if (evt.type != InputBuffer.InputType.PointerDown) return;

        switch(_stateManager.CurrentPrepPhase)
        {
            case PrepPhase.StageSelect:
                _stateManager.TransitionPrepPhase(PrepPhase.UnitSelect);
                break;
            case PrepPhase.UnitSelect:
                _sectionManager.Build(_stageData);
                _stateManager.TransitionPrepPhase(PrepPhase.FuelSet);
                break;
            case PrepPhase.FuelSet:
                if(_fuelReady)
                {
                    _stateManager.transitionTo(GameState.Operation);
                }
                break;
        }
    }

    private void TickStageSelect(float fixedDt) { /* ③で実装 */ }
    private void TickUnitSelect(float fixedDt) { /* ③で実装 */ }
    private void TickFuelSet(float fixedDt) 
    {
        _uIManager.UpdateFuelUI(_fuelManager.CurrentFuel,_fuelManager.MaxFuel);

        if (_fuelReady) return;
        if(_fuelManager.InitializingFuel())
        {
            _fuelReady = true;
        }
    }
}