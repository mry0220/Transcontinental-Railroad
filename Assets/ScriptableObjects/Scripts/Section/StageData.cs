using System.Collections.Generic;
using UnityEngine;

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

    [SerializeField] private List<StageEntry> _entries;
    public List<StageEntry> entries => _entries;
}
