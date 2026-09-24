using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Data_Wave", menuName = "Scriptable Objects/Wave/Data_Wave")]
public class Data_Wave : ScriptableObject
{
    public List<EnemySpawnData> spawns;
}
