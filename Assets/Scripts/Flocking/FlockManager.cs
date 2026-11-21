using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestor central del enjambre (Flock) de mini bolitas.
/// Orquesta el algoritmo de Flocking (Boids de Craig Reynolds), instanciación automática,
/// ponderación de fuerzas, confinamiento y renderizado visual con estelas y emisión.
/// </summary>
public class FlockManager : MonoBehaviour
{
    public enum TargetMode
    {
        Ignore,
        Seek,
        Flee,
        ScatterOnApproach
    }

    public enum BoundaryType
    {
        Sphere,
        Box
    }

    [Header("Configuración del Enjambre (Mini Bolitas)")]
    [Tooltip("Cantidad de mini bolitas en el enjambre")]
    [Range(5, 200)] public int flockSize = 35;

    [Tooltip("Prefab de la bolita. Si se deja vacío, se auto-generan esferas pequeñas procedimentalmente")]
    public GameObject boidPrefab;

    [Tooltip("Diámetro de las mini bolitas")]
    public float bolitaScale = 0.25f;

    [Tooltip("Color base y emisivo de las mini bolitas")]
    public Color bolitaColor = new Color(0.1f, 0.9f, 1f, 1f);

    [Tooltip("Material personalizado opcional (URP Lit / Unlit)")]
    public Material customMaterial;

    [Tooltip("¿Generar estela luminosa (TrailRenderer) en cada bolita?")]
    public bool enableTrails = true;

    [Tooltip("Duración de la estela en segundos")]
    public float trailTime = 0.35f;

    [Tooltip("¿Vuelo libre 3D o restringido al plano del suelo (Y fijo)?")]
    public bool is3D = true;

    [Tooltip("Radio de dispersión inicial al instanciar las bolitas")]
    public float spawnRadius = 3f;

    [Header("Reglas de Reynolds (Pesos de Fuerzas)")]
    [Tooltip("Separación: Fuerza para evitar colisiones con vecinos")]
    public float separationWeight = 1.5f;

    [Tooltip("Alineación: Fuerza para igualar dirección y velocidad con el grupo")]
    public float alignmentWeight = 1.0f;

    [Tooltip("Cohesión: Fuerza para permanecer unidos al centro de masa")]
    public float cohesionWeight = 1.0f;

    [Header("Radios Sensoriales")]
    [Tooltip("Radio de detección de compañeros para Alineación y Cohesión")]
    public float neighborRadius = 3.5f;

    [Tooltip("Distancia mínima de separación entre bolitas")]
    public float separationRadius = 0.85f;

    [Header("Dinámica y Velocidades")]
    [Tooltip("Velocidad mínima para evitar que queden estáticas")]
    public float minSpeed = 2.0f;

    [Tooltip("Velocidad máxima de desplazamiento")]
    public float maxSpeed = 5.5f;

    [Tooltip("Fuerza máxima de giro aplicada por segundo")]
    public float maxSteerForce = 8.0f;

    [Tooltip("Velocidad de rotación visual")]
    public float rotationSpeed = 8.0f;

    [Header("Objetivo e Interacción (Target / Jugador)")]
    [Tooltip("Transform del objetivo (si es null, busca automáticamente al Player)")]
    public Transform target;

    [Tooltip("Comportamiento del enjambre respecto al objetivo")]
    public TargetMode targetMode = TargetMode.ScatterOnApproach;

    [Tooltip("Peso de atracción / repulsión hacia el objetivo")]
    public float targetWeight = 1.2f;

    [Tooltip("Distancia a la que se activa la dispersión de pánico (ScatterOnApproach)")]
    public float fleeDistance = 4.0f;

    [Header("Confinamiento y Límites")]
    [Tooltip("Tipo de volumen de confinamiento si no hay RoomData")]
    public BoundaryType boundaryType = BoundaryType.Box;

    [Tooltip("Desplazamiento del centro de confinamiento relativo a este objeto")]
    public Vector3 boundsCenterOffset = Vector3.zero;

    [Tooltip("Radio para confinamiento esférico")]
    public float boundsRadius = 8.0f;

    [Tooltip("Dimensiones de la caja de confinamiento (X, Y, Z)")]
    public Vector3 boundsBoxSize = new Vector3(14f, 4f, 14f);

    [Tooltip("Fuerza correctiva para devolver a las bolitas dentro de los límites")]
    public float boundaryWeight = 2.5f;

    [Tooltip("Habitación procedural opcional (RoomData) para confinar dentro del laberinto")]
    public RoomData boundRoom;

    [Header("Evitación de Obstáculos")]
    [Tooltip("¿Detectar y esquivar muros/obstáculos con raycast frontal?")]
    public bool avoidObstacles = true;

    [Tooltip("Capas de colisión que representan muros y obstáculos")]
    public LayerMask obstacleMask = ~0;

    [Tooltip("Distancia de anticipación para esquivar")]
    public float avoidDistance = 1.5f;

    [Tooltip("Peso de la fuerza de evasión de obstáculos")]
    public float avoidObstaclesWeight = 3.0f;

    [Header("Merodeo Suave (Wander)")]
    [Tooltip("Fuerza de variación estocástica sutil")]
    public float wanderWeight = 0.25f;

    [Header("Depuración")]
    public bool drawGizmos = true;

