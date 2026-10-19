using System.Collections.Generic;
using UnityEngine;

// A swept sphere is advanced by the same integrator as the preview, not by a second
// Rigidbody integrator. The spherecast detects impacts even between rendered frames.
public class BallisticProjectile : MonoBehaviour
{
    public Vector3 Velocity { get; private set; }
    public float FlightTime { get; private set; }
    public bool IsFlying { get; private set; }
    public bool manualSimulation;
    public BallisticDangerZone DangerZone { get; private set; }
    public bool InitialPredictionHadHit { get; private set; }
    public Vector3 InitialPredictedImpact { get; private set; }
    public float InitialPredictedTime { get; private set; }

    BallisticModel model;
    Transform owner;
    PredictiveThrower thrower;
    Material visualMaterial;
    float accumulator, nextDangerUpdate, radiusOfDanger, effectTime;
    readonly RaycastHit[] castBuffer = new RaycastHit[128];
    Vector3 effectScale;

    public void Initialize(BallisticModel settings, Vector3 velocity, Transform ownerRoot,
        PredictiveThrower source, float dangerRadius, Material material,
        List<TrajectoryPoint> predictedPoints, bool predictedHit, RaycastHit hit)
    {
        model = settings; Velocity = velocity; owner = ownerRoot; thrower = source;
        radiusOfDanger = dangerRadius; visualMaterial = material;
        FlightTime = 0f; accumulator = 0f; IsFlying = true;
        InitialPredictionHadHit = predictedHit;
        if (predictedHit && predictedPoints != null && predictedPoints.Count > 0) {
            InitialPredictedImpact = hit.point;
            InitialPredictedTime = predictedPoints[predictedPoints.Count - 1].time;
            UpdateDanger(hit.point, InitialPredictedTime);
        }
        nextDangerUpdate = 0.12f;
    }

    void Update()
    {
        if (IsFlying) {
            if (!manualSimulation) AdvanceSimulation(Time.deltaTime);
        } else if (effectTime > 0f) {
            effectTime -= Time.deltaTime;
            transform.localScale = effectScale * Mathf.Max(0f, effectTime / 0.18f);
            if (effectTime <= 0f) Destroy(gameObject);
        }
    }

    // Public deterministic entry point for comparing preview/actual impact in tests.
    public void AdvanceSimulation(float deltaTime)
    {
        if (!IsFlying || deltaTime <= 0f) return;
        float previousFlightTime = FlightTime;
        accumulator += deltaTime;
        int budget = 0;
        while (IsFlying && accumulator + 0.000001f >= model.step && budget++ < 256) {
            float dt = Mathf.Min(model.step, model.lifetime - FlightTime);
            if (dt <= 0.000001f) { Cancel(); break; }
            Vector3 from = transform.position;
            BallisticPredictor.Integrate(model, from, Velocity, dt, out Vector3 next, out Vector3 nextVelocity);
            if (BallisticPredictor.CastSegment(model, from, next, owner, castBuffer,
                out RaycastHit hit, out float fraction)) {
                transform.position = Vector3.Lerp(from, next, fraction);
                Velocity = Vector3.Lerp(Velocity, nextVelocity, fraction);
                FlightTime += dt * fraction;
                Impact(hit);
                break;
            }
            transform.position = next; Velocity = nextVelocity; FlightTime += dt;
            accumulator -= dt;
            if (FlightTime >= model.lifetime - 0.000001f) { Cancel(); break; }
        }
        if (DangerZone != null && IsFlying)
            DangerZone.timeToImpact = Mathf.Max(0f, DangerZone.timeToImpact - (FlightTime - previousFlightTime));
        if (IsFlying && FlightTime >= nextDangerUpdate) {
            RefreshDangerPrediction(); nextDangerUpdate = FlightTime + 0.12f;
        }
    }

    void RefreshDangerPrediction()
    {
        BallisticModel remainingModel = model;
        remainingModel.lifetime = Mathf.Max(model.step, model.lifetime - FlightTime);
        var future = BallisticPredictor.Predict(remainingModel, transform.position, Velocity, owner,
            castBuffer, out RaycastHit hit, out bool hasHit);
        if (hasHit) UpdateDanger(hit.point, future[future.Count - 1].time);
        else ClearDanger();
    }

    void UpdateDanger(Vector3 point, float time)
    {
        if (DangerZone == null) {
            GameObject zone = new GameObject("Zona de impacto prevista");
            zone.layer = 2;
            DangerZone = zone.AddComponent<BallisticDangerZone>();
            DangerZone.managedByProjectile = true;
        }
        DangerZone.Configure(point, time, radiusOfDanger, visualMaterial);
    }

    void Impact(RaycastHit hit)
    {
        IsFlying = false; ClearDanger();
        if (thrower != null) thrower.RecordImpact(this, hit.point, FlightTime);
        EnemyAIController enemy = hit.collider == null ? null : hit.collider.GetComponentInParent<EnemyAIController>();
        if (enemy != null) enemy.ReceiveProjectileImpact();
        transform.position = hit.point;
        effectScale = Vector3.one * model.radius * 5f;
        transform.localScale = effectScale; effectTime = 0.18f;
        if (TryGetComponent<Renderer>(out var renderer)) {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(1f, 0.4f, 0.08f));
            block.SetColor("_Color", new Color(1f, 0.4f, 0.08f));
            renderer.SetPropertyBlock(block);
        }
    }

    void ClearDanger()
    {
        if (DangerZone == null) return;
        DangerZone.gameObject.SetActive(false); Destroy(DangerZone.gameObject); DangerZone = null;
    }

    public void Cancel()
    {
        IsFlying = false; ClearDanger();
        gameObject.SetActive(false); Destroy(gameObject);
    }

    void OnDisable() { IsFlying = false; ClearDanger(); }
    void OnDestroy() { ClearDanger(); if (thrower != null) thrower.ForgetProjectile(this); }
}
