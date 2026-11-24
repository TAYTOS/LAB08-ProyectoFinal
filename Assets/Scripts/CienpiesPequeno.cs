using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EstadoCienpiesPequeno
{
    Deambulando,
    Huyendo
}

[RequireComponent(typeof(NavMeshAgent))]
public class CienpiesPequeno : MonoBehaviour
{
    [Header("Configuración del Cuerpo Miniatura")]
    public int cantidadSegmentos = 8;
    public float distanciaSegmentos = 0.28f;
    public float anchoSegmento = 0.35f;
    public float alturaSegmento = 0.18f;
    public float longitudSegmento = 0.25f;

    [Header("Velocidades y Movimiento")]
    public float velocidadNormal = 3.2f;
    public float velocidadHuida = 6.5f;
    public float frecuenciaOndulacion = 6.0f;
    public float amplitudOndulacion = 0.12f;

    [Header("Comportamiento con el Jugador")]
    public float radioDeteccionJugador = 6.5f;
    public float radioCalma = 9.5f;
    public float radioPaseo = 8.0f;

    [Header("Material y Visuales")]
    public Material materialCienpies;
    [Tooltip("Opcional: Si se asigna un prefab o modelo 3D (por ej. CienpiesDesierto.obj), se usará su malla en lugar de cubos procedurales.")]
    public GameObject modeloCienpiesPrefab;

    [Header("Estado Actual (Debug)")]
    [SerializeField] private EstadoCienpiesPequeno estadoActual = EstadoCienpiesPequeno.Deambulando;
    [SerializeField] private float distanciaAlJugador;

    private NavMeshAgent agent;
    private Transform player;
    private List<Transform> segmentos = new List<Transform>();
    private List<Transform> patas = new List<Transform>();
    private float tiempoProximaDecision;
    private bool inicializado = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = velocidadNormal;
        agent.angularSpeed = 360f;
        agent.acceleration = 16f;
        agent.stoppingDistance = 0.3f;
        agent.radius = 0.2f;
        agent.height = 0.3f;

