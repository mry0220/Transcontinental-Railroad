using System;
using Unity.VisualScripting;
using UnityEngine;

public enum GameState
{
    Title,
    Prep,
    Battle,
    Result,
    GameOver,
}

public class StateManager : MonoBehaviour
{
    // ====Inspector Config ====
#if UNITY_EDITOR
    [SerializeField] private GameState InitialState = GameState.Title;
    #else
    private GameState InitialState = GameState.Title;
#endif

    //====Runtime State====
    private GameState currentState;
    public GameState Current => currentState;


    //====Events====
    public event Action<GameState,GameState> OnStateChanged; //(before,after)

    private void Awake()
    {
        currentState = InitialState;
    }

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
            case GameState.Battle:
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
            case GameState.Battle:
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
                return to == GameState.Battle;
            case GameState.Battle:
                return to == GameState.Result || to == GameState.GameOver;
            case GameState.Result:
                return to == GameState.Title;
            case GameState.GameOver:
                return to == GameState.Title;
            default:
                return false;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void OnGUI()
    {
        
    GUILayout.BeginArea(new Rect(10, 400, 400, 150));
    GUILayout.Label($"=== State Debug===");
    GUILayout.Label($"Current State: {currentState}");
    GUILayout.Label($"Total Frame: {Time.frameCount}");
    GUILayout.Label("");
    GUILayout.EndArea();
    }

#endif
}
