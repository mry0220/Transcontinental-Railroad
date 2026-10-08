using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 横スクロールの多層背景。spriteを画面幅に足りる枚数だけ並べ、左へ流す
/// このオブジェクト自体は、Scaleを１にしておくこと
/// </summary>

public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public Sprite sprite;
        [Tooltip("列車の速度に対する倍率。遠景ほど小さくする")]
        public float speedFactor = 1f;
        [Tooltip("ワールド座標のY")]
        public float y = 0f;
        [Tooltip("タイル同士の間隔")]
        public float gapX = 0f;
        [Tooltip("初期のXずらし量。正の値で左へずれる。層ごとに変え継ぎ目をそろえない")]
        public float startShiftX = 0f;
        

        [Tooltip("負の値ほど後ろ。列車や敵のSpriteより小さくすること")]
        public int sortingOrder = -10;
    }

    private class LayerRuntime
    {
        public Layer layer;
        public readonly List<Transform> tiles = new();
        public float tileWidth;
        public float stride;
        public float offset;
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private List<Layer> layers = new();

    private readonly List<LayerRuntime> _runtimes = new();

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

        

        float cameraWidth = targetCamera.orthographicSize * 2f * targetCamera.aspect;

        foreach (var layer in layers)
        {
            if (layer.sprite == null) continue;

            var runtime = new LayerRuntime
            {
                layer = layer,
                tileWidth = layer.sprite.bounds.size.x,
            };
            runtime.stride = Mathf.Max(0.01f, runtime.tileWidth + layer.gapX);
            runtime.offset = Mathf.Repeat(layer.startShiftX, runtime.stride);

            int count = Mathf.CeilToInt(cameraWidth / runtime.stride) + 3;
            for(int i = 0; i<count;i++)
            {
                var tileObject = new GameObject($"{layer.sprite.name}_{i}");
                tileObject.transform.SetParent(transform, false);

                var renderer = tileObject.AddComponent<SpriteRenderer>();
                renderer.sprite = layer.sprite;
                renderer.sortingOrder = layer.sortingOrder;

                runtime.tiles.Add(tileObject.transform);
            }

            _runtimes.Add(runtime);
            Layout(runtime);
        }
    }

    private void Layout(LayerRuntime runtime)
    {
        float cameraWidth = targetCamera.orthographicSize * 2f * targetCamera.aspect;
        float startX = targetCamera.transform.position.x - cameraWidth / 2f - runtime.stride;

        for(int i =0;i<runtime.tiles.Count;i++)
        {
            float x = startX + i * runtime.stride - runtime.offset;
            runtime.tiles[i].position = new Vector3(x, runtime.layer.y, transform.position.z);
        }
    }

    ///<summary> ワールド距離ぶんだけ、背景を左へ流す。層ごとの倍率がここでかかる</summary>

    public void Scroll(float worldDistance)
    {
        foreach(var runtime in _runtimes)
        {
            runtime.offset = Mathf.Repeat(
                runtime.offset + worldDistance * runtime.layer.speedFactor,
                runtime.stride);
            Layout(runtime);
        }
    }

    public void ResetScroll()
    {
        foreach(var runtime in _runtimes)
        {
            runtime.offset = Mathf.Repeat(runtime.layer.startShiftX,runtime.stride);
            Layout(runtime);
        }
    }

}
