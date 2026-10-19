using UnityEngine;
public class WallFollowingBehavior : SteeringBehavior
{
    public float wallDistance = 1.2f;
    public float rayLength = 2.5f;
    public LayerMask wallLayer = (1 << 6) | (1 << 7);
    public override Vector3 CalculateForce()
    {
        Vector3 force = Vector3.zero;
        if (agent.ConfinedToRoom && agent.HomeRoom != null)
        {
            Bounds bounds = agent.HomeRoom.MovementBounds;
            Vector3 p = transform.position;
            float padding = wallDistance + agent.Radius;
            force.x += Mathf.Clamp01((bounds.min.x + padding - p.x) / padding);
            force.x -= Mathf.Clamp01((p.x - bounds.max.x + padding) / padding);
            force.z += Mathf.Clamp01((bounds.min.z + padding - p.z) / padding);
            force.z -= Mathf.Clamp01((p.z - bounds.max.z + padding) / padding);
            force *= agent.maxSpeed;
        }
        Vector3 desiredRoute = agent.GetRouteDesiredVelocity();
        Vector3 forward = desiredRoute.sqrMagnitude > 0.01f ? desiredRoute.normalized : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3[] probes = { right, -right, (forward + right).normalized, (forward - right).normalized };
        Vector3 origin = transform.position + Vector3.up * 0.7f;
        foreach (Vector3 direction in probes)
        {
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, rayLength, wallLayer, QueryTriggerInteraction.Ignore)) continue;
            if (hit.transform.IsChildOf(transform) || hit.transform == transform) continue;
            Vector3 normal = hit.normal; normal.y = 0f;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up).normalized;
            if (Vector3.Dot(tangent, forward) < 0f) tangent = -tangent;
            float correction = Mathf.Clamp((wallDistance - hit.distance) / wallDistance, -0.5f, 1f);
            force += ((tangent + normal * correction).normalized * agent.maxSpeed - agent.velocity) * 0.5f;
            break;
        }
        return force;
    }
}
