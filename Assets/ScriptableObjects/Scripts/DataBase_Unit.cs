using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "DataBase_Unit", menuName = "Scriptable Objects/DataBase_Unit")]
public class DataBase_Unit : ScriptableObject
{
    public List<Item_Unit> units;

    public Item_Unit GetUnit(string id)
    {
        return units.Find(u => u.id == id);
    }

    public bool HasUnit(string id)
    {
        return units.Exists(u => u.id == id);
    }

}
