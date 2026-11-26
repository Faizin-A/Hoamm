using System;
using UnityEngine;

/// <summary>
/// Sistem suara global. Siapa pun (player, item jatuh, pintu, dll)
/// bisa memanggil NoiseSystem.Emit(posisi, radius), dan enemy yang
/// cukup dekat akan mendengarnya.
/// </summary>
public static class NoiseSystem
{
    public static event Action<Vector3, float> OnNoise;

    public static void Emit(Vector3 position, float radius)
    {
        if (radius <= 0f)
            return;

        OnNoise?.Invoke(position, radius);
    }
}