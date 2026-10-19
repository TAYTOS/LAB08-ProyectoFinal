using UnityEngine;

public class PredictiveEvasionBehavior : SteeringBehavior
{
    [Header("Configuración de Evasión Anticipada")]
    [Tooltip("Distancia de seguridad extra que busca el agente fuera del radio de peligro")]
    public float safetyBuffer = 2.0f;

    public override Vector3 CalculateForce()
    {
        Vector3 totalForce = Vector3.zero;

        // Iterar sobre las zonas de peligro activas calculadas por el sistema balístico
        foreach (var dangerZone in BallisticDangerZone.ActiveDangerZones)
        {
            if (dangerZone == null) continue;

            Vector3 impactPoint = dangerZone.predictedImpactPoint;
            Vector3 diff = transform.position - impactPoint;
            diff.y = 0; // Plano horizontal

            float distance = diff.magnitude;
            float totalThreatRadius = dangerZone.dangerRadius + safetyBuffer;

            // Si el agente está dentro de la zona de riesgo o en trayectoria directa
            if (distance < totalThreatRadius)
            {
                // Calcular urgencia inversamente proporcional al tiempo que queda para el impacto
                float urgency = Mathf.Clamp(1f / Mathf.Max(0.1f, dangerZone.timeToImpact), 1f, 5f);
                
                // Fuerza de huida (Flee) en dirección opuesta al punto de impacto futuro
                Vector3 escapeDir = distance > 0.01f ? diff.normalized : transform.forward;
                Vector3 desiredVelocity = escapeDir * agent.maxSpeed * urgency;
                
                totalForce += (desiredVelocity - agent.velocity);
            }
        }

        return totalForce;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 1.0f);
    }
}
