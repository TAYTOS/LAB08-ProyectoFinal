using UnityEngine;

/// <summary>
/// Representa a un agente individual (una mini bolita) dentro del enjambre (Flock).
/// Implementa el algoritmo de Flocking de Craig Reynolds (Separación, Alineación, Cohesión)
/// además de fuerzas adicionales de evasión de obstáculos, seguimiento/huida de objetivos y confinamiento.
/// </summary>
[SelectionBase]
public class BoidAgent : MonoBehaviour
{
    [HideInInspector] public FlockManager manager;
    [HideInInspector] public Vector3 velocity;

    private Transform cachedTransform;

    public Vector3 Position => cachedTransform != null ? cachedTransform.position : transform.position;
    public Vector3 Velocity => velocity;

    void Awake()
    {
        cachedTransform = transform;
    }

    public void Initialize(FlockManager flockManager, Vector3 initialVelocity)
    {
        manager = flockManager;
        velocity = initialVelocity;
        cachedTransform = transform;

        if (velocity.sqrMagnitude > 0.001f)
        {
            cachedTransform.rotation = Quaternion.LookRotation(velocity.normalized);
        }
    }

    /// <summary>
    /// Ejecuta un paso de simulación para esta mini bolita. Es invocado por el FlockManager.
    /// </summary>
    public void StepSimulation(float dt)
    {
        if (manager == null || dt <= 0f) return;

        Vector3 myPos = cachedTransform.position;

        Vector3 separationSum = Vector3.zero;
        Vector3 alignmentSum = Vector3.zero;
        Vector3 cohesionSum = Vector3.zero;
        int neighborCount = 0;
        int separationCount = 0;

        float neighborRadSqr = manager.neighborRadius * manager.neighborRadius;
        float sepRadSqr = manager.separationRadius * manager.separationRadius;

        // Evaluar vecinos en el enjambre
        var allBoids = manager.ActiveBoids;
        int total = allBoids.Count;

        for (int i = 0; i < total; i++)
        {
            BoidAgent other = allBoids[i];
            if (other == null || other == this) continue;

            Vector3 diff = other.Position - myPos;
            if (!manager.is3D) diff.y = 0f;

            float distSqr = diff.sqrMagnitude;

            // 1. REGLA DE SEPARACIÓN: Evitar chocar con compañeros muy cercanos
            if (distSqr < sepRadSqr && distSqr > 0.00001f)
            {
                float dist = Mathf.Sqrt(distSqr);
                Vector3 away = -diff / dist; // Dirección opuesta
                // Ponderación inversamente proporcional a la distancia
                separationSum += away * (1f - (dist / manager.separationRadius));
                separationCount++;
            }

            // 2. REGLAS DE ALINEACIÓN Y COHESIÓN: Compañeros en el radio de visión
            if (distSqr < neighborRadSqr && distSqr > 0.00001f)
            {
                alignmentSum += other.velocity;
                cohesionSum += other.Position;
                neighborCount++;
            }
        }

        Vector3 steerSeparation = Vector3.zero;
        Vector3 steerAlignment = Vector3.zero;
        Vector3 steerCohesion = Vector3.zero;

        if (separationCount > 0)
        {
            Vector3 desiredSep = (separationSum / separationCount).normalized * manager.maxSpeed;
            steerSeparation = Vector3.ClampMagnitude(desiredSep - velocity, manager.maxSteerForce);
        }

        if (neighborCount > 0)
        {
            // Alineación: desear la velocidad promedio de los vecinos
            Vector3 avgVelocity = alignmentSum / neighborCount;
            if (!manager.is3D) avgVelocity.y = 0f;

            if (avgVelocity.sqrMagnitude > 0.001f)
            {
                Vector3 desiredAlign = avgVelocity.normalized * manager.maxSpeed;
                steerAlignment = Vector3.ClampMagnitude(desiredAlign - velocity, manager.maxSteerForce);
            }

            // Cohesión: desear la posición del centro de masa de los vecinos
            Vector3 centerOfMass = cohesionSum / neighborCount;
            Vector3 toCenter = centerOfMass - myPos;
            if (!manager.is3D) toCenter.y = 0f;

            if (toCenter.sqrMagnitude > 0.001f)
            {
                Vector3 desiredCohesion = toCenter.normalized * manager.maxSpeed;
                steerCohesion = Vector3.ClampMagnitude(desiredCohesion - velocity, manager.maxSteerForce);
            }
        }

        // Fuerzas complementarias del ecosistema
        Vector3 steerTarget = CalculateTargetForce(myPos);
        Vector3 steerBoundary = CalculateBoundaryForce(myPos);
        Vector3 steerObstacles = CalculateObstacleAvoidance(myPos);
        Vector3 steerWander = CalculateWanderForce();

        // Suma ponderada de fuerzas de Reynolds + extensiones
        Vector3 totalForce = (steerSeparation * manager.separationWeight)
                           + (steerAlignment * manager.alignmentWeight)
                           + (steerCohesion * manager.cohesionWeight)
                           + (steerTarget * manager.targetWeight)
                           + (steerBoundary * manager.boundaryWeight)
                           + (steerObstacles * manager.avoidObstaclesWeight)
                           + (steerWander * manager.wanderWeight);

        if (!manager.is3D) totalForce.y = 0f;

        totalForce = Vector3.ClampMagnitude(totalForce, manager.maxSteerForce);

        // Integración de aceleración y velocidad
        velocity += totalForce * dt;

        // Limitar velocidad entre minSpeed y maxSpeed
        float speed = velocity.magnitude;
        if (speed < 0.001f)
        {
            Vector3 randomHeading = manager.is3D ? Random.insideUnitSphere : new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            velocity = randomHeading.normalized * manager.minSpeed;
        }
        else if (speed < manager.minSpeed)
        {
            velocity = velocity.normalized * manager.minSpeed;
        }
        else if (speed > manager.maxSpeed)
        {
            velocity = velocity.normalized * manager.maxSpeed;
        }

        if (!manager.is3D) velocity.y = 0f;

        // Actualizar posición
        cachedTransform.position += velocity * dt;

        // Orientación suave hacia la dirección de movimiento
        if (velocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized);
            cachedTransform.rotation = Quaternion.Slerp(cachedTransform.rotation, targetRotation, dt * manager.rotationSpeed);
        }
    }

    private Vector3 CalculateTargetForce(Vector3 myPos)
    {
        if (manager.target == null || manager.targetMode == FlockManager.TargetMode.Ignore)
            return Vector3.zero;

        Vector3 targetPos = manager.target.position;
        Vector3 toTarget = targetPos - myPos;
        if (!manager.is3D) toTarget.y = 0f;

        float dist = toTarget.magnitude;
        if (dist < 0.001f) return Vector3.zero;

        if (manager.targetMode == FlockManager.TargetMode.Seek)
        {
            Vector3 desired = toTarget.normalized * manager.maxSpeed;
            return Vector3.ClampMagnitude(desired - velocity, manager.maxSteerForce);
        }
        else if (manager.targetMode == FlockManager.TargetMode.Flee)
        {
            Vector3 desired = -toTarget.normalized * manager.maxSpeed;
            return Vector3.ClampMagnitude(desired - velocity, manager.maxSteerForce);
        }
        else if (manager.targetMode == FlockManager.TargetMode.ScatterOnApproach)
        {
            if (dist < manager.fleeDistance)
            {
                // Dispersión de pánico cuando el objetivo (Jugador) se acerca
                float panicIntensity = 1f - (dist / manager.fleeDistance);
                Vector3 away = -toTarget.normalized;
                // Pequeño desvío perpendicular para que se dispersen radialmente
                Vector3 scatterDir = (away + Random.insideUnitSphere * 0.35f).normalized;
                if (!manager.is3D) scatterDir.y = 0f;

                Vector3 desired = scatterDir * (manager.maxSpeed * (1f + panicIntensity));
                return Vector3.ClampMagnitude(desired - velocity, manager.maxSteerForce * 1.5f);
            }
            else
            {
                // Cuando está lejos, orbita o se siente atraído suavemente
                Vector3 desired = toTarget.normalized * (manager.maxSpeed * 0.5f);
                return Vector3.ClampMagnitude(desired - velocity, manager.maxSteerForce * 0.5f);
            }
        }

        return Vector3.zero;
    }

    private Vector3 CalculateBoundaryForce(Vector3 myPos)
    {
        // 1. Si está confinado a un RoomData específico del laberinto
        if (manager.boundRoom != null)
        {
            if (!manager.boundRoom.ContainsMovementPoint(myPos, 0.5f))
            {
                Vector3 toCenter = manager.boundRoom.FloorCenter - myPos;
                if (!manager.is3D) toCenter.y = 0f;
                Vector3 desired = toCenter.normalized * manager.maxSpeed;
                return Vector3.ClampMagnitude(desired - velocity, manager.maxSteerForce * 1.5f);
            }
            return Vector3.zero;
        }

        Vector3 center = manager.BoundsCenter;

        // 2. Confinamiento esférico
        if (manager.boundaryType == FlockManager.BoundaryType.Sphere)
        {
            Vector3 offset = myPos - center;
            if (!manager.is3D) offset.y = 0f;

            float dist = offset.magnitude;
            if (dist > manager.boundsRadius)
            {
                float excess = dist - manager.boundsRadius;
                Vector3 desired = -offset.normalized * manager.maxSpeed;
                return Vector3.ClampMagnitude((desired - velocity) * (1f + excess * 0.5f), manager.maxSteerForce * 2f);
            }
        }
        // 3. Confinamiento en caja (Bounding Box)
        else if (manager.boundaryType == FlockManager.BoundaryType.Box)
        {
            Vector3 half = manager.boundsBoxSize * 0.5f;
            Vector3 local = myPos - center;
            Vector3 pull = Vector3.zero;

            if (local.x > half.x) pull.x = -1f;
            else if (local.x < -half.x) pull.x = 1f;

            if (manager.is3D)
            {
                if (local.y > half.y) pull.y = -1f;
                else if (local.y < -half.y) pull.y = 1f;
            }

            if (local.z > half.z) pull.z = -1f;
            else if (local.z < -half.z) pull.z = 1f;

            if (pull.sqrMagnitude > 0.001f)
            {
                Vector3 desired = pull.normalized * manager.maxSpeed;
                return Vector3.ClampMagnitude(desired - velocity, manager.maxSteerForce * 1.5f);
            }
        }

        return Vector3.zero;
    }

    private Vector3 CalculateObstacleAvoidance(Vector3 myPos)
    {
        if (!manager.avoidObstacles) return Vector3.zero;

        Vector3 forward = velocity.sqrMagnitude > 0.01f ? velocity.normalized : cachedTransform.forward;
        float checkDist = manager.avoidDistance;
        float radius = manager.bolitaScale * 0.6f;

        Ray ray = new Ray(myPos, forward);
        if (Physics.SphereCast(ray, radius, out RaycastHit hit, checkDist, manager.obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // Reflejar o esquivar según la normal del obstáculo
            Vector3 avoidDirection = Vector3.Reflect(forward, hit.normal);
            if (!manager.is3D) avoidDirection.y = 0f;

            Vector3 desired = avoidDirection.normalized * manager.maxSpeed;
            return Vector3.ClampMagnitude(desired - velocity, manager.maxSteerForce * 2f);
        }

        return Vector3.zero;
    }

    private Vector3 CalculateWanderForce()
    {
        if (manager.wanderWeight <= 0f) return Vector3.zero;

        Vector3 jitter = manager.is3D
            ? Random.insideUnitSphere
            : new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));

        return jitter.normalized * (manager.maxSpeed * 0.4f);
    }
}
