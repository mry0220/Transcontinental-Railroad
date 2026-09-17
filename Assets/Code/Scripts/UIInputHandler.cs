using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
public class UIInputHandler : MonoBehaviour
{
    //UIDocument からrootVisualElementを取得

    //==== References====
    [SerializeField] private InputBuffer inputBuffer;
    
    //==== Runtime State====
    private VisualElement root;
    private string currentDraggedItemId;

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        if (inputBuffer == null)
        {
            inputBuffer = FindAnyObjectByType<InputBuffer>();
            if (inputBuffer == null)
            {
                Debug.LogError("InputBuffer not found!");
                return;
            }
        }
#endif
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

        currentDraggedItemId = null;
    }

    private void Enqueue(IPointerEvent evt,InputBuffer.InputType type,Vector2 delta,string itemId,bool fromUI)
    {
        if (inputBuffer == null) return;
        var pos = evt.position;
        var ts = Time.timeAsDouble;
        inputBuffer.EnqueueEvent(new InputBuffer.InputEvent(type, pos, delta, ts,itemId,fromUI));
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
        if (inputBuffer == null) return;

        GUILayout.BeginArea(new Rect(10, 270, 350, 100));
        GUILayout.Label("=== UIInputHandler ===");
        GUILayout.Label($"InputBuffer: {(inputBuffer != null ? "OK" : "NULL")}");
        GUILayout.Label($"Root: {(root != null ? "OK" : "NULL")}");
        GUILayout.Label($"CurrentDragItem: {currentDraggedItemId ?? "null"}");
        GUILayout.EndArea();
    }
#endif
}
