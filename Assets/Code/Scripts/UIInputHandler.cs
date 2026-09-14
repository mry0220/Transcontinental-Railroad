using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
public class UIInputHandler : MonoBehaviour
{
    //UIDocument からrootVisualElementを取得

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private InputBuffer inputBuffer;

    private VisualElement root;

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        root = uiDocument.rootVisualElement;
        if (root == null) return;
        root.RegisterCallback<PointerDownEvent>(OnPointerDown,TrickleDown.TrickleDown);
        root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        root.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);

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
        Enqueue(evt,InputBuffer.InputType.PointerDown,Vector2.zero);
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        Vector2 delta = evt.deltaPosition;

        Enqueue(evt,InputBuffer.InputType.PointerMove, delta);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        root.ReleasePointer(evt.pointerId);
        Enqueue(evt, InputBuffer.InputType.PointerUp, Vector2.zero);
    }

    private void Enqueue(IPointerEvent evt,InputBuffer.InputType type,Vector2 delta)
    {
        if (inputBuffer == null) return;
        var pos = evt.position;
        var ts = Time.timeAsDouble;
        inputBuffer.EnqueueEvent(new InputBuffer.InputEvent(type, pos, delta, ts));
    }
}
