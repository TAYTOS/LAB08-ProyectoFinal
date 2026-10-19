using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallisticPredictor))]
public class PredictiveThrower : MonoBehaviour
{
    [Header("Controles: RMB apuntar, LMB lanzar, G rápido, T anticipación")]
    public bool leadTargeting;
    public float cooldown = 0.55f;
    public float dangerRadius = 3f;
    public float aimDistance = 70f;
    public float targetAcquireRadius = 0.5f;
    public bool showControls = true;
    public BallisticPredictor predictor;
    public Transform aimingCamera;

    public Vector3 LastPredictedImpact { get; private set; }
    public Vector3 LastActualImpact { get; private set; }
    public float LastPredictedImpactTime { get; private set; }
    public float LastActualImpactTime { get; private set; }
    public float LastPredictionError { get; private set; }
    public float LastTimeError { get; private set; }
    public bool LastLaunchHadPrediction { get; private set; }
    public bool HasImpactComparison { get; private set; }
    public int LaunchCount { get; private set; }
    public int ImpactCount { get; private set; }
    public bool LeadSolutionValid { get; private set; }
    public int ActiveProjectileCount => projectiles.Count;

    readonly List<BallisticProjectile> projectiles = new List<BallisticProjectile>();
    Material lineMaterial, projectileMaterial;
    LineRenderer preview, impactRing;
    SimplePlayerController player;
    AnxietyManager anxiety;
    float nextLaunchTime, nextLeadTime;
    bool isAiming;
    Transform cachedTarget;
    Vector3 cachedLeadVelocity;
    bool cachedLeadValid;

    void Awake()
    {
        predictor = GetComponent<BallisticPredictor>();
        predictor.ignoredRoot = transform;
        player = GetComponent<SimplePlayerController>(); anxiety = GetComponent<AnxietyManager>();
        lineMaterial = MakeMaterial(Color.white);
        projectileMaterial = MakeMaterial(new Color(0.2f, 0.95f, 1f));
        preview = CreateLine("Trayectoria prevista", 0.045f, Color.cyan);
        impactRing = CreateLine("Impacto previsto", 0.035f, new Color(1f, 0.6f, 0.12f));
        impactRing.loop = true;
        predictor.lineRenderer = preview;
        LastPredictionError = float.NaN; LastTimeError = float.NaN;
        HidePreview();
    }

