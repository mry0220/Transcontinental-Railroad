using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DataBase_UI", menuName = "Scriptable Objects/UI/DataBase_UI")]
public class DataBase_UI : ScriptableObject
{
    public List<Data_UI> UIs;

    public Data_UI GetUI(string id)
    {
        return UIs.Find(u => u.id == id);
    }

    public bool HasUI(string id)
    {
        return UIs.Exists(u => u.id == id);
    }
}
