using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DataBase_Train", menuName = "Scriptable Objects/Item/DataBase_Train")]
public class DataBase_Train : ScriptableObject
{
    public List<Item_Train> trains;

    public Item_Train GetTrain(string id)
    {
        return trains.Find(t => t.id == id);
    }
}
