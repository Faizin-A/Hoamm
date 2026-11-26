using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class KeyItem : InteractableObject
{
    [Header("Key Settings")]
    public KeyColor keyColor;

    void Awake()
    {
        interactionName = "Kunci " + keyColor;
        pickupPrompt = "[ LMB ] Ambil Kunci";
    }
}