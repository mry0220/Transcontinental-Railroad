using UnityEngine;

/// <summary>
/// Prep(MasterState)配下のServantState(StageSelect/UnitSelect/FuelSet)を進行させる。
/// </summary>
public class PrepPhaseManager : ITickable
{
    #region Type

    private enum FuelStage { Charge,Pay,Ready}
    private FuelStage _fuelStage;
    private int _payRemaining;

    #endregion


    #region References

    private readonly StateManager _stateManager;
    private readonly FuelManager _fuelManager;
    private readonly SectionManager _sectionManager;

    #endregion

    #region RuntimeState
    private readonly Loadout _loadout = new();
    private StageData _selectedStage;
    private bool _fuelReady;
    private bool InUnitSelect =>
        _stateManager.Current == GameState.Prep &&
        _stateManager.CurrentPrepPhase == PrepPhase.UnitSelect;
    private int FuelBudget =>
        _loadout.Train != null ? _loadout.Train.maxHP - _fuelManager.MinStartFuel : int.MaxValue;

    private bool CanAfford(Item_Unit unit) =>
        _loadout.TotalFuelCost + unit.fuelCost <= FuelBudget;

    #endregion

    #region Event
    public event System.Action<Loadout> OnLoadoutConfirmed;
    public event System.Action<Loadout> OnLoadoutChanged;
    public event System.Action<bool> OnFuelSetReadyChanged;
    #endregion

    #region Setup
    public PrepPhaseManager(
        StateManager stateManager,
        FuelManager fuelManager,
        SectionManager sectionManager)
    {
        _stateManager = stateManager;
        _fuelManager = fuelManager;
        _sectionManager = sectionManager;
    }

    #endregion

    #region EntryAndTick
    /// <summary> GameState.Prep突入時にGameLoopManagerから呼ばれる </summary>
    public void Enter()
    {
        _fuelReady = false;
        _fuelStage = FuelStage.Charge;
        _selectedStage = null;
        _loadout.Clear();
        Debug.Log("[PrepPhaseManager] Enter: StageSelectから開始");
    }
    /// <summary> GameLoopManagerのFixedStepから、GameState.Prep中のみ呼ばれる </summary>
    public void Tick(float fixedDt)
    {
        if(_stateManager.CurrentPrepPhase == PrepPhase.FuelSet)
        {
            TickFuelSet(fixedDt);
        }
    }
    private void TickFuelSet(float fixedDt) 
    {

        switch(_fuelStage)
        {
            case FuelStage.Charge:
                if(_fuelManager.InitializingFuel())
                {
                    _payRemaining = _loadout.TotalFuelCost;
                    _fuelStage = FuelStage.Pay;
                }
                break;
            case FuelStage.Pay:
                _payRemaining = _fuelManager.PayingFuel(_payRemaining);
                if(_payRemaining <= 0)
                {
                    _fuelStage = FuelStage.Ready;
                    _fuelReady = true;
                    OnFuelSetReadyChanged?.Invoke(true);
                }
                break;
        }
    }

    #endregion

    #region StageSelect
    public void SelectStage(StageData stage)
    {
        if (_stateManager.Current != GameState.Prep) return;
        if (_stateManager.CurrentPrepPhase != PrepPhase.StageSelect) return;
        if (stage == null) return;

        _selectedStage = stage;
        _stateManager.TransitionPrepPhase(PrepPhase.UnitSelect);
        OnLoadoutChanged?.Invoke(_loadout);
    }

    #endregion

    #region UnitSelect
    public void ChooseTrain(Item_Train train)
    {
        if (!InUnitSelect) return;
        _loadout.SetTrain(train);
        OnLoadoutChanged?.Invoke(_loadout);
    }
    public void ToggleUnit(Item_Unit unit)
    {
        if (!InUnitSelect) return;

        if (_loadout.Contains(unit)) _loadout.RemoveUnit(unit);
        else if (CanAfford(unit)) _loadout.TryAddUnit(unit);

        OnLoadoutChanged?.Invoke(_loadout);
    }
    public void ClearUnitSlot(int slotIndex)
    {
        if (!InUnitSelect) return;
        _loadout.RemoveAt(slotIndex);
        OnLoadoutChanged?.Invoke(_loadout);
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
        if (_loadout.TotalFuelCost > FuelBudget) return;

        _sectionManager.Build(_selectedStage);
        OnLoadoutConfirmed?.Invoke(_loadout);
        _stateManager.TransitionPrepPhase(PrepPhase.FuelSet);
    }

    #endregion

    #region FuelSet
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

    #endregion


}