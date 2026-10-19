using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Patrol, Investigate, Chase, Evade, Hide, TraverseJumpLink }

[RequireComponent(typeof(NavMeshAgent), typeof(SteeringAgent))]
public class EnemyAIController : MonoBehaviour
{
    public Transform player;
    public float detectionRadius = 18f;
    [Range(0f, 360f)] public float fieldOfViewAngle = 150f;
    public LayerMask obstacleMask = ~0;
    public float loseTargetTime = 3f;
    public float patrolSpeed = 2.2f;
    public float chaseSpeed = 4.2f;
    public float evadeSpeed = 4.7f;
    public float waypointStoppingDistance = 0.7f;
    public float waitTimeAtWaypoint = 0.7f;
    public List<Transform> customWaypoints = new List<Transform>();
    public EnemyState currentState = EnemyState.Patrol;
    public RoomData HomeRoom { get; private set; }
    public bool ConfinedToRoom { get; private set; }
    public bool IsVulnerable => !defeated && PlayerPowerState.Instance != null && PlayerPowerState.Instance.IsEmpowered;
    public bool UsesFlowField => currentState == EnemyState.Patrol && flowFollowing != null && flowFollowing.enabled;
    public bool IsReady => steering != null && steering.IsOnNavigation;

    private SteeringAgent steering;
    private PursuitBehavior pursuit;
    private EvasionBehavior evasion;
    private WallFollowingBehavior wallFollowing;
    private FlowFieldFollowingBehavior flowFollowing;
    private NavMeshFlowField flowField;
    private PredictiveEvasionBehavior predictiveEvasion;
    private AnxietyManager anxiety;
    private bool initialized;
    private bool defeated;
    private bool useFlowPatrol;
    private bool hasPatrolGoal;
    private bool hasHideGoal;
    private Vector3 patrolGoal;
    private Vector3 hideGoal;
    private Vector3 lastKnownPlayerPosition;
    private float lastSeenTime = -100f;
    private float nextDecision;
    private float waitUntil;
    private float impactEvadeUntil;
    private float nextHideSearch;
    private int waypointIndex;
    private int patrolLeg;
    private static int spawnSequence;

    void Awake() { EnsureComponents(); }
    void OnEnable()
    {
        defeated = false;
        initialized = false;
        nextDecision = waitUntil = impactEvadeUntil = nextHideSearch = 0f;
        lastSeenTime = -100f;
        hasPatrolGoal = hasHideGoal = false;
        waypointIndex = patrolLeg = 0;
        currentState = EnemyState.Patrol;
        if (steering != null) steering.ResetMotion();
    }
    void OnDisable()
    {
        initialized = false;
        if (steering != null) steering.ResetMotion();
        if (flowField != null) flowField.Invalidate();
    }

    T Ensure<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
    void EnsureComponents()
    {
        // Entity, pickup and projectile layers are handled by their own systems.
        obstacleMask = obstacleMask.value & ~((1 << 8) | (1 << 9) | (1 << 10));
        steering = Ensure<SteeringAgent>();
        pursuit = Ensure<PursuitBehavior>();
        evasion = Ensure<EvasionBehavior>();
        wallFollowing = Ensure<WallFollowingBehavior>();
        flowFollowing = Ensure<FlowFieldFollowingBehavior>();
        flowField = Ensure<NavMeshFlowField>();
        predictiveEvasion = Ensure<PredictiveEvasionBehavior>();
        ObstacleAvoidanceBehavior avoidance = Ensure<ObstacleAvoidanceBehavior>();
        avoidance.obstacleLayer = obstacleMask;
        avoidance.weight = 7f;
        avoidance.enabled = true;
        SeparationBehavior separation = Ensure<SeparationBehavior>();
        separation.weight = 2.4f; separation.enabled = true;
        UnalignedCollisionAvoidanceBehavior crossing = Ensure<UnalignedCollisionAvoidanceBehavior>();
        crossing.weight = 2.8f; crossing.enabled = true;
        predictiveEvasion.weight = 4f; predictiveEvasion.enabled = true;
        wallFollowing.weight = 0.8f;
        wallFollowing.wallLayer = (1 << 6) | (1 << 7);
        flowFollowing.flowField = flowField;
        flowFollowing.weight = 1.7f;
        pursuit.weight = 2.5f;
        evasion.weight = 2.5f;
        foreach (WanderBehavior oldWander in GetComponents<WanderBehavior>()) oldWander.enabled = false;
        foreach (SeekBehavior oldSeek in GetComponents<SeekBehavior>()) oldSeek.enabled = false;
        // Floating-light art is offset independently of the root on the NavMesh.
        if (GetComponent<Light>() != null)
        {
            Transform sphere = transform.Find("Sphere");
            if (sphere != null && sphere.GetComponent<MeshRenderer>() != null)
            {
                Vector3 visualPosition = sphere.localPosition;
                visualPosition.y = Mathf.Max(0.9f, visualPosition.y);
                sphere.localPosition = visualPosition;
            }
        }
        steering.collisionMask = obstacleMask;
        steering.maxForce = 24f;
        steering.RefreshBehaviors();
    }

