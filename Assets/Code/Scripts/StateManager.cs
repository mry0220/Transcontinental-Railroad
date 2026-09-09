using System;
using Unity.VisualScripting;
using UnityEngine;

public enum GameState
{
    Title,
    Prep,
    Buttle,
    Result,
    GameOver,
}

public class StateManager : MonoBehaviour
{
    private GameState currentState;
    public GameState Current => currentState;

    public event Action<GameState,GameState> OnStateChanged; //(before,after)

    public void transitionTo(GameState next)
    {
        if(!isValidTransition(currentState, next))
        {
            throw new InvalidOperationException($"遷移禁止");
        }

        var prev = currentState;
        onExit(currentState);
        currentState = next;
        onEnter(next);
        OnStateChanged?.Invoke(prev, next);
    }

    private void onEnter(GameState state)
    {
        switch (state)
        {
            case GameState.Title:
                break;
            case GameState.Prep:
                break;
            case GameState.Buttle:
                break;
            case GameState.Result:
                break;
            case GameState.GameOver:
                break;
         
        }
    }

    private void onExit(GameState state)
    {
        switch (state)
        {
            case GameState.Title:
                break;
            case GameState.Prep:
                break;
            case GameState.Buttle:
                break;
            case GameState.Result:
                break;
            case GameState.GameOver:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    private bool isValidTransition(GameState from, GameState to)
    {
        if(from == to ) return false;

        switch (from)
        {
            case GameState.Title:
                return to == GameState.Prep;
            case GameState.Prep:
                return to == GameState.Buttle;
            case GameState.Buttle:
                return to == GameState.Result;
            case GameState.Result:
                return to == GameState.Title || to == GameState.GameOver;
            case GameState.GameOver:
                return to == GameState.Title;
            default:
                return false;
        }
    }
}
