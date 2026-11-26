using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

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
        Searching,
        InvestigatingNoise,
        Investigating,
        WaitingAtItem,
        Attacking
    }

    [Header("Target")]
    public Transform player;
    public Transform lookTarget;

    [Header("Vision (Penglihatan)")]
    [Tooltip("Titik mata enemy (opsional). Kalau kosong, dipakai posisi enemy + Eye Height.")]
    public Transform eyePoint;

    public float eyeHeight = 1.6f;

    [FormerlySerializedAs("detectionRadius")]
    [Tooltip("Jarak pandang maksimum.")]
    public float viewDistance = 12f;

    [Range(10f, 360f)]
    [Tooltip("Total sudut pandang (derajat). 110 = 55 derajat ke kiri & kanan.")]
    public float viewAngle = 110f;

    [Tooltip("Jarak dekat di mana enemy 'merasakan' player walau di belakang (tetap tidak tembus tembok).")]
    public float proximityRadius = 2.5f;

    [Tooltip("Layer yang menghalangi pandangan (tembok, pintu, dll). " +
             "JANGAN centang layer Player atau Enemy.")]
    public LayerMask obstacleMask = 1; // Default layer

    [Tooltip("Tinggi titik-titik di tubuh player yang dicek (dari posisi kaki player).")]
    public float[] playerCheckHeights = new float[] { 0.5f, 1.4f };

    [Header("Lost Sight / Searching")]
    [Tooltip("Enemy masih mengejar ke posisi terakhir player selama waktu ini setelah kehilangan pandangan.")]
    public float loseSightDelay = 0.75f;

    [Tooltip("Lama enemy celingukan di posisi terakhir player sebelum menyerah.")]
    public float searchWaitTime = 5f;

    public float searchLookSpeed = 90f;

    [Header("Hearing (Pendengaran)")]
    public bool enableHearing = true;

    [Tooltip("Pengali jarak dengar. Lebih dari 1 = pendengaran lebih tajam.")]
    public float hearingMultiplier = 1f;

    [Range(0f, 1f)]
    [Tooltip("Pengali radius suara kalau terhalang tembok. 0.5 = jarak dengar setengahnya.")]
    public float wallMuffle = 0.5f;

    [Tooltip("Jeda enemy berhenti dan menoleh ke arah suara sebelum bergerak.")]
    public float noiseReactionDelay = 0.8f;

    public float noiseInvestigateSpeed = 3f;

    [Tooltip("Lama enemy celingukan di lokasi suara sebelum kembali patrol.")]
    public float noiseSearchTime = 4f;

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

    [Tooltip("Batas waktu mengejar item. Kalau lewat, enemy tetap lanjut ke animasi pickup (anti nyangkut).")]
    public float itemChaseTimeout = 8f;

    [Header("Door Interaction (Buka Pintu)")]
    public bool enableDoorOpening = true;

    [Tooltip("Layer collider pintu. Pintu harus punya collider di layer ini.")]
    public LayerMask doorMask = 1; // Default layer

    [Tooltip("Seberapa jauh di depan enemy pintu dicari.")]
    public float doorCheckDistance = 1.5f;

    public float doorCheckRadius = 1f;
    public float doorCheckHeight = 1f;

    [Tooltip("Jeda enemy berhenti dan menghadap pintu sebelum membukanya.")]
    public float doorOpenDelay = 0.7f;

    [Tooltip("Jeda setelah pintu dibuka sebelum enemy lanjut jalan (menunggu pintu terbuka).")]
    public float doorOpenDuration = 0.6f;

    [Tooltip("Jeda sebelum enemy boleh berinteraksi dengan pintu lagi.")]
    public float doorCooldown = 0.5f;

    [Tooltip("Nama trigger Animator untuk animasi buka pintu. Kosongkan kalau belum ada.")]
    public string openDoorTrigger = "";

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

    // Vision / searching
    private Vector3 lastKnownPlayerPosition;
    private float lastSeenTime;
    private bool isSearchWaiting;
    private float searchEndTime;
    private float searchLookDirection = 1f;
    private bool playerVisibleGizmo;

    // Hearing
    private Vector3 noiseTarget;
    private bool isReactingToNoise;
    private float noiseReactionEndTime;

    // Door interaction
    private enum DoorPhase
    {
        None,
        Preparing,
        Opening
    }

    private DoorPhase doorPhase = DoorPhase.None;
    private IEnemyOpenable doorTarget;
    private Transform doorTargetTransform;
    private Vector3 doorLookPoint;
    private float doorPhaseEndTime;
    private float doorCooldownEnd;
    private readonly Collider[] doorHits = new Collider[8];
    private readonly RaycastHit[] doorRayHits = new RaycastHit[8];

    private PickupableObject targetThrownItem;
    private float itemChaseStartTime;
    private Collider[] enemyColliders;
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
        NoiseSystem.OnNoise += HandleNoise;
    }

    void OnDisable()
    {
        PlayerInteractor.OnObjectThrown -= HandleThrownItem;
        NoiseSystem.OnNoise -= HandleNoise;
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

        enemyColliders = GetComponentsInChildren<Collider>();

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

        // Buka pintu yang menghalangi jalan (berlaku di semua state yang bergerak).
        if (UpdateDoorInteraction())
        {
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

        // ---------- VISION ----------
        bool seesPlayer = CanSeePlayer();
        playerVisibleGizmo = seesPlayer;

        if (seesPlayer)
        {
            lastKnownPlayerPosition = player.position;
            lastSeenTime = Time.time;

            float distance = Vector3.Distance(
                transform.position,
                player.position
            );

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

        // Player tidak terlihat.
        if (currentState == EnemyState.Chasing)
        {
            // Masih "ingat" sebentar: lari ke posisi terakhir player terlihat.
            if (Time.time - lastSeenTime < loseSightDelay)
            {
                agent.isStopped = false;
                agent.SetDestination(lastKnownPlayerPosition);
                UpdateAnimation();
                return;
            }

            Debug.Log("[ENEMY] Kehilangan pandangan. Cek posisi terakhir player.");
            StartSearching();
        }

        if (currentState == EnemyState.Searching)
        {
            HandleSearching();
            UpdateAnimation();
            return;
        }

        if (currentState == EnemyState.InvestigatingNoise)
        {
            HandleNoiseInvestigation();
            UpdateAnimation();
            return;
        }

        UpdatePatrol();
        UpdateAnimation();
    }

    // ==========================================
    // VISION SYSTEM
    // ==========================================

    Vector3 GetEyePosition()
    {
        if (eyePoint != null)
            return eyePoint.position;

        return transform.position + Vector3.up * eyeHeight;
    }

    bool CanSeePlayer()
    {
        if (player == null)
            return false;

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        // 1. Cek jarak pandang.
        if (distance > viewDistance)
            return false;

        // 2. Cek sudut pandang (kecuali player sangat dekat).
        if (distance > proximityRadius)
        {
            Vector3 flatDirection = player.position - transform.position;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 flatForward = transform.forward;
                flatForward.y = 0f;

                float angle = Vector3.Angle(flatForward, flatDirection);

                if (angle > viewAngle * 0.5f)
                    return false;
            }
        }

        // 3. Cek line of sight (tidak terhalang tembok/objek).
        Vector3 eye = GetEyePosition();

        if (playerCheckHeights == null || playerCheckHeights.Length == 0)
        {
            Vector3 target = player.position + Vector3.up;

            return !Physics.Linecast(
                eye, target, obstacleMask,
                QueryTriggerInteraction.Ignore
            );
        }

        for (int i = 0; i < playerCheckHeights.Length; i++)
        {
            Vector3 target = player.position +
                             Vector3.up * playerCheckHeights[i];

            if (!Physics.Linecast(
                eye, target, obstacleMask,
                QueryTriggerInteraction.Ignore))
                return true;
        }

        return false;
    }

    // ==========================================
    // SEARCHING (setelah kehilangan player)
    // ==========================================

    void StartSearching()
    {
        currentState = EnemyState.Searching;

        isChasing = false;
        isSearchWaiting = false;

        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.updateRotation = true;

        Vector3 target = lastKnownPlayerPosition;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 3f, agent.areaMask))
            target = hit.position;

        hasDestination = agent.SetDestination(target);
    }

    void HandleSearching()
    {
        if (isSearchWaiting)
        {
            agent.isStopped = true;

            // Celingak-celinguk di tempat.
            transform.Rotate(
                0f,
                searchLookSpeed * searchLookDirection * Time.deltaTime,
                0f
            );

            if (Time.time >= searchEndTime)
            {
                Debug.Log("[ENEMY] Player tidak ketemu. Kembali patrol.");
                ResetPatrol();
            }

            return;
        }

        bool arrived = IsDestinationReached();
        bool noPath = !agent.pathPending && !agent.hasPath;

        if (arrived || noPath)
        {
            agent.isStopped = true;
            agent.ResetPath();

            isSearchWaiting = true;
            searchEndTime = Time.time + searchWaitTime;
            searchLookDirection = Random.value > 0.5f ? 1f : -1f;
            hasDestination = false;

            Debug.Log("[ENEMY] Sampai posisi terakhir player. Mencari...");
        }
    }

    // ==========================================
    // DOOR INTERACTION
    // ==========================================

    // Return true kalau enemy sedang sibuk dengan pintu (logika lain dilewati).
    bool UpdateDoorInteraction()
    {
        if (!enableDoorOpening)
            return false;

        // --- Belum berinteraksi: cari pintu tertutup di depan. ---
        if (doorPhase == DoorPhase.None)
        {
            if (Time.time < doorCooldownEnd)
                return false;

            IEnemyOpenable door;
            Transform doorTransform;
            Vector3 lookPoint;

            if (!FindClosedDoorAhead(out door, out doorTransform, out lookPoint))
                return false;

            doorTarget = door;
            doorTargetTransform = doorTransform;
            doorLookPoint = lookPoint;

            doorPhase = DoorPhase.Preparing;
            doorPhaseEndTime = Time.time + doorOpenDelay;

            // Berhenti seketika supaya tidak menembus pintu.
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            Debug.Log("[ENEMY] Ada pintu tertutup. Mau membuka.");

            if (animator != null && !string.IsNullOrEmpty(openDoorTrigger))
                animator.SetTrigger(openDoorTrigger);

            return true;
        }

        // Pintu hilang/dihancurkan di tengah proses.
        if (doorTargetTransform == null)
        {
            EndDoorInteraction();
            return false;
        }

        // Pastikan agent tetap berhenti (event lain bisa saja menjalankannya).
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        FaceDirection(doorLookPoint - transform.position, 8f);

        if (doorPhase == DoorPhase.Preparing)
        {
            if (Time.time >= doorPhaseEndTime)
            {
                doorTarget.OpenByEnemy();

                doorPhase = DoorPhase.Opening;
                doorPhaseEndTime = Time.time + doorOpenDuration;

                Debug.Log("[ENEMY] Pintu dibuka.");
            }

            return true;
        }

        // DoorPhase.Opening
        if (Time.time >= doorPhaseEndTime)
        {
            EndDoorInteraction();
            return false;
        }

        return true;
    }

    void EndDoorInteraction()
    {
        doorPhase = DoorPhase.None;
        doorTarget = null;
        doorTargetTransform = null;
        doorCooldownEnd = Time.time + doorCooldown;

        agent.isStopped = false;
    }


    bool FindClosedDoorAhead(
        out IEnemyOpenable door,
        out Transform doorTransform,
        out Vector3 lookPoint)
    {
        door = null;
        doorTransform = null;
        lookPoint = Vector3.zero;

        // Enemy harus memiliki tujuan perjalanan.
        if (agent.isStopped || agent.pathPending || !agent.hasPath)
            return false;

        // Ambil arah perjalanan enemy.
        Vector3 moveDirection = agent.steeringTarget - transform.position;
        moveDirection.y = 0f;

        // Fallback jika steeringTarget belum memberikan arah.
        if (moveDirection.sqrMagnitude < 0.01f)
        {
            moveDirection = agent.desiredVelocity;
            moveDirection.y = 0f;
        }

        if (moveDirection.sqrMagnitude < 0.01f)
            return false;

        moveDirection.Normalize();

        // Posisi awal pengecekan pintu.
        Vector3 origin = transform.position +
                         Vector3.up * doorCheckHeight;

        // 1. SphereCast untuk mendeteksi pintu di depan enemy.
        int count = Physics.SphereCastNonAlloc(
            origin,
            doorCheckRadius,
            moveDirection,
            doorRayHits,
            doorCheckDistance,
            doorMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            if (TryGetClosedDoor(
                doorRayHits[i].collider,
                moveDirection,
                out door,
                out doorTransform,
                out lookPoint))
            {
                return true;
            }
        }

        // 2. OverlapSphere sebagai deteksi tambahan.
        Vector3 checkCenter = origin +
                              moveDirection * doorCheckDistance;

        count = Physics.OverlapSphereNonAlloc(
            checkCenter,
            doorCheckRadius,
            doorHits,
            doorMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            if (TryGetClosedDoor(
                doorHits[i],
                moveDirection,
                out door,
                out doorTransform,
                out lookPoint))
            {
                return true;
            }
        }

        return false;
    }

    // Memeriksa apakah collider merupakan pintu tertutup.
    bool TryGetClosedDoor(
        Collider hit,
        Vector3 moveDirection,
        out IEnemyOpenable door,
        out Transform doorTransform,
        out Vector3 lookPoint)
    {
        door = null;
        doorTransform = null;
        lookPoint = Vector3.zero;

        if (hit == null)
            return false;

        IEnemyOpenable candidate =
            hit.GetComponentInParent<IEnemyOpenable>();

        if (candidate == null || candidate.IsOpen)
            return false;

        Component candidateComponent = candidate as Component;

        if (candidateComponent == null)
            return false;

        Vector3 toDoor = hit.bounds.center - transform.position;
        toDoor.y = 0f;

        // Pastikan pintu berada di depan enemy.
        if (toDoor.sqrMagnitude > 0.0001f &&
            Vector3.Dot(moveDirection, toDoor.normalized) < 0.2f)
            return false;

        door = candidate;
        doorTransform = candidateComponent.transform;
        lookPoint = hit.bounds.center;

        return true;
    }

    void FaceDirection(Vector3 direction, float speed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            speed * Time.deltaTime
        );
    }

    // ==========================================
    // HEARING SYSTEM
    // ==========================================

    void HandleNoise(Vector3 noisePosition, float noiseRadius)
    {
        if (!enableHearing)
            return;

        // Sedang menangkap / mengejar item / sudah mengejar player -> abaikan.
        if (isAttacking || isInvestigatingItem)
            return;

        if (currentState == EnemyState.Chasing)
            return;

        float effectiveRadius = noiseRadius * hearingMultiplier;

        // Suara teredam kalau terhalang tembok.
        if (Physics.Linecast(
            GetEyePosition(),
            noisePosition + Vector3.up * 0.5f,
            obstacleMask,
            QueryTriggerInteraction.Ignore))
        {
            effectiveRadius *= wallMuffle;
        }

        float distance = Vector3.Distance(
            transform.position,
            noisePosition
        );

        if (distance > effectiveRadius)
            return;

        NavMeshHit hit;

        if (!NavMesh.SamplePosition(
            noisePosition, out hit, 3f, agent.areaMask))
            return;

        StartInvestigatingNoise(hit.position);
    }

    void StartInvestigatingNoise(Vector3 position)
    {
        bool alreadyInvestigating =
            currentState == EnemyState.InvestigatingNoise;

        // Kalau sedang menunggu di waypoint, waypoint itu dianggap selesai.
        MarkPatrolInterrupted();

        noiseTarget = position;

        currentState = EnemyState.InvestigatingNoise;
        isChasing = false;
        hasDestination = false;

        if (!alreadyInvestigating)
        {
            // Berhenti sebentar dan menoleh ke arah suara.
            Debug.Log("[ENEMY] Mendengar suara!");

            isReactingToNoise = true;
            isSearchWaiting = false;
            noiseReactionEndTime = Time.time + noiseReactionDelay;

            agent.isStopped = true;
            agent.ResetPath();
        }
        else if (!isReactingToNoise)
        {
            // Suara baru saat sudah menuju / mencari -> ubah tujuan.
            isSearchWaiting = false;

            agent.speed = noiseInvestigateSpeed;
            agent.isStopped = false;
            agent.SetDestination(noiseTarget);
        }
    }

    void HandleNoiseInvestigation()
    {
        // Fase 1: berhenti dan menoleh ke sumber suara.
        if (isReactingToNoise)
        {
            agent.isStopped = true;

            Vector3 direction = noiseTarget - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction),
                    6f * Time.deltaTime
                );
            }

            if (Time.time >= noiseReactionEndTime)
            {
                isReactingToNoise = false;

                agent.speed = noiseInvestigateSpeed;
                agent.isStopped = false;
                agent.updateRotation = true;

                hasDestination = agent.SetDestination(noiseTarget);
            }

            return;
        }

        // Fase 3: celingukan di lokasi suara.
        if (isSearchWaiting)
        {
            agent.isStopped = true;

            transform.Rotate(
                0f,
                searchLookSpeed * searchLookDirection * Time.deltaTime,
                0f
            );

            if (Time.time >= searchEndTime)
            {
                Debug.Log("[ENEMY] Tidak ada apa-apa. Kembali patrol.");
                ResetPatrol();
            }

            return;
        }

        // Fase 2: berjalan menuju lokasi suara.
        bool arrived = IsDestinationReached();
        bool noPath = !agent.pathPending && !agent.hasPath;

        if (arrived || noPath)
        {
            agent.isStopped = true;
            agent.ResetPath();

            isSearchWaiting = true;
            searchEndTime = Time.time + noiseSearchTime;
            searchLookDirection = Random.value > 0.5f ? 1f : -1f;
            hasDestination = false;

            Debug.Log("[ENEMY] Sampai sumber suara. Mencari...");
        }
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

    // Dipanggil sebelum enemy meninggalkan patrol (mengejar / dengar suara).
    // Kalau enemy sedang menunggu di waypoint, berarti waypoint itu sudah selesai,
    // jadi lanjutannya adalah waypoint berikutnya.
    void MarkPatrolInterrupted()
    {
        if (currentState == EnemyState.Patrol && isWaitingAtWaypoint)
            currentPatrolIndex++;

        isWaitingAtWaypoint = false;
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
        // currentPatrolIndex TIDAK direset: lanjut ke path yang tadi dituju/berikutnya.
        currentState = EnemyState.Patrol;

        isChasing = false;
        hasDestination = false;
        isWaitingAtWaypoint = false;
        isSearchWaiting = false;
        isReactingToNoise = false;

        agent.isStopped = false;
        agent.updateRotation = true;
        agent.ResetPath();

        if (patrolPoints.Count > 0 && currentPatrolIndex < patrolPoints.Count)
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
        if (currentState != EnemyState.Chasing)
        {
            Debug.Log("[ENEMY] Player terlihat! Mengejar.");

            // Kalau sedang menunggu di waypoint, waypoint itu dianggap selesai.
            MarkPatrolInterrupted();
        }

        currentState = EnemyState.Chasing;
        isChasing = true;
        isSearchWaiting = false;
        isReactingToNoise = false;

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
        itemChaseStartTime = Time.time;

        // Supaya enemy tidak mendorong item yang sedang dikejar.
        IgnoreItemCollision(thrownItem);

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

    // Enemy tidak boleh mendorong item distraksi (kalau tidak, item terus menjauh
    // dan jarak tidak pernah cukup dekat untuk memulai animasi pickup).
    void IgnoreItemCollision(PickupableObject item)
    {
        if (item == null || enemyColliders == null)
            return;

        Collider[] itemColliders = item.GetComponentsInChildren<Collider>(true);

        foreach (Collider enemyCol in enemyColliders)
        {
            if (enemyCol == null)
                continue;

            foreach (Collider itemCol in itemColliders)
            {
                if (itemCol == null)
                    continue;

                Physics.IgnoreCollision(enemyCol, itemCol, true);
            }
        }
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

        Vector3 itemPosition = targetThrownItem.transform.position;

        Vector3 destination = itemPosition;
        NavMeshHit navHit;

        if (NavMesh.SamplePosition(itemPosition, out navHit, 2f, agent.areaMask))
            destination = navHit.position;

        agent.SetDestination(destination);

        // Jarak horizontal saja (tinggi item diabaikan).
        Vector3 flat = itemPosition - transform.position;
        flat.y = 0f;

        float reach = Mathf.Max(itemReachDistance, agent.stoppingDistance + 0.3f);
        bool closeEnough = flat.magnitude <= reach;

        bool pathArrived = !agent.pathPending && agent.hasPath &&
            agent.remainingDistance <= agent.stoppingDistance + 0.3f;

        bool timedOut = Time.time - itemChaseStartTime > itemChaseTimeout;

        if (closeEnough || pathArrived || timedOut)
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

        Debug.Log("[ENEMY] Distraksi selesai. Lanjut ke Path berikutnya.");

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

        // Enemy dianggap "lari" saat mengejar, menuju item, atau menuju posisi terakhir player.
        bool running =
            isChasing ||
            currentState == EnemyState.Investigating ||
            (currentState == EnemyState.Searching && !isSearchWaiting);

        animator.SetFloat("Speed", agent.velocity.magnitude);
        animator.SetBool("IsChasing", running);
    }

    // ==========================================
    // GIZMOS
    // ==========================================

    void OnDrawGizmosSelected()
    {
        // --- Vision cone ---
        Vector3 eye = eyePoint != null
            ? eyePoint.position
            : transform.position + Vector3.up * eyeHeight;

        float half = viewAngle * 0.5f;

        Vector3 leftDir = Quaternion.Euler(0f, -half, 0f) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0f, half, 0f) * transform.forward;

        Gizmos.color = Application.isPlaying && playerVisibleGizmo
            ? Color.red
            : Color.white;

        Gizmos.DrawLine(eye, eye + leftDir * viewDistance);
        Gizmos.DrawLine(eye, eye + rightDir * viewDistance);

        // Busur ujung cone.
        int segments = 24;
        Vector3 prev = eye + leftDir * viewDistance;

        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-half, half, i / (float)segments);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Vector3 next = eye + dir * viewDistance;

            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        // Radius deteksi dekat & tangkap.
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, proximityRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, catchRadius);

        // Area pengecekan pintu (di depan arah jalan enemy).
        if (enableDoorOpening)
        {
            Vector3 doorDirection = transform.forward;

            if (Application.isPlaying &&
                agent != null &&
                agent.desiredVelocity.sqrMagnitude > 0.01f)
            {
                doorDirection = agent.desiredVelocity;
                doorDirection.y = 0f;
                doorDirection.Normalize();
            }

            Gizmos.color = new Color(0.6f, 0.3f, 1f);
            Gizmos.DrawWireSphere(
                transform.position +
                Vector3.up * doorCheckHeight +
                doorDirection * doorCheckDistance,
                doorCheckRadius
            );
        }

        // Lokasi suara yang sedang diselidiki.
        if (Application.isPlaying &&
            currentState == EnemyState.InvestigatingNoise)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(noiseTarget, 0.5f);
            Gizmos.DrawLine(transform.position, noiseTarget);
        }

        // Posisi terakhir player terlihat.
        if (Application.isPlaying &&
            (currentState == EnemyState.Searching ||
             currentState == EnemyState.Chasing))
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastKnownPlayerPosition, 0.4f);
        }

        // --- Patrol gizmos ---
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