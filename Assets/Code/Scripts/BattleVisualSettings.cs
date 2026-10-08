using UnityEngine;

[System.Serializable]
public class BattleVisualSettings
{
    [Header("HPバー")]
    public bool showHpBars = true;
    public float hpBarWidth = 1.0f;
    public float hpBarHeight = 0.12f;
    [Tooltip("Combatantの中心から、バーまでの高さ(ワールド)")]
    public float hpBarOffsetY = -0.9f;
    public Color hpBarBackColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    public Color allyHpColor = new Color(0.35f, 0.85f, 0.3f, 1f);
    public Color enemyHpColor = new Color(0.9f, 0.3f, 0.3f, 1f);

    [Header("ダメージ数字")]
    public bool showDamagePopups = true;
    public float popupDuration = 0.8f;
    public float popupRiseDistance = 0.8f;
    public float popupStartOffsetY = 0.6f;
    [Tooltip("数字が重ならないように、左右にずらす幅")]
    public float popupJistterX = 0.25f;
    public float popupCharacterSize = 0.1f;
    public int popupFontSize = 48;
    public Color damageToEnemyColor = Color.white;
    public Color damageToAllyColor = new Color(1f, 0.4f, 0.4f, 1f);

    [Header("頭上表示(攻撃対象Icon・MoveSpeed)")]
    public bool showIndicators = true;
    [Tooltip("Combatantの中心からIconまでの高さ(ワールド)")]
    public float indicatorOffsetY = 0.9f;
    public float indicatorIconSize = 0.35f;
    public float indicatorSlotGap = 0.08f;
    public Color indicatorInnerColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [Header("枠：Match（確定）")]
    public Color matchFrameColor = new Color(1f, 0.3f, 0.3f, 1f);
    public float matchFrameThickness = 0.05f;
    [Header("枠：Match（待機中）")]
    public Color provisionalFrameColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    public float provisionalFrameTickness = 0.025f;
    [Header("MoveSpeed表示")]
    public float indicatorSpeedGap = 0.05f;
    public float indicatorSpeedCharSize = 0.08f;
    public int indicatorSpeedFontSize = 48;
    public Color indicatorSpeedColor = Color.white;
}
