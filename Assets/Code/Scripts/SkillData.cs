using UnityEditor.ShaderKeywordFilter;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;

public enum SkillEffectType
{
    SelfBuff,
    Heal,
    SingleAttack,
}

public enum SkillHealTarget
{
    Self,
    AllAllies,
    TrainFuel,
}
[CreateAssetMenu(fileName = "SkillData",menuName = "Scriptable Objects/Skill/SkillData")]
public class SkillData : ScriptableObject
{
    [SerializeField] private string _id; public string id => _id;
    [SerializeField] private string _displayName; public string displayName => _displayName;
    [SerializeField] private Sprite _icon; public Sprite icon => _icon;
    [SerializeField] private float _cooldown = 10f; public float cooldown => _cooldown;
    [SerializeField] private SkillEffectType _effectType; public SkillEffectType effectType => _effectType;

    [Header("SelfBuff")]
    [SerializeField] private float _attackMultiplier = 1.5f; public float attackMultiplier => _attackMultiplier;
    [SerializeField] private float _speedMultiplier = 1f; public float speedMultiplier => _speedMultiplier;
    [SerializeField] private float _duration = 5f; public float duration => _duration;

    [Header("Heal")]
    [SerializeField] private SkillHealTarget _healTarget;public SkillHealTarget healTarget => _healTarget;
    [Tooltip("Unitへの回復力。割合"), SerializeField]
    private float _healRatio = 0.3f; public float healRatio => _healRatio;
    [Tooltip("列車への回復,固定値"), SerializeField]
    private int _fuelAmount = 50; public int fuelAmount => _fuelAmount;

    [Header("SingleAttack")]
    [Tooltip("使用したUnitの攻撃力に対する倍率"), SerializeField]
    private float _attackRate = 2f;public float attackRate => _attackRate;

}