    private readonly List<BoidAgent> boids = new List<BoidAgent>();
    public IReadOnlyList<BoidAgent> ActiveBoids => boids;

    public Vector3 BoundsCenter => transform.position + boundsCenterOffset;

    private Material runtimeSharedMaterial;

    void Start()
    {
        // Buscar al jugador automáticamente si no hay objetivo
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        SpawnFlock();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Limpiar referencias nulas si alguna bolita fue destruida externamente
        for (int i = boids.Count - 1; i >= 0; i--)
        {
            if (boids[i] == null)
            {
                boids.RemoveAt(i);
            }
        }

        // Simular cada mini bolita
        int count = boids.Count;
        for (int i = 0; i < count; i++)
        {
            boids[i].StepSimulation(dt);
        }
    }

    /// <summary>
    /// Instancia el conjunto de mini bolitas según flockSize y configuración.
    /// </summary>
    public void SpawnFlock()
    {
        ClearFlock();

        for (int i = 0; i < flockSize; i++)
        {
            Vector3 spawnOffset = is3D
                ? Random.insideUnitSphere * spawnRadius
                : new Vector3(Random.Range(-spawnRadius, spawnRadius), 0f, Random.Range(-spawnRadius, spawnRadius));

            Vector3 spawnPos = BoundsCenter + spawnOffset;

            GameObject bolitaObj;
            BoidAgent agent;

            if (boidPrefab != null)
            {
                bolitaObj = Instantiate(boidPrefab, spawnPos, Quaternion.identity, transform);
                agent = bolitaObj.GetComponent<BoidAgent>();
                if (agent == null) agent = bolitaObj.AddComponent<BoidAgent>();
            }
            else
            {
                // Auto-generación procedimental de mini bolita
                bolitaObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bolitaObj.name = $"MiniBolita_{i:D2}";
                bolitaObj.transform.SetParent(transform);
                bolitaObj.transform.position = spawnPos;
                bolitaObj.transform.localScale = Vector3.one * bolitaScale;

                // Collider como trigger para no bloquear el paso del jugador
                Collider col = bolitaObj.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                // Material con emisión URP
                Renderer rend = bolitaObj.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = GetOrCreateMaterial();
                }

                // Estela opcional
                if (enableTrails)
                {
                    TrailRenderer tr = bolitaObj.AddComponent<TrailRenderer>();
                    tr.time = trailTime;
                    tr.startWidth = bolitaScale * 0.5f;
                    tr.endWidth = 0f;
                    tr.material = rend != null ? rend.sharedMaterial : null;
                    tr.startColor = bolitaColor;
                    tr.endColor = new Color(bolitaColor.r, bolitaColor.g, bolitaColor.b, 0f);
                }

                agent = bolitaObj.AddComponent<BoidAgent>();
            }

            Vector3 initialDir = is3D ? Random.insideUnitSphere : new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            if (initialDir.sqrMagnitude < 0.01f) initialDir = transform.forward;

            Vector3 initialVelocity = initialDir.normalized * Random.Range(minSpeed, maxSpeed);
            if (!is3D) initialVelocity.y = 0f;

            agent.Initialize(this, initialVelocity);
            boids.Add(agent);
        }
    }

    /// <summary>
    /// Elimina todas las mini bolitas activas del enjambre.
    /// </summary>
    public void ClearFlock()
    {
        foreach (var boid in boids)
        {
            if (boid != null && boid.gameObject != null)
            {
                if (Application.isPlaying) Destroy(boid.gameObject);
                else DestroyImmediate(boid.gameObject);
            }
        }
        boids.Clear();
    }

    private Material GetOrCreateMaterial()
    {
        if (customMaterial != null) return customMaterial;
        if (runtimeSharedMaterial != null) return runtimeSharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Standard");

        runtimeSharedMaterial = new Material(shader)
        {
            name = "Mat_MiniBolita_Flock_Runtime"
        };

        if (runtimeSharedMaterial.HasProperty("_BaseColor"))
            runtimeSharedMaterial.SetColor("_BaseColor", bolitaColor);
        else if (runtimeSharedMaterial.HasProperty("_Color"))
            runtimeSharedMaterial.SetColor("_Color", bolitaColor);

        // Habilitar emisión para efecto bioluminiscente en los cuartos oscuros
        if (runtimeSharedMaterial.HasProperty("_EmissionColor"))
        {
            runtimeSharedMaterial.EnableKeyword("_EMISSION");
            runtimeSharedMaterial.SetColor("_EmissionColor", bolitaColor * 2.2f);
        }

        return runtimeSharedMaterial;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        Vector3 center = BoundsCenter;

        if (boundRoom != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(boundRoom.FloorCenter, boundRoom.RoomSize);
        }
        else if (boundaryType == BoundaryType.Sphere)
        {
            Gizmos.DrawWireSphere(center, boundsRadius);
        }
        else
        {
            Gizmos.DrawWireCube(center, boundsBoxSize);
        }

        // Dibujar línea hacia el objetivo
        if (target != null)
        {
            Gizmos.color = targetMode == TargetMode.Flee ? Color.red : Color.yellow;
            Gizmos.DrawLine(center, target.position);
            if (targetMode == TargetMode.ScatterOnApproach)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
                Gizmos.DrawWireSphere(target.position, fleeDistance);
            }
        }
    }
}
