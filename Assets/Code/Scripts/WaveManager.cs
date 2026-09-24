using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [SerializeField] private List<Data_Wave> _waves;
    public List<Data_Wave> Waves => _waves;

    private MatchManager _matchManager;

    private int _currentWaveIndex;
    public int CurrentWaveIndex => _currentWaveIndex;

    private readonly List<GameObject> _spawnedEnemies = new();
    private IReadOnlyList<GameObject> SpawnedEnemies => _spawnedEnemies;

    public void StartBattlePhase()
    {
        _currentWaveIndex = 0;
        SpawnWaveImmediate(CurrentWaveIndex);
    }

    private void SpawnWaveImmediate(int waveIndex)
    {
        if(_waves == null || waveIndex < 0||waveIndex >= _waves.Count)
        {
            Debug.LogWarning($"WaveManager: 無効なwaveIndexが指定された({waveIndex})");
            return;
        }

        Data_Wave wave = _waves[waveIndex];
        foreach(EnemySpawnData spawnData in wave.spawns)
        {
            SpawnEnemy(spawnData);
        }

    }

    public void SetMatchManager(MatchManager matchManager)
    {
        _matchManager = matchManager;
    }

    private void SpawnEnemy(EnemySpawnData spawnData)
    {
        if(spawnData.enemyData  == null || spawnData.enemyData.prefab == null)
        {
            Debug.LogWarning("WaveManager: EnemySpawnDataのenemyDataまたはprefabが未設定");
            return;
        }

        GameObject enemyObject = Instantiate(
            spawnData.enemyData.prefab,
            spawnData.spawnPosition,
            Quaternion.identity
            );

        var combatant = enemyObject.GetComponent<Combatant>();
        combatant?.Initialize(spawnData.enemyData, _matchManager);

        _spawnedEnemies.Add(enemyObject);
    }
}
