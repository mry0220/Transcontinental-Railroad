using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NavigatorExpression
{
    public string id;
    public Sprite sprite;
    public Sprite bubbleSprite;
    [TextArea] public List<string> lines = new();

    public string PickLine()
    {
        if(lines == null || lines.Count == 0) return "";
        return lines[UnityEngine.Random.Range(0, lines.Count)];
    }
}
[CreateAssetMenu(fileName = "Data_Navigator",menuName ="ScriptableObjects/UI/Data_Navigator")]
public class Data_Navigator : ScriptableObject
{
    [Tooltip("0番が初期表情")]
    [SerializeField] private List<NavigatorExpression> _expressions = new();
    [SerializeField] private Sprite _defaultBubble;
    [Tooltip("最後のタップから初期表情に戻るまでの秒数。0なら戻らない")]
    [SerializeField] private float _resetSecounds = 4f;

    public int Count => _expressions != null ? _expressions.Count : 0;
    public NavigatorExpression Get(int index) => _expressions[index];
    public Sprite DefaultBubble => _defaultBubble;
    public float ResetSeconds => _resetSecounds;
}
