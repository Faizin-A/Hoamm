using UnityEngine;

// Pasang di GameObject senter (yang memiliki komponen Light)
public class FlashlightController : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("Biasanya Main Camera")]
    public Transform target;

    [Tooltip("Makin kecil makin terasa mengayun, makin besar makin responsif")]
    public float followSpeed = 12f;

    [Header("Flashlight Settings")]
    [Tooltip("Kosongkan jika Light ada di GameObject ini atau child-nya")]
    public Light flashlight;

    public bool startOn = true;

    [Min(0.01f)]
    public float fadeDuration = 0.25f;

    [Header("Wall Dimming")]
    [Tooltip("Layer yang dianggap penghalang. Keluarkan layer Player")]
    public LayerMask wallMask = ~0;

    [Tooltip("Pada jarak ini atau lebih dekat, cahaya paling redup")]
    public float nearDistance = 0.5f;

    [Tooltip("Pada jarak ini atau lebih jauh, cahaya terang penuh")]
    public float farDistance = 4f;

    [Tooltip("Sisa terang saat menempel tembok (0.15 = 15%)")]
    [Range(0f, 1f)]
    public float nearIntensity = 0.15f;

    [Tooltip("Tambahan lebar sorot saat dekat tembok")]
    public float nearAngleBonus = 10f;

    [Tooltip("Kecepatan penyesuaian terang")]
    public float adaptSpeed = 6f;

    [Header("Input Settings")]
    public KeyCode toggleKey = KeyCode.F;

    private Quaternion currentRotation;
    private bool isOn;
    private float maxIntensity;
    private float baseAngle;
    private float fade;
    private float closeness; // 0 = jauh, 1 = menempel tembok

    void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        if (flashlight == null)
        {
            flashlight = GetComponentInChildren<Light>();
        }

        if (target == null || flashlight == null)
        {
            Debug.LogError("Target atau Light senter belum dihubungkan!");
            enabled = false;
            return;
        }

        currentRotation = target.rotation;

        maxIntensity = flashlight.intensity;
        baseAngle = flashlight.spotAngle;
        isOn = startOn;
        fade = isOn ? 1f : 0f;
        flashlight.intensity = maxIntensity * fade;
        flashlight.enabled = isOn;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isOn = !isOn;
            Debug.Log(isOn ? "[FLASHLIGHT] ON" : "[FLASHLIGHT] OFF");
        }

        float targetFade = isOn ? 1f : 0f;
        fade = Mathf.MoveTowards(fade, targetFade, Time.deltaTime / fadeDuration);
    }

    // LateUpdate: dijalankan setelah kamera selesai bergerak
    void LateUpdate()
    {
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        currentRotation = Quaternion.Slerp(currentRotation, target.rotation, t);
        transform.rotation = currentRotation;

        UpdateWallDimming();
        ApplyLight();
    }

    void UpdateWallDimming()
    {
        float distance = farDistance;

        // SphereCast agar tidak berkedip di sudut atau celah sempit
        if (Physics.SphereCast(target.position, 0.1f, transform.forward,
                out RaycastHit hit, farDistance, wallMask,
                QueryTriggerInteraction.Ignore))
        {
            distance = hit.distance;
        }

        float targetCloseness = 1f - Mathf.InverseLerp(nearDistance, farDistance, distance);
        float t = 1f - Mathf.Exp(-adaptSpeed * Time.deltaTime);
        closeness = Mathf.Lerp(closeness, targetCloseness, t);
    }

    void ApplyLight()
    {
        float wallFactor = Mathf.Lerp(1f, nearIntensity, closeness);

        flashlight.intensity = maxIntensity * wallFactor * Mathf.SmoothStep(0f, 1f, fade);
        flashlight.spotAngle = baseAngle + nearAngleBonus * closeness;
        flashlight.enabled = fade > 0f;
    }
}