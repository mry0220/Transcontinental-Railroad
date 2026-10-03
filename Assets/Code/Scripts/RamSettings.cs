using UnityEngine;

[System.Serializable]
public class RamSettings 
{
    [Tooltip("赤い点滅のアラートの長さ（秒）")]
    public float alertDuration = 1.5f;

    public float acceleration = 20f;
    public float maxSpeed = 30;
    [Tooltip("列車の中心から戦闘までの距離（ワールド）")]
    public float frontOffset = 1f;

    [Tooltip("画面の右端から、さらに進む距離(ワールド)")]
    public float exitMargin = 3f;

    [Tooltip("味方を轢いたときに、1体あたり補充する燃料")]
    public int allyCrushFuelRefund = 0;

    [Tooltip("轢かれたUnitの復活コストにかける倍率")]
    public float crushedReviveCostMultiplier = 2f;
}
