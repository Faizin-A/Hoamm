using UnityEngine;

public class PadlockInteractor : InteractableObject
{
    private const string PromptOpen = "[ LMB ] Buka Gembok";
    private const string PromptOpened = "Gembok sudah terbuka";
    private const string MsgNoPlayer = "Player Interactor belum diatur";
    private const string MsgNoKey = "Tidak memegang kunci";
    private const string MsgWrongKey = "Kunci tidak sesuai";

    [Header("Padlock Settings")]
    public KeyColor lockColor;

    [Tooltip("Jika aktif, kunci hilang setelah berhasil membuka gembok.")]
    public bool consumeKeyOnUnlock = true;

    [Header("Padlock Visual")]
    public GameObject lockedPadlock;
    public GameObject unlockedPadlock;

    [Header("References")]
    [Tooltip("Dicari otomatis jika dikosongkan.")]
    public PlayerInteractor playerInteractor;
    public Collider interactionCollider;

    private bool isUnlocked;

    void Awake()
    {
        interactionName = "Gembok " + lockColor;

        if (playerInteractor == null)
            playerInteractor = FindObjectOfType<PlayerInteractor>();

        if (interactionCollider == null)
            interactionCollider = GetComponent<Collider>();
    }

    void Start()
    {
        SetPadlockVisual(false);
    }

    // ==========================================
    // UI
    // ==========================================

    public override string GetInteractionPrompt()
    {
        if (isUnlocked)
            return PromptOpened;

        string error = ValidateKey(out _);

        return error ?? PromptOpen;
    }

    // ==========================================
    // INTERACTION
    // ==========================================

    public override void Interact()
    {
        if (isUnlocked)
            return;

        string error = ValidateKey(out KeyItem key);

        if (error != null)
        {
            Debug.Log("[PADLOCK] " + error);
            return;
        }

        Unlock();

        if (consumeKeyOnUnlock)
            playerInteractor.ConsumeHeldKey();

        base.Interact();
    }

    // Mengembalikan pesan error, atau null jika kunci yang dipegang cocok.
    private string ValidateKey(out KeyItem key)
    {
        key = null;

        if (playerInteractor == null)
            return MsgNoPlayer;

        key = playerInteractor.HeldKey;

        if (key == null)
            return MsgNoKey;

        if (key.keyColor != lockColor)
            return MsgWrongKey;

        return null;
    }

    private void Unlock()
    {
        isUnlocked = true;

        SetPadlockVisual(true);

        if (interactionCollider != null)
            interactionCollider.enabled = false;

        Debug.Log("[PADLOCK] Gembok " + lockColor + " berhasil dibuka!");
    }

    private void SetPadlockVisual(bool unlocked)
    {
        if (lockedPadlock != null)
            lockedPadlock.SetActive(!unlocked);

        if (unlockedPadlock != null)
            unlockedPadlock.SetActive(unlocked);
    }
}