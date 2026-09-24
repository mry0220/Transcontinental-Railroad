using UnityEngine;
using System;
/// <summary>
/// Matchが捜査対象とする戦闘エンティティの最小Intaface
/// 実態はUnit/Enemy側のMonoBehaviourが実装する想定
/// </summary>
public interface ICombatant
{
    Base_Item.Affiliation Affiliation { get; }
    int CurrentHP { get; }
    bool IsDead { get; }
    int MatchCapacity { get; }
    int CurrentMatchCount { get; }
    float MoveSpeed { get; }

    void ApplyDamage(int amount);

    ///<summary>MatchManagerでMatchが成立した際にPushされる</summary>
    void NotifyMatchStarted(Match match);

    ///<summary>Matchが終了（死亡/退却）した際にPushされる</summary>
    void NotifyMatchEnded(Match match);
}

/// <summary>
/// UnitA - EnemyAのような一対一想定の戦闘ペア単位
/// 相手への影響適用と両者の生死を監視する
/// CT、Commandはエンティティ管理
/// 自身のList&lt;Match&gt;を見てCTを回し、タイミングが来たらApplyDamageを呼ぶ想定
/// </summary>
public class Match
{
    public ICombatant Left { get; }
    public ICombatant Right { get; }

    public bool IsFinished { get; private set; }

    ///<summary>
    ///Match終了時に発火(死亡/退却)
    ///</summary>
    public event Action<Match> OnFinished;

    public Match(ICombatant left,ICombatant right)
    {
        Left = left;
        Right = right;
    }
    
    ///<summary>
    ///このMatchに参加している側から見た相手を返す
    ///エンティティ側がCTを回す際、攻撃対象の特定に使用
    /// </summary>
    public ICombatant GetOpponent(ICombatant self)
    {
        if (self == Left) return Right;
        if (self == Right) return Left;
        return null;
    }

    ///<summary>
    ///attackerからtargetへのダメージ適用、target/attackerがこのMatchの
    ///参加者でない場合は何もしない。適用後、targetが死亡していればMatchを終了させる
    /// </summary>
    public void ApplyDamage(ICombatant attacker,ICombatant target,int amount)
    {
        if (IsFinished) return;
        if (!IsParticipant(attacker) || !IsParticipant(target)) return;

        target.ApplyDamage(amount);

        if(target.IsDead)
        {
            Finish();
        }
    }

    ///<summary>
    ///退却などダメージ処理を経由せずにMatchを終了させたい場合に呼ぶ
    /// </summary>
    public void Withdraw()
    {
        if (IsFinished) return;
        Finish();
    }

    private bool IsParticipant(ICombatant combatant)
    {
        return combatant == Left || combatant == Right;
    }

    private void Finish()
    {
        IsFinished = true;
        OnFinished?.Invoke(this);
    }
}
