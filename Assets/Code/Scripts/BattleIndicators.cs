using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Battle中、Entityの頭上に攻撃対象のIconとMoveSpeedを描く
/// Combatantの状態は読むだけで、変更しない。GameLoopManagerがPresentationを呼ぶ
/// </summary>

public class BattleIndicators : ITickable
{
    #region Types
    private const int OrderFrame = 110;
    private const int OrderInner = 111;
    private const int OrderIcon = 112;
    private const int OrderText = 113;

    private sealed class Slot
    {
        public GameObject root;
        public SpriteRenderer frame;
        public SpriteRenderer inner;
        public SpriteRenderer icon;
    }

    private sealed class View
    {
        public GameObject root;
        public TextMesh speedText;
        public readonly List<Slot> slots = new();
        public bool lastHasTargets = true;
        public float lastSpeed = -1f;
    }

    #endregion

    #region Fields
    private static Sprite _whiteSprite;

    private readonly BattleVisualSettings _settings;
    private readonly System.Func<bool> _isBattle;
    private readonly Dictionary<Combatant, View> _views = new();
    private readonly List<Combatant.TargetView> _buffer = new();
    private readonly List<Combatant> _purge = new();
    private bool _wasVisible;

    #endregion

    #region Setup
    public BattleIndicators(BattleVisualSettings settings, System.Func<bool>isBattle)
    {
        _settings = settings;
        _isBattle = isBattle;
    }
    /// <summary>中心を原点にした、1x1ワールド単位の白いSprite</summary>
    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;

        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        _whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _whiteSprite;
    }
    #endregion

    #region Tick
    public void Tick(float dt)
    {
        bool visible = _settings.showIndicators && _isBattle();
        if(!visible)
        {
            if (_wasVisible) SetAllVisible(false);
            _wasVisible = false;
            return;
        }
        _wasVisible = true;

        var all = Combatant.All;
        for(int i =0;i<all.Count;i++)
        {
            var c = all[i];
            if (c == null) continue;

            bool show = c.IsActive && !c.IsDead && !c.IsTrain;

            if(!_views.TryGetValue(c,out var view))
            {
                if (!show) continue;
                view = CreateView(c);
                _views[c] = view;
            }

            view.root.SetActive(show);
            if (show) Refresh(c, view);
        }

        PurgeDestroyed();
    }

    /// <summary>新しい運行の前に呼ぶ。Combatant側のGameObjectは別途破棄</summary>
    public void Clear()
    {
        _views.Clear();
        _wasVisible = false;
    }

    private void SetAllVisible(bool visible)
    {
        foreach(var pair in _views)
        {
            if (pair.Key != null && pair.Value.root != null)
                pair.Value.root.SetActive(visible);
        }
    }

    private void PurgeDestroyed()
    {
        if (_views.Count <= Combatant.All.Count) return;

        foreach(var key in _views.Keys)
        {
            if (key == null) _purge.Add(key);
        }
      
        foreach (var key in _purge) _views.Remove(key);
        _purge.Clear();
    }
    #endregion

    #region Create
    private View CreateView(Combatant owner)
    {
        var root = new GameObject("Indicators");
        root.transform.SetParent(owner.transform, false);

        Vector3 s = owner.transform.lossyScale;
        float sx = s.x != 0f ? 1f / s.x : 1f;
        float sy = s.y != 0f ? 1f / s.y : 1f;
        root.transform.localScale = new Vector3(sx, sy, 1f);
        root.transform.localPosition = new Vector3(0f, _settings.indicatorOffsetY * sy, 0f);

        var textObj = new GameObject("Speed");
        textObj.transform.SetParent(root.transform, false);
        var text = textObj.AddComponent<TextMesh>();
        text.characterSize = _settings.indicatorSpeedCharSize;
        text.fontSize = _settings.indicatorSpeedFontSize;
        text.alignment = TextAlignment.Center;
        text.anchor = TextAnchor.LowerCenter;
        text.color = _settings.indicatorSpeedColor;
        textObj.GetComponent<MeshRenderer>().sortingOrder = OrderText;

        return new View { root = root, speedText = text };
    }

    private Slot CreateSlot(Transform parent)
    {
        var root = new GameObject("Slot");
        root.transform.SetParent(parent, false);

        var slot = new Slot { root = root };
        slot.frame = CreateSprite("Frame", root.transform, OrderFrame, GetWhiteSprite());
        slot.inner = CreateSprite("Inner", root.transform, OrderInner, GetWhiteSprite());
        slot.icon = CreateSprite("Icon", root.transform, OrderIcon, null);
        return slot;
    }

    private static SpriteRenderer CreateSprite(string name,Transform parent,int order,Sprite sprite)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }
    #endregion

    #region Refrash
    private void Refresh(Combatant owner,View view)
    {
        owner.CollectTargets(_buffer);
        int count = _buffer.Count;

        float size = _settings.indicatorIconSize;
        float maxThickness = Mathf.Max(_settings.matchFrameThickness, _settings.provisionalFrameTickness);
        float pitch = size + maxThickness * 2f + _settings.indicatorSlotGap;

        while(view.slots.Count < count)
        {
            view.slots.Add(CreateSlot(view.root.transform));
        }

        for(int i = 0;i<view.slots.Count;i++)
        {
            var slot = view.slots[i];
            bool used = i < count;
            slot.root.SetActive(used);
            if (!used) continue;

            var target = _buffer[i];
            float thickness = target.IsProvisional
                ? _settings.provisionalFrameTickness
                : _settings.matchFrameThickness;

            slot.root.transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * pitch, 0f, 0f);

            slot.frame.color = target.IsProvisional
                ? _settings.provisionalFrameColor
                : _settings.matchFrameColor;
            float outer = size + thickness * 2f;
            slot.frame.transform.localScale = new Vector3(outer, outer, 1f);

            slot.inner.color = _settings.indicatorInnerColor;
            slot.inner.transform.localScale = new Vector3(size, size, 1f);

            Sprite sprite = target.Target != null ? target.Target.Icon : null;
            slot.icon.sprite = sprite;
            slot.icon.enabled = sprite != null;
            if(sprite != null)
            {
                Vector3 b = sprite.bounds.size;
                float fit = size / Mathf.Max(b.x, b.y);
                slot.icon.transform.localScale = new Vector3(fit, fit, 1f);
            }
        }

        RefreshSpeed(owner, view, count > 0, size, maxThickness);
    }
    
    private void RefreshSpeed(Combatant owner,View view,bool hasTargets,float size,float maxThickness)
    {
        float speed = owner.MoveSpeed;
        if(Mathf.Abs(speed - view.lastSpeed) > 0.001f)
        {
            view.lastSpeed = speed;
            view.speedText.text = speed.ToString("F1");
        }

        if(hasTargets != view.lastHasTargets)
        {
            view.lastHasTargets = hasTargets;
            view.speedText.anchor = hasTargets ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter;
        }

        float y = hasTargets
            ? size * 0.5f + maxThickness + _settings.indicatorSpeedGap
            : 0f;
        view.speedText.transform.localPosition = new Vector3(0f, y, 0f);
    }

    public void Hide()
    {
        if (_wasVisible) SetAllVisible(false);
        _wasVisible = false;
    }
    #endregion
}
