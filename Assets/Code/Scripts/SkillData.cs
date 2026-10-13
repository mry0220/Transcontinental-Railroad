using UnityEngine;

public enum SkillEffectType
{
    Buff,
    Heal,
    SingleAttack,
}

public enum SkillHealTarget
{
    Self,
    AllAllies,
    TrainFuel,
    SelectedAlly,
}

public enum SkillBuffTarget
{
    Self,
    SelectedAlly,
}

[CreateAssetMenu(fileName = "SkillData",menuName = "Scriptable Objects/Skill/SkillData")]
public class SkillData : ScriptableObject
{
    [SerializeField] private string _id; public string id => _id;
    [SerializeField] private string _displayName; public string displayName => _displayName;
    [SerializeField] private Sprite _icon; public Sprite icon => _icon;
    [SerializeField] private float _cooldown = 10f; public float cooldown => _cooldown;
    [SerializeField] private SkillEffectType _effectType; public SkillEffectType effectType => _effectType;

    [Header("Buff")]
    [SerializeField] private float _attackMultiplier = 1.5f; public float attackMultiplier => _attackMultiplier;
    [SerializeField] private float _speedMultiplier = 1f; public float speedMultiplier => _speedMultiplier;
    [SerializeField] private SkillBuffTarget _buffTarget; public SkillBuffTarget buffTarget => _buffTarget;
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


    public bool requiresAllyTarget =>
        (_effectType == SkillEffectType.Buff && _buffTarget == SkillBuffTarget.SelectedAlly) ||
        (_effectType == SkillEffectType.Heal && _healTarget == SkillHealTarget.SelectedAlly);
}
