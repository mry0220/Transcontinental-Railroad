using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    private MatchManager _matchManager;
    private readonly List<GameObject> _spawnedEnemies = new();
    private OperationModifiers _modifiers;
    public void SetModifiers(OperationModifiers modifiers) => _modifiers = modifiers;

    public void SetMatchManager(MatchManager matchManger)
    {
        _matchManager = matchManger;
    }

    ///<summary>BattleSection突入時にOperationPhaseManagerから呼ばれる</summary>
    public void StartBattlePhase(Data_Wave wave)
    {
        if(wave == null)
        {
            Debug.LogWarning("WaveManager: Data_Waveがnull");
            return;
        }

        foreach(EnemySpawnData spawnData in wave.spawns)
        {
            SpawnEnemy(spawnData);
        }
    }

    private void SpawnEnemy(EnemySpawnData spawnData)
    {
        if(spawnData.enemyData == null || spawnData.enemyData.prefab == null)
        {
            Debug.LogWarning("WaveManager: EnemySpawnDataのEnemyDataまたはprefabが未設定");
            return;
        }

        GameObject enemyObject = Instantiate(
            spawnData.enemyData.prefab,
            spawnData.spawnPosition,
            Quaternion.identity
            );
        var combatant = enemyObject.GetComponent<Combatant>();
        combatant?.Initialize(spawnData.enemyData, _matchManager);
        combatant?.SetStatMultiplier(
            _modifiers != null ? _modifiers.GetMultiplier(Base_Item.Affiliation.Enemy) : 1f);

        _spawnedEnemies.Add(enemyObject);

        
    }

    public void ResetForNewRun()
    {
        _spawnedEnemies.Clear();
    }
}
