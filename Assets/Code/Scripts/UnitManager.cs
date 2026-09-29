using System;
using System.Collections.Generic;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    //====Inspector Config====
    [SerializeField] private DataBase_Unit unitDB;
    [SerializeField] private Camera mainCamera;

    //====Events====
    /// <summary> unitが出撃時に発火 </summary>
    public event Action<string, GameObject> OnUnitDeployed;

    /// <summary> unitが帰還時に発火 </summary>
    public event Action<string, GameObject> OnUnitReturned;

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
    }

    private readonly Dictionary<string, UnitBattleStatus> unitStatus = new();
    private readonly Dictionary<string, GameObject> deployedUnits = new();
    private readonly Dictionary<string, DeadRecoad> deadRecords = new();
    private readonly List<string> _reviveCompleted = new();

    public event Action<string> OnUnitDied;
    public event Action<string> OnUnitRevived;

    [Header("最大出撃数")]
    [SerializeField] private int maxDeployCount = 6;
    private GameObject currentPreview;
    private string currentPreviewItemId;

    private MatchManager _matchManager;
    private FuelManager _fuelManager;

    private bool _isDragging;
    private void Awake()
    {
        if (unitDB == null)
            Debug.LogError("[BattleManager] DataBase_Unit not assigned");

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    public void SetFuelManager(FuelManager fuelManager)
    {
        _fuelManager = fuelManager;
    }

    public void SetMatchManager(MatchManager matchManager)
    {
        _matchManager = matchManager;
    }

    public event System.Action<string> OnDeadUnitTapped;

    public Combatant GetDeployedCombatant(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (!deployedUnits.TryGetValue(itemId, out var unit) || unit == null) return null;
            return unit.GetComponent<Combatant>();
    }

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

    private bool IsReviving(string itemId)
    {
        return deadRecords.TryGetValue(itemId, out var recoad) && recoad.isReviving;
    }

    public int GetReviveFuelCost(string itemId)
    {
        var data = unitDB != null ? unitDB.GetUnit(itemId) : null;
        return data != null ? data.reviveFuelCost : 0;
    }

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
            float duration = data != null ? data.reviveRuration : 0f;

            if (record.timer < duration) record.timer += fixedDt;
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

    public UnitBattleStatus GetUnitStatus(string itemId)
    {
        return unitStatus.TryGetValue(itemId, out var status) ? status : UnitBattleStatus.Standby;

    }

    public bool CanDeploy(string itemId)
    {
        return GetUnitStatus(itemId) == UnitBattleStatus.Standby && deployedUnits.Count < maxDeployCount;
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

        unitStatus[itemId] = UnitBattleStatus.Deployed;
        deployedUnits[itemId] = deployedUnit;

        OnUnitDeployed?.Invoke(itemId, deployedUnit);
    }

    public void ResetForNewRun()
    {
        HideUnitPlacementPreview();
        unitStatus.Clear();
        deployedUnits.Clear();
        deadRecords.Clear();
    }
}
