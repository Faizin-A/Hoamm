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

    // Teks yang ditampilkan di UI saat objek ini ditargetkan.
    // Override jika teksnya perlu berubah sesuai kondisi (contoh: gembok).
    public virtual string GetInteractionPrompt()
    {
        return interactionPrompt;
    }

    public virtual void Interact()
    {
        Debug.Log("[INTERACTION] Interacting with: " + interactionName);

        onInteract?.Invoke();
    }
}