using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using TMPro;
using UnityEngine;

/// <summary>
/// Skillカードキー。手札要素のuserDataと、仕様要求でともに使用
/// </summary>

public static class SkillKey
{
    private const string Prefix = "skill:";

    public static string Make(string unitId, int skillIndex) => $"{Prefix}{unitId}:{skillIndex}";
   public static bool IsSkillKey(string key) =>
            !string.IsNullOrEmpty(key) && key.StartsWith(Prefix);

    public static bool TryParse(string key,out string unitId,out int skillIndex)
    {
        unitId = null;
        skillIndex = -1;
        if (!IsSkillKey(key)) return false;
        int split = key.LastIndexOf(':');
        if (split < Prefix.Length) return false;

        unitId = key.Substring(Prefix.Length, split - Prefix.Length);
        return int.TryParse(key.Substring(split + 1), out skillIndex);
    }
}

/// <summary>
/// Skillカードの山札、手札への補充、発動を扱う
/// 山札はいったんLogicだけ
/// </summary>
public class SkillManager
{
    public const int MaxSkillsPreUnit = 2;
    private const int SkillOrderBase = 100;

    private class Entry
    {
        public string unitId;
        public int index;
        public SkillData data;
        public float timer;
        public bool inHand;
    }

    private readonly Hand _hand;
    private readonly UnitManager _units;
    private readonly FuelManager _fuel;
    private readonly List<string> _roster = new();
    private readonly List<Entry> _entries = new();
    private readonly Queue<string> _useRequests = new();

    public SkillManager(Hand hand,UnitManager units,FuelManager fuel)
    {
        _hand = hand;
        _units = units;
        _fuel = fuel;

        _units.OnUnitDeployed += (id, _) => HandleDeployed(id);
        _units.OnUnitReturned += (id, _) => RemoveUnit(id);
        _units.OnUnitDied += RemoveUnit;
        _units.OnDeployedUnitDied += RemoveUnit;
        _units.OnBattleSettled += Clear;
    }
    /// <summary>編成が決まったら呼ぶ。Skillカードの並び順に使う</summary>
    public void SetRoster(IEnumerable<string> unitIds)
    {
        _roster.Clear();
        _roster.AddRange(unitIds);
    }
    public void Clear()
    {
        _entries.Clear();
        _useRequests.Clear();
        _hand.RemoveAllSkills();
    }

    /// <summary>UIのTapから呼ばれる。実際の発動は次Tickで行う</summary>
    public void RequestUse(string skillKey)
    {
        _useRequests.Enqueue(skillKey);
    }

    #region Tick

    public void Tick(float fixedDt)
    {
        ProcessUseRequests();

        for(int i = 0;i<_entries.Count;i++)
        {
            var e = _entries[i];
            if (e.inHand) continue;

            e.timer += fixedDt;
            if (e.timer < e.data.cooldown) continue;

            if (_hand.AddSkill(e.unitId, e.index, SkillOrder(e))) e.inHand = true;
        }
    }

    private int SkillOrder(Entry e)
    {
        int rosterIndex = Mathf.Max(0, _roster.IndexOf(e.unitId));
        return SkillOrderBase + rosterIndex * MaxSkillsPreUnit + e.index;
    }
    #endregion

    #region EntryManagement
    private void HandleDeployed(string unitId)
    {
        RemoveUnit(unitId);

        var data = _units.GetUnitData(unitId);
        if (data == null || data.skills == null) return;

        for(int i = 0;i<data.skills.Count && i<MaxSkillsPreUnit;i++)
        {
            if (data.skills[i] == null) continue;
            _entries.Add(new Entry { unitId = unitId, index = i, data = data.skills[i] });
        }
    }

    private void RemoveUnit(string unitId)
    {
        _entries.RemoveAll(e => e.unitId == unitId);
        _hand.RemoveSkillsOf(unitId);
    }

    #endregion

    #region Use

    private void ProcessUseRequests()
    {
        while(_useRequests.Count > 0)
        {
            var key = _useRequests.Dequeue();
            if (!SkillKey.TryParse(key, out var unitId, out var index)) continue;

            var entry = _entries.Find(e => e.unitId == unitId && e.index == index);
            if (entry == null || !entry.inHand) continue;

            var caster = _units.GetDeployedCombatant(unitId);
            if (caster == null || caster.IsDead)
            {
                RemoveUnit(unitId);
                continue;
            }

            if (!Execute(entry.data, caster)) continue;

            _hand.RemoveSkill(unitId, index);
            entry.inHand = false;
            entry.timer = 0f;
        }
    }

    private bool Execute(SkillData data,Combatant caster)
    {
        switch(data.effectType)
        {
            case SkillEffectType.SelfBuff:
                caster.AddTimedBuff(data.attackMultiplier, data.speedMultiplier, data.duration);
                return true;
            case SkillEffectType.Heal:
                ApplyHeal(data,caster);
                return true;
            case SkillEffectType.SingleAttack:
                return ApplySingleAttack(data, caster);
            default:
                return false;


        }
    }

    private void ApplyHeal(SkillData data,Combatant caster)
    {
        switch(data.healTarget)
        {
            case SkillHealTarget.Self:
                caster.Heal(Mathf.RoundToInt(caster.MaxHp * data.healRatio));
                break;
            case SkillHealTarget.AllAllies:
                foreach(var c in new List<Combatant>(Combatant.All))
                {
                    if (c.Affiliation != Base_Item.Affiliation.Ally) continue;
                    if (c.IsTrain || !c.IsActive || c.IsDead) continue;
                    c.Heal(Mathf.RoundToInt(c.MaxHp * data.healRatio));
                }
                break;
            case SkillHealTarget.TrainFuel:
                _fuel?.ConsumeAmount(-data.fuelAmount);
                break;
        }
    }

    private bool ApplySingleAttack(SkillData data,Combatant caster)
    {
        var target = caster.GetSkillTarget();
        if (target == null) return false;

        caster.DealSkillDamage(target, Mathf.RoundToInt(caster.AttackPower * data.attackRate));
        return true;
    }

    #endregion

}
