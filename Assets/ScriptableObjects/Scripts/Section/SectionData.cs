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
    [SerializeField] private EventData _eventData;
    public EventData eventData => _eventData;

    [SerializeField] private float _enemyStatModifier = 1f;
    public float enemyStatModifier => _enemyStatModifier;

    
}