    static Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = "Balística runtime" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }

    LineRenderer CreateLine(string objectName, float width, Color color)
    {
        var go = new GameObject(objectName); go.layer = 2; go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial; line.useWorldSpace = true; line.widthMultiplier = width;
        line.startColor = line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
        return line;
    }

    bool CanThrow()
    {
        return Time.timeScale > 0f && !OptionsMenuUI.IsOpen && !GameIntroductionUI.IsOpen
            && !OptionsMenuUI.EscapeConsumedThisFrame && (anxiety == null || !anxiety.isDead)
            && (player == null || player.enabled);
    }

    void Update()
    {
        if (!CanThrow()) { isAiming = false; HidePreview(); return; }
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame) {
            leadTargeting = !leadTargeting; nextLeadTime = 0f;
        }
        isAiming = Mouse.current != null && Mouse.current.rightButton.isPressed;
        if (isAiming) ShowPreview(); else HidePreview();
        bool quickThrow = Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;
        bool aimedThrow = isAiming && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (quickThrow || aimedThrow) TryLaunch();
    }

    Transform ResolveCamera()
    {
        if (aimingCamera != null) return aimingCamera;
        if (player != null && player.playerCamera != null) return player.playerCamera;
        return Camera.main == null ? transform : Camera.main.transform;
    }

    bool BuildAim(out Vector3 origin, out Vector3 velocity, out List<TrajectoryPoint> points,
        out RaycastHit impact, out bool hasHit, bool forceLeadUpdate = false)
    {
        Transform cameraTransform = ResolveCamera();
        BallisticModel model = predictor.CaptureModel();
        // Spawn just outside the player in both first/third person. Aim still follows the camera.
        origin = transform.position + Vector3.up * 0.45f + transform.forward * 0.7f;
        Vector3 direction = cameraTransform.forward;
        RaycastHit[] aimHits = Physics.SphereCastAll(cameraTransform.position, targetAcquireRadius,
            cameraTransform.forward, aimDistance, model.collisionMask, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Transform target = null; Vector3 targetPoint = Vector3.zero;
        foreach (var hit in aimHits) {
            if (hit.collider == null || hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
            nearest = hit.distance;
            direction = (hit.point - origin).normalized;
            EnemyAIController enemy = hit.collider.GetComponentInParent<EnemyAIController>();
            SteeringAgent steering = hit.collider.GetComponentInParent<SteeringAgent>();
            target = enemy != null ? enemy.transform : steering != null ? steering.transform : null;
            targetPoint = hit.collider.bounds.center;
        }
        velocity = direction * Mathf.Max(0.1f, predictor.launchSpeed);
        LeadSolutionValid = false;
        if (leadTargeting && target != null) {
            if (forceLeadUpdate || target != cachedTarget || Time.time >= nextLeadTime) {
                Vector3 targetVelocity = Vector3.zero;
                if (target.TryGetComponent<SteeringAgent>(out var steeringAgent) && steeringAgent.enabled)
                    targetVelocity = steeringAgent.velocity;
                else if (target.TryGetComponent<NavMeshAgent>(out var navAgent) && navAgent.enabled && navAgent.isOnNavMesh)
                    targetVelocity = navAgent.velocity;
                cachedLeadValid = predictor.CalculateLeadLaunchVelocity(origin, targetPoint, targetVelocity,
                    out cachedLeadVelocity, out _);
                cachedTarget = target; nextLeadTime = Time.time + 0.12f;
            }
            if (cachedLeadValid) { velocity = cachedLeadVelocity; LeadSolutionValid = true; }
        } else cachedTarget = null;
        points = predictor.PredictTrajectory(origin, velocity, out impact, out hasHit);
        // Avoid spawning inside a nearby wall. Spherecasts do not report initial overlaps.
        Collider[] overlaps = Physics.OverlapSphere(origin, model.radius, model.collisionMask, QueryTriggerInteraction.Ignore);
        foreach (var collider in overlaps)
            if (!collider.transform.IsChildOf(transform) && collider.GetComponentInParent<SimplePlayerController>() == null) return false;
        return true;
    }

    void ShowPreview()
    {
        if (!BuildAim(out _, out _, out var points, out RaycastHit impact, out bool hasHit)) { HidePreview(); return; }
        preview.enabled = true; predictor.RenderTrajectory(points);
        impactRing.enabled = hasHit; impactRing.positionCount = hasHit ? 40 : 0;
        if (hasHit) {
            Vector3 normal = impact.normal;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up).normalized;
            if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.right;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            for (int i = 0; i < 40; i++) {
                float angle = i * 2f * Mathf.PI / 40f;
                impactRing.SetPosition(i, impact.point + normal * 0.035f
                    + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * dangerRadius);
            }
        }
    }

    public bool TryLaunch()
    {
        if (!isActiveAndEnabled || !CanThrow() || Time.time < nextLaunchTime) return false;
        if (!BuildAim(out Vector3 origin, out Vector3 velocity, out var points, out RaycastHit hit, out bool hasHit, true)) return false;
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        BallisticModel model = predictor.CaptureModel();
        go.name = "Proyectil predictivo"; go.layer = 2;
        go.transform.position = origin; go.transform.localScale = Vector3.one * model.radius * 2f;
        Collider collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        go.GetComponent<Renderer>().sharedMaterial = projectileMaterial;
        BallisticProjectile projectile = go.AddComponent<BallisticProjectile>();
        projectiles.Add(projectile);
        projectile.Initialize(model, velocity, transform, this, dangerRadius,
            lineMaterial, points, hasHit, hit);
        LastLaunchHadPrediction = hasHit; HasImpactComparison = false;
        LastPredictedImpact = hasHit ? hit.point : points[points.Count - 1].position;
        LastPredictedImpactTime = points[points.Count - 1].time;
        LastPredictionError = float.NaN; LastTimeError = float.NaN;
        LaunchCount++; nextLaunchTime = Time.time + cooldown;
        return true;
    }

    public void RecordImpact(BallisticProjectile projectile, Vector3 point, float flightTime)
    {
        ImpactCount++; LastActualImpact = point; LastActualImpactTime = flightTime;
        LastLaunchHadPrediction = projectile.InitialPredictionHadHit;
        HasImpactComparison = projectile.InitialPredictionHadHit;
        if (HasImpactComparison) {
            LastPredictedImpact = projectile.InitialPredictedImpact; LastPredictedImpactTime = projectile.InitialPredictedTime;
            LastPredictionError = Vector3.Distance(point, LastPredictedImpact);
            LastTimeError = Mathf.Abs(flightTime - LastPredictedImpactTime);
        }
        // Moving targets/changed geometry can change the actual impact; these are measurements,
        // not an assertion that all shots reproduce the initial static-world prediction.
    }

    public void ForgetProjectile(BallisticProjectile projectile) { projectiles.Remove(projectile); }
    public void HidePreview()
    {
        if (preview != null) { preview.positionCount = 0; preview.enabled = false; }
        if (impactRing != null) { impactRing.positionCount = 0; impactRing.enabled = false; }
    }

    void OnDisable()
    {
        HidePreview();
        foreach (var projectile in projectiles.ToArray()) if (projectile != null) projectile.Cancel();
        projectiles.Clear();
    }

    void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
        if (projectileMaterial != null) Destroy(projectileMaterial);
    }

    void OnGUI()
    {
        if (!showControls || !CanThrow()) return;
        GUI.Label(new Rect(18f, Screen.height - 56f, 720f, 24f),
            "Clic derecho: apuntar | Clic izquierdo: lanzar | G: lanzamiento rápido | T: anticipación " + (leadTargeting ? "ON" : "OFF"));
        if (isAiming && leadTargeting) GUI.Label(new Rect(18f, Screen.height - 82f, 600f, 24f),
            LeadSolutionValid ? "Apuntado adelantado estimado" : "Apunta a un enemigo a tu alcance para anticipar su movimiento");
    }
}
