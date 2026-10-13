using Unity.VisualScripting;
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
        if (evt.isDragFromUI || string.IsNullOrEmpty(evt.draggedItemId)) return;

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
           // return _skills.
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
                //if(evt.pointerOverUI)
                //{
                //  //  _units.SetPlacementPreviewVisible(false);
                //}
                //else
                //{
                //    _units.UpdateUnitPlacementPreview(evt);
                //    _units.SetPlacementPreviewVisible(true);
                //}
                break;
            case CardAction.Return:
            case CardAction.SkillTargetAlly:
                UpdateTargetMarker(evt);
                break;
        }
    }

    private void OnEnd(InputBuffer.InputEvent evt,OperationPhase phase)
    {

    }

    private void UpdateTargetMarker(InputBuffer.InputEvent evt)
    {

    }

    private void Reset()
    {

    }

    #endregion

}

internal class DragTargetMarker
{

}
