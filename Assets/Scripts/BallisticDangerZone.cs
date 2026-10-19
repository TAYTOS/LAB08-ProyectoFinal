using UnityEngine;
using System.Collections.Generic;

public class BallisticDangerZone : MonoBehaviour
{
    public static List<BallisticDangerZone> ActiveDangerZones = new List<BallisticDangerZone>();

    [Header("Zona de Impacto")]
    public Vector3 predictedImpactPoint;
    public float dangerRadius = 4f;
    public float timeToImpact = 2f;

    void OnEnable()
    {
        ActiveDangerZones.Add(this);
    }

    void OnDisable()
    {
        ActiveDangerZones.Remove(this);
    }

    void Update()
    {
        timeToImpact -= Time.deltaTime;
        if (timeToImpact <= 0f)
        {
            // El impacto ocurrió o la amenaza expiró
            Destroy(gameObject, 0.5f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawSphere(predictedImpactPoint, dangerRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(predictedImpactPoint, dangerRadius);
    }
}
