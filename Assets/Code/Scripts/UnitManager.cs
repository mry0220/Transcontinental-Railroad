using System;
using System.Collections.Generic;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    #region Type

    public struct BattleDamageEntry
    {
        public string itemId;
        public Sprite icon;
        public int damage;
    }

    public enum UnitBattleStatus
    {
        Standby,
        Deployed,
        Returned,
        Dead,
    }
    private class DeadRecoad
    {
        public bool isReviving;
        public float timer;
        public bool crushed;
    }

    #endregion

    #region InspectorConfig
    [SerializeField] private DataBase_Unit unitDB;
    [SerializeField] private Camera mainCamera;
    [Header("最大出撃数")]
    [SerializeField] private int maxDeployCount = 6;

    #endregion

    #region References
    private MatchManager _matchManager;
    private FuelManager _fuelManager;
    private OperationModifiers _modifiers;
    private readonly List<string> _roster = new();
    #endregion

    #region RuntimeState
    private readonly Dictionary<string, UnitBattleStatus> unitStatus = new();
    private readonly Dictionary<string, GameObject> deployedUnits = new();
    private readonly Dictionary<string, DeadRecoad> deadRecords = new();
    private readonly Dictionary<string, int> _battleDamage = new();
    private readonly List<string> _reviveCompleted = new();
    private GameObject currentPreview;
    private string currentPreviewItemId;
    private bool _isDragging;
    private float _crushedReviveCostMultiplier = 1f;
    private RunStats _runStats;

    #endregion

    #region Events
    /// <summary> unitが出撃時に発火 </summary>
    public event Action<string, GameObject> OnUnitDeployed;
    /// <summary> unitが帰還時に発火 </summary>
    public event Action<string, GameObject> OnUnitReturned;
    public event Action<string> OnReviveStarted;
    public event Action<string, float, float> OnReviveProgress; //ItemId,残り秒,進行度
    public event Action<string> OnUnitDied;
    public event Action<string> OnUnitRevived;
    public event System.Action<string> OnDeadUnitTapped;

    #endregion

    #region UnityLifecycle
    private void Awake()
    {
        if (unitDB == null)
            Debug.LogError("[BattleManager] DataBase_Unit not assigned");

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    #endregion

    #region Setup
    public void SetFuelManager(FuelManager fuelManager)
    {
        _fuelManager = fuelManager;
    }
    public void SetMatchManager(MatchManager matchManager)
    {
        _matchManager = matchManager;
    }

    public void SetCrushedReviveCostMultiplier(float multiplier)
    {
        _crushedReviveCostMultiplier = Mathf.Max(0f, multiplier);
    }
    public void ResetForNewRun()
    {
        HideUnitPlacementPreview();
        unitStatus.Clear();
        deployedUnits.Clear();
        deadRecords.Clear();
        _battleDamage.Clear();
        _roster.Clear();
    }

    public void BeginBattleStats()
    {
        _battleDamage.Clear();
    }

    public void SetRunStats(RunStats runStats)
    {
        _runStats = runStats;
    }



    public void SetModifiers(OperationModifiers modifiers) => _modifiers = modifiers;

    public void SetRoster(IEnumerable<string> itemIds)
    {
        _roster.Clear();
        _roster.AddRange(itemIds);
    }

    #endregion

    #region Queries
    public UnitBattleStatus GetUnitStatus(string itemId)
    {
        return unitStatus.TryGetValue(itemId, out var status) ? status : UnitBattleStatus.Standby;

    }
    public bool CanDeploy(string itemId)
    {
        return GetUnitStatus(itemId) == UnitBattleStatus.Standby && deployedUnits.Count < maxDeployCount;
    }
    public Combatant GetDeployedCombatant(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (!deployedUnits.TryGetValue(itemId, out var unit) || unit == null) return null;
            return unit.GetComponent<Combatant>();
    }
    public Sprite GetUnitIcon(string itemId)
    {
        var data = unitDB != null ? unitDB.GetUnit(itemId) : null;
        return data != null ? data.icon : null;
    }
    public int GetReviveFuelCost(string itemId)
    {
        var data = unitDB != null ? unitDB.GetUnit(itemId) : null;
        if (data == null) return 0;

        int cost = data.reviveFuelCost;
        if(deadRecords.TryGetValue(itemId,out var recoad) && recoad.crushed)
        {
            cost = Mathf.RoundToInt(cost * _crushedReviveCostMultiplier);
        }
        return cost;
    }
    private bool IsReviving(string itemId)
    {
        return deadRecords.TryGetValue(itemId, out var recoad) && recoad.isReviving;
    }

    #endregion

    #region DeployAndReturn
    /// <summary>
    /// 出撃済みアイコンのタップ受け口。生存中なら従来通り帰還、死亡中なら復活タップとして通知
    /// </summary>
    public void HandleUnitTap(string itemId,bool isMovePhase)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        
        switch(GetUnitStatus(itemId))
        {
            case UnitBattleStatus.Deployed:
                ReturnUnit(itemId);
                break;
            case UnitBattleStatus.Dead:
                if(isMovePhase && !IsReviving(itemId))
                {
                    OnDeadUnitTapped?.Invoke(itemId);
                }
                break;
        }
    }
    public void UpdateUnitPlacementPreview(InputBuffer.InputEvent evt)
    {
        if (string.IsNullOrEmpty(evt.draggedItemId)) return;

        if(!_isDragging)
        {
            _isDragging = true;
            SetAllEnemyVisuals(true);
        }

        if(currentPreview != null && currentPreviewItemId != evt.draggedItemId)
        {
            HideUnitPlacementPreview();
        }

        if(currentPreview == null)
        {
            var unitData = unitDB != null ? unitDB.GetUnit(evt.draggedItemId) : null;
            if (unitData == null || unitData.prefab == null) return;
             
            currentPreview = Instantiate(unitData.prefab);
            currentPreviewItemId = evt.draggedItemId;

            var previewCombatant = currentPreview.GetComponent<Combatant>();
            previewCombatant?.InitializeAsPreview(unitData);
            
        }

        currentPreview.transform.position = ScreenToWorldPosition(evt.position);
    }
    public void HideUnitPlacementPreview()
    {
        if (currentPreview != null)
        {
            
                Destroy(currentPreview);
                currentPreview = null;
        }
        if(_isDragging)
        {
            _isDragging = false;
            SetAllEnemyVisuals(false);
        }
    }
    private void SetAllEnemyVisuals(bool visible)
    {
        foreach(var combatant in Combatant.All)
        {
            if(combatant.Affiliation == Base_Item.Affiliation.Enemy)
            {
                combatant.SetPreviewVisualsViisivle(visible);
            }
        }
    }
    private Vector3 ScreenToWorldPosition(Vector2 uiPosition)
    {

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return Vector3.zero;

        Vector2 screenPos = new Vector2(uiPosition.x, Screen.height - uiPosition.y);

        float zDistance = -mainCamera.transform.position.z;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDistance));
        worldPos.z = 0f;

        
        return worldPos;
    }
    public void DeployUnit(string itemId)
    {


        if (string.IsNullOrEmpty(itemId)) return;

        if(!CanDeploy(itemId) || currentPreview == null || currentPreviewItemId != itemId)
        {
            HideUnitPlacementPreview();
            return;
        }

        var deployedUnit = currentPreview;
        currentPreview = null;
        currentPreviewItemId = null;

        if(_isDragging)
        {
            _isDragging = false;
            SetAllEnemyVisuals(false);
        }

        var combatant = deployedUnit.GetComponent<Combatant>();
        combatant?.Activate(_matchManager);
        combatant?.SetStatMultiplier(
            _modifiers != null ? _modifiers.GetMultiplier(Base_Item.Affiliation.Ally) : 1f);

        if (!_battleDamage.ContainsKey(itemId)) _battleDamage[itemId] = 0;
        if(combatant != null)
        {
            combatant.OnDealtDamage += dmg => AddBattleDamage(itemId, dmg);
            combatant.OnActualDamageTaken += dmg => _runStats?.AddTaken(itemId, dmg);
        }

        unitStatus[itemId] = UnitBattleStatus.Deployed;
        deployedUnits[itemId] = deployedUnit;

        OnUnitDeployed?.Invoke(itemId, deployedUnit);
    }
    public void ReturnUnit(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        if (GetUnitStatus(itemId) != UnitBattleStatus.Deployed) return;
        if (!deployedUnits.TryGetValue(itemId, out var unit)) return;

        var combatant = unit != null ? unit.GetComponent<Combatant>() : null;
        if (combatant != null && combatant.IsDead) return;

        deployedUnits.Remove(itemId);
        unitStatus[itemId] = UnitBattleStatus.Returned;

        if(unit != null)
        {
            Destroy(unit);
        }

        OnUnitReturned?.Invoke(itemId, unit);
    }
    ///<summary>出撃中の生存Unitをすべて帰還させる</summary>
    public void ReturnAllAliveUnits()
    {
        foreach(var itemId in new List<string>(deployedUnits.Keys))
        {
            ReturnUnit(itemId);
        }
    }

    #endregion

    #region EventEffects
    /// <summary>編成中の生存ユニットをランダムにcount人、死亡させる。実際に死亡させた人数を返す</summary>
    public int KillRandomAliveUnits(int count,Func<int,int>pickIndex)
    {
        var alive = new List<string>();
        foreach(var id in _roster)
        {
            if (GetUnitStatus(id) != UnitBattleStatus.Dead) alive.Add(id);
        }

        int killed = 0;
        while (killed < count && alive.Count > 0)
        {
            int i = pickIndex(alive.Count);
            string id = alive[i];
            alive.RemoveAt(i);

            unitStatus[id] = UnitBattleStatus.Dead;
            deadRecords[id] = new DeadRecoad();
            OnUnitDied?.Invoke(id);
            killed++;
        }

        return killed;
    }

    public int ReviveDeadUnitsFree(int count,Func<int,int>pickIndex)
    {
        var dead = new List<string>(deadRecords.Keys);
        int target = count <= 0 ? dead.Count : Mathf.Min(count, dead.Count);

        int revived = 0;
        while (revived < target && dead.Count > 0)
        {
            int i = pickIndex(dead.Count);
            string id = dead[i];
            dead.RemoveAt(i);

            deadRecords.Remove(id);
            unitStatus.Remove(id);
            OnUnitRevived?.Invoke(id);
            revived++;
        }
        return revived;
    }

    #endregion

    #region BattleSettlement
    public void SettleBattle()
    {
        foreach(var pair in new List<KeyValuePair<string,GameObject>>(deployedUnits))
        {
            var itemId = pair.Key;
            var unit = pair.Value;
            var combatant = unit != null ? unit.GetComponent<Combatant>() : null;
            bool isDead = combatant != null && combatant.IsDead;

            if (unit != null) Destroy(unit);
            deployedUnits.Remove(itemId);

            if(isDead)
            {
                unitStatus[itemId] = UnitBattleStatus.Dead;
                deadRecords[itemId] = new DeadRecoad();
                OnUnitDied?.Invoke(itemId);
            }
            else
            {
                unitStatus.Remove(itemId);
                OnUnitReturned?.Invoke(itemId, null);
            }
        }

        foreach(var itemId in new List<string>(unitStatus.Keys))
        {
            if (unitStatus[itemId] == UnitBattleStatus.Returned)
            {
                unitStatus.Remove(itemId);
            }
        }
    }
    /// <summary>
    /// 轢かれた味方を、死亡状態にして破棄する
    /// </summary>
    public bool CrushUnit(Combatant combatant)
    {
        string foundId = null;
        foreach(var pair in deployedUnits)
        {
            if(pair.Value != null && pair.Value.GetComponent<Combatant>() == combatant)
            {
                foundId = pair.Key;
                break;
            }
        }
        if (foundId == null) return false;

        var unit = deployedUnits[foundId];
        deployedUnits.Remove(foundId);
        unitStatus[foundId] = UnitBattleStatus.Dead;
        deadRecords[foundId] = new DeadRecoad { crushed = true };

        if (unit != null) Destroy(unit);
        OnUnitDied?.Invoke(foundId);
        return true;
    }

    private void AddBattleDamage(string itemId,int amount)
    {
        _battleDamage.TryGetValue(itemId, out var current);
        _battleDamage[itemId] = current + amount;
        _runStats?.AddDealt(itemId, amount);
    }

    ///<summary>今回のBattleで出撃したUnitを、読ダメージの多い順に返す</summary>
    public List<BattleDamageEntry> GetBattleDamageRanking()
    {
        var list = new List<BattleDamageEntry>();
        foreach(var pair in _battleDamage)
        {
            list.Add(new BattleDamageEntry
            {
                itemId = pair.Key,
                icon = GetUnitIcon(pair.Key),
                damage = pair.Value,
            });
        }
        list.Sort((a, b) => b.damage.CompareTo(a.damage));
        return list;
    }

    #endregion

    #region Revive
    /// <summary>
    ///ポップアップの決定タップで呼ばれる。Fuelが足りなければ何もせずFalseを返す 
    /// </summary>
    public bool TryConfirmRevive(string itemId)
    {
        if (_fuelManager == null) return false;
        if (!deadRecords.TryGetValue(itemId, out var recoad) || recoad.isReviving) return false;

        int cost = GetReviveFuelCost(itemId);
        if (_fuelManager.CurrentFuel < cost) return false;

        _fuelManager.ConsumeAmount(cost);
        recoad.isReviving = true;
        recoad.timer = 0f;

        OnReviveStarted?.Invoke(itemId);
        return true;

    }
    /// <summary>
    /// Operation中は毎tick呼ぶ。タイマーは常にすすみ、完了はMoveの間だけ
    /// </summary>
    public void TickRevivals(float fixedDt,bool canComplete)
    {
        _reviveCompleted.Clear();

        foreach(var pair in deadRecords)
        {
            var record = pair.Value;
            if (!record.isReviving) continue;

            var data = unitDB != null ? unitDB.GetUnit(pair.Key) : null;
            float duration = data != null ? data.reviveDuration : 0f;

            if (record.timer < duration) record.timer += fixedDt;

            float progress = duration > 0f ? Mathf.Clamp01(record.timer / duration) : 1f;
            float remaining = Mathf.Max(0f, duration - record.timer);
            OnReviveProgress?.Invoke(pair.Key, remaining, progress);

            if(canComplete && record.timer >= duration)
            {
                _reviveCompleted.Add(pair.Key);
            }
        }

        foreach(var itemId in _reviveCompleted)
        {
            deadRecords.Remove(itemId);
            unitStatus.Remove(itemId);
            OnUnitRevived?.Invoke(itemId);
            
        }
    }

    #endregion
}
