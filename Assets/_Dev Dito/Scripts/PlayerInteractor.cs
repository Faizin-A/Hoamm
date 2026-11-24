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
    private PickupableObject heldObject;
    private Rigidbody heldRigidbody;

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
    }

    // Mengikuti posisi kamera setelah kamera selesai bergerak
    void LateUpdate()
    {
        if (heldObject == null || holdPoint == null)
            return;

        heldObject.transform.SetPositionAndRotation(
            holdPoint.position,
            holdPoint.rotation
        );
    }

    // Mendeteksi object di depan kamera
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

    // Menjalankan interaksi
    void TryInteract()
    {
        if (currentTarget == null)
            return;

        PickupableObject pickupable =
            currentTarget as PickupableObject;

        if (pickupable != null)
        {
            // Tangan sudah penuh, tidak bisa mengambil barang lain.
            if (heldObject != null)
                return;

            if (TryPickup(pickupable))
            {
                currentTarget = null;
            }
        }
        else
        {
            currentTarget.Interact();
            currentTarget = null;
        }
    }

    // Mengambil object
    bool TryPickup(PickupableObject pickupable)
    {
        if (holdPoint == null || playerCamera == null)
        {
            Debug.LogError(
                "Player Camera atau Hold Point belum diatur!"
            );
            return false;
        }

        Rigidbody rb = pickupable.GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError(
                "Object tidak memiliki Rigidbody!"
            );
            return false;
        }

        heldObject = pickupable;
        heldRigidbody = rb;

        // Simpan kondisi awal Rigidbody
        originalIsKinematic = rb.isKinematic;
        originalUseGravity = rb.useGravity;
        originalDetectCollisions = rb.detectCollisions;
        originalInterpolation = rb.interpolation;

        // Simpan Collider object dan anak-anaknya
        heldColliders =
            pickupable.GetComponentsInChildren<Collider>(true);

        originalColliderStates = new bool[heldColliders.Length];

        for (int i = 0; i < heldColliders.Length; i++)
        {
            originalColliderStates[i] =
                heldColliders[i].enabled;

            heldColliders[i].enabled = false;
        }

        // Hentikan gerakan object
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Matikan physics selama dipegang
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;

        // Tidak membutuhkan interpolation saat mengikuti kamera
        rb.interpolation = RigidbodyInterpolation.None;

        // Langsung posisikan object di HoldPoint
        pickupable.transform.SetPositionAndRotation(
            holdPoint.position,
            holdPoint.rotation
        );

        Debug.Log(
            "[PICKUP] Mengambil: " + pickupable.interactionName
        );

        return true;
    }

    // Melempar object
    void ThrowObject()
    {
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

        // Memberitahu Enemy bahwa ada item yang dilempar
        OnObjectThrown?.Invoke(thrownObject);

        Debug.Log("[THROW] Melempar: " + objectName);
    }

    // Menjatuhkan object
    void DropObject()
    {
        if (heldObject == null)
            return;

        Rigidbody rb = heldRigidbody;

        ReleaseHeldObject(false);

        if (rb != null)
        {
            rb.AddForce(
                playerCamera.transform.forward * dropForwardForce,
                ForceMode.VelocityChange
            );
        }

        Debug.Log("[DROP] Menjatuhkan object");
    }

    // Mengembalikan object ke sistem fisika
    void ReleaseHeldObject(bool isThrowing)
    {
        if (heldObject == null || heldRigidbody == null)
            return;

        Rigidbody rb = heldRigidbody;

        // Sinkronkan posisi terakhir object
        rb.position = heldObject.transform.position;
        rb.rotation = heldObject.transform.rotation;

        // Pulihkan pengaturan Rigidbody
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

        // Pulihkan Collider
        for (int i = 0; i < heldColliders.Length; i++)
        {
            if (heldColliders[i] != null)
            {
                heldColliders[i].enabled =
                    originalColliderStates[i];
            }
        }

        heldObject = null;
        heldRigidbody = null;

        heldColliders = null;
        originalColliderStates = null;
    }

    // UI interaksi
    void OnGUI()
    {
        if (promptStyle == null)
            return;

        promptStyle.fontSize = fontSize;
        promptStyle.normal.textColor = textColor;

        string message = "";

        if (heldObject != null)
        {
            message = throwPrompt + "\n" + dropPrompt;

            // Tampilkan prompt interaksi (misalnya pintu) jika ada.
            if (currentTarget != null && !(currentTarget is PickupableObject))
            {
                message += "\n" + currentTarget.interactionPrompt;
            }
        }
        else if (currentTarget != null)
        {
            message = currentTarget is PickupableObject
                ? currentTarget.pickupPrompt
                : currentTarget.interactionPrompt;
        }

        if (string.IsNullOrEmpty(message))
            return;

        float boxWidth = Screen.width * 0.8f;
        float boxHeight = 100f;

        float x = (Screen.width - boxWidth) / 2f;
        float y = Screen.height / 2f;

        GUI.Label(
            new Rect(x, y, boxWidth, boxHeight),
            message,
            promptStyle
        );
    }

    // Visualisasi jangkauan interaksi
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
