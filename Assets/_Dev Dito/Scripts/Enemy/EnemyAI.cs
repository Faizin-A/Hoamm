using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class EnemyPatrolPoint
{
    public Transform point;

    [Min(0f)]
    public float waitTime = 2f;

    [Min(0f)]
    [Tooltip("Radius random posisi PatrolPoint dari posisi aslinya.")]
    public float randomRadius = 3f;
}

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Patrol,
        Roaming,
        Chasing,
        Investigating,
        WaitingAtItem,
        Attacking
    }

    [Header("Target")]
    public Transform player;
    public Transform lookTarget;

    [Header("Detection")]
    public float detectionRadius = 10f;

    [Header("Movement")]
    public float walkSpeed = 2f;
    public float chaseSpeed = 4.5f;

    [Header("Patrol Path")]
    public List<EnemyPatrolPoint> patrolPoints =
        new List<EnemyPatrolPoint>();

    [Header("Patrol Randomization")]
    public bool randomizePatrolPositions = true;

    [Min(1)]
    public int patrolRandomAttempts = 30;

    [Header("Random Roaming")]
    public float randomPointWaitTime = 1.5f;
    public int randomSearchAttempts = 30;

    [Header("Capture Settings")]
    public float catchRadius = 1.5f;
    public float attackRotationSpeed = 5f;

    [Header("Distraction Settings")]
    public float distractionWaitTime = 20f;
    public float itemReachDistance = 0.7f;

    [Header("References")]
    public NavMeshAgent agent;
    public Animator animator;

    private EnemyState currentState = EnemyState.Patrol;

    private int currentPatrolIndex;
    private float waitEndTime;

    private bool isChasing;
    private bool isInvestigatingItem;
    private bool isAttacking;
    private bool hasDestination;
    private bool isWaitingAtWaypoint;

    private PickupableObject targetThrownItem;
    private FPSController playerController;

    private NavMeshPath testPath;

    private Vector3[] patrolDestinations;

    private Vector3[] navVertices;
    private int[] navIndices;
    private float[] triangleAreas;
    private float totalTriangleArea;

    void OnEnable()
    {
        PlayerInteractor.OnObjectThrown += HandleThrownItem;
    }

    void OnDisable()
    {
        PlayerInteractor.OnObjectThrown -= HandleThrownItem;
    }

    void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (agent == null || animator == null)
        {
            Debug.LogError("[EnemyAI] NavMeshAgent atau Animator tidak ditemukan!");
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("[EnemyAI] Enemy tidak berada di NavMesh!");
            enabled = false;
            return;
        }

        if (player != null)
        {
            playerController = player.GetComponent<FPSController>();

            if (playerController == null)
                playerController = player.GetComponentInParent<FPSController>();
        }

        testPath = new NavMeshPath();

        agent.speed = walkSpeed;
        agent.isStopped = false;
        agent.updateRotation = true;

        CacheNavMeshTriangles();

        // Buat posisi patrol random tanpa mengubah Transform asli.
        GeneratePatrolDestinations();

        currentPatrolIndex = 0;

        if (patrolPoints.Count > 0)
            MoveToCurrentPatrolPoint();
        else
            StartRandomRoaming();
    }

    void Update()
    {
        if (isAttacking)
        {
            HandleAttack();
            UpdateAnimation();
            return;
        }

        if (isInvestigatingItem)
        {
            HandleItemDistraction();
            UpdateAnimation();
            return;
        }

        if (player == null)
        {
            UpdatePatrol();
            UpdateAnimation();
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance <= detectionRadius)
        {
            if (distance <= catchRadius)
            {
                CatchPlayer();
                UpdateAnimation();
                return;
            }

            ChasePlayer();
            UpdateAnimation();
            return;
        }

        if (currentState == EnemyState.Chasing)
        {
            Debug.Log("[ENEMY] Player hilang. Kembali ke Path pertama.");

            isChasing = false;
            ResetPatrol();
        }

        UpdatePatrol();
        UpdateAnimation();
    }

    // ==========================================
    // PATROL RANDOMIZATION
    // ==========================================

    void GeneratePatrolDestinations()
    {
        patrolDestinations = new Vector3[patrolPoints.Count];

        for (int i = 0; i < patrolPoints.Count; i++)
        {
            EnemyPatrolPoint patrolPoint = patrolPoints[i];

            if (patrolPoint == null || patrolPoint.point == null)
                continue;

            Vector3 originalPosition = patrolPoint.point.position;

            patrolDestinations[i] = originalPosition;

            if (!randomizePatrolPositions ||
                patrolPoint.randomRadius <= 0f)
                continue;

            bool found = false;

            for (int attempt = 0;
                 attempt < patrolRandomAttempts;
                 attempt++)
            {
                Vector2 randomCircle = Random.insideUnitCircle *
                                      patrolPoint.randomRadius;

                Vector3 candidate = originalPosition +
                    new Vector3(randomCircle.x, 0f, randomCircle.y);

                NavMeshHit hit;

                if (!NavMesh.SamplePosition(
                    candidate,
                    out hit,
                    patrolPoint.randomRadius + 1f,
                    agent.areaMask))
                    continue;

                // Hindari posisi yang berpindah terlalu jauh secara vertikal.
                if (Mathf.Abs(hit.position.y - originalPosition.y) > 2f)
                    continue;

                if (!NavMesh.CalculatePath(
                    transform.position,
                    hit.position,
                    agent.areaMask,
                    testPath))
                    continue;

                if (testPath.status != NavMeshPathStatus.PathComplete)
                    continue;

                patrolDestinations[i] = hit.position;
                found = true;
                break;
            }

            if (!found)
            {
                Debug.LogWarning(
                    "[ENEMY] Gagal merandom Path " + (i + 1) +
                    ". Menggunakan posisi asli."
                );
            }
            else
            {
                Debug.Log(
                    "[ENEMY] Posisi random Path " + (i + 1) +
                    ": " + patrolDestinations[i]
                );
            }
        }
    }

    // ==========================================
    // PATROL SYSTEM
    // ==========================================

    void UpdatePatrol()
    {
        if (currentState == EnemyState.Patrol)
            HandleWaypointPatrol();
        else if (currentState == EnemyState.Roaming)
            HandleRandomRoaming();
    }

    void HandleWaypointPatrol()
    {
        if (patrolPoints.Count == 0)
        {
            StartRandomRoaming();
            return;
        }

        if (currentPatrolIndex >= patrolPoints.Count)
        {
            StartRandomRoaming();
            return;
        }

        EnemyPatrolPoint patrolPoint = patrolPoints[currentPatrolIndex];

        if (patrolPoint == null || patrolPoint.point == null)
        {
            currentPatrolIndex++;
            hasDestination = false;
            isWaitingAtWaypoint = false;
            return;
        }

        if (isWaitingAtWaypoint)
        {
            if (Time.time >= waitEndTime)
            {
                isWaitingAtWaypoint = false;
                currentPatrolIndex++;

                if (currentPatrolIndex >= patrolPoints.Count)
                    StartRandomRoaming();
                else
                    MoveToCurrentPatrolPoint();
            }

            return;
        }

        if (!hasDestination)
        {
            MoveToCurrentPatrolPoint();
            return;
        }

        if (IsDestinationReached())
        {
            agent.isStopped = true;
            agent.ResetPath();

            waitEndTime = Time.time + patrolPoint.waitTime;

            hasDestination = false;
            isWaitingAtWaypoint = true;

            Debug.Log(
                "[ENEMY] Sampai Path " + (currentPatrolIndex + 1) +
                ". Menunggu " + patrolPoint.waitTime + " detik."
            );
        }
    }

    void MoveToCurrentPatrolPoint()
    {
        if (currentPatrolIndex >= patrolPoints.Count)
        {
            StartRandomRoaming();
            return;
        }

        if (patrolPoints[currentPatrolIndex].point == null)
        {
            currentPatrolIndex++;
            hasDestination = false;
            return;
        }

        currentState = EnemyState.Patrol;
        isChasing = false;

        agent.speed = walkSpeed;
        agent.isStopped = false;
        agent.updateRotation = true;

        Vector3 destination = patrolDestinations != null &&
            currentPatrolIndex < patrolDestinations.Length
            ? patrolDestinations[currentPatrolIndex]
            : patrolPoints[currentPatrolIndex].point.position;

        if (agent.SetDestination(destination))
        {
            hasDestination = true;

            Debug.Log(
                "[ENEMY] Menuju Path " + (currentPatrolIndex + 1)
            );
        }
        else
        {
            hasDestination = false;
        }
    }

    bool IsDestinationReached()
    {
        if (agent.pathPending || !agent.hasPath)
            return false;

        return agent.remainingDistance <=
               agent.stoppingDistance + 0.15f;
    }

    void ResetPatrol()
    {
        currentPatrolIndex = 0;
        currentState = EnemyState.Patrol;

        hasDestination = false;
        isWaitingAtWaypoint = false;

        agent.isStopped = false;
        agent.ResetPath();

        if (patrolPoints.Count > 0)
            MoveToCurrentPatrolPoint();
        else
            StartRandomRoaming();
    }

    // ==========================================
    // RANDOM ROAMING
    // ==========================================

    void CacheNavMeshTriangles()
    {
        NavMeshTriangulation triangulation =
            NavMesh.CalculateTriangulation();

        navVertices = triangulation.vertices;
        navIndices = triangulation.indices;

        int count = navIndices.Length / 3;

        triangleAreas = new float[count];
        totalTriangleArea = 0f;

        for (int i = 0; i < count; i++)
        {
            Vector3 a = navVertices[navIndices[i * 3]];
            Vector3 b = navVertices[navIndices[i * 3 + 1]];
            Vector3 c = navVertices[navIndices[i * 3 + 2]];

            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;

            triangleAreas[i] = area;
            totalTriangleArea += area;
        }

        Debug.Log("[ENEMY] NavMesh triangles: " + count);
    }

    void StartRandomRoaming()
    {
        currentState = EnemyState.Roaming;
        isChasing = false;

        agent.speed = walkSpeed;
        agent.isStopped = false;
        agent.updateRotation = true;

        hasDestination = false;

        Debug.Log("[ENEMY] Memulai Random Roaming.");

        ChooseRandomDestination();
    }

    void HandleRandomRoaming()
    {
        if (hasDestination)
        {
            if (IsDestinationReached())
            {
                agent.isStopped = true;
                agent.ResetPath();

                hasDestination = false;
                waitEndTime = Time.time + randomPointWaitTime;
            }

            return;
        }

        if (Time.time >= waitEndTime)
            ChooseRandomDestination();
    }

    void ChooseRandomDestination()
    {
        if (navVertices == null ||
            navIndices == null ||
            navIndices.Length < 3 ||
            totalTriangleArea <= 0f)
            return;

        for (int i = 0; i < randomSearchAttempts; i++)
        {
            Vector3 randomPoint = GetRandomPointOnNavMesh();

            NavMeshHit hit;

            if (!NavMesh.SamplePosition(
                randomPoint, out hit, 0.5f, agent.areaMask))
                continue;

            if (!NavMesh.CalculatePath(
                transform.position, hit.position,
                agent.areaMask, testPath))
                continue;

            if (testPath.status != NavMeshPathStatus.PathComplete)
                continue;

            if (Vector3.Distance(transform.position, hit.position) < 2f)
                continue;

            agent.isStopped = false;

            if (agent.SetDestination(hit.position))
            {
                hasDestination = true;
                return;
            }
        }

        waitEndTime = Time.time + 2f;
        hasDestination = false;
    }

    Vector3 GetRandomPointOnNavMesh()
    {
        float randomArea = Random.value * totalTriangleArea;
        int selected = 0;
        float accumulated = 0f;

        for (int i = 0; i < triangleAreas.Length; i++)
        {
            accumulated += triangleAreas[i];

            if (randomArea <= accumulated)
            {
                selected = i;
                break;
            }
        }

        int index = selected * 3;

        Vector3 a = navVertices[navIndices[index]];
        Vector3 b = navVertices[navIndices[index + 1]];
        Vector3 c = navVertices[navIndices[index + 2]];

        float r1 = Mathf.Sqrt(Random.value);
        float r2 = Random.value;

        return (1f - r1) * a +
               (r1 * (1f - r2)) * b +
               (r1 * r2) * c;
    }

    // ==========================================
    // PLAYER CHASE
    // ==========================================

    void ChasePlayer()
    {
        currentState = EnemyState.Chasing;
        isChasing = true;

        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.updateRotation = true;

        hasDestination = true;
        agent.SetDestination(player.position);
    }

    // ==========================================
    // ITEM DISTRACTION
    // ==========================================

    void HandleThrownItem(PickupableObject thrownItem)
    {
        if (!isChasing || isAttacking || thrownItem == null)
            return;

        targetThrownItem = thrownItem;

        isInvestigatingItem = true;
        currentState = EnemyState.Investigating;

        isChasing = false;
        hasDestination = false;

        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.updateRotation = true;

        agent.SetDestination(thrownItem.transform.position);

        animator.SetBool("IsInvestigating", false);

        Debug.Log("[ENEMY] Mengejar item yang dilempar.");
    }

    void HandleItemDistraction()
    {
        if (currentState == EnemyState.WaitingAtItem)
        {
            agent.isStopped = true;

            if (Time.time >= waitEndTime)
                FinishItemDistraction();

            return;
        }

        if (targetThrownItem == null)
        {
            StartWaitingAtItem();
            return;
        }

        agent.isStopped = false;
        agent.speed = chaseSpeed;

        agent.SetDestination(targetThrownItem.transform.position);

        float distance = Vector3.Distance(
            transform.position,
            targetThrownItem.transform.position
        );

        if (!agent.pathPending && distance <= itemReachDistance)
            StartWaitingAtItem();
    }

    void StartWaitingAtItem()
    {
        currentState = EnemyState.WaitingAtItem;

        isChasing = false;

        agent.isStopped = true;
        agent.ResetPath();

        waitEndTime = Time.time + distractionWaitTime;

        animator.SetBool("IsInvestigating", true);

        Debug.Log("[ENEMY] Menunggu di item.");
    }

    void FinishItemDistraction()
    {
        if (targetThrownItem != null)
            Destroy(targetThrownItem.gameObject);

        targetThrownItem = null;
        isInvestigatingItem = false;

        animator.SetBool("IsInvestigating", false);

        Debug.Log("[ENEMY] Distraksi selesai. Kembali ke Path pertama.");

        ResetPatrol();
    }

    // ==========================================
    // PLAYER CAPTURE
    // ==========================================

    void CatchPlayer()
    {
        if (isAttacking)
            return;

        isAttacking = true;
        currentState = EnemyState.Attacking;

        isChasing = false;
        isInvestigatingItem = false;

        targetThrownItem = null;

        agent.isStopped = true;
        agent.ResetPath();

        agent.velocity = Vector3.zero;
        agent.updateRotation = false;

        animator.SetFloat("Speed", 0f);
        animator.SetBool("IsChasing", false);
        animator.SetBool("IsInvestigating", false);
        animator.SetBool("IsAttacking", true);

        if (playerController != null)
        {
            Transform cameraTarget =
                lookTarget != null ? lookTarget : transform;

            playerController.CaptureByEnemy(cameraTarget);
        }

        Debug.Log("[ENEMY] PLAYER BERHASIL DITANGKAP!");
    }

    void HandleAttack()
    {
        if (player == null)
            return;

        agent.isStopped = true;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                attackRotationSpeed * Time.deltaTime
            );
        }
    }

    public void AnimationAttackHit()
    {
        if (playerController != null)
        {
            playerController.TriggerCameraShake();
            Debug.Log("[ENEMY] Attack hit!");
        }
    }

    // ==========================================
    // ANIMATION
    // ==========================================

    void UpdateAnimation()
    {
        if (animator == null || agent == null)
            return;

        animator.SetFloat("Speed", agent.velocity.magnitude);
        animator.SetBool("IsChasing", isChasing);
    }

    // ==========================================
    // GIZMOS
    // ==========================================

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, catchRadius);

        if (patrolPoints == null)
            return;

        for (int i = 0; i < patrolPoints.Count; i++)
        {
            if (patrolPoints[i] == null || patrolPoints[i].point == null)
                continue;

            Vector3 original = patrolPoints[i].point.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(original, 0.2f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(
                original,
                patrolPoints[i].randomRadius
            );

            if (patrolDestinations != null &&
                i < patrolDestinations.Length)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(patrolDestinations[i], 0.2f);

                Gizmos.DrawLine(original, patrolDestinations[i]);
            }
        }
    }
}