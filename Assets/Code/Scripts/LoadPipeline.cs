using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 準備の一工程。Runは一回で終わる処理にする。重い処理は工程分けして登録
/// </summary>
public class LoadStep
{
    public readonly string Name;
    public readonly float Weight;
    public readonly System.Action Run;

    public LoadStep(string name,float weight,System.Action run)
    {
        Name = name;
        Weight = Mathf.Max(0.01f,weight);
        Run = run;
    }
}
/// <summary>
/// FuelSet中の準備工程を、1Tickの時間予算内で順に実行
/// 進捗は完了した工程の重み合計。最低表示時間の間は、表示上の進捗を頭打ちに
/// </summary>
public class LoadPipeline
{
    private readonly List<LoadStep> _steps = new();
    private readonly float _minDuration;
    private readonly double _budgetMs;
    private readonly System.Diagnostics.Stopwatch _sw = new();

    private int _index;
    private float _doneWeight;
    private float _totalWeight;
    private float _elapsed;

    public LoadPipeline(float minDuration = 0.8f,double budgetMs = 4.0)
    {
        _minDuration = minDuration;
        _budgetMs = budgetMs;
    }

    public float RealProgress => _totalWeight > 0f ? _doneWeight / _totalWeight : 1f;

    public float DisplayProgress =>
        Mathf.Min(RealProgress, _minDuration > 0f ? Mathf.Clamp01(_elapsed / _minDuration) : 1f);

    public bool IsComplete => _index >= _steps.Count && _elapsed >= _minDuration;

    public string CurrentStepName => _index < _steps.Count ? _steps[_index].Name : "";

    public void Clear()
    {
        _steps.Clear();
        _index = 0;
        _doneWeight = 0f;
        _totalWeight = 0f;
        _elapsed = 0f;
    }

    public void Add(LoadStep step)
    {
        _steps.Add(step);
        _totalWeight += step.Weight;
    }

    public void Tick(float dt)
    {
        _elapsed += dt;
        if (_index >= _steps.Count) return;

        _sw.Restart();
        do
        {
            var step = _steps[_index];
            try { step.Run(); }
            catch (System.Exception e)
            {
                Debug.LogError($"[LoadPipeline]工程{step.Name}で例外:{e}");
            }
            _doneWeight += step.Weight;
            _index++;
        }
        while (_index < _steps.Count && _sw.Elapsed.TotalMilliseconds < _budgetMs);
    }
}
