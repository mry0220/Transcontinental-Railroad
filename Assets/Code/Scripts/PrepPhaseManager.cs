using UnityEngine;

public enum FuelSetStatus { None,Charging,Paying,Ready}

/// <summary>
/// Prep(MasterState)配下のServantState(StageSelect/UnitSelect/FuelSet)を進行させる。
/// </summary>
public class PrepPhaseManager : ITickable
{
    #region Type

    private enum FuelStage { Load,Pay,Ready}
    private FuelStage _fuelStage;
    private int _payRemaining;

    #endregion


    #region References

    private readonly StateManager _stateManager;
    private readonly FuelManager _fuelManager;
    private readonly SectionManager _sectionManager;
    private readonly RNG _rng;
    private readonly LoadPipeline _loader = new LoadPipeline();
    private readonly LoadPipeline _payloader = new LoadPipeline(0.4f);
    

    public LoadPipeline Loader => _loader;
    public LoadPipeline PayLoader => _payloader;
    public StageData SelectedStage => _selectedStage;

    private int _payStartFuel;

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
    public event System.Action<Loadout,int> OnLoadoutChanged;
    public event System.Action<FuelSetStatus> OnFuelSetReadyChanged;
    
    #endregion

    #region Setup
    public PrepPhaseManager(
        StateManager stateManager,
        FuelManager fuelManager,
        SectionManager sectionManager,
        RNG rng)
    {
        _stateManager = stateManager;
        _fuelManager = fuelManager;
        _sectionManager = sectionManager;
        _rng = rng;
    }

    #endregion

    #region EntryAndTick
    /// <summary> GameState.Prep突入時にGameLoopManagerから呼ばれる </summary>
    public void Enter()
    {
        _fuelReady = false;
        _fuelStage = FuelStage.Load;
        _loader.Clear();
        _selectedStage = null;
        _loadout.Clear();
        _payloader.Clear();
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
            case FuelStage.Load:
                _loader.Tick(fixedDt);
                _fuelManager.SetFuelRatio(_loader.DisplayProgress);
                if(_loader.IsComplete)
                {
                    _payStartFuel = _fuelManager.MaxFuel;
                    _fuelStage = FuelStage.Pay;
                    OnFuelSetReadyChanged?.Invoke(FuelSetStatus.Paying);
                }
                break;
            case FuelStage.Pay:
                _payloader.Tick(fixedDt);
                int paid = Mathf.RoundToInt(_loadout.TotalFuelCost * _payloader.DisplayProgress);
                _fuelManager.SetFuel(_payStartFuel - paid);
                if(_payloader.IsComplete)
                {
                    _fuelStage = FuelStage.Ready;
                    _fuelReady = true;
                    OnFuelSetReadyChanged?.Invoke(FuelSetStatus.Ready);
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
        NotifyLoadoutChanged();
    }

    #endregion

    #region UnitSelect
    public void ChooseTrain(Item_Train train)
    {
        if (!InUnitSelect) return;
        _loadout.SetTrain(train);
        NotifyLoadoutChanged();
    }
    public void ToggleUnit(Item_Unit unit)
    {
        if (!InUnitSelect) return;
        if (_loadout.Train == null) return;

        if (_loadout.Contains(unit)) _loadout.RemoveUnit(unit);
        else if (CanAfford(unit)) _loadout.TryAddUnit(unit);

        NotifyLoadoutChanged();
    }
    public void ClearUnitSlot(int slotIndex)
    {
        if (!InUnitSelect) return;
        _loadout.RemoveAt(slotIndex);
        NotifyLoadoutChanged();
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
        _loader.Clear();
        _payloader.Clear();
        var stage = _selectedStage;
        _payloader.Add(new LoadStep("路線構築", 1f, () => _sectionManager.Build(stage, _rng)));
        OnLoadoutConfirmed?.Invoke(_loadout);
        _stateManager.TransitionPrepPhase(PrepPhase.FuelSet);
        OnFuelSetReadyChanged?.Invoke(FuelSetStatus.Charging);
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

    private void NotifyLoadoutChanged()
    {
        OnLoadoutChanged?.Invoke(_loadout, FuelBudget);
    }

    #endregion


}