using UnityEngine;

public class WallFollowingBehavior : SteeringBehavior
{
    public float wallDistance = 2f;
    public float rayLength = 3f;
    public LayerMask wallLayer;

    public override Vector3 CalculateForce()
    {
        // Lanzamos rayos en 4 direcciones (diagonales y laterales) para encontrar una pared
        Vector3[] rayDirections = {
            transform.forward + transform.right, // Diagonal derecha
            transform.forward - transform.right, // Diagonal izquierda
            transform.right,                     // Derecha
            -transform.right                     // Izquierda
        };

        bool foundWall = false;
        Vector3 targetNormal = Vector3.zero;
        Vector3 hitPoint = Vector3.zero;

        foreach (Vector3 dir in rayDirections)
        {
            if (Physics.Raycast(transform.position, dir.normalized, out RaycastHit hit, rayLength, wallLayer))
            {
                foundWall = true;
                targetNormal = hit.normal;
                hitPoint = hit.point;
                break; // Seguimos la primera pared encontrada
            }
        }

        if (foundWall)
        {
            // 1. Calcular vector paralelo a la pared
            Vector3 parallelDir = Vector3.Cross(targetNormal, Vector3.up).normalized;
            
            // Decidir qué dirección paralela seguir según nuestra velocidad actual
            if (Vector3.Dot(parallelDir, agent.velocity) < 0)
            {
                parallelDir = -parallelDir;
            }

            // 2. Ajustar distancia a la pared
            Vector3 targetPos = hitPoint + (targetNormal * wallDistance);
            Vector3 correctionDir = (targetPos - transform.position).normalized;

            // 3. Combinar movimiento paralelo con corrección
            Vector3 desired = (parallelDir + correctionDir).normalized * agent.maxSpeed;
            return desired - agent.velocity;
        }

        return Vector3.zero;
    }
}
