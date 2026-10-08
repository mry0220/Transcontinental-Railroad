using UnityEngine;

public class DamagePopup
{
    private readonly GameObject _object;
    private readonly TextMesh _text;
    private readonly Vector3 _startPosition;
    private readonly float _duration;
    private readonly float _riseDistance;
    private readonly Color _color;
    private float _elapsed;

    public DamagePopup(Vector3 worldPosition,int amount,Color color,BattleVisualSettings settings)
    {
        _object = new GameObject("DamagePopup");

        float jitter = Random.Range(-settings.popupJistterX, settings.popupJistterX);
        _startPosition = worldPosition + new Vector3(jitter, 0f, 0f);
        _object.transform.position = _startPosition;
        _duration = Mathf.Max(0.05f, settings.popupDuration);
        _riseDistance = settings.popupRiseDistance;
        _color = color;

        _text = _object.AddComponent<TextMesh>();
        _text.text = amount.ToString();
        _text.characterSize = settings.popupCharacterSize;
        _text.fontSize = settings.popupFontSize;
        _text.anchor = TextAnchor.MiddleCenter;
        _text.alignment = TextAlignment.Center;
        _text.color = color;

        var renderer = _object.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sortingOrder = 200;
    }
    
    ///<summary>終わったらTrueを返し、自分のオブジェクトを破棄する</summary>
    public bool Tick(float dt)
    {
        _elapsed += dt;
        float t = Mathf.Clamp01(_elapsed / _duration);

        _object.transform.position = _startPosition + Vector3.up * (_riseDistance * t);

        var c = _color;
        c.a = 1f - t * t;
        _text.color = c;

        if (t < 1f) return false;

        Object.Destroy(_object);
        return true;
    }

    public void Dispose()
    {
        if (_object != null) Object.Destroy(_object);
    }
}
