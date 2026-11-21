using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Hit Event")]
public class HitEventChannel : ScriptableObject
{
    public event Action<Vector3, Vector3> OnRaised; // point, normal
    public void Raise(Vector3 point, Vector3 normal) => OnRaised?.Invoke(point, normal);
}