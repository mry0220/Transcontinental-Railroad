using System;
using UnityEngine;

public sealed class RNG
{
    private readonly System.Random _Random;

    public RNG(int? seed = null)
    {
        int s = seed ?? 12345;

        _Random = new System.Random(s);

        Debug.Log($"[RNG] seed = {s}");
    }

    public int Next() => _Random.Next();

    public float NextFloat() => (float)_Random.NextDouble(); //[0,1]

    public float Range(float min, float max) => min + (max - min) * NextFloat(); //[min,max])
}