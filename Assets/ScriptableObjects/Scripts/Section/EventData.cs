using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

public enum EventEffectType
{
    Fuel,
    KillUnit,
    ReviveUnit,
    StatModifier,
    ChangeBattle,
}

[System.Serializable]
public class EventEffect
{
    public EventEffectType type;

    [Header("Fuel")]
    public int fuelAmount;

    [Header("KillUnit / ReviveUnit")]
    public int count = 1;

    [Header("StatModifier")]
    public Base_Item.Affiliation target = Base_Item.Affiliation.Enemy;
    public float multiplier = 1f;

    [Header("ChangeBattle")]
    public Data_Wave extraWave;
}

[System.Serializable]
public class EventChoice
{
    public string label;
    [TextArea] public string description;
    public List<EventEffect> effects = new();
}

[CreateAssetMenu(fileName = "EventData", menuName = "Scriptable Objects/Section/EventData")]
public class EventData : ScriptableObject
{
    [SerializeField] private string _title;
    [SerializeField, TextArea] private string _body;
    [SerializeField] private Sprite _image;
    [SerializeField] private List<EventChoice> _choices = new();

    public string title => _title;
    public string body => _body;
    public Sprite image => _image;
    public IReadOnlyList<EventChoice> choices => _choices;
}
