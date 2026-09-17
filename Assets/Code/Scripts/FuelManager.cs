using UnityEngine;

public class FuelManager : MonoBehaviour
{
    //====Inspector Config====
    [SerializeField] private int initialFuel = 60;
    [SerializeField] private int fillingAmount = 1;
    [SerializeField] private int consumeAmount = 1;

    //====Public State====
    public int CurrentFuel { get; private set; }
    public int MaxFuel => initialFuel;


    /// <summary>
    /// 燃料充填中処理
    /// </summary>fuel filling
    /// <returns></returns>
   public bool InitializingFuel()
   {
       if(CurrentFuel >= initialFuel)
       {
           CurrentFuel = initialFuel;
           return true;
       }
       CurrentFuel += fillingAmount;
       return false;
   }

   public bool ConsumingFuel()
   {
        if(CurrentFuel <= 0)
        {
            CurrentFuel = 0;
            return false;
        }
        CurrentFuel -= consumeAmount;
        return true;
   }

   
}
