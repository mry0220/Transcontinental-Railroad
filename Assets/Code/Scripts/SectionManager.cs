using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Stageが持つSection列を進行させる。PrepのFuelSet中に構築され
/// Operation内のMoveで次のSectionを解凍する
/// </summary>
public class SectionManager
{
    private readonly List<SectionPlan> _plans = new();
    private int _currentSectionIndex;
    private float _totalDistance;

    public SectionPlan CurrentPlan =>
        _currentSectionIndex < _plans.Count ? _plans[_currentSectionIndex] : null;

    public bool HasNextSection => _currentSectionIndex +1 < _plans.Count;
    public int CurrentIndex => _currentSectionIndex;
    public int SectionCount => _plans.Count;
    public float TotalDistance => _totalDistance;

    
    ///<summary>PrepのFuelSet中に呼ばれる。StageDataを受け取りSection進行を初期化</summary>
    public void Build(StageData stageData,RNG rng)
    {
        Debug.Log($"[SectionManager]Build開始 stage= {(stageData != null ? stageData.name : "null Stage")} entries ={(stageData != null ? stageData.entries.Count : -1)}");
        _plans.Clear();
        _currentSectionIndex = 0;
        _totalDistance = 0f;
        if (stageData == null) return;

        float pos = 0f;
        foreach(var entry in stageData.entries)
        {
            var section = entry.section;
            float d = rng.Range(entry.distanceMin, entry.distanceMax);

            _plans.Add(new SectionPlan
            {
                type = section != null ? section.type : SectionType.Battle,
                distance = d,
                startPos = pos,
                endPos = pos + d,
                wave = section != null ? section.wave : null,
                eventData = section != null ? section.eventData : null,
            });
            pos += d;
        }
        _totalDistance = pos;

        Debug.Log($"[SectionManager]Build完了 plans = {_plans.Count} total = {_totalDistance:F1}");
    }

    ///<summary>現在のSectionを処理し終えたら、次のSectionへ進める</summary>
    public bool AdvanceToNextSection()
    {
        if (!HasNextSection) return false;
        _currentSectionIndex++;
        return true;
    }
    /// <summary>
    /// 区間バーの印。位置は0～１でGoalが１
    /// </summary>
    public List<(SectionType type,float fraction)> GetRouteMarkers()
    {
        var list = new List<(SectionType, float)>();
        for(int i = 0;i<_plans.Count;i++)
        {
            float f = _totalDistance > 0f ? _plans[i].endPos / _totalDistance : (i + 1f) / _plans.Count;
            list.Add((_plans[i].type, f));
        }
        return list;
    }

    public float GetTrainFraction(float legProgress)
    {
        var plan = CurrentPlan;
        if (plan == null || _totalDistance <= 0f) return 0f;
        return (plan.startPos + plan.distance * Mathf.Clamp01(legProgress)) / _totalDistance;
    }
  
}
