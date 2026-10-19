using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The stopped NavMeshAgent owns positional synchronization. Only this component
// calls Move; the brain and behaviors never write the object's position.
[RequireComponent(typeof(NavMeshAgent))]
public class SteeringAgent : MonoBehaviour
{
    private static readonly List<SteeringAgent> activeAgents = new List<SteeringAgent>();
    public static IReadOnlyList<SteeringAgent> ActiveAgents => activeAgents;
    public float maxSpeed = 4f;
    public float maxForce = 20f;
    public float mass = 1f;
    public float routeWeight = 1.5f;
    public LayerMask collisionMask = ~0;
    public Vector3 velocity;
    public RoomData HomeRoom { get; private set; }
    public bool ConfinedToRoom { get; private set; }
    public float Radius => navigation != null ? navigation.radius : 0.4f;
    public bool IsOnNavigation => navigation != null && navigation.enabled && navigation.isOnNavMesh;
    public bool HasRoute => corners.Length > 0;
    public bool MovementWasClipped { get; private set; }
    public Vector3 RouteGoal { get; private set; }

    private NavMeshAgent navigation;
    private SteeringBehavior[] behaviors = new SteeringBehavior[0];
    private NavMeshPath route;
    private Vector3[] corners = new Vector3[0];
    private int cornerIndex;
    private float nextRepath;
    private float nextBindAttempt;
    private bool hasRequestedGoal;
    private Vector3 requestedGoal;

    void Awake() { EnsureNavigation(); }
    void OnEnable()
    {
        if (!activeAgents.Contains(this)) activeAgents.Add(this);
        EnsureNavigation();
        ResetMotion();
        RefreshBehaviors();
    }
    void OnDisable() { activeAgents.Remove(this); ResetMotion(); }

    void EnsureNavigation()
    {
        if (navigation == null) navigation = GetComponent<NavMeshAgent>();
        // NavMeshPath creates a native handle: Unity forbids it in a field
        // initializer because MonoBehaviour construction can run off-thread.
        if (route == null) route = new NavMeshPath();
        navigation.updatePosition = true;
        navigation.updateRotation = false;
        navigation.autoTraverseOffMeshLink = false;
        if (IsOnNavigation) navigation.isStopped = true;
    }

