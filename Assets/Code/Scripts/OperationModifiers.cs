using UnityEngine;

/// <summary>
/// Operationの生存期間中保持される、Ally/Enemyの恒久ステータス倍率
/// EventSectionの"敵Status増加"等から呼ばれ、既存の倍率に乗算累積する
/// </summary>
public class OperationModifiers
{
    private float _allyMultiplier = 1f;
    private float _enemyMultiplier = 1f;

    public float GetMultiplier(Base_Item.Affiliation affiliation)
    {
        return affiliation == Base_Item.Affiliation.Ally ? _allyMultiplier : _enemyMultiplier;
    }
    
    public void ApplyMultiplier(Base_Item.Affiliation affiliation,float multiplier)
    {
        if (affiliation == Base_Item.Affiliation.Ally)
            _allyMultiplier *= multiplier;
        else
            _enemyMultiplier *= multiplier;
    }

    public void Reset()
    {
        _allyMultiplier = 1f;
        _enemyMultiplier = 1f;
    }
}
