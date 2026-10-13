using UnityEngine;

/// <summary>
/// 入力はInputBufferのEventだけを見る
/// </summary>
public class CardDragController
{
    private enum CardAction
    {
        None,
        Deploy,
        Return,
        Rebuild,
        SkillAnywhere,
        SkillTargetAlly,
    }

    private const float TargetPickRadius = 1.0f;

    private static readonly Color ReturnMarkerColor = new Color(0.35f, 0.67f, 1f);
    private static readonly Color SkillMarkerColor = new Color(0.75f, 0.47f, 1f);

    private readonly UnitManager _units;
    private readonly SkillManager _skills;
    private readonly DragTargetMarker _marker = new();

    private string _activeKey;
    private CardAction _action = CardAction.None;

    public CardDragController(UnitManager units,SkillManager skills)
    {
        _units = units;
        _skills = skills;
    }

    #region Input
    public void HandleInput(InputBuffer.InputEvent evt,OperationPhase phase)
    {
        if (!evt.isDragFromUI || string.IsNullOrEmpty(evt.draggedItemId)) return;

        switch(evt.type)
        {
            case InputBuffer.InputType.PointerDown:
                Begin(evt.draggedItemId, phase);
                break;
            case InputBuffer.InputType.PointerMove:
                OnMove(evt);
                break;
            case InputBuffer.InputType.PointerUp:
                OnEnd(evt, phase);
                break;
        }
    }

    public void Cancel()
    {
        if (_action == CardAction.Deploy) _units.HideUnitPlacementPreview();
        Reset();
    }

    #endregion

    #region Drag
    private void Begin(string key,OperationPhase phase)
    {
        Reset();
        _activeKey = key;
        _action = Resolve(key, phase);
    }

    private CardAction Resolve(string key,OperationPhase phase)
    {
        if(SkillKey.IsSkillKey(key))
        {
            if (phase != OperationPhase.Battle) return CardAction.None;
            return _skills.RequiresAllyTarget(key) ? CardAction.SkillTargetAlly : CardAction.SkillAnywhere;
        }

        switch(_units.GetUnitStatus(key))
        {
            case UnitManager.UnitBattleStatus.Standby:
                return phase == OperationPhase.Battle ? CardAction.Deploy : CardAction.None;
            case UnitManager.UnitBattleStatus.Deployed:
                return phase == OperationPhase.Battle ? CardAction.Return : CardAction.None;
            case UnitManager.UnitBattleStatus.Dead:
                return phase == OperationPhase.Move ? CardAction.Rebuild : CardAction.None;
            default:
                return CardAction.None;
        }
    }

    private void OnMove(InputBuffer.InputEvent evt)
    {
        if (_action == CardAction.None || evt.draggedItemId != _activeKey) return;

        switch(_action)
        {
            case CardAction.Deploy:
                if(evt.pointerOverUI)
                {
                    _units.SetPlacementPreviewVisible(false);
                }
                else
                {
                    _units.UpdateUnitPlacementPreview(evt);
                    _units.SetPlacementPreviewVisible(true);
                }
                break;
            case CardAction.Return:
            case CardAction.SkillTargetAlly:
                UpdateTargetMarker(evt);
                break;
        }
    }

    private void OnEnd(InputBuffer.InputEvent evt,OperationPhase phase)
    {
        if(_action == CardAction.None || evt.draggedItemId != _activeKey)
        {
            Reset();
            return;
        }

        string key = _activeKey;
        var action = _action;
        bool cancelled = evt.pointerOverUI;
        Reset();

        switch(action)
        {
            case CardAction.Deploy:
                if (cancelled) _units.HideUnitPlacementPreview();
                else _units.DeployUnit(key);
                break;
            case CardAction.Return:
                if (cancelled) break;
                if (_units.FindDeployedUnitAt(evt.position, TargetPickRadius) == key) _units.ReturnUnit(key);
                    break;
            case CardAction.Rebuild:
                if (!cancelled) _units.HandleUnitTap(key, phase == OperationPhase.Move);
                break;
            case CardAction.SkillAnywhere:
                if (!cancelled) _skills.RequestUse(key, null);
                break;
            case CardAction.SkillTargetAlly:
                if (cancelled) break;
                var targetId = _units.FindDeployedUnitAt(evt.position, TargetPickRadius);
                if (targetId != null) _skills.RequestUse(key,targetId);
                break;
        }
    }

    private void UpdateTargetMarker(InputBuffer.InputEvent evt)
    {
        string id = evt.pointerOverUI ? null : _units.FindDeployedUnitAt(evt.position, TargetPickRadius);
        if (_action == CardAction.Return && id != _activeKey) id = null;

        var combatant = id != null ? _units.GetDeployedCombatant(id) : null;
        if(combatant == null)
        {
            _marker.Hide();
            return;
        }

        _marker.ShowOn(combatant.transform, _action == CardAction.Return ? ReturnMarkerColor : SkillMarkerColor);
    }

    private void Reset()
    {
        _activeKey = null;
        _action = CardAction.None;
        _marker.Hide();
    }
    #endregion

}

/// <summary>
/// ドラッグ中、対象のUnitの足元に出す
/// </summary>
internal class DragTargetMarker
{
    private const int segments = 40;
    private const float Radius = 0.7f;

    private GameObject _go;
    private LineRenderer _line;

    public void ShowOn(Transform unit,Color color)
    {
        Ensure();
        if (_go.transform.parent != unit) _go.transform.SetParent(unit, false);
        _go.transform.localPosition = Vector3.zero;
        _line.startColor = color;
        _line.endColor = color;
        if (!_go.activeSelf) _go.SetActive(true);
    }

    public void Hide()
    {
        if(_go == null) return;

        _go.transform.SetParent(null,false);
        _go.SetActive(false);
    }

    private void Ensure()
    {
        if (_go != null) return;

        _go = new GameObject("DragTargetMarker");
        _line = _go.AddComponent<LineRenderer>();
        _line.useWorldSpace = false;
        _line.loop = true;
        _line.positionCount = segments;
        _line.widthMultiplier = 0.06f;
        _line.sortingOrder = 100;
        _line.material = new Material(Shader.Find("Sprites/Default"));
        
        for(int i = 0;i<segments; i++) 
        {
            float a = Mathf.PI * 2f * i / segments;
            _line.SetPosition(i, new Vector3(Mathf.Cos(a) * Radius, Mathf.Sin(a) * Radius, 0f));
        }
    }
}
