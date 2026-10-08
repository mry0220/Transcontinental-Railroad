using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StageEntry
{
    [SerializeField] private SectionData _section;
    public SectionData section => _section;

    [Header("Move所要時間(Range 実際の値はRNGで確定)")]
    [SerializeField] private float _distanceMin;
    public float distanceMin => _distanceMin;

    [SerializeField] private float _distanceMax;
    public float distanceMax => _distanceMax;
}

[CreateAssetMenu(fileName = "StageData", menuName = "Scriptable Objects/Section/StageData")]
public class StageData : ScriptableObject
{
    [SerializeField] private string _stageName;
    public string stageName => _stageName;

    public int SectionCount => _entries != null ? _entries.Count : 0;


    [SerializeField] private List<StageEntry> _entries;
    public List<StageEntry> entries => _entries;
    /// <summary>各Sectionの距離Range中央値の合計。大まかに丸め返す</summary>
    public int RoughTotalDistance
    {
        get
        {
            if (_entries == null) return 0;
            float sum = 0f;
            foreach(var e in _entries)
            {
                if (e == null) continue;
                sum += (e.distanceMin + e.distanceMax) * 0.5f;
            }
            int step = sum >= 100f ? 10 : 1;
            return Mathf.RoundToInt(sum / step) * step;
        }
    }
}
