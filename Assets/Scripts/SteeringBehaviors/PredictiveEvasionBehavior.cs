using UnityEngine;
public class PredictiveEvasionBehavior : SteeringBehavior
{
    public float safetyBuffer = 1.5f;
    public override Vector3 CalculateForce()
    {
        Vector3 force = Vector3.zero;
        foreach (BallisticDangerZone zone in BallisticDangerZone.ActiveDangerZones)
        {
            if (zone == null || zone.timeToImpact <= 0f) continue;
            Vector3 away = transform.position - zone.predictedImpactPoint; away.y = 0f;
            float radius = zone.dangerRadius + safetyBuffer;
            if (away.magnitude >= radius) continue;
            if (away.sqrMagnitude < 0.01f) away = transform.right;
            Vector3 escape = zone.predictedImpactPoint + away.normalized * (radius + 1f);
            float urgency = Mathf.Clamp(1f / Mathf.Max(0.2f, zone.timeToImpact), 1f, 3f);
            force += (agent.GetNavigableDesiredVelocity(escape) - agent.velocity) * urgency;
        }
        return force;
    }
}
