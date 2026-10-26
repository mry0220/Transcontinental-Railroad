using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背景。静止した1枚絵(雲など)と、横へ流れる多層の絵(街並みなど)を持つ
/// このオブジェクト自体は、Scaleを1にしておくこと
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public Sprite sprite;

        [Tooltip("列車の速度に対する倍率。遠景ほど小さくする")]
        public float speedFactor = 0.3f;

        [Tooltip("画面の高さに対するSpriteの高さの比。1で、画面の高さちょうど")]
        public float heightRatio = 1f;

        [Tooltip("カメラ中央からの縦のずらし量(ワールド座標)")]
        public float yOffset = 0f;

        [Tooltip("タイル同士の間隔(ワールド座標)。負の値で重ねる")]
        public float gapX = 0f;

        [Tooltip("初期のXずらし量。正の値で左へずれる。層ごとに変えて継ぎ目をそろえない")]
        public float startShiftX = 0f;

        [Tooltip("負の値ほど後ろ。列車や敵のSpriteより小さくすること")]
        public int sortingOrder = -10;
    }

    private class LayerRuntime
    {
        public Layer layer;
        public readonly List<Transform> tiles = new();
        public float scale;
        public float tileWidth;
        public float stride;
        public float offset;
    }

    [SerializeField] private Camera targetCamera;

    [Header("静止する背景(1枚)")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private int backgroundSortingOrder = -30;

    [Header("流れる層(奥から順に)")]
    [SerializeField] private List<Layer> layers = new();

    private readonly List<LayerRuntime> _runtimes = new();
    private Transform _backgroundTile;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        Build();
    }

    private void Build()
    {
        if (targetCamera == null)
        {
            Debug.LogError("[ParallaxBackground]カメラが見つからない");
            return;
        }

        BuildBackground();

        float cameraWidth = CameraHeight() * targetCamera.aspect;

        foreach (var layer in layers)
        {
            if (layer.sprite == null) continue;

            var runtime = new LayerRuntime { layer = layer };
            runtime.scale = CameraHeight() * layer.heightRatio / layer.sprite.bounds.size.y;
            runtime.tileWidth = layer.sprite.bounds.size.x * runtime.scale;
            runtime.stride = Mathf.Max(0.01f, runtime.tileWidth + layer.gapX);
            runtime.offset = Mathf.Repeat(layer.startShiftX, runtime.stride);

            // 画面を覆うのに必要な最小枚数。ループの継ぎ目ぶんの1枚だけ足す
            int count = Mathf.CeilToInt(cameraWidth / runtime.stride) + 1;
            for (int i = 0; i < count; i++)
            {
                var tileObject = new GameObject($"{layer.sprite.name}_{i}");
                tileObject.transform.SetParent(transform, false);
                tileObject.transform.localScale = new Vector3(runtime.scale, runtime.scale, 1f);

                var renderer = tileObject.AddComponent<SpriteRenderer>();
                renderer.sprite = layer.sprite;
                renderer.sortingOrder = layer.sortingOrder;

                runtime.tiles.Add(tileObject.transform);
            }

            _runtimes.Add(runtime);
            Layout(runtime);
        }
    }

    private void BuildBackground()
    {
        if (backgroundSprite == null) return;

        var tileObject = new GameObject(backgroundSprite.name);
        tileObject.transform.SetParent(transform, false);

        var renderer = tileObject.AddComponent<SpriteRenderer>();
        renderer.sprite = backgroundSprite;
        renderer.sortingOrder = backgroundSortingOrder;

        _backgroundTile = tileObject.transform;
        FitBackground();
    }

    private float CameraHeight()
    {
        return targetCamera.orthographicSize * 2f;
    }

    /// <summary>静止背景を、画面を隙間なく覆う大きさで、カメラ中央に置く</summary>
    private void FitBackground()
    {
        if (_backgroundTile == null) return;

        float cameraHeight = CameraHeight();
        float cameraWidth = cameraHeight * targetCamera.aspect;
        Vector2 size = backgroundSprite.bounds.size;
        float scale = Mathf.Max(cameraWidth / size.x, cameraHeight / size.y);

        _backgroundTile.localScale = new Vector3(scale, scale, 1f);

        Vector3 camPos = targetCamera.transform.position;
        Vector3 center = backgroundSprite.bounds.center * scale;
        _backgroundTile.position = new Vector3(
            camPos.x - center.x, camPos.y - center.y, transform.position.z);
    }

    private void Layout(LayerRuntime runtime)
    {
        Vector3 camPos = targetCamera.transform.position;
        float cameraWidth = CameraHeight() * targetCamera.aspect;
        float cameraLeft = camPos.x - cameraWidth / 2f;

        Sprite sprite = runtime.layer.sprite;
        float leftOffset = sprite.bounds.min.x * runtime.scale;
        float centerY = sprite.bounds.center.y * runtime.scale;
        float y = camPos.y + runtime.layer.yOffset - centerY;

        // タイルの左端を、カメラ左端から、offsetぶん左へ置いて、stride刻みで並べる
        for (int i = 0; i < runtime.tiles.Count; i++)
        {
            float left = cameraLeft - runtime.offset + i * runtime.stride;
            runtime.tiles[i].position = new Vector3(left - leftOffset, y, transform.position.z);
        }
    }

    /// <summary>カメラの解像度や位置を変えたときに呼ぶ。全体を置き直す</summary>
    public void Fit()
    {
        FitBackground();
        foreach (var runtime in _runtimes) Layout(runtime);
    }

    /// <summary>ワールド距離ぶんだけ、街並みを左へ流す。層ごとの倍率がここでかかる</summary>
    public void Scroll(float worldDistance)
    {
        foreach (var runtime in _runtimes)
        {
            runtime.offset = Mathf.Repeat(
                runtime.offset + worldDistance * runtime.layer.speedFactor,
                runtime.stride);
            Layout(runtime);
        }
    }

    public void ResetScroll()
    {
        foreach (var runtime in _runtimes)
        {
            runtime.offset = Mathf.Repeat(runtime.layer.startShiftX, runtime.stride);
            Layout(runtime);
        }
    }
}