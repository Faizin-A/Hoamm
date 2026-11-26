using UnityEngine;
using UnityEngine.AI;

public class DoorController : InteractableObject, IEnemyOpenable
{
    [Header("Door Settings")]
    public float openAngle = 90f;
    public float rotationSpeed = 180f;

    [Header("Noise (dipancarkan saat PLAYER memakai pintu)")]
    public float openNoiseRadius = 6f;
    public float closeNoiseRadius = 10f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private NavMeshObstacle obstacle;

    private bool isOpen;
    public bool IsOpen => isOpen;

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, 0f, openAngle);

        obstacle = GetComponent<NavMeshObstacle>();

        if (obstacle != null)
        {
            obstacle.carving = true;
            obstacle.enabled = !isOpen;   // tertutup = menghalangi
        }
        else
        {
            Debug.LogWarning("[DOOR] NavMeshObstacle tidak ditemukan di " + name);
        }
    }

    void Update()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    // Dipanggil PLAYER.
    public override void Interact()
    {
        ToggleDoor();

        // Player memakai pintu = bersuara. Menutup lebih keras.
        NoiseSystem.Emit(
            transform.position,
            isOpen ? openNoiseRadius : closeNoiseRadius
        );
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;

        if (obstacle != null)
            obstacle.enabled = !isOpen;

        Debug.Log(isOpen ? "[DOOR] Pintu dibuka." : "[DOOR] Pintu ditutup.");
    }

    // Dipanggil ENEMY. Sengaja TIDAK memancarkan suara.
    public void OpenByEnemy()
    {
        if (isOpen)
            return;

        isOpen = true;

        if (obstacle != null)
            obstacle.enabled = false;   // buka jalur NavMesh

        Debug.Log("[DOOR] Pintu dibuka enemy.");
    }
}