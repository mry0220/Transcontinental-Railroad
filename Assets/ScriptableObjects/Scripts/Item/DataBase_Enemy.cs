using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DataBase_Enemy", menuName = "Scriptable Objects/Item/DataBase_Enemy")]
public class DataBase_Enemy : ScriptableObject
{
    public List<Item_Enemy> enemies;

    public Item_Enemy GetEnemy(string id)
    {
        return enemies.Find(e => e.id == id);
    }

    public bool HasEnemy(string id)
    {
        return enemies.Exists(e => e.id == id);
    }

}
