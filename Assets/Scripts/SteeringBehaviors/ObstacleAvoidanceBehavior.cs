using UnityEngine;

public class ObstacleAvoidanceBehavior : SteeringBehavior
{
    public float avoidDistance = 3f;
    public float rayOffset = 0.5f;
    public LayerMask obstacleLayer;

    public override Vector3 CalculateForce()
    {
        Vector3 force = Vector3.zero;
        
        // Tres rayos: Centro, Izquierda, Derecha
        force += GetAvoidanceForce(transform.position, transform.forward);
        force += GetAvoidanceForce(transform.position - transform.right * rayOffset, transform.forward);
        force += GetAvoidanceForce(transform.position + transform.right * rayOffset, transform.forward);

        return force;
    }
    
    private Vector3 GetAvoidanceForce(Vector3 origin, Vector3 direction)
    {
        if (Physics.Raycast(origin, direction, out RaycastHit hit, avoidDistance, obstacleLayer))
        {
            // A menor distancia de colisión, mayor la fuerza de evasión
            float multiplier = 1.0f + ((avoidDistance - hit.distance) / avoidDistance);
            Vector3 avoidForce = hit.normal * agent.maxSpeed * multiplier;
            return avoidForce - agent.velocity;
        }
        return Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * avoidDistance);
        Gizmos.DrawLine(transform.position - transform.right * rayOffset, transform.position - transform.right * rayOffset + transform.forward * avoidDistance);
        Gizmos.DrawLine(transform.position + transform.right * rayOffset, transform.position + transform.right * rayOffset + transform.forward * avoidDistance);
    }
}
