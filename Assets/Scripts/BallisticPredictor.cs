using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct TrajectoryPoint
{
    public Vector3 position;
    public Vector3 velocity;
    public float time;
}

// Immutable per shot: preview and flight use exactly the same accelerations and step.
public struct BallisticModel
{
    public Vector3 gravity, spin;
    public float drag, magnus, step, radius, lifetime;
    public int collisionMask;
}

public class BallisticPredictor : MonoBehaviour
{
    [Header("Lanzamiento")]
    public float launchSpeed = 18f;
    public Vector3 customGravity = new Vector3(0f, -9.81f, 0f);
    [Header("Aceleraciones aerodinámicas (coeficientes efectivos)")]
    [Min(0f)] public float dragCoefficient = 0.015f;
    public float magnusStrength = 0.02f;
    public Vector3 spinVector = new Vector3(0f, 5f, 0f);
    [Header("Simulación compartida con el vuelo real")]
    [Range(50, 1200)] public int simulationSteps = 400;
    [Range(0.005f, 0.05f)] public float timeStep = 0.02f;
    [Min(0.01f)] public float projectileRadius = 0.13f;
    public LayerMask collisionMask = ~0;
    public Transform ignoredRoot;
    public LineRenderer lineRenderer;
    public bool drawGizmos;
    [Header("Apuntado adelantado numérico")]
    [Min(0.02f)] public float leadTolerance = 0.25f;
    public float LastLeadResidual { get; private set; }

    readonly RaycastHit[] castBuffer = new RaycastHit[128];

