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
    private VisualElement draggedElement;


    
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
        root.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
    }

    public void DeinitializeInputHandler()
    {
        if (root == null) return;
        root.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        root.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        root.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
        root.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
        root = null;
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        root.CapturePointer(evt.pointerId);

        draggedElement = FindItemElement(evt.target as VisualElement);
        currentDraggedItemId = draggedElement != null ? draggedElement.userData as string : null;

        bool isUIItem = !string.IsNullOrEmpty(currentDraggedItemId);

        if (isUIItem && IsCardElement(draggedElement))
        {
            draggedElement.AddToClassList("card-dragging");
        }

        Enqueue(evt,InputBuffer.InputType.PointerDown,Vector2.zero,currentDraggedItemId,isUIItem);
        
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        Vector2 delta = evt.deltaPosition;

        bool isUIItem = !string.IsNullOrEmpty(currentDraggedItemId);
        bool overUI = isUIItem && IsOverUI(evt.position);

        if(draggedElement != null && IsCardElement(draggedElement))
        {
            draggedElement.EnableInClassList("card-drop-cancel", overUI);
        }

        Enqueue(evt,InputBuffer.InputType.PointerMove, delta,currentDraggedItemId,isUIItem,overUI);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        root.ReleasePointer(evt.pointerId);

        bool isUIItem = !string.IsNullOrEmpty(currentDraggedItemId);
        bool overUI = isUIItem && IsOverUI(evt.position);

        Enqueue(evt, InputBuffer.InputType.PointerUp, Vector2.zero,currentDraggedItemId,isUIItem,overUI);

        EndDrag();
       
    }

    /// <summary>中断はキャンセル扱いで </summary>
    private void OnPointerCancel(PointerCancelEvent evt)
    {
        root.ReleasePointer(evt.pointerId);

        bool isUIItem = !string.IsNullOrEmpty(currentDraggedItemId);
        Enqueue(evt, InputBuffer.InputType.PointerUp, Vector2.zero, currentDraggedItemId,isUIItem,true);

        EndDrag();
    }

    private void EndDrag()
    {
        if(draggedElement != null)
        {
            draggedElement.RemoveFromClassList("card-dragging");
            draggedElement.RemoveFromClassList("card-drop-cancel");
            draggedElement = null;
        }
        currentDraggedItemId = null;
    }

    private void Enqueue(IPointerEvent evt, InputBuffer.InputType type, Vector2 delta, string itemId, bool fromUI, bool overUI = false)
    {
        if (_inputBuffer == null) return;
        float scale = (root != null && root.panel != null) ? root.panel.scaledPixelsPerPoint : 1f;
        var pos = evt.position * scale;
        var ts = Time.timeAsDouble * scale;
        _inputBuffer.EnqueueEvent(new InputBuffer.InputEvent(type, pos, delta, ts,itemId,fromUI,overUI));
    }

    private VisualElement FindItemElement(VisualElement target)
    {
        while (target != null)
        {
            if (target.userData is string) return target;
            target = target.parent;
        }
        return null;
    }

    private bool IsCardElement(VisualElement element)
    {
        return element != null
            && element.ClassListContains("unit-icon")
            && _stateManager != null
            && _stateManager.Current == GameState.Operation;
    }

    private bool IsOverUI(Vector2 panelPosition)
    {
        if(root == null || root.panel == null) return false;

        var picked = root.panel.Pick(panelPosition);
        if(picked == null) return false;

        return !IsFieldElement(picked);
    }

    private bool IsFieldElement(VisualElement element)
    {
        return element == root
            || element is TemplateContainer
            || element.name == "root"
            || element.ClassListContains("ui-field");
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
