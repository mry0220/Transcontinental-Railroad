using UnityEngine;


public class Base_Item : ScriptableObject
{
    public enum Affiliation
    {
            Ally,
            Enemy,
    }

    public enum TargetingStrategy
    {
        Nearest,    //一番近い敵
        Farthest,   //一番遠い敵
        LowestHp,   //一番HPが低い敵
    }

    public enum AttackPattern
    {
        Single,
    }

    //====Common Status====
    [Header("共通ステータス")]

    [SerializeField] private Affiliation _affiliation;
    public Affiliation affiliation => _affiliation;

    [SerializeField] private int _maxHP;
    public int maxHP => _maxHP;

    [SerializeField] private int _attackPower;
    public int attackPower => _attackPower;

    [SerializeField] private float _attackInterval;
    public float attackInterval => _attackInterval;

    [SerializeField] private AttackPattern _attackPattern;
    public AttackPattern attackPattern => _attackPattern;

    [SerializeField] private int _matchCapacity;
    public int matchCapacity => _matchCapacity;

    [SerializeField] private float _moveSpeed;
    public float moveSpeed => _moveSpeed;

    [SerializeField] private TargetingStrategy _targetingStrategy;
    public TargetingStrategy targetingStrategy => _targetingStrategy;

    [SerializeField] private float _attackRange;
    public float attackRange => _attackRange;
}
