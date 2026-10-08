using UnityEngine;
using UnityEngine.UIElements;
public class UIInputHandler : MonoBehaviour
{
    //UIDocument からrootVisualElementを取得

    //==== References====
    private InputBuffer _inputBuffer;
    private UnitManager _unitManager;
    private StateManager _stateManager;
    
    //==== Runtime State====
    private VisualElement root;
    private string currentDraggedItemId;
    private string tappedItemId;

    
    public void SetInputBuffer(InputBuffer inputBuffer)
    {
        _inputBuffer = inputBuffer;
    }

    public void SetUnitManager(UnitManager unitManager)
    {
        _unitManager = unitManager;
    }
    
    public void SetStateManager(StateManager stateManager)
    {
        _stateManager = stateManager;
    }

    public void InitializeInputHadler(VisualElement root)
    {
        this.root = root;

        root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        root.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
    }

    public void DeinitializeInputHandler()
    {
        if (root == null) return;
        root.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        root.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        root.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
        root = null;
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        root.CapturePointer(evt.pointerId);

        currentDraggedItemId = GetDraggedItemId(evt.target as VisualElement);

        bool isUIItem = !string.IsNullOrEmpty(currentDraggedItemId);

        var status = (isUIItem && _unitManager != null)
            ? _unitManager.GetUnitStatus(currentDraggedItemId)
            : UnitManager.UnitBattleStatus.Standby;

        if(status == UnitManager.UnitBattleStatus.Deployed
            || status == UnitManager.UnitBattleStatus.Dead)
        {
            tappedItemId = currentDraggedItemId;
            currentDraggedItemId = null;
            isUIItem = false;
        }
        else
        {
            tappedItemId = null;
        }

        Enqueue(evt,InputBuffer.InputType.PointerDown,Vector2.zero,currentDraggedItemId,isUIItem);
        
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        Vector2 delta = evt.deltaPosition;

        bool isUIItem = !string.IsNullOrEmpty(currentDraggedItemId);

        Enqueue(evt,InputBuffer.InputType.PointerMove, delta,currentDraggedItemId,isUIItem);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        root.ReleasePointer(evt.pointerId);

        bool isUIItem = !string.IsNullOrEmpty(currentDraggedItemId);

        Enqueue(evt, InputBuffer.InputType.PointerUp, Vector2.zero,currentDraggedItemId,isUIItem);

        if(!string.IsNullOrEmpty(tappedItemId))
        {
            _unitManager?.HandleUnitTap(tappedItemId,_stateManager?.CurrentOperationPhase == OperationPhase.Move);
            tappedItemId = null;
        }

        currentDraggedItemId = null;
    }

    private void Enqueue(IPointerEvent evt,InputBuffer.InputType type,Vector2 delta,string itemId,bool fromUI)
    {
        if (_inputBuffer == null) return;
        float scale = (root != null && root.panel != null) ? root.panel.scaledPixelsPerPoint : 1f;
        var pos = evt.position * scale;
        var ts = Time.timeAsDouble * scale;
        _inputBuffer.EnqueueEvent(new InputBuffer.InputEvent(type, pos, delta, ts,itemId,fromUI));
    }

    private string GetDraggedItemId(VisualElement target)
    {
        if (target == null) return null;

        if(target.userData is string itemId)
        {
            Debug.Log($"Found itemId: {itemId}");
            return itemId;
        }

        return GetDraggedItemId(target.parent); //いったん仮置き
    }

#if UNITY_EDITOR
    void OnGUI()
    {
        if (_inputBuffer == null) return;

        GUILayout.BeginArea(new Rect(10, 270, 350, 200));
        GUILayout.Label("=== UIInputHandler ===");
        GUILayout.Label($"CurrentState:{_stateManager.Current}");
        GUILayout.Label($"CurrentPrepState:{_stateManager.CurrentPrepPhase}");
        GUILayout.Label($"CurrentOperationState:{_stateManager.CurrentOperationPhase}");
        GUILayout.Label($"InputBuffer: {(_inputBuffer != null ? "OK" : "NULL")}");
        GUILayout.Label($"Root: {(root != null ? "OK" : "NULL")}");
        GUILayout.Label($"CurrentDragItem: {currentDraggedItemId ?? "null"}");
        GUILayout.EndArea();
    }
#endif
}
