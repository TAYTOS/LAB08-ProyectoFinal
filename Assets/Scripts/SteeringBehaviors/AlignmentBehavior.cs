using UnityEngine;

/// <summary>
/// Comportamiento de Alineación (Alignment) de Reynolds:
/// Calcula una fuerza directriz que orienta la velocidad del agente hacia la velocidad promedio
/// de los agentes vecinos registrados en SteeringAgent.ActiveAgents.
/// </summary>
public class AlignmentBehavior : SteeringBehavior
{
    [Tooltip("Radio sensorial para percibir y alinearse con vecinos")]
    public float neighborRadius = 4.5f;

    public override Vector3 CalculateForce()
    {
        Vector3 averageVelocity = Vector3.zero;
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
                averageVelocity += neighbor.velocity;
                count++;
            }
        }

        if (count == 0 || averageVelocity.sqrMagnitude < 0.001f)
            return Vector3.zero;

        averageVelocity /= count;
        averageVelocity.y = 0f;

        Vector3 desired = averageVelocity.normalized * agent.maxSpeed;
        return desired - agent.velocity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, neighborRadius);
    }
}
