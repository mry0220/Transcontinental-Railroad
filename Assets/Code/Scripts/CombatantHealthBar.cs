using UnityEngine;

public class CombatantHealthBar : MonoBehaviour
{
    private static Sprite _whiteSprite;

    private Transform _fill;
    private float _width;
    private float _height;

    ///<summary>左端を原点にした白い1ピクセルのSprite。横に伸縮させてバーにする</summary>
    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;

        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        _whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        return _whiteSprite;
    }

    public static CombatantHealthBar Create(Transform owner,bool isAlly,BattleVisualSettings settings)
    {
        var root = new GameObject("HpBar");
        root.transform.SetParent(owner, false);

        //親の拡縮を打消し、
        Vector3 s = owner.lossyScale;
        float sx = s.x != 0f ? 1f / s.x : 1f;
        float sy = s.y != 0f ? 1f / s.y : 1f;
        root.transform.localScale = new Vector3(sx, sy, 1f);
        root.transform.localPosition = new Vector3(0f, settings.hpBarOffsetY * sy, 0f);

        var bar = root.AddComponent<CombatantHealthBar>();
        bar._width = settings.hpBarWidth;
        bar._height = settings.hpBarHeight;

        var back = CreateSprite("Back", root.transform, settings.hpBarBackColor, 100);
        back.transform.localPosition = new Vector3(-settings.hpBarWidth / 2f, 0f, 0f);
        back.transform.localScale = new Vector3(settings.hpBarWidth, settings.hpBarHeight, 1f);

        var fill = CreateSprite("Fill", root.transform,
            isAlly ? settings.allyHpColor : settings.enemyHpColor, 101);
        fill.transform.localPosition = new Vector3(-settings.hpBarWidth / 2f, 0f, 0f);
        fill.transform.localScale = new Vector3(settings.hpBarWidth, settings.hpBarHeight, 1f);
        bar._fill = fill.transform;

        return bar;
    }

    private static GameObject CreateSprite(string name,Transform parent,Color color,int sortingOrder)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return obj;
    }

    public void Refrash(int currentHp,int maxHp)
    {
        float ratio = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
        _fill.localScale = new Vector3(_width * ratio, _height, 1f);

        gameObject.SetActive(currentHp > 0);
    }
}
