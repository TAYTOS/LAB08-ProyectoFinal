using UnityEngine;
using System.Collections.Generic;

public enum RoomArchetype
{
    Normal,
    Claustrophobic,
    Batophobic,
    SuperIlluminated,
    Dark,
    Empty,
    Cluttered,
    SafeRoom
}

public class RoomData : MonoBehaviour
{
    [Header("Arquetipo")]
    public RoomArchetype archetype = RoomArchetype.Normal;
    [Range(0f, 1f)]
    public float archetypeIntensity = 0f;

    [Header("Propiedades de la Habitación")]
    public float areaSize;
    public int entranceCount;
    [Range(0f, 1f)]
    public float illuminationLevel = 1.0f; // 1 = Brillante, 0 = Oscuridad total
    
    [Header("Estado del Jugador")]
    public bool isVisited = false;
    
    [Header("Optimización")]
    public GameObject geometryContainer;
    public bool isAlwaysRendered = false;
    
    [HideInInspector]
    public List<RoomData> adjacentRooms = new List<RoomData>();

    [SerializeField] private Bounds movementBounds;
    private Renderer[] roomRenderers;
    private Light[] roomLights;
    private bool? geometryVisible;

    public void SetGeometryVisible(bool visible)
    {
        if (geometryContainer == null || geometryVisible == visible) return;
        geometryVisible = visible;
        if (roomRenderers == null) roomRenderers = geometryContainer.GetComponentsInChildren<Renderer>(true);
        if (roomLights == null) roomLights = geometryContainer.GetComponentsInChildren<Light>(true);
        // Mantener colliders activos permite reconstruir toda la malla, incluso fuera de cámara.
        foreach (Renderer renderer in roomRenderers) if (renderer != null) renderer.forceRenderingOff = !visible;
        foreach (Light light in roomLights) if (light != null) light.enabled = visible;
    }
    public Bounds MovementBounds
    {
        get
        {
            if (movementBounds.size.x > 0f) return movementBounds;
            Collider volume = GetComponent<Collider>();
            return volume != null ? volume.bounds : new Bounds(transform.position, new Vector3(8f, 6f, 8f));
        }
    }

    public Vector3 FloorCenter => new Vector3(MovementBounds.center.x, 0f, MovementBounds.center.z);

    public void ConfigureMovementBounds(Bounds bounds) { movementBounds = bounds; }

    public bool ContainsMovementPoint(Vector3 point, float padding = 0f)
    {
        Bounds bounds = MovementBounds;
        float margin = Mathf.Max(0f, padding);
        return point.x >= bounds.min.x + margin && point.x <= bounds.max.x - margin
            && point.z >= bounds.min.z + margin && point.z <= bounds.max.z - margin;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        if (other.CompareTag("Player") || other.name.Contains("Player") || other.name.Contains("Jugador"))
        {
            isVisited = true;
            // Si la habitación se visita, los cuartos adyacentes se consideran "conocidos"
        }
    }
}