    public BallisticModel CaptureModel()
    {
        float step = Mathf.Clamp(timeStep, 0.005f, 0.05f);
        int mask = collisionMask.value & ~(1 << 2); // Ignore Raycast: our projectile/preview objects.
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer > 0) mask &= ~(1 << playerLayer);
        return new BallisticModel {
            gravity = customGravity, spin = spinVector, drag = Mathf.Max(0f, dragCoefficient),
            magnus = magnusStrength, step = step, radius = Mathf.Max(0.01f, projectileRadius),
            lifetime = Mathf.Clamp(simulationSteps, 50, 1200) * step, collisionMask = mask
        };
    }

    public static void Integrate(BallisticModel model, Vector3 position, Vector3 velocity,
        float dt, out Vector3 nextPosition, out Vector3 nextVelocity)
    {
        Vector3 acceleration = model.gravity - 0.5f * model.drag * velocity.magnitude * velocity
            + model.magnus * Vector3.Cross(model.spin, velocity);
        nextPosition = position + velocity * dt + 0.5f * acceleration * dt * dt;
        nextVelocity = velocity + acceleration * dt;
    }

    public static bool CastSegment(BallisticModel model, Vector3 from, Vector3 to,
        Transform owner, RaycastHit[] buffer, out RaycastHit nearest, out float fraction)
    {
        nearest = new RaycastHit();
        fraction = 1f;
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.000001f) return false;
        int count = Physics.SphereCastNonAlloc(from, model.radius, delta / distance, buffer,
            distance, model.collisionMask, QueryTriggerInteraction.Ignore);
        // A saturated buffer must not silently miss a wall. This rare path allocates.
        RaycastHit[] hits = buffer;
        if (count == buffer.Length) {
            hits = Physics.SphereCastAll(from, model.radius, delta / distance, distance,
                model.collisionMask, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        bool found = false;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++) {
            Collider collider = hits[i].collider;
            if (collider == null || (owner != null && collider.transform.IsChildOf(owner))) continue;
            if (collider.CompareTag("Player") || collider.GetComponentInParent<SimplePlayerController>() != null) continue;
            if (hits[i].distance < nearestDistance) {
                nearest = hits[i]; nearestDistance = hits[i].distance; found = true;
            }
        }
        if (found) fraction = Mathf.Clamp01(nearestDistance / distance);
        return found;
    }

    public List<TrajectoryPoint> PredictTrajectory(Vector3 startPos, Vector3 initialVelocity,
        out RaycastHit hitInfo, out bool hasHit)
    {
        return Predict(CaptureModel(), startPos, initialVelocity, ignoredRoot, castBuffer, out hitInfo, out hasHit);
    }

    public static List<TrajectoryPoint> Predict(BallisticModel model, Vector3 startPos,
        Vector3 initialVelocity, Transform owner, RaycastHit[] buffer, out RaycastHit hitInfo, out bool hasHit)
    {
        var points = new List<TrajectoryPoint>();
        Vector3 pos = startPos, vel = initialVelocity;
        float elapsed = 0f;
        hasHit = false; hitInfo = new RaycastHit();
        points.Add(new TrajectoryPoint { position = pos, velocity = vel, time = 0f });
        int steps = Mathf.CeilToInt(model.lifetime / model.step);
        for (int i = 0; i < steps; i++) {
            float dt = Mathf.Min(model.step, model.lifetime - elapsed);
            if (dt <= 0f) break;
            Integrate(model, pos, vel, dt, out Vector3 nextPos, out Vector3 nextVel);
            if (CastSegment(model, pos, nextPos, owner, buffer, out hitInfo, out float fraction)) {
                hasHit = true;
                points.Add(new TrajectoryPoint { position = Vector3.Lerp(pos, nextPos, fraction),
                    velocity = Vector3.Lerp(vel, nextVel, fraction), time = elapsed + dt * fraction });
                break;
            }
            pos = nextPos; vel = nextVel; elapsed += dt;
            points.Add(new TrajectoryPoint { position = pos, velocity = vel, time = elapsed });
        }
        return points;
    }

    // Solve yaw, pitch and flight time by finite-difference Newton iterations against the
    // SAME drag/Magnus integrator. Constant target velocity is an estimate, not a guarantee:
    // a target can turn or evade, and a wall can block the computed shot.
    public bool CalculateLeadLaunchVelocity(Vector3 origin, Vector3 targetPos, Vector3 targetVelocity,
        out Vector3 launchVelocity, out float estimatedFlightTime)
    {
        BallisticModel model = CaptureModel();
        float speed = Mathf.Max(0.1f, launchSpeed);
        Vector3 delta = targetPos - origin;
        float yaw = Mathf.Atan2(delta.x, delta.z);
        float time = Mathf.Clamp(delta.magnitude / speed, 0.05f, model.lifetime);
        Vector3 seed = delta / time - 0.5f * model.gravity * time;
        float pitch = Mathf.Atan2(seed.y, new Vector2(seed.x, seed.z).magnitude);
        LastLeadResidual = float.PositiveInfinity;
        for (int iteration = 0; iteration < 16; iteration++) {
            Vector3 velocity = LaunchVector(yaw, pitch, speed);
            Vector3 error = PositionAfter(model, origin, velocity, time) - targetPos - targetVelocity * time;
            LastLeadResidual = error.magnitude;
            if (LastLeadResidual <= leadTolerance) {
                launchVelocity = velocity; estimatedFlightTime = time; return true;
            }
            const float angleEpsilon = 0.002f, timeEpsilon = 0.01f;
            Vector3 a = (PositionAfter(model, origin, LaunchVector(yaw + angleEpsilon, pitch, speed), time)
                - targetPos - targetVelocity * time - error) / angleEpsilon;
            Vector3 b = (PositionAfter(model, origin, LaunchVector(yaw, pitch + angleEpsilon, speed), time)
                - targetPos - targetVelocity * time - error) / angleEpsilon;
            Vector3 c = (PositionAfter(model, origin, velocity, time + timeEpsilon)
                - targetPos - targetVelocity * (time + timeEpsilon) - error) / timeEpsilon;
            float determinant = Vector3.Dot(a, Vector3.Cross(b, c));
            if (Mathf.Abs(determinant) < 0.00001f) break;
            Vector3 rhs = -error;
            yaw += Mathf.Clamp(Vector3.Dot(rhs, Vector3.Cross(b, c)) / determinant, -0.4f, 0.4f);
            pitch = Mathf.Clamp(pitch + Mathf.Clamp(Vector3.Dot(a, Vector3.Cross(rhs, c)) / determinant,
                -0.35f, 0.35f), -1.45f, 1.45f);
            time = Mathf.Clamp(time + Mathf.Clamp(Vector3.Dot(a, Vector3.Cross(b, rhs)) / determinant,
                -0.4f, 0.4f), 0.05f, model.lifetime);
        }
        launchVelocity = Vector3.zero; estimatedFlightTime = 0f;
        return false;
    }

    static Vector3 LaunchVector(float yaw, float pitch, float speed)
    {
        return new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch),
            Mathf.Cos(yaw) * Mathf.Cos(pitch)) * speed;
    }

    public static Vector3 PositionAfter(BallisticModel model, Vector3 position, Vector3 velocity, float time)
    {
        float remaining = Mathf.Max(0f, time);
        int count = Mathf.CeilToInt(remaining / model.step);
        for (int i = 0; i < count; i++) {
            float dt = Mathf.Min(model.step, remaining);
            Integrate(model, position, velocity, dt, out position, out velocity);
            remaining -= dt;
        }
        return position;
    }

    public void RenderTrajectory(List<TrajectoryPoint> points)
    {
        if (lineRenderer == null) return;
        lineRenderer.positionCount = points == null ? 0 : points.Count;
        if (points != null) for (int i = 0; i < points.Count; i++) lineRenderer.SetPosition(i, points[i].position);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        var points = PredictTrajectory(transform.position, transform.forward * launchSpeed, out RaycastHit hit, out bool hasHit);
        Gizmos.color = Color.cyan;
        for (int i = 1; i < points.Count; i++) Gizmos.DrawLine(points[i - 1].position, points[i].position);
        if (hasHit) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(hit.point, projectileRadius); }
    }
}