        if (materialCienpies == null)
            materialCienpies = Resources.Load<Material>("Materials/CienpiesPequeno_Mat");
#if UNITY_EDITOR
        if (materialCienpies == null)
            materialCienpies = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CienpiesPequeno_Mat.mat");
        if (modeloCienpiesPrefab == null)
            modeloCienpiesPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/CienpiesDesierto.obj");
#endif
    }

    void Start()
    {
        ResolverJugador();
        ConstruirCuerpoProcedural();
        ElegirNuevoDestinoPaseo();
        inicializado = true;
    }

    void ResolverJugador()
    {
        if (player != null) return;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;
        else if (Camera.main != null) player = Camera.main.transform;
    }

    void ConstruirCuerpoProcedural()
    {
        transform.name = "Cienpies_Pequeno";
        CrearVisualSegmento(transform, true, 0);

        Vector3 posInicial = transform.position;
        for (int i = 1; i <= cantidadSegmentos; i++)
        {
            posInicial -= transform.forward * distanciaSegmentos;
            GameObject segObj = new GameObject($"MiniSegmento_{i}");
            segObj.transform.position = posInicial;
            segObj.transform.rotation = transform.rotation;

            CrearVisualSegmento(segObj.transform, false, i);
            segmentos.Add(segObj.transform);
        }
    }

    void CrearVisualSegmento(Transform padre, bool esCabeza, int indice)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(padre, false);
        visual.transform.localPosition = Vector3.up * (alturaSegmento * 0.5f);

        float taper = Mathf.Lerp(1f, 0.5f, (float)indice / cantidadSegmentos);
        visual.transform.localScale = new Vector3(anchoSegmento * taper, alturaSegmento, longitudSegmento);

        // Sin colisiones pesadas para los mini segmentos
        Destroy(visual.GetComponent<Collider>());

        Renderer rend = visual.GetComponent<Renderer>();
        if (materialCienpies != null) rend.material = materialCienpies;

        // Ojos si es cabeza
        if (esCabeza)
        {
            CrearOjo(padre, new Vector3(-0.1f, alturaSegmento * 0.7f, longitudSegmento * 0.5f));
            CrearOjo(padre, new Vector3(0.1f, alturaSegmento * 0.7f, longitudSegmento * 0.5f));
        }

        // Patitas
        CrearPatita(padre, true, indice, anchoSegmento * taper);
        CrearPatita(padre, false, indice, anchoSegmento * taper);
    }

    void CrearOjo(Transform padre, Vector3 localPos)
    {
        GameObject ojo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ojo.name = "Ojo";
        ojo.transform.SetParent(padre, false);
        ojo.transform.localPosition = localPos;
        ojo.transform.localScale = Vector3.one * 0.05f;
        Destroy(ojo.GetComponent<Collider>());

        Renderer r = ojo.GetComponent<Renderer>();
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material m = new Material(s);
        m.color = Color.black;
        r.material = m;
    }

    void CrearPatita(Transform padre, bool izquierda, int indice, float ancho)
    {
        GameObject pata = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pata.name = "Pata";
        pata.transform.SetParent(padre, false);

        float x = izquierda ? -(ancho * 0.55f + 0.05f) : (ancho * 0.55f + 0.05f);
        pata.transform.localPosition = new Vector3(x, alturaSegmento * 0.2f, 0);
        pata.transform.localRotation = Quaternion.Euler(0, 0, izquierda ? 50f : -50f);
        pata.transform.localScale = new Vector3(0.02f, 0.12f, 0.02f);
        Destroy(pata.GetComponent<Collider>());

        Renderer r = pata.GetComponent<Renderer>();
        if (materialCienpies != null) r.material = materialCienpies;
        patas.Add(pata.transform);
    }

    void Update()
    {
        if (!inicializado) return;

        ResolverJugador();
        ActualizarIA();
        ActualizarCinematicaSegmentos();
    }

    void ActualizarIA()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (player != null)
        {
            distanciaAlJugador = Vector3.Distance(transform.position, player.position);
        }

        if (Time.time < tiempoProximaDecision) return;
        tiempoProximaDecision = Time.time + 0.25f;

        // Si el jugador está cerca: ¡HUIR!
        if (player != null && distanciaAlJugador <= radioDeteccionJugador)
        {
            estadoActual = EstadoCienpiesPequeno.Huyendo;
            agent.speed = velocidadHuida;
            HuirDelJugador();
        }
        else if (estadoActual == EstadoCienpiesPequeno.Huyendo && distanciaAlJugador > radioCalma)
        {
            // Se calmó, volver a deambular
            estadoActual = EstadoCienpiesPequeno.Deambulando;
            agent.speed = velocidadNormal;
            ElegirNuevoDestinoPaseo();
        }
        else if (estadoActual == EstadoCienpiesPequeno.Deambulando)
        {
            // Deambular por el suelo
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
            {
                ElegirNuevoDestinoPaseo();
            }
        }
    }

    void HuirDelJugador()
    {
        if (player == null) return;

        Vector3 dirContraria = (transform.position - player.position).normalized;
        dirContraria.y = 0f;

        // Intentar huir en dirección opuesta con algo de dispersión aleatoria
        for (int i = 0; i < 8; i++)
        {
            float anguloRandom = Random.Range(-45f, 45f);
            Vector3 dirVariada = Quaternion.Euler(0, anguloRandom, 0) * dirContraria;
            Vector3 destinoCandidato = transform.position + dirVariada * Random.Range(6f, 10f);

            if (NavMesh.SamplePosition(destinoCandidato, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    void ElegirNuevoDestinoPaseo()
    {
        for (int i = 0; i < 12; i++)
        {
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * radioPaseo;
            randomPoint.y = transform.position.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, radioPaseo * 0.5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    void ActualizarCinematicaSegmentos()
    {
        Transform lider = transform;
        float dt = Time.deltaTime;
        float speed = agent.velocity.magnitude;

        for (int i = 0; i < segmentos.Count; i++)
        {
            Transform seg = segmentos[i];
            if (seg == null) continue;

            Vector3 offset = lider.position - seg.position;
            offset.y = 0f;

            if (offset.sqrMagnitude > 0.0001f)
            {
                Vector3 direccion = offset.normalized;
                Vector3 posDeseada = lider.position - direccion * distanciaSegmentos;

                if (speed > 0.1f)
                {
                    float onda = Mathf.Sin(Time.time * frecuenciaOndulacion - (i * 0.7f)) * amplitudOndulacion;
                    posDeseada += Vector3.Cross(direccion, Vector3.up).normalized * onda;
                }

                posDeseada.y = lider.position.y;
                seg.position = Vector3.Lerp(seg.position, posDeseada, dt * 25f);
                seg.rotation = Quaternion.Slerp(seg.rotation, Quaternion.LookRotation(offset), dt * 20f);
            }

            lider = seg;
        }

        // Animar patitas
        float cycle = Time.time * frecuenciaOndulacion * (speed > 0.1f ? 1.8f : 0.4f);
        for (int i = 0; i < patas.Count; i++)
        {
            if (patas[i] != null)
            {
                float wave = Mathf.Sin(cycle + i * 0.8f);
                patas[i].localRotation = Quaternion.Euler(wave * 20f, 0, (i % 2 == 0 ? 50f : -50f) + wave * 10f);
            }
        }
    }
}
