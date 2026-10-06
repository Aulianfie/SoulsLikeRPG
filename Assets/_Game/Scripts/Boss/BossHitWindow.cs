using System;
using UnityEngine;

[Serializable]
public struct BossHitWindow
{
    [SerializeField, Range(0, 1)]
    private float _start;
    [SerializeField, Range(0, 1)]
    private float _end;

    public float Start => _start;
    public float End => _end;
}
