using UnityEngine;

/// <summary>
/// Comportamiento de Cohesión (Cohesion) de Reynolds:
/// Calcula una fuerza que conduce al agente hacia el centro de masa (posición promedio)
/// de sus vecinos cercanos registrados en SteeringAgent.ActiveAgents.
/// </summary>
public class CohesionBehavior : SteeringBehavior
{
    [Tooltip("Radio sensorial para percibir el centro de masa de los vecinos")]
    public float neighborRadius = 4.5f;

    public override Vector3 CalculateForce()
    {
        Vector3 centerOfMass = Vector3.zero;
        int count = 0;

        Vector3 myPos = transform.position;

        foreach (SteeringAgent neighbor in SteeringAgent.ActiveAgents)
        {
            if (neighbor == null || neighbor == agent) continue;

            Vector3 offset = neighbor.transform.position - myPos;
            offset.y = 0f;
            float distSqr = offset.sqrMagnitude;

            if (distSqr > 0.0001f && distSqr < neighborRadius * neighborRadius)
            {
                centerOfMass += neighbor.transform.position;
                count++;
            }
        }

        if (count == 0)
            return Vector3.zero;

        centerOfMass /= count;
        centerOfMass.y = myPos.y;

        Vector3 desiredOffset = centerOfMass - myPos;
        desiredOffset.y = 0f;

        if (desiredOffset.sqrMagnitude < 0.001f)
            return Vector3.zero;

        Vector3 desiredVelocity = desiredOffset.normalized * agent.maxSpeed;
        return desiredVelocity - agent.velocity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, neighborRadius);
    }
}
