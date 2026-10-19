using UnityEngine;
public class ObstacleAvoidanceBehavior : SteeringBehavior
{
    public float avoidDistance = 2.5f;
    public float rayOffset = 0.45f;
    public LayerMask obstacleLayer = ~0;
    public override Vector3 CalculateForce()
    {
        Vector3 forward = agent.velocity.sqrMagnitude > 0.05f ? agent.velocity.normalized : transform.forward;
        forward.y = 0f;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.6f, agent.Radius + 0.2f);
        Vector3 force = Vector3.zero;
        for (int i = -1; i <= 1; i++)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin + right * rayOffset * i, forward,
                avoidDistance, obstacleLayer, QueryTriggerInteraction.Ignore);
            float nearest = avoidDistance;
            Vector3 normal = Vector3.zero;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.normal.y > 0.7f) continue;
                if (hit.distance < nearest) { nearest = hit.distance; normal = hit.normal; }
            }
            normal.y = 0f;
            if (normal.sqrMagnitude > 0.001f)
                force += normal.normalized * agent.maxSpeed * (1f - nearest / avoidDistance);
        }
        return force;
    }
}
