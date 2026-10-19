using UnityEngine;
public class SeparationBehavior : SteeringBehavior
{
    public float separationRadius = 2f;
    public override Vector3 CalculateForce()
    {
        Vector3 force = Vector3.zero;
        foreach (SteeringAgent neighbor in SteeringAgent.ActiveAgents)
        {
            if (neighbor == null || neighbor == agent) continue;
            Vector3 away = transform.position - neighbor.transform.position; away.y = 0f;
            float distance = away.magnitude;
            if (distance >= separationRadius) continue;
            if (distance < 0.01f)
                away = agent.GetInstanceID() < neighbor.GetInstanceID() ? Vector3.right : Vector3.left;
            else away /= distance;
            force += away * (1f - Mathf.Clamp01(distance / separationRadius)) * agent.maxSpeed;
        }
        return Vector3.ClampMagnitude(force, agent.maxSpeed * 2f);
    }
}