    public void Initialize(RoomData homeRoom, Transform playerTarget, bool confinedToRoom)
    {
        EnsureComponents();
        HomeRoom = homeRoom;
        ConfinedToRoom = confinedToRoom && homeRoom != null;
        player = playerTarget;
        anxiety = null;
        ResolvePlayer();
        steering.Configure(HomeRoom, ConfinedToRoom);
        bool bound = steering.BindToNavigation(transform.position);
        if (!bound && HomeRoom != null) bound = steering.BindToNavigation(HomeRoom.FloorCenter);
        if (!bound && HomeRoom != null)
        {
            for (int i = 0; i < 20 && !bound; i++) bound = steering.BindToNavigation(RandomRoomPoint());
        }
        useFlowPatrol = (spawnSequence++ % 2) == 0;
        defeated = false;
        initialized = true;
        hasPatrolGoal = hasHideGoal = false;
        impactEvadeUntil = waitUntil = nextDecision = nextHideSearch = 0f;
        lastSeenTime = -100f;
        waypointIndex = patrolLeg = 0;
        pursuit.target = evasion.target = player;
        pursuit.ResetPrediction(); evasion.ResetPrediction();
        flowField.Invalidate();
        SetState(EnemyState.Patrol);
    }

    void ResolvePlayer()
    {
        if (player == null)
        {
            GameObject candidate = GameObject.FindGameObjectWithTag("Player");
            if (candidate != null) player = candidate.transform;
            else if (Camera.main != null) player = Camera.main.transform;
        }
        if (player != null && anxiety == null)
            anxiety = player.GetComponentInParent<AnxietyManager>();
    }

    void Update()
    {
        if (defeated || Time.deltaTime <= 0f) return;
        if (!initialized)
        {
            RoomData closest = null;
            float distance = float.MaxValue;
            foreach (RoomData room in Object.FindObjectsByType<RoomData>(FindObjectsSortMode.None))
            {
                if (!room.ContainsMovementPoint(transform.position)) continue;
                float d = SteeringAgent.PlanarDistance(transform.position, room.FloorCenter);
                if (d < distance) { closest = room; distance = d; }
            }
            Initialize(closest, player, closest != null);
        }
        ResolvePlayer();
        pursuit.target = evasion.target = player;
        if (!steering.IsOnNavigation) return;
        if (Time.time < nextDecision) return;
        nextDecision = Time.time + 0.15f;

        bool visible = CanSeePlayer();
        if (visible && player != null)
        {
            lastKnownPlayerPosition = player.position;
            lastSeenTime = Time.time;
        }
        bool danger = TryDangerEscape(out Vector3 dangerEscape);
        bool empowered = PlayerPowerState.Instance != null && PlayerPowerState.Instance.IsEmpowered;
        if (danger || Time.time < impactEvadeUntil)
        {
            SetState(EnemyState.Evade);
            if (danger) steering.SetRouteGoal(dangerEscape);
            else if (hasHideGoal) steering.SetRouteGoal(hideGoal);
        }
        else if (empowered && player != null)
        {
            if (!hasHideGoal || Time.time >= nextHideSearch
                || SteeringAgent.PlanarDistance(hideGoal, player.position) < 3f)
                SelectHidePoint();
            bool concealed = hasHideGoal && IsOccludedFromPlayer(transform.position);
            SetState(concealed ? EnemyState.Hide : EnemyState.Evade);
            if (hasHideGoal) steering.SetRouteGoal(hideGoal);
        }
        else if (anxiety != null && anxiety.IsCritical && player != null && visible)
        {
            SetState(EnemyState.Chase);
            steering.SetRouteGoal(pursuit.PredictedTargetPosition());
        }
        else if (anxiety != null && anxiety.IsCritical && Time.time - lastSeenTime < loseTargetTime)
        {
            SetState(EnemyState.Investigate);
            steering.SetRouteGoal(lastKnownPlayerPosition);
        }
        else
        {
            SetState(EnemyState.Patrol);
            UpdatePatrol();
        }
        ApplyWeights();
    }

