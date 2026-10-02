using System.Linq;
using UnityEngine;

/// <summary>
/// Stageが持つSection列を進行させる。PrepのFuelSet中に構築され
/// Operation内のMoveで次のSectionを解凍する
/// </summary>
public class SectionManager
{
    private StageData _StageData;
    private int _currentSectionIndex;

    public StageEntry CurrentSection =>
        (_StageData != null && _currentSectionIndex < _StageData.entries.Count)
        ? _StageData.entries[_currentSectionIndex] : null;

    public bool HasNextSection => _StageData != null && _currentSectionIndex + 1 < _StageData.entries.Count;

    public int CurrentIndex => _currentSectionIndex;
    public int SectionCount => _StageData != null ? _StageData.entries.Count : 0;

    public StageEntry CurrentEntry =>
        (_StageData != null && _currentSectionIndex < _StageData.entries.Count)
        ? _StageData.entries[_currentSectionIndex] : null;
    ///<summary>PrepのFuelSet中に呼ばれる。StageDataを受け取りSection進行を初期化</summary>
    public void Build(StageData stageData)
    {
        _StageData = stageData;
        _currentSectionIndex = 0; 
    }

    ///<summary>現在のSectionを処理し終えたら、次のSectionへ進める</summary>
    public bool AdvanceToNextSection()
    {
        if (!HasNextSection) return false;
        _currentSectionIndex++;
        return true;
    }

  
}
