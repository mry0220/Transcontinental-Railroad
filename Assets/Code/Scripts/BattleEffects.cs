using System.Collections.Generic;
using UnityEngine;

/// <summary>戦闘の見た目の演出をまとめて進める。GameLoopManagerが持ち、Tickを呼ぶ </summary>
public class BattleEffects : ITickable
{
    private readonly BattleVisualSettings _settings;
    private readonly List<DamagePopup> _popups = new();

    public BattleEffects(BattleVisualSettings settings)
    {
        _settings = settings;
    }

    public void SpawnDamage(Vector3 worldPosition,int amount,Color color)
    {
        if (!_settings.showDamagePopups) return;
        _popups.Add(new DamagePopup(worldPosition, amount, color, _settings));
    }

    public void Tick(float dt)
    {
        for(int i = _popups.Count -1; i >= 0;i--)
        {
            if (_popups[i].Tick(dt)) _popups.RemoveAt(i);
        }
    }

    public void Clear()
    {
        foreach (var popup in _popups) popup.Dispose();
        _popups.Clear();
    }
}
