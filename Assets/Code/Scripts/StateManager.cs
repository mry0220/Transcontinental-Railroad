using System;
using UnityEngine;

public enum GameState
{
    Title,
    Prep,
    Operation,
    Result,
}

public enum RunOutcome
{
    None,
    Cleared,
    Failed,
}

public enum PrepPhase
{
    StageSelect,
    UnitSelect,
    FuelSet,
}

public enum OperationPhase
{
    Move,
    Battle,
    Ram,
    Result,
}

public class StateManager : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] private GameState InitialState = GameState.Title;
#else
    private GameState InitialState = GameState.Title;
#endif

    private GameState currentState;
    public GameState Current => currentState;

    private PrepPhase currentPrepPhase;
    public PrepPhase CurrentPrepPhase => currentPrepPhase;

    private OperationPhase currentOperationPhase;
    public OperationPhase CurrentOperationPhase => currentOperationPhase;

    private RunOutcome currentOutcome = RunOutcome.None;
    public RunOutcome CurrentOutcome => currentOutcome;

    public event Action<GameState, GameState> OnStateChanged;
    public event Action<PrepPhase, PrepPhase> OnPrepPhaseChanged;
    public event Action<OperationPhase, OperationPhase> OnOperationPhaseChanged;

    private void Awake()
    {
        currentState = InitialState;
    }

    public void transitionTo(GameState next)
    {
        if (!isValidTransition(currentState, next))
        {
            throw new InvalidOperationException($"遷移禁止: {currentState} -> {next}");
        }

        if(next == GameState.Result && currentOutcome == RunOutcome.None)
        {
            throw new InvalidOperationException("ResultへはTransitionToResult経由で遷移してください");
        }

        var prev = currentState;
        currentState = next;

        // MasterStateが切り替わった瞬間、対応するServantStateを初期値にリセットする
        if (next == GameState.Prep)
        {
            currentPrepPhase = PrepPhase.StageSelect;
            currentOutcome = RunOutcome.None;
        }
        else if (next == GameState.Operation)
        {
            currentOperationPhase = OperationPhase.Move;
        }

        OnStateChanged?.Invoke(prev, next);
    }

    public void TransitionPrepPhase(PrepPhase next)
    {
        if (currentState != GameState.Prep)
        {
            throw new InvalidOperationException($"PrepPhase遷移はGameState.Prep中のみ有効(現在: {currentState})");
        }
        if (!isValidPrepPhaseTransition(currentPrepPhase, next))
        {
            throw new InvalidOperationException($"PrepPhase遷移禁止: {currentPrepPhase} -> {next}");
        }

        var prev = currentPrepPhase;
        currentPrepPhase = next;
        OnPrepPhaseChanged?.Invoke(prev, next);
    }

    public void TransitionOperationPhase(OperationPhase next)
    {
        if (currentState != GameState.Operation)
        {
            throw new InvalidOperationException($"OperationPhase遷移はGameState.Operation中のみ有効(現在: {currentState})");
        }
        if (!isValidOperationPhaseTransition(currentOperationPhase, next))
        {
            throw new InvalidOperationException($"OperationPhase遷移禁止: {currentOperationPhase} -> {next}");
        }

        var prev = currentOperationPhase;
        currentOperationPhase = next;
        OnOperationPhaseChanged?.Invoke(prev, next);
    }

    private bool isValidTransition(GameState from, GameState to)
    {
        if (from == to) return false;

        switch (from)
        {
            case GameState.Title:
                return to == GameState.Prep;
            case GameState.Prep:
                return to == GameState.Operation;
            case GameState.Operation:
                return to == GameState.Result;
            case GameState.Result:
                return to == GameState.Prep;
            
            default:
                return false;
        }
    }

    private bool isValidPrepPhaseTransition(PrepPhase from, PrepPhase to)
    {
        if (from == to) return false;

        switch (from)
        {
            case PrepPhase.StageSelect:
                return to == PrepPhase.UnitSelect;
            case PrepPhase.UnitSelect:
                return to == PrepPhase.FuelSet || to == PrepPhase.StageSelect;
            case PrepPhase.FuelSet:
                return false; // ここから先はMasterState側のtransitionTo(Operation)で抜ける
            default:
                return false;
        }
    }

    private bool isValidOperationPhaseTransition(OperationPhase from, OperationPhase to)
    {
        if (from == to) return false;

        switch (from)
        {
            case OperationPhase.Move:
                return to == OperationPhase.Battle;
            case OperationPhase.Battle:
                return to == OperationPhase.Result || to == OperationPhase.Ram;
            case OperationPhase.Ram:
                return to == OperationPhase.Result;
            case OperationPhase.Result:
                return to == OperationPhase.Move;
            default:
                return false;
        }
    }

    public void TransitionToResult(RunOutcome outcome)
    {
        if(currentState != GameState.Operation)
        {
            throw new InvalidOperationException($"TransitionToResultはGameState.Operation中のみ有効:現在{currentState}");
        }
        if(outcome == RunOutcome.None)
        {
            throw new InvalidOperationException("outcomeにNoneは指定できません");
        }

        currentOutcome = outcome;
        transitionTo(GameState.Result);
    }
}