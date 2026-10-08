using System.Collections.Generic;

/// <summary>運行全体の集計。Logicが書き込み、Result表示が読む</summary>
public class RunStats
{
    public sealed class UnitRecord
    {
        public string itemId;
        public int dealt;
        public int taken;
        public int revives;
        public int deaths;
    }

    private readonly List<UnitRecord> _units = new();
    private readonly Dictionary<string, UnitRecord> _lookup = new();

    public IReadOnlyList<UnitRecord> Units => _units;
    public int CrushedUnits { get; private set; }
    public int CrushedEnemies { get; private set; }
    public float TotalDistance { get; private set; }
    public void Reset()
    {
        _units.Clear();
        _lookup.Clear();
        CrushedUnits = 0;
        CrushedEnemies = 0;
        TotalDistance = 0f;
    }

    public void RegisterUnit(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || _lookup.ContainsKey(itemId)) return;
        var record = new UnitRecord { itemId = itemId };
        _units.Add(record);
        _lookup[itemId] = record;
    }

    public void AddDealt(string itemId,int amount)
    {
        if (_lookup.TryGetValue(itemId, out var r)) r.dealt += amount;
    }
    public void AddToken(string itemId,int amount)
    {
        if (_lookup.TryGetValue((itemId), out var r)) r.taken += amount;
    }
    public void AddDeath(string itemId)
    {
        if (_lookup.TryGetValue(itemId, out var r)) r.dealt++;
    }
    public void AddRevive(string itemId)
    {
        if (_lookup.TryGetValue(itemId, out var r)) r.revives++;
    }
    public void AddCrushedUnit() => CrushedUnits++;
    public void AddCrushedEnemy() => CrushedEnemies++;
    public void AddDistance(float distance) => TotalDistance += distance;

}
