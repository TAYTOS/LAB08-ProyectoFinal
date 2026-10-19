using UnityEngine;
public class UnalignedCollisionAvoidanceBehavior : SteeringBehavior
{
    public float predictionTime = 2f;
    public float avoidanceRadius = 1.3f;
    public override Vector3 CalculateForce()
    {
        float earliest = predictionTime + 1f;
        Vector3 escape = Vector3.zero;
        foreach (SteeringAgent other in SteeringAgent.ActiveAgents)
        {
            if (other == null || other == agent) continue;
            Vector3 position = other.transform.position - transform.position;
            Vector3 relativeVelocity = other.velocity - agent.velocity;
            position.y = relativeVelocity.y = 0f;
            float speedSquared = relativeVelocity.sqrMagnitude;
            if (speedSquared < 0.0001f) continue;
            float time = -Vector3.Dot(position, relativeVelocity) / speedSquared;
            if (time < 0f || time > predictionTime || time >= earliest) continue;
            Vector3 closestOffset = position + relativeVelocity * time;
            float safeRadius = Mathf.Max(avoidanceRadius, agent.Radius + other.Radius + 0.25f);
            if (closestOffset.magnitude >= safeRadius) continue;
            earliest = time;
            escape = -closestOffset;
            if (escape.sqrMagnitude < 0.01f)
                escape = Vector3.Cross(relativeVelocity.normalized, Vector3.up);
        }
        if (earliest > predictionTime) return Vector3.zero;
        return escape.normalized * agent.maxSpeed - agent.velocity;
    }
}
