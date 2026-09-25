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
    }

    private readonly Dictionary<string, UnitBattleStatus> unitStatus = new();
    private readonly Dictionary<string, GameObject> deployedUnits = new();

    [Header("最大出撃数")]
    [SerializeField] private int maxDeployCount = 6;
    private GameObject currentPreview;
    private string currentPreviewItemId;

    private MatchManager _matchManager;

    private bool _isDragging;
    private void Awake()
    {
        if (unitDB == null)
            Debug.LogError("[BattleManager] DataBase_Unit not assigned");

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    public void SetMatchManager(MatchManager matchManager)
    {
        _matchManager = matchManager;
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

    public void ResetUnitStatuses()
    {
        unitStatus.Clear();
        deployedUnits.Clear();
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
}
