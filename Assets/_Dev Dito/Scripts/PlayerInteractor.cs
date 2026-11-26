using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    public static event System.Action<PickupableObject> OnObjectThrown;

    [Header("Interaction Settings")]
    public Camera playerCamera;
    public float interactionRange = 3f;
    public KeyCode interactionKey = KeyCode.E;
    public LayerMask interactionLayer = ~0;

    [Header("Pickup Settings")]
    public Transform holdPoint;
    public float throwForce = 12f;
    public float dropForwardForce = 1f;

    [Header("Pickup Settings")]
    public float dropClearanceRadius = 0.1f;   // tambahkan di bawah dropForwardForce
    private CollisionDetectionMode originalCollisionMode;  // tambahkan di dekat originalInterpolation

    [Header("Input Settings")]
    public KeyCode throwKey = KeyCode.Mouse0;
    public KeyCode dropKey = KeyCode.Mouse1;

    [Header("Detection Settings")]
    [Range(0.01f, 0.2f)]
    public float detectionInterval = 0.03f;

    [Range(0.01f, 0.5f)]
    public float targetGracePeriod = 0.2f;

    [Range(0f, 0.15f)]
    public float raycastRadius = 0.05f;

    [Header("UI Settings")]
    public int fontSize = 24;
    public Color textColor = Color.white;

    [TextArea(1, 3)]
    public string throwPrompt = "[KLIK KIRI] Lempar";

    [TextArea(1, 3)]
    public string dropPrompt = "[KLIK KANAN] Jatuhkan";

    private InteractableObject currentTarget;

    // Objek distraksi yang sedang dipegang.
    private PickupableObject heldObject;

    // Kunci yang sedang dipegang.
    private KeyItem heldKey;

    // Referensi umum objek yang sedang dipegang.
    private InteractableObject heldItem;
    private Rigidbody heldRigidbody;

    public PickupableObject HeldObject => heldObject;
    public KeyItem HeldKey => heldKey;

    private Collider[] heldColliders;
    private bool[] originalColliderStates;

    private bool originalIsKinematic;
    private bool originalUseGravity;
    private bool originalDetectCollisions;

    private RigidbodyInterpolation originalInterpolation;

    private float nextDetectionTime;
    private float lastTargetSeenTime;

    private GUIStyle promptStyle;

    void Awake()
    {
        promptStyle = new GUIStyle();
        promptStyle.fontSize = fontSize;
        promptStyle.fontStyle = FontStyle.Bold;
        promptStyle.alignment = TextAnchor.MiddleCenter;
        promptStyle.wordWrap = true;
    }

    void Update()
    {
        DetectInteractable();

        if (Input.GetKeyDown(interactionKey))
        {
            TryInteract();
        }

        // Objek distraksi: bisa dilempar atau dijatuhkan.
        if (heldObject != null)
        {
            if (Input.GetKeyDown(throwKey))
            {
                ThrowObject();
            }
            else if (Input.GetKeyDown(dropKey))
            {
                DropObject();
            }
        }
        // Kunci: hanya bisa dijatuhkan.
        else if (heldKey != null)
        {
            if (Input.GetKeyDown(dropKey))
            {
                DropObject();
            }
        }
    }

    void LateUpdate()
    {
        if (heldItem == null || holdPoint == null)
            return;

        heldItem.transform.SetPositionAndRotation(
            holdPoint.position,
            holdPoint.rotation
        );
    }

    // ==========================================
    // DETECTION
    // ==========================================

    void DetectInteractable()
    {
        if (playerCamera == null)
            return;

        if (Time.time < nextDetectionTime)
            return;

        nextDetectionTime = Time.time + detectionInterval;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        InteractableObject detectedTarget = null;

        if (Physics.SphereCast(
            ray,
            raycastRadius,
            out RaycastHit hit,
            interactionRange,
            interactionLayer,
            QueryTriggerInteraction.Ignore))
        {
            detectedTarget =
                hit.collider.GetComponentInParent<InteractableObject>();
        }

        if (detectedTarget != null)
        {
            currentTarget = detectedTarget;
            lastTargetSeenTime = Time.time;
        }
        else if (currentTarget != null)
        {
            if (Time.time - lastTargetSeenTime > targetGracePeriod)
            {
                currentTarget = null;
            }
        }
    }

    // ==========================================
    // INTERACTION
    // ==========================================

    // Objek yang diambil ke tangan (bukan di-Interact).
    static bool IsPickupTarget(InteractableObject target)
    {
        return target is PickupableObject || target is KeyItem;
    }

    void TryInteract()
    {
        if (currentTarget == null)
            return;

        // Tangan penuh: tidak boleh mengambil objek lain,
        // tetapi tetap boleh berinteraksi dengan pintu, gembok, dll.
        if (heldItem != null)
        {
            if (IsPickupTarget(currentTarget))
                return;

            currentTarget.Interact();
            currentTarget = null;
            return;
        }

        if (currentTarget is KeyItem key)
        {
            if (TryPickupKey(key))
                currentTarget = null;

            return;
        }

        if (currentTarget is PickupableObject pickupable)
        {
            if (TryPickup(pickupable))
                currentTarget = null;

            return;
        }

        currentTarget.Interact();
        currentTarget = null;
    }

    // ==========================================
    // PICKUP
    // ==========================================

    bool TryPickup(PickupableObject pickupable)
    {
        if (!PreparePickup(pickupable))
            return false;

        heldObject = pickupable;

        Debug.Log(
            "[PICKUP] Mengambil: " + pickupable.interactionName
        );

        return true;
    }

    bool TryPickupKey(KeyItem key)
    {
        if (!PreparePickup(key))
            return false;

        heldKey = key;

        Debug.Log(
            "[KEY] Mengambil kunci: " + key.keyColor
        );

        return true;
    }

    // Fungsi pickup umum untuk kunci dan objek distraksi.
    bool PreparePickup(InteractableObject item)
    {
        if (holdPoint == null || playerCamera == null)
        {
            Debug.LogError(
                "Player Camera atau Hold Point belum diatur!"
            );

            return false;
        }

        Rigidbody rb = item.GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError(
                item.interactionName + " tidak memiliki Rigidbody!"
            );

            return false;
        }

        heldItem = item;
        heldRigidbody = rb;

        // Simpan kondisi awal Rigidbody.
        originalIsKinematic = rb.isKinematic;
        originalUseGravity = rb.useGravity;
        originalDetectCollisions = rb.detectCollisions;
        originalInterpolation = rb.interpolation;
        originalCollisionMode = rb.collisionDetectionMode;

        // Simpan Collider object dan anak-anaknya.
        heldColliders =
            item.GetComponentsInChildren<Collider>(true);

        originalColliderStates = new bool[heldColliders.Length];

        for (int i = 0; i < heldColliders.Length; i++)
        {
            originalColliderStates[i] =
                heldColliders[i].enabled;

            heldColliders[i].enabled = false;
        }

        // Hentikan gerakan object.
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Matikan physics selama dipegang.
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;

        rb.interpolation = RigidbodyInterpolation.None;

        // Posisikan object di HoldPoint.
        item.transform.SetPositionAndRotation(
            holdPoint.position,
            holdPoint.rotation
        );

        return true;
    }

    // ==========================================
    // THROW - KHUSUS OBJEK DISTRAKSI
    // ==========================================

    void ThrowObject()
    {
        // Kunci tidak boleh dilempar.
        if (heldObject == null)
            return;

        Rigidbody rb = heldRigidbody;
        PickupableObject thrownObject = heldObject;

        string objectName = heldObject.interactionName;

        ReleaseHeldObject(true);

        if (rb != null)
        {
            rb.AddForce(
                playerCamera.transform.forward * throwForce,
                ForceMode.VelocityChange
            );
        }

        // Memberitahu Enemy bahwa ada item yang dilempar.
        OnObjectThrown?.Invoke(thrownObject);

        Debug.Log("[THROW] Melempar: " + objectName);
    }

    // ==========================================
    // DROP - UNTUK KUNCI DAN OBJEK DISTRAKSI
    // ==========================================

    void DropObject()
    {
        if (heldItem == null)
            return;

        string itemName = heldItem.interactionName;
        Rigidbody rb = heldRigidbody;

        ReleaseHeldObject(false);

        if (rb != null)
        {
            rb.AddForce(
                playerCamera.transform.forward * dropForwardForce,
                ForceMode.VelocityChange
            );
        }

        Debug.Log("[DROP] Menjatuhkan: " + itemName);
    }

    // ==========================================
    // CONSUME - DIPANGGIL GEMBOK SETELAH TERBUKA
    // ==========================================

    // Menghapus kunci yang sedang dipegang (kunci habis dipakai).
    public void ConsumeHeldKey()
    {
        if (heldKey == null)
            return;

        KeyItem key = heldKey;

        ClearHeldState();

        Destroy(key.gameObject);
    }

    // ==========================================
    // RELEASE
    // ==========================================

    void ReleaseHeldObject(bool isThrowing)
    {
        if (heldItem == null || heldRigidbody == null)
            return;

        Rigidbody rb = heldRigidbody;

        // Sinkronkan posisi terakhir object.
        // Sebelumnya:
        // rb.position = heldItem.transform.position;
        // rb.rotation = heldItem.transform.rotation;

        // Sekarang:
        Vector3 safePosition = GetSafeReleasePosition();
        heldItem.transform.position = safePosition;
        rb.position = safePosition;
        rb.rotation = heldItem.transform.rotation;

        // Pulihkan pengaturan Rigidbody.
        rb.detectCollisions = originalDetectCollisions;
        rb.interpolation = originalInterpolation;

        if (isThrowing)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        else
        {
            rb.isKinematic = originalIsKinematic;
            rb.useGravity = originalUseGravity;
        }

        // Cegah objek kecil menembus lantai.
        if (!rb.isKinematic)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxDepenetrationVelocity = 2f;
        }
        else
        {
            rb.collisionDetectionMode = originalCollisionMode;
        }

        // Pulihkan Collider.
        for (int i = 0; i < heldColliders.Length; i++)
        {
            if (heldColliders[i] != null)
            {
                heldColliders[i].enabled =
                    originalColliderStates[i];
            }
        }

        ClearHeldState();
    }

    // Jika jalur dari kamera ke HoldPoint terhalang dinding/lantai,
    // letakkan objek tepat sebelum penghalang itu.
    Vector3 GetSafeReleasePosition()
    {
        Vector3 origin = playerCamera.transform.position;
        Vector3 target = holdPoint.position;

        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance < 0.001f)
            return target;

        direction /= distance;

        if (Physics.SphereCast(
            origin,
            dropClearanceRadius,
            direction,
            out RaycastHit hit,
            distance,
            interactionLayer,
            QueryTriggerInteraction.Ignore))
        {
            return origin + direction * hit.distance;
        }

        return target;
    }

    void ClearHeldState()
    {
        heldItem = null;
        heldObject = null;
        heldKey = null;
        heldRigidbody = null;

        heldColliders = null;
        originalColliderStates = null;
    }

    // ==========================================
    // UI
    // ==========================================

    // Teks untuk target yang sedang dilihat.
    string GetTargetPrompt()
    {
        if (currentTarget == null)
            return "";

        if (IsPickupTarget(currentTarget))
            return heldItem != null ? "" : currentTarget.pickupPrompt;

        return currentTarget.GetInteractionPrompt();
    }

    // Gabungan prompt untuk item yang dipegang dan target yang dilihat.
    string BuildPromptMessage()
    {
        string message = "";

        if (heldObject != null)
            message = throwPrompt + "\n" + dropPrompt;
        else if (heldKey != null)
            message = dropPrompt;

        string targetPrompt = GetTargetPrompt();

        if (!string.IsNullOrEmpty(targetPrompt))
        {
            message = string.IsNullOrEmpty(message)
                ? targetPrompt
                : message + "\n" + targetPrompt;
        }

        return message;
    }

    void OnGUI()
    {
        if (promptStyle == null)
            return;

        promptStyle.fontSize = fontSize;
        promptStyle.normal.textColor = textColor;

        string message = BuildPromptMessage();

        if (string.IsNullOrEmpty(message))
            return;

        float boxWidth = Screen.width * 0.8f;
        float boxHeight = 120f;

        float x = (Screen.width - boxWidth) / 2f;
        float y = Screen.height / 2f;

        GUI.Label(
            new Rect(x, y, boxWidth, boxHeight),
            message,
            promptStyle
        );
    }

    // ==========================================
    // GIZMOS
    // ==========================================

    void OnDrawGizmos()
    {
        if (playerCamera == null)
            return;

        Gizmos.color = Color.green;

        Gizmos.DrawRay(
            playerCamera.transform.position,
            playerCamera.transform.forward * interactionRange
        );
    }
}