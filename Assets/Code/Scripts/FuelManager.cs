using UnityEngine;

public class FuelManager : MonoBehaviour
{
    //====Inspector Config====
   // [SerializeField] private int initialFuel = 60;
    [SerializeField] private int fillingAmount = 1;
    [SerializeField] private float moveFuelCoefficient = 1f;

    private float _consumeRemainder;

    //====Public State====
    public int CurrentFuel { get; private set; }
    public int MaxFuel { get; private set; }


    /// <summary>
    /// 燃料充填中処理
    /// </summary>fuel filling
    /// <returns></returns>
    public bool InitializingFuel()
    {
        CurrentFuel = Mathf.Min(CurrentFuel + fillingAmount, MaxFuel);
        return CurrentFuel >= MaxFuel;
    }

    public void SetMaxFuel(int maxFuel)
    {
        MaxFuel = maxFuel;
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
        CurrentFuel = Mathf.Max(0,CurrentFuel - amount);
    }

    public void ResetFuel()
    {
        CurrentFuel = 0;
        _consumeRemainder = 0f;
    }

   
}
