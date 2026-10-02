using System.Collections.Generic;

/// <summary>
/// UnitSelectで確定する編成。出撃Trainと、Unit6枠
/// </summary>
public class Loadout
{
    public const int SlotCount = 6;

    private readonly Item_Unit[] _slots = new Item_Unit[SlotCount];

    public Item_Train Train { get; private set; }
    public IReadOnlyList<Item_Unit> slots => _slots;

    public int UnitCount
    {
        get
        {
            int count = 0;
            foreach(var unit in _slots)
            {
                if (unit != null) count++;
            }
            return count;
        }
    }

    public bool IsValid => Train != null && UnitCount >= 1;

    public void SetTrain(Item_Train train)
    {
        Train = train;
    }

    public bool Contains(Item_Unit unit)
    {
        return unit != null && System.Array.IndexOf<Item_Unit>(_slots, unit) >= 0;

    }

    public bool TryAddUnit(Item_Unit unit)
    {
        if (unit == null || Contains(unit)) return false;

        int index = System.Array.IndexOf<Item_Unit>(_slots, null);
        if (index < 0) return false;

        _slots[index] = unit;
        return true;
    }

    public void RemoveUnit(Item_Unit unit)
    {
        int index = System.Array.IndexOf<Item_Unit>(_slots, unit);
        if (index >= 0) _slots[index] = null;
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= SlotCount) return;
        _slots[index] = null;
    }

    ///<summary>
    ///枠の順に、選択済みのUnitを詰めて返す。
    ///</summary>
    public List<Item_Unit> GetSelectedUnits()
    {
        var list = new List<Item_Unit>();
        foreach(var unit in _slots)
        {
            if (unit != null) list.Add(unit);
        }
        return list;
    }

    public void Clear()
    {
        Train = null;
        System.Array.Clear(_slots, 0, SlotCount);   
    }
}
