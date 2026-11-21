using UnityEngine;

/// <summary>
/// Comportamiento integrado de Flocking de Craig Reynolds (Boids) para SteeringAgent:
/// Agrupa en un único componente las 3 leyes fundamentales:
/// 1. Separación (repulsión a corta distancia)
/// 2. Alineación (igualar vector velocidad del grupo)
/// 3. Cohesión (atracción al centro de masa local)
/// </summary>
public class FlockingBehavior : SteeringBehavior
{
    [Header("Radios Sensoriales")]
    [Tooltip("Radio para detectar vecinos en Alineación y Cohesión")]
    public float neighborRadius = 4.5f;

    [Tooltip("Radio personal de confort para Separación")]
    public float separationRadius = 1.8f;

    [Header("Ponderación de Reglas de Reynolds")]
    [Tooltip("Peso relativo de la Separación")]
    public float separationWeight = 1.5f;

    [Tooltip("Peso relativo de la Alineación")]
    public float alignmentWeight = 1.0f;

    [Tooltip("Peso relativo de la Cohesión")]
    public float cohesionWeight = 1.0f;

    public override Vector3 CalculateForce()
    {
        Vector3 separationForce = Vector3.zero;
        Vector3 averageVelocity = Vector3.zero;
        Vector3 centerOfMass = Vector3.zero;
        int neighborCount = 0;
        int separationCount = 0;

        Vector3 myPos = transform.position;

        foreach (SteeringAgent neighbor in SteeringAgent.ActiveAgents)
        {
            if (neighbor == null || neighbor == agent) continue;

            Vector3 diff = neighbor.transform.position - myPos;
            diff.y = 0f;
            float distSqr = diff.sqrMagnitude;

            // 1. Separación
            if (distSqr < separationRadius * separationRadius && distSqr > 0.00001f)
            {
                float dist = Mathf.Sqrt(distSqr);
                Vector3 away = -diff / dist;
                float intensity = 1f - Mathf.Clamp01(dist / separationRadius);
                separationForce += away * intensity * agent.maxSpeed;
                separationCount++;
            }

            // 2 & 3. Alineación y Cohesión
            if (distSqr < neighborRadius * neighborRadius && distSqr > 0.00001f)
            {
                averageVelocity += neighbor.velocity;
                centerOfMass += neighbor.transform.position;
                neighborCount++;
            }
        }

        Vector3 totalForce = Vector3.zero;

        if (separationCount > 0)
        {
            totalForce += Vector3.ClampMagnitude(separationForce, agent.maxSpeed * 2f) * separationWeight;
        }

        if (neighborCount > 0)
        {
            // Alineación
            averageVelocity /= neighborCount;
            averageVelocity.y = 0f;
            if (averageVelocity.sqrMagnitude > 0.001f)
            {
                Vector3 desiredAlign = averageVelocity.normalized * agent.maxSpeed;
                Vector3 alignSteer = desiredAlign - agent.velocity;
                totalForce += alignSteer * alignmentWeight;
            }

            // Cohesión
            centerOfMass /= neighborCount;
            centerOfMass.y = myPos.y;
            Vector3 toCenter = centerOfMass - myPos;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.001f)
            {
                Vector3 desiredCohesion = toCenter.normalized * agent.maxSpeed;
                Vector3 cohesionSteer = desiredCohesion - agent.velocity;
                totalForce += cohesionSteer * cohesionWeight;
            }
        }

        totalForce.y = 0f;
        return totalForce;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, neighborRadius);
    }
}