    public void Configure(RoomData room, bool confined)
    {
        EnsureNavigation();
        HomeRoom = room;
        ConfinedToRoom = confined && room != null;
        ResetMotion();
        RefreshBehaviors();
    }
    public void RefreshBehaviors() { behaviors = GetComponents<SteeringBehavior>(); }
    public void ResetMotion()
    {
        velocity = Vector3.zero;
        corners = new Vector3[0];
        cornerIndex = 0;
        hasRequestedGoal = false;
        nextRepath = nextBindAttempt = 0f;
        if (IsOnNavigation)
        {
            navigation.isStopped = true;
            navigation.ResetPath();
            navigation.velocity = Vector3.zero;
        }
    }
    public bool IsInsideRoom(Vector3 point, float padding = 0f)
    {
        return !ConfinedToRoom || HomeRoom.ContainsMovementPoint(point, padding);
    }
    public Vector3 ClampToRoom(Vector3 point, float padding)
    {
        if (!ConfinedToRoom) return point;
        Bounds bounds = HomeRoom.MovementBounds;
        float insetX = Mathf.Min(padding, bounds.extents.x * 0.8f);
        float insetZ = Mathf.Min(padding, bounds.extents.z * 0.8f);
        point.x = Mathf.Clamp(point.x, bounds.min.x + insetX, bounds.max.x - insetX);
        point.z = Mathf.Clamp(point.z, bounds.min.z + insetZ, bounds.max.z - insetZ);
        point.y = HomeRoom.FloorCenter.y;
        return point;
    }
    public bool TrySamplePoint(Vector3 desired, out Vector3 point, float sampleRadius = 2f)
    {
        EnsureNavigation();
        desired = ClampToRoom(desired, Radius + 0.1f);
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, sampleRadius, navigation.areaMask)
            && IsInsideRoom(hit.position, Radius + 0.05f))
        {
            point = hit.position;
            return true;
        }
        point = transform.position;
        return false;
    }
    public bool BindToNavigation(Vector3 desired)
    {
        EnsureNavigation();
        if (!gameObject.activeInHierarchy) return false;
        if (!navigation.enabled) navigation.enabled = true;
        if (!TrySamplePoint(desired, out Vector3 point, 3f)) return false;
        bool bound = navigation.Warp(point);
        if (bound) navigation.isStopped = true;
        return bound;
    }
    public bool TryReachablePoint(Vector3 desired, out Vector3 point)
    {
        if (!IsOnNavigation || !TrySamplePoint(desired, out point))
        {
            point = transform.position;
            return false;
        }
        NavMeshPath candidate = new NavMeshPath();
        return NavMesh.CalculatePath(navigation.nextPosition, point, navigation.areaMask, candidate)
            && candidate.status == NavMeshPathStatus.PathComplete && PathStaysInRoom(candidate.corners);
    }
    bool PathStaysInRoom(Vector3[] pathCorners)
    {
        foreach (Vector3 point in pathCorners)
            if (!IsInsideRoom(point, Radius * 0.5f)) return false;
        return pathCorners.Length > 0;
    }
    public bool SetRouteGoal(Vector3 desired, bool force = false)
    {
        if (!IsOnNavigation) return false;
        bool changed = !hasRequestedGoal || (desired - requestedGoal).sqrMagnitude > 0.25f;
        if (!force && !changed && Time.time < nextRepath) return HasRoute;
        hasRequestedGoal = true;
        requestedGoal = desired;
        nextRepath = Time.time + 0.4f;
        if (!TrySamplePoint(desired, out Vector3 point)
            || !NavMesh.CalculatePath(navigation.nextPosition, point, navigation.areaMask, route)
            || route.status != NavMeshPathStatus.PathComplete || !PathStaysInRoom(route.corners))
        {
            corners = new Vector3[0];
            return false;
        }
        RouteGoal = point;
        corners = route.corners;
        cornerIndex = corners.Length > 1 ? 1 : 0;
        return true;
    }
    public Vector3 GetRouteDesiredVelocity()
    {
        if (!HasRoute) return Vector3.zero;
        float cornerRadius = Mathf.Max(0.2f, Radius * 0.65f);
        while (cornerIndex < corners.Length - 1 && PlanarDistance(transform.position, corners[cornerIndex]) < cornerRadius)
            cornerIndex++;
        Vector3 offset = corners[cornerIndex] - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        float speed = maxSpeed;
        if (cornerIndex == corners.Length - 1)
            speed *= Mathf.Clamp01((distance - 0.15f) / 1.4f);
        return distance > 0.001f ? offset / distance * speed : Vector3.zero;
    }
    // Local forces can take a direct route only when it is navigable and clear.
    public Vector3 GetNavigableDesiredVelocity(Vector3 target, float stopRadius = 0.3f)
    {
        if (!IsOnNavigation || !TrySamplePoint(target, out Vector3 point)) return GetRouteDesiredVelocity();
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        if (NavMesh.Raycast(navigation.nextPosition, point, out NavMeshHit _, navigation.areaMask)
            || !IsSegmentClear(transform.position, point)) return GetRouteDesiredVelocity();
        float speed = maxSpeed * Mathf.Clamp01((offset.magnitude - stopRadius) / 1.2f);
        return offset.sqrMagnitude > 0.0001f ? offset.normalized * speed : Vector3.zero;
    }
    public bool IsSegmentClear(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.0001f) return true;
        float height = Mathf.Max(Radius + 0.2f, navigation.height * 0.5f);
        RaycastHit[] hits = Physics.SphereCastAll(from + Vector3.up * height,
            Mathf.Max(0.12f, Radius * 0.85f), delta.normalized, delta.magnitude,
            collisionMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
            if (!hit.transform.IsChildOf(transform) && hit.transform != transform && hit.normal.y < 0.7f) return false;
        return true;
    }
    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;
        if (!IsOnNavigation)
        {
            if (Time.time >= nextBindAttempt)
            {
                nextBindAttempt = Time.time + 0.5f;
                BindToNavigation(transform.position);
            }
            return;
        }
        navigation.isStopped = true;
        Vector3 force = (GetRouteDesiredVelocity() - velocity) * routeWeight;
        foreach (SteeringBehavior behavior in behaviors)
            if (behavior != null && behavior.isActiveAndEnabled && behavior.weight > 0f)
                force += behavior.CalculateForce() * behavior.weight;
        force.y = 0f;
        if (!IsFinite(force)) force = -velocity;
        force = Vector3.ClampMagnitude(force, Mathf.Max(0.1f, maxForce));
        velocity = Vector3.ClampMagnitude(velocity + force / Mathf.Max(0.1f, mass) * dt, Mathf.Max(0f, maxSpeed));
        velocity.y = 0f;
        Vector3 before = navigation.nextPosition;
        Vector3 proposed = ClampToRoom(before + velocity * dt, Radius + 0.05f);
        Vector3 displacement = proposed - before;
        displacement.y = 0f;
        MovementWasClipped = false;
        if (NavMesh.Raycast(before, proposed, out NavMeshHit edge, navigation.areaMask))
        {
            displacement = Vector3.ClampMagnitude(edge.position - before, displacement.magnitude);
            displacement.y = 0f;
            displacement *= 0.9f;
            MovementWasClipped = true;
        }
        if (!IsInsideRoom(before + displacement, Radius * 0.5f)
            || !IsSegmentClear(before, before + displacement))
        {
            displacement = Vector3.zero;
            MovementWasClipped = true;
        }
        navigation.Move(displacement);
        velocity = (navigation.nextPosition - before) / dt;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity.normalized), dt * 8f);
    }
    public static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }
    static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