    void SetState(EnemyState state)
    {
        if (currentState != state)
        {
            currentState = state;
            flowField.Invalidate();
            if (state == EnemyState.Patrol) hasPatrolGoal = false;
            if (state != EnemyState.Evade && state != EnemyState.Hide) hasHideGoal = false;
        }
        ApplyWeights();
    }
    void ApplyWeights()
    {
        if (steering == null) return;
        steering.maxSpeed = currentState == EnemyState.Chase ? chaseSpeed
            : currentState == EnemyState.Evade ? evadeSpeed : patrolSpeed;
        steering.routeWeight = 1.6f;
        pursuit.enabled = currentState == EnemyState.Chase;
        evasion.enabled = currentState == EnemyState.Evade && player != null;
        wallFollowing.enabled = currentState == EnemyState.Patrol || currentState == EnemyState.Hide;
        flowFollowing.enabled = currentState == EnemyState.Patrol && ((patrolLeg % 2 == 0) == useFlowPatrol);
        if (flowFollowing.enabled && steering.HasRoute) flowField.Configure(steering, steering.RouteGoal);
    }

    void UpdatePatrol()
    {
        if (Time.time < waitUntil) return;
        if (hasPatrolGoal && SteeringAgent.PlanarDistance(transform.position, patrolGoal) < waypointStoppingDistance)
        {
            hasPatrolGoal = false;
            waitUntil = Time.time + waitTimeAtWaypoint;
            patrolLeg++;
            return;
        }
        if (!hasPatrolGoal)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Transform waypoint = customWaypoints.Count > 0 ? customWaypoints[waypointIndex++ % customWaypoints.Count] : null;
                Vector3 candidate = waypoint != null ? waypoint.position : RandomRoomPoint();
                if (!steering.TryReachablePoint(candidate, out Vector3 point)
                    || SteeringAgent.PlanarDistance(transform.position, point) < 1.2f) continue;
                patrolGoal = point;
                hasPatrolGoal = true;
                break;
            }
        }
        if (hasPatrolGoal && !steering.SetRouteGoal(patrolGoal)) hasPatrolGoal = false;
    }

    Vector3 RandomRoomPoint()
    {
        if (HomeRoom != null && ConfinedToRoom)
        {
            Bounds bounds = HomeRoom.MovementBounds;
            return new Vector3(Random.Range(bounds.min.x, bounds.max.x), HomeRoom.FloorCenter.y,
                Random.Range(bounds.min.z, bounds.max.z));
        }
        Vector2 offset = Random.insideUnitCircle * 10f;
        return transform.position + new Vector3(offset.x, 0f, offset.y);
    }

    void SelectHidePoint()
    {
        nextHideSearch = Time.time + 1.5f;
        float bestScore = float.NegativeInfinity;
        hasHideGoal = false;
        for (int i = 0; i < 28; i++)
        {
            Vector3 candidate = RandomRoomPoint();
            if (!steering.TryReachablePoint(candidate, out Vector3 point)) continue;
            float playerDistance = player != null ? SteeringAgent.PlanarDistance(point, player.position) : 8f;
            bool cover = player != null && IsOccludedFromPlayer(point);
            float score = playerDistance + (cover ? 30f : 0f)
                - SteeringAgent.PlanarDistance(transform.position, point) * 0.15f;
            if (score <= bestScore) continue;
            bestScore = score;
            hideGoal = point;
            hasHideGoal = true;
        }
    }

    bool IsOccludedFromPlayer(Vector3 point)
    {
        if (player == null) return false;
        Vector3 start = player.position + Vector3.up;
        Vector3 target = point + Vector3.up;
        foreach (RaycastHit hit in Physics.RaycastAll(start, target - start, Vector3.Distance(start, target),
            obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)
                || hit.transform == player || hit.transform.IsChildOf(player)) continue;
            return true;
        }
        return false;
    }

    bool CanSeePlayer()
    {
        if (player == null || SteeringAgent.PlanarDistance(transform.position, player.position) > detectionRadius) return false;
        Vector3 direction = player.position - transform.position; direction.y = 0f;
        if (Vector3.Angle(transform.forward, direction) > fieldOfViewAngle * 0.5f) return false;
        return !IsOccludedFromPlayer(transform.position);
    }

    bool TryDangerEscape(out Vector3 escape)
    {
        escape = transform.position;
        BallisticDangerZone threat = null;
        foreach (BallisticDangerZone zone in BallisticDangerZone.ActiveDangerZones)
        {
            if (zone == null || zone.timeToImpact <= 0f) continue;
            if (SteeringAgent.PlanarDistance(transform.position, zone.predictedImpactPoint)
                < zone.dangerRadius + predictiveEvasion.safetyBuffer) { threat = zone; break; }
        }
        if (threat == null) return false;
        float score = float.PositiveInfinity;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI * 2f / 16f;
            Vector3 candidate = threat.predictedImpactPoint
                + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (threat.dangerRadius + predictiveEvasion.safetyBuffer + 1f);
            if (!steering.TryReachablePoint(candidate, out Vector3 point)) continue;
            bool safe = true;
            foreach (BallisticDangerZone zone in BallisticDangerZone.ActiveDangerZones)
                if (zone != null && zone.timeToImpact > 0f
                    && SteeringAgent.PlanarDistance(point, zone.predictedImpactPoint) < zone.dangerRadius + 0.5f) safe = false;
            float distance = SteeringAgent.PlanarDistance(transform.position, point);
            if (safe && distance < score) { score = distance; escape = point; }
        }
        // If the whole room is threatened, stay confined and use the farthest
        // reachable location instead of escaping through a wall or a doorway.
        if (float.IsPositiveInfinity(score))
        {
            float farthest = -1f;
            for (int i = 0; i < 20; i++)
                if (steering.TryReachablePoint(RandomRoomPoint(), out Vector3 point))
                {
                    float distance = SteeringAgent.PlanarDistance(point, threat.predictedImpactPoint);
                    if (distance > farthest) { farthest = distance; escape = point; }
                }
        }
        return true;
    }

    public void ReceiveProjectileImpact()
    {
        if (IsVulnerable) { Defeat(); return; }
        if (!initialized) return;
        impactEvadeUntil = Time.time + 3f;
        SelectHidePoint();
        SetState(EnemyState.Evade);
        if (hasHideGoal) steering.SetRouteGoal(hideGoal, true);
    }
    public void Defeat()
    {
        if (!IsVulnerable || defeated) return;
        defeated = true;
        steering.ResetMotion();
        if (PlayerPowerState.Instance != null) PlayerPowerState.Instance.RegisterDefeat();
        if (EntityManager.Instance != null) EntityManager.Instance.DespawnEntity(gameObject);
        else gameObject.SetActive(false);
    }
    public void InvalidateNavigation()
    {
        if (flowField != null) flowField.Invalidate();
        if (steering != null && steering.HasRoute) steering.SetRouteGoal(steering.RouteGoal, true);
    }
}
