using UnityEngine;
using UnityEngine.AI;

public class DoorController : InteractableObject
{
    [Header("Door Settings")]
    public float openAngle = 90f;
    public float rotationSpeed = 180f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private NavMeshObstacle obstacle;

    private bool isOpen;
    public bool IsOpen => isOpen;

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        obstacle = GetComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.enabled = !isOpen;   // tertutup = menghalangi
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

    public override void Interact()
    {
        ToggleDoor();
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
        obstacle.enabled = !isOpen;

        Debug.Log(isOpen ? "[DOOR] Pintu dibuka." : "[DOOR] Pintu ditutup.");
    }
}