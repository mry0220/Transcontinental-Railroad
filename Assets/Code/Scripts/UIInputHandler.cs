using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
public class UIInputHandler : MonoBehaviour
{
    //UIDocument からrootVisualElementを取得

    
    [SerializeField] private InputBuffer inputBuffer;
    [SerializeField] private DataBase_Unit unitDB;

    private UIDocument uiDocument;
    private VisualElement root;
    private string currentDraggedItemId;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("UIDocument not found!");
            return;
        }

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

    void OnEnable()
    {
        Debug.Log("===OnEnable Start===");
        
        root = uiDocument.rootVisualElement;
        
        if (root == null)
        {
            Debug.LogError("rootVisualElement is null!");
            return;
        }
        Debug.Log($"UnitDB:OK,unitscount = {(unitDB.units != null ? unitDB.units.Count : 0)}");
        if(unitDB.units != null && unitDB.units.Count > 0)
        {
            SetupUI();
        }

        //SetupUI();

        root.RegisterCallback<PointerDownEvent>(OnPointerDown,TrickleDown.TrickleDown);
        root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        root.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);

        Debug.Log("UIInputHandler: Events registered");
    }

    //========UI SetUp
    void SetupUI()
    {
        var container = root.Q<VisualElement>("unit-container");

        foreach(var unitData in unitDB.units)
        {
            Debug.Log($"setting up unit:id={unitData.id},icon = {unitData.icon}");

            var icon = new VisualElement();
            icon.AddToClassList("unit-icon");
            icon.style.backgroundImage = new StyleBackground(unitData.icon);
            icon.userData = unitData.id;

            container.Add(icon);
        }
    }

    private void OnDisable()
    {
        if (root == null) return;
        root.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        root.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        root.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
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
        GUILayout.Label($"UIDocument: {(uiDocument != null ? "OK" : "NULL")}");
        GUILayout.Label($"InputBuffer: {(inputBuffer != null ? "OK" : "NULL")}");
        GUILayout.Label($"Root: {(root != null ? "OK" : "NULL")}");
        GUILayout.Label($"CurrentDragItem: {currentDraggedItemId ?? "null"}");
        GUILayout.EndArea();
    }
#endif
}
