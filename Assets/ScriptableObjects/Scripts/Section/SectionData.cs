using UnityEngine;

public enum SectionType
{
    Battle,
    Event,
    Goal,
}



[CreateAssetMenu(fileName = "SectionData", menuName = "Scriptable Objects/Section/SectionData")]
public class SectionData : ScriptableObject
{
    [SerializeField] private SectionType _type;
    public SectionType type => _type;

    [Header("Battle用")]
    [SerializeField] private Data_Wave _wave;
    public Data_Wave wave => _wave;

    [Header("Event用")]
    [SerializeField] private int _fuelRestoreAmount;
    public int fuelRestoreAmount => _fuelRestoreAmount;

    [SerializeField] private float _enemyStatModifier = 1f;
    public float enemyStatModifier => _enemyStatModifier;

    
}
