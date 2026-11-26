using UnityEngine;

/// <summary>
/// Pasang di object Player (yang sama dengan FPSController).
/// Memancarkan suara langkah berdasarkan kecepatan gerak dan status crouch.
/// Tidak perlu mengubah FPSController.
/// </summary>
public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("Crouch Detection")]
    [Tooltip("CharacterController player. Crouch dideteksi dari tinggi controller yang mengecil.")]
    public CharacterController controller;

    [Range(0.5f, 0.95f)]
    [Tooltip("Dianggap jongkok kalau tinggi controller < tinggi awal x nilai ini.")]
    public float crouchHeightRatio = 0.85f;

    [Tooltip("Dipakai kalau Controller kosong. Memakai Input lama (Input Manager).")]
    public KeyCode fallbackCrouchKey = KeyCode.LeftControl;

    [Header("Noise Radius (meter)")]
    public float crouchRadius = 3f;
    public float walkRadius = 8f;
    public float runRadius = 16f;

    [Header("Movement Thresholds")]
    [Tooltip("Di bawah kecepatan ini dianggap diam (tidak ada suara).")]
    public float minMoveSpeed = 0.3f;

    [Tooltip("Di atas kecepatan ini dianggap lari. Atur di antara kecepatan jalan dan lari player.")]
    public float runSpeedThreshold = 4f;

    [Header("Footstep Interval (detik)")]
    public float crouchInterval = 0.7f;
    public float walkInterval = 0.45f;
    public float runInterval = 0.3f;

    // Info untuk debug.
    public bool IsCrouching { get; private set; }
    public float CurrentSpeed { get; private set; }
    public float CurrentNoiseRadius { get; private set; }

    private Vector3 lastPosition;
    private float standingHeight;
    private float nextNoiseTime;

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (controller != null)
            standingHeight = controller.height;

        lastPosition = transform.position;
    }

    void Update()
    {
        if (Time.deltaTime <= 0f)
            return;

        // Kecepatan horizontal dari perubahan posisi.
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;

        CurrentSpeed = delta.magnitude / Time.deltaTime;
        lastPosition = transform.position;

        IsCrouching = DetectCrouch();

        if (CurrentSpeed < minMoveSpeed)
        {
            CurrentNoiseRadius = 0f;
            return;
        }

        float radius;
        float interval;

        if (IsCrouching)
        {
            radius = crouchRadius;
            interval = crouchInterval;
        }
        else if (CurrentSpeed >= runSpeedThreshold)
        {
            radius = runRadius;
            interval = runInterval;
        }
        else
        {
            radius = walkRadius;
            interval = walkInterval;
        }

        CurrentNoiseRadius = radius;

        if (Time.time >= nextNoiseTime)
        {
            NoiseSystem.Emit(transform.position, radius);
            nextNoiseTime = Time.time + interval;
        }
    }

    bool DetectCrouch()
    {
        if (controller != null && standingHeight > 0f)
            return controller.height < standingHeight * crouchHeightRatio;

        return Input.GetKey(fallbackCrouchKey);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, crouchRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, walkRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, runRadius);
    }
}