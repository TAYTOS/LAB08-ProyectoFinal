using System.Collections.Generic;
using UnityEngine;

public class BallisticDangerZone : MonoBehaviour
{
    public static List<BallisticDangerZone> ActiveDangerZones = new List<BallisticDangerZone>();
    public Vector3 predictedImpactPoint;
    public float dangerRadius = 3f;
    public float timeToImpact = 2f;
    public bool managedByProjectile;
    LineRenderer ring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() { ActiveDangerZones.Clear(); }

    void OnEnable() { if (!ActiveDangerZones.Contains(this)) ActiveDangerZones.Add(this); }
    void OnDisable() { ActiveDangerZones.Remove(this); }
    void OnDestroy() { ActiveDangerZones.Remove(this); }

    public void Configure(Vector3 point, float remainingTime, float radius, Material material = null)
    {
        predictedImpactPoint = point;
        timeToImpact = Mathf.Max(0f, remainingTime);
        dangerRadius = Mathf.Max(0.1f, radius);
        transform.position = point;
        if (material != null && ring == null) {
            ring = gameObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = material; ring.useWorldSpace = true; ring.loop = true;
            ring.widthMultiplier = 0.045f; ring.positionCount = 48;
            ring.startColor = ring.endColor = new Color(1f, 0.3f, 0.1f, 1f);
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
        }
        if (ring != null) for (int i = 0; i < ring.positionCount; i++) {
            float angle = i * 2f * Mathf.PI / ring.positionCount;
            ring.SetPosition(i, point + new Vector3(Mathf.Cos(angle) * dangerRadius, 0.05f, Mathf.Sin(angle) * dangerRadius));
        }
    }

    void Update()
    {
        if (managedByProjectile) return; // The projectile owns its simulation clock and lifetime.
        timeToImpact -= Time.deltaTime;
        if (timeToImpact <= 0f) Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0f, 0.5f);
        Gizmos.DrawWireSphere(predictedImpactPoint, dangerRadius);
    }
}
