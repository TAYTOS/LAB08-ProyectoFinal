using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct TrajectoryPoint
{
    public Vector3 position;
    public Vector3 velocity;
    public float time;
}

public class BallisticPredictor : MonoBehaviour
{
    [Header("Configuración Balística")]
    public float launchSpeed = 15f;
    public Vector3 customGravity = new Vector3(0, -9.81f, 0);

    [Header("Resistencia del Aire (Drag) y Aerodinámica")]
    [Tooltip("Coeficiente de arrastre aerodinámico")]
    public float dragCoefficient = 0.05f;
    [Tooltip("Intensidad de elevación o desvío por rotación (Efecto Magnus)")]
    public float magnusStrength = 0.02f;
    public Vector3 spinVector = new Vector3(0, 5f, 0); // Giro sobre su eje

    [Header("Muestreo y Simulación Anticipada")]
    [Range(10, 200)]
    public int simulationSteps = 60;
    public float timeStep = 0.04f;
    public LayerMask collisionMask = ~0;

    [Header("Visualización")]
    public LineRenderer lineRenderer;
    public bool drawGizmos = true;

    void Awake()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    /// <summary>
    /// Simula la trayectoria paso a paso hacia el futuro considerando gravedad, drag y spin.
    /// Retorna la lista de posiciones y detecta el punto de impacto.
    /// </summary>
    public List<TrajectoryPoint> PredictTrajectory(Vector3 startPos, Vector3 initialVelocity, out RaycastHit hitInfo, out bool hasHit)
    {
        List<TrajectoryPoint> points = new List<TrajectoryPoint>();
        Vector3 currentPos = startPos;
        Vector3 currentVel = initialVelocity;
        float currentTime = 0f;

        hasHit = false;
        hitInfo = new RaycastHit();

        points.Add(new TrajectoryPoint { position = currentPos, velocity = currentVel, time = currentTime });

        for (int i = 0; i < simulationSteps; i++)
        {
            // 1. Calcular fuerzas: Gravedad + Resistencia del Aire (Drag) + Magnus (Spin)
            Vector3 dragForce = -0.5f * dragCoefficient * currentVel.magnitude * currentVel;
            Vector3 magnusForce = magnusStrength * Vector3.Cross(spinVector, currentVel);
            Vector3 acceleration = customGravity + dragForce + magnusForce;

            // 2. Integrar velocidad y siguiente posición
            Vector3 nextVel = currentVel + acceleration * timeStep;
            Vector3 nextPos = currentPos + currentVel * timeStep + 0.5f * acceleration * (timeStep * timeStep);
            currentTime += timeStep;

            // 3. Validar trayectorias con Raycast entre segmentos continuos
            Vector3 stepDir = nextPos - currentPos;
            float stepDistance = stepDir.magnitude;

            if (stepDistance > 0.001f && Physics.Raycast(currentPos, stepDir.normalized, out RaycastHit hit, stepDistance, collisionMask))
            {
                hasHit = true;
                hitInfo = hit;
                points.Add(new TrajectoryPoint { position = hit.point, velocity = nextVel, time = currentTime });
                break;
            }

            currentPos = nextPos;
            currentVel = nextVel;
            points.Add(new TrajectoryPoint { position = currentPos, velocity = currentVel, time = currentTime });
        }

        return points;
    }

    /// <summary>
    /// Apuntado Predictivo (Lead Target):
    /// Calcula la velocidad y ángulo de lanzamiento requeridos para interceptar un objetivo en movimiento.
    /// </summary>
    public bool CalculateLeadLaunchVelocity(Vector3 origin, Vector3 targetPos, Vector3 targetVelocity, out Vector3 launchVelocity, out float estimatedFlightTime)
    {
        launchVelocity = Vector3.zero;
        estimatedFlightTime = 0f;

        // Estimación iterativa del tiempo de vuelo
        float speed = launchSpeed;
        float distance = Vector3.Distance(origin, targetPos);
        estimatedFlightTime = distance / speed;

        for (int i = 0; i < 4; i++)
        {
            // Posición futura predicha del blanco: r_futuro = r_0 + v * t
            Vector3 predictedPos = targetPos + targetVelocity * estimatedFlightTime;
            
            // Vector delta
            Vector3 delta = predictedPos - origin;
            float distXZ = new Vector2(delta.x, delta.z).magnitude;
            float deltaY = delta.y;
            float g = Mathf.Abs(customGravity.y);

            // Ecuación balística clásica: v^4 - g*(g*x^2 + 2*y*v^2)
            float v2 = speed * speed;
            float discriminant = (v2 * v2) - g * (g * distXZ * distXZ + 2f * deltaY * v2);

            if (discriminant < 0)
            {
                // No hay ángulo físico suficiente para alcanzar la distancia
                return false;
            }

            // Seleccionar ángulo de tiro bajo o rasante (signo negativo)
            float angle = Mathf.Atan((v2 - Mathf.Sqrt(discriminant)) / (g * distXZ));
            Vector3 dirXZ = new Vector3(delta.x, 0, delta.z).normalized;
            launchVelocity = dirXZ * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));

            estimatedFlightTime = distXZ / (speed * Mathf.Cos(angle));
        }

        return true;
    }

    /// <summary>
    /// Renderiza la trayectoria calculada en un LineRenderer si está disponible
    /// </summary>
    public void RenderTrajectory(List<TrajectoryPoint> points)
    {
        if (lineRenderer == null || points == null || points.Count == 0) return;
        lineRenderer.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            lineRenderer.SetPosition(i, points[i].position);
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Previsualizar la trayectoria de lanzamiento hacia adelante en el editor
        Vector3 startVel = transform.forward * launchSpeed;
        var points = PredictTrajectory(transform.position, startVel, out RaycastHit hit, out bool hasHit);

        Gizmos.color = Color.green;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Gizmos.DrawLine(points[i].position, points[i + 1].position);
        }

        if (hasHit)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hit.point, 0.5f);
        }
    }
}
