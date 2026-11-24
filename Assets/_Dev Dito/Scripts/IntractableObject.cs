using UnityEngine;
using UnityEngine.Events;

public class InteractableObject : MonoBehaviour
{
    [Header("Interaction Settings")]
    public string interactionName = "Object";

    [TextArea(1, 3)]
    public string interactionPrompt = "[E] Interaksi";

    [TextArea(1, 3)]
    public string pickupPrompt = "[E] Ambil";

    public UnityEvent onInteract;

    public virtual void Interact()
    {
        Debug.Log("[INTERACTION] Interacting with: " + interactionName);

        onInteract?.Invoke();
    }
}
