using System;
using UnityEngine;

public class FuelManager : MonoBehaviour
{
    //====Inspector Config====
    [SerializeField] private float moveFuelCoefficient = 1f;
    [SerializeField] private int minStartFuel = 1;
    [SerializeField] private int payingAmount = 1;
    [SerializeField] private int enemyDefeatRefund = 10;
    public int EnemyDefeatRefund => enemyDefeatRefund;
   
    public int MinStartFuel => minStartFuel;

    private float _consumeRemainder;

    //====Public State====
    public int CurrentFuel { get; private set; }
    public int MaxFuel { get; private set; }


    

    public void SetMaxFuel(int maxFuel)
    {
        MaxFuel = maxFuel;
    }

    public void SetFuel(int value)
    {
        CurrentFuel = Mathf.Clamp(value, 0, MaxFuel);
    }

    /// <summary>
    /// Operation中の燃料消費。恒久燃費は常時、移動燃費はisMoving時のみ
    /// 消費後に燃費が残っていればtrue,尽きればfalse
    /// </summary>

    public bool ConsumeOperationFuel(float attackPower, float speed, bool isMoving, float fixedDt)
    {
        if (CurrentFuel <= 0) return false;

        float perSecond = attackPower;
        if(isMoving)
        {
            perSecond += attackPower * speed * moveFuelCoefficient;
        }

        _consumeRemainder += perSecond * fixedDt;
        int whole = Mathf.FloorToInt(_consumeRemainder);
        if(whole > 0)
        {
            _consumeRemainder -= whole;
            CurrentFuel = Mathf.Max(0, CurrentFuel - whole);
        }
        return CurrentFuel > 0;
    }

    public void ConsumeAmount(int amount)
    {
        CurrentFuel = Mathf.Clamp(CurrentFuel - amount,0,MaxFuel);
    }

    public void ResetFuel()
    {
        CurrentFuel = 0;
        _consumeRemainder = 0f;
    }

    ///<summary>支払いを１ステップ進める。残りの支払額を返す</summary>
    public int PayingFuel(int remaining)
    {
        int pay = Mathf.Min(payingAmount, remaining);
        CurrentFuel = Mathf.Max(0, CurrentFuel - pay);
        return remaining - pay;
    }

    public void SetFuelRatio(float ratio)
    {
        CurrentFuel = Mathf.Clamp(Mathf.RoundToInt(MaxFuel * Mathf.Clamp01(ratio)),0,MaxFuel);
    }

   
}
