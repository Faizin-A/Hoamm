using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupableObject : InteractableObject
{
    // Script ini menandai object yang bisa diambil.
    // Rigidbody wajib tersedia pada object.
}
