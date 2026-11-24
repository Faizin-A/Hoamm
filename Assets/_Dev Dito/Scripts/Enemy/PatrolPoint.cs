using UnityEngine;

public class PatrolPoint : MonoBehaviour
{
    [Header("Patrol Settings")]
    [Min(0f)]
    public float waitTime = 2f;

    [Header("Gizmos")]
    public Color gizmoColor = Color.cyan;
    public float gizmoSize = 0.25f;

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoSize);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            gizmoSize + 0.15f
        );
    }
}