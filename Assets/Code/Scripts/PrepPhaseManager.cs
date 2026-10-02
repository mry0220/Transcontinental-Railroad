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
    private readonly Loadout _loadout = new();

    public event System.Action<Loadout> OnLoadoutConfirmed;

    private bool InUnitSelect =>
        _stateManager.Current == GameState.Prep &&
        _stateManager.CurrentPrepPhase == PrepPhase.UnitSelect;

    private bool _fuelReady;
    private StageData _selectedStage;

    public PrepPhaseManager(StateManager stateManager,FuelManager fuelManager,UIManager uIManager,SectionManager sectionManager)
    {
        _stateManager = stateManager;
        _fuelManager = fuelManager;
        _uIManager = uIManager;
        _sectionManager = sectionManager;
    }

    /// <summary> GameState.Prep突入時にGameLoopManagerから呼ばれる </summary>
    public void Enter()
    {
        _fuelReady = false;
        _selectedStage = null;
        _loadout.Clear();
        Debug.Log("[PrepPhaseManager] Enter: StageSelectから開始");
    }

    /// <summary> GameLoopManagerのFixedStepから、GameState.Prep中のみ呼ばれる </summary>
    public void Tick(float fixedDt)
    {
        switch (_stateManager.CurrentPrepPhase)
        {
            case PrepPhase.StageSelect:
                TickStageSelect(fixedDt);
                break;
            case PrepPhase.UnitSelect:
                TickUnitSelect(fixedDt);
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
                break;
            case PrepPhase.UnitSelect:
                
                break;
            case PrepPhase.FuelSet:
                if(_fuelReady)
                {
                    _stateManager.transitionTo(GameState.Operation);
                }
                break;
        }
    }

    public void SelectStage(StageData stage)
    {
        if (_stateManager.Current != GameState.Prep) return;
        if (_stateManager.CurrentPrepPhase != PrepPhase.StageSelect) return;
        if (stage == null) return;

        _selectedStage = stage;
        _stateManager.TransitionPrepPhase(PrepPhase.UnitSelect);
        _uIManager.RefreshUnitSelect(_loadout);
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
            _uIManager.SetFuelSetReady(true);
        }
    }

    /// <summary>
    /// FuelSet画面のTapから呼ばれる。充填が終わっていれば、Operationへ
    /// </summary>

    public void ConfirmFuelSet()
    {
        if (_stateManager.Current != GameState.Prep) return;
        if (_stateManager.CurrentPrepPhase != PrepPhase.FuelSet) return;
        if (!_fuelReady) return;

        _stateManager.transitionTo(GameState.Operation);
    }
    public void ChooseTrain(Item_Train train)
    {
        if (!InUnitSelect) return;
        _loadout.SetTrain(train);
         _uIManager.RefreshUnitSelect(_loadout);
    }

    public void ChooseUnit(Item_Unit unit)
    {
        if (!InUnitSelect) return;

        if (_loadout.Contains(unit))_loadout.RemoveUnit(unit);
        else _loadout.TryAddUnit(unit);

        _uIManager.RefreshUnitSelect(_loadout);
    }

    public void ToggleUnit(Item_Unit unit)
    {
        if (!InUnitSelect) return;

        if (_loadout.Contains(unit)) _loadout.RemoveUnit(unit);
        else _loadout.TryAddUnit(unit);

        _uIManager.RefreshUnitSelect(_loadout);
    }

    public void ClearUnitSlot(int slotIndex)
    {
        if (!InUnitSelect) return;
        _loadout.RemoveAt(slotIndex);
        _uIManager.RefreshUnitSelect(_loadout);
    }

    public void BackToStageSelect()
    {
        if (!InUnitSelect) return;
        _stateManager.TransitionPrepPhase(PrepPhase.StageSelect);
    }

    public void ConfirmUnitSelect()
    {
        if (!InUnitSelect) return;
        if (!_loadout.IsValid || _selectedStage == null) return;

        _sectionManager.Build(_selectedStage);
        OnLoadoutConfirmed?.Invoke(_loadout);
        _stateManager.TransitionPrepPhase(PrepPhase.FuelSet);
    }
}