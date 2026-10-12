using System;
using System.Collections.Generic;

#region Card

public enum CardKind
{
    Deploy,  //出撃カード
    Return, //帰還カード　
    Rebuild,   //死亡中
    Skill,
}

public enum HandMode
{
    Battle,
    Rebuild,
}

public readonly struct Card
{
    public readonly string unitId;
    public readonly CardKind kind;
    public readonly int order;
    public readonly int skillIndex;

    public Card(string unitId, CardKind kind, int order,int skillIndex = -1)
    {
        this.unitId = unitId;
        this.kind = kind;
        this.order = order;
        this.skillIndex = skillIndex;
    }
}

#endregion

#region Hand

/// <summary>
/// 手札　カードの並びと上限だけ
/// </summary>
public class Hand
{
    public const int MaxSize = 12;

    private readonly List<Card> _cards = new();

    public IReadOnlyList<Card> Cards => _cards;

    /// <summary>中身が変わったときに発火</summary>
    public event Action OnChanged;

    ///<summary>
    ///同じunitIdのカードは種類だけ差し替え、なければ追加。上限に達していたらFalse
    /// </summary>

    public bool Set(string unitId,CardKind kind,int order)
    {
        int index = _cards.FindIndex(c => c.kind != CardKind.Skill && c.unitId == unitId);
        if (index < 0 && _cards.Count >= MaxSize) return false;

        var card = new Card(unitId, kind, order);
        if (index >= 0) _cards[index] = card;
        else _cards.Add(card);

        SortByOrder();
        OnChanged?.Invoke();
        return true;
    }

    public bool Remove(string unitId)
    {
        int removed = _cards.RemoveAll(c => c.kind != CardKind.Skill && c.unitId == unitId); ;
        if (removed == 0) return false;

        OnChanged?.Invoke();
        return true;
    }
    /// <summary>Skillカードを足す。すでにあれば何もしない。手札が満杯ならFalse</summary>
    public bool AddSkill(string unitId,int skillIndex,int order)
    {
        if (_cards.Exists(c => c.kind == CardKind.Skill && c.unitId == unitId && c.skillIndex == skillIndex))
            return true;
        if (_cards.Count >= MaxSize) return false;

        _cards.Add(new Card(unitId, CardKind.Skill, order, skillIndex));
        SortByOrder();
        OnChanged?.Invoke();
        return true;
    }

    public bool RemoveSkill(string unitId,int skillIndex)
    {
        int removed = _cards.RemoveAll(c =>
            c.kind == CardKind.Skill && c.unitId == unitId && c.skillIndex == skillIndex);
        if (removed == 0) return false;

        OnChanged?.Invoke();
        return true;
    }

    public bool RemoveSkillsOf(string unitId)
    {
        int removed = _cards.RemoveAll(c => c.kind == CardKind.Skill && c.unitId == unitId);
        if (removed == 0) return false;

        OnChanged?.Invoke();
        return true;
    }

    public bool RemoveAllSkills()
    {
        int removed = _cards.RemoveAll(c => c.kind == CardKind.Skill);
        if (removed == 0) return false;

        OnChanged?.Invoke();
        return true;
    }

    public void Replace(IEnumerable<Card> cards)
    {
        _cards.Clear();
        foreach(var card in cards)
        {
            if (_cards.Count >= MaxSize) break;
            _cards.Add(card);
        }

        SortByOrder();
        OnChanged?.Invoke();
    }

    private void SortByOrder()
    {
        _cards.Sort((a, b) => a.order.CompareTo(b.order));
    }
}

#endregion

#region HandUnitBinder
/// <summary>
/// UnitManagerの状態変化を、手札のカード種別に反映
/// </summary>
public class HandUnitBinder
{
    private readonly Hand _hand;
    private readonly UnitManager _units;
    private readonly List<string> _roster = new();
    private HandMode _mode = HandMode.Rebuild;

    public HandUnitBinder(Hand hand,UnitManager units)
    {
        _hand = hand;
        _units = units;

        _units.OnUnitDeployed += (id, _) => Refresh(id);
        _units.OnUnitReturned += (id, _) => Refresh(id);
        _units.OnUnitDied += Refresh;
        _units.OnUnitRevived += Refresh;
        _units.OnBattleSettled += RebuildAll;
        _units.OnDeployedUnitDied += id => _hand.Remove(id);
    }

    public void SetMode(HandMode mode)
    {
        if (_mode == mode) return;
        _mode = mode;
        RebuildAll();
    }

    /// <summary>
    /// 編成が決まったら呼ぶ。
    /// </summary>
    public void SetRoster(IEnumerable<string> unitIds)
    {
        _roster.Clear();
        _roster.AddRange(unitIds);
        RebuildAll();
    }

   

    public void RebuildAll()
    {
        var cards = new List<Card>();
        for(int i = 0;i<_roster.Count; i++)
        {
            if (TryMakeCard(_roster[i], i, out var card)) cards.Add(card);
        }
        _hand.Replace(cards);
    }


    private void Refresh(string unitId)
    {
        int order = _roster.IndexOf(unitId);
        if (order < 0) return;

        if (TryMakeCard(unitId, order, out var card)) _hand.Set(card.unitId, card.kind, card.order);
        else _hand.Remove(unitId);
    }

    private bool TryMakeCard(string unitId,int order,out Card card)
    {
        var status = _units.GetUnitStatus(unitId);

        if(_mode == HandMode.Rebuild)
        {
            if(status==UnitManager.UnitBattleStatus.Dead)
            {
                card = new Card(unitId, CardKind.Rebuild, order);
                return true;
            }

            card = default;
            return false;
        }

        switch(status)
        {
            case UnitManager.UnitBattleStatus.Standby:
                card = new Card(unitId, CardKind.Deploy, order);
                return true;
            case UnitManager.UnitBattleStatus.Deployed:
                var deployed = _units.GetDeployedCombatant(unitId);
                if(deployed != null && deployed.IsDead)
                {
                    card = default;
                    return false;
                }
                card = new Card(unitId, CardKind.Return, order);
                return true;
            default:
                card = default;
                return false;
            }
    }
}

#endregion