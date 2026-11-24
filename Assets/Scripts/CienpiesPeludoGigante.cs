using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CienpiesPeludoGigante : MonoBehaviour
{
    [Header("Configuración del Cuerpo Procedural")]
    public int cantidadSegmentos = 16;
    public float distanciaSegmentos = 1.25f;
    public float anchoSegmento = 2.0f;
    public float alturaSegmento = 1.1f;
    public float longitudSegmento = 1.3f;

    [Header("Ondulación y Movimiento")]
    public float velocidadPatrulla = 3.8f;
    public float frecuenciaOndulacion = 3.5f;
    public float amplitudOndulacion = 0.45f;
    public float faseOndulacion = 0.4f;

    [Header("Materiales")]
    public Material materialPeludo;
    public Material materialPatas;

    [Header("Mecánica de Montura")]
    public float radioDeteccionMontura = 1.4f;
    public LayerMask capaJugador = ~0;

    [Header("Estado Actual (Debug)")]
    [SerializeField] private bool jugadorEstaMontado = false;
    [SerializeField] private int indiceSegmentoJugador = -1;
    [SerializeField] private int waypointActual = 0;

    public bool JugadorEstaMontado => jugadorEstaMontado;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private CharacterController playerController;

    private List<Transform> segmentos = new List<Transform>();
    private List<Vector3> posicionesAnteriores = new List<Vector3>();
    private List<Transform> patasIzquierdas = new List<Transform>();
    private List<Transform> patasDerechas = new List<Transform>();

    private List<Vector3> rutaPasillos = new List<Vector3>();
    private float tiempoProximaBusquedaRuta = 0f;
    private bool inicializado = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = velocidadPatrulla;
        agent.angularSpeed = 140f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 1.2f;

        if (materialPeludo == null)
            materialPeludo = Resources.Load<Material>("Materials/CienpiesPeludo_Mat");
#if UNITY_EDITOR
        if (materialPeludo == null)
            materialPeludo = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CienpiesPeludo_Mat.mat");
#endif
        if (materialPatas == null)
            materialPatas = materialPeludo;
    }

    void Start()
    {
        ResolverJugador();
        ConstruirCuerpoProcedural();
        GenerarRutaPasillosLargos();
        inicializado = true;
    }

    void ResolverJugador()
    {
        if (playerTransform != null && playerController != null) return;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
        {
            playerTransform = go.transform;
            playerController = go.GetComponent<CharacterController>();
        }
    }

    void ConstruirCuerpoProcedural()
    {
        // 1. Configurar Cabeza
        transform.name = "Cienpies_Peludo_Gigante";
        CrearVisualSegmento(transform, true, 0);

        posicionesAnteriores.Add(transform.position);

        // 2. Crear los N segmentos seguidores
        Vector3 posInicial = transform.position;
        for (int i = 1; i <= cantidadSegmentos; i++)
        {
            posInicial -= transform.forward * distanciaSegmentos;
            GameObject segObj = new GameObject($"Segmento_{i}");
            segObj.transform.position = posInicial;
            segObj.transform.rotation = transform.rotation;

            CrearVisualSegmento(segObj.transform, false, i);

            segmentos.Add(segObj.transform);
            posicionesAnteriores.Add(segObj.transform.position);
        }
    }

    void CrearVisualSegmento(Transform padre, bool esCabeza, int indice)
    {
        // Contenedor visual
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = esCabeza ? "Cabeza_Visual" : "Lomo_Visual";
        visual.transform.SetParent(padre, false);
        visual.transform.localPosition = Vector3.up * (alturaSegmento * 0.5f);

        // Ajustar escala del segmento
        float escalaAncho = esCabeza ? anchoSegmento * 1.15f : anchoSegmento * Mathf.Lerp(1.1f, 0.7f, (float)indice / cantidadSegmentos);
        float escalaAlto = alturaSegmento;
        float escalaLargo = longitudSegmento;
        visual.transform.localScale = new Vector3(escalaAncho, escalaAlto, escalaLargo);

        // Asignar Material Peludo
        Renderer rend = visual.GetComponent<Renderer>();
        if (materialPeludo != null) rend.material = materialPeludo;

        // Si es cabeza, agregar ojos brillantes y antenas
        if (esCabeza)
        {
            CrearOjo(padre, new Vector3(-0.6f, alturaSegmento * 0.8f, longitudSegmento * 0.55f));
            CrearOjo(padre, new Vector3(0.6f, alturaSegmento * 0.8f, longitudSegmento * 0.55f));

            CrearAntena(padre, new Vector3(-0.4f, alturaSegmento, longitudSegmento * 0.6f), new Vector3(15, -20, 0));
            CrearAntena(padre, new Vector3(0.4f, alturaSegmento, longitudSegmento * 0.6f), new Vector3(15, 20, 0));
        }

        // Crear patas izquierda y derecha para el segmento
        CrearPata(padre, true, indice, escalaAncho);
        CrearPata(padre, false, indice, escalaAncho);

        // Plataforma de montura en el lomo (Collider superior para que el jugador pueda pararse cómodamente)
        BoxCollider boxCol = visual.GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            // Ajustar colisión para que sea fácil caminar por encima
            boxCol.size = new Vector3(1.1f, 1.0f, 1.1f);
        }
    }

    void CrearOjo(Transform padre, Vector3 localPos)
    {
        GameObject ojo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ojo.name = "Ojo";
        ojo.transform.SetParent(padre, false);
        ojo.transform.localPosition = localPos;
        ojo.transform.localScale = Vector3.one * 0.28f;

        Destroy(ojo.GetComponent<Collider>());
        Renderer r = ojo.GetComponent<Renderer>();
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material m = new Material(s);
        m.color = new Color(1f, 0.3f, 0f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(1f, 0.2f, 0f) * 3f);
        r.material = m;
    }

    void CrearAntena(Transform padre, Vector3 localPos, Vector3 rotacion)
    {
        GameObject antena = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        antena.name = "Antena";
        antena.transform.SetParent(padre, false);
        antena.transform.localPosition = localPos;
        antena.transform.localRotation = Quaternion.Euler(rotacion);
        antena.transform.localScale = new Vector3(0.06f, 0.6f, 0.06f);

        Destroy(antena.GetComponent<Collider>());
        Renderer r = antena.GetComponent<Renderer>();
        if (materialPeludo != null) r.material = materialPeludo;
    }

    void CrearPata(Transform padre, bool izquierda, int indice, float anchoSeg)
    {
        GameObject pata = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pata.name = izquierda ? "Pata_Izq" : "Pata_Der";
        pata.transform.SetParent(padre, false);

        float xOffset = izquierda ? -(anchoSeg * 0.55f + 0.3f) : (anchoSeg * 0.55f + 0.3f);
        pata.transform.localPosition = new Vector3(xOffset, alturaSegmento * 0.3f, 0f);

        float zRot = izquierda ? 45f : -45f;
        pata.transform.localRotation = Quaternion.Euler(0, 0, zRot);
        pata.transform.localScale = new Vector3(0.09f, 0.55f, 0.09f);

        Destroy(pata.GetComponent<Collider>());
        Renderer r = pata.GetComponent<Renderer>();
        if (materialPeludo != null) r.material = materialPeludo;

        if (izquierda) patasIzquierdas.Add(pata.transform);
        else patasDerechas.Add(pata.transform);
    }

    void Update()
    {
        if (!inicializado) return;

        ResolverJugador();
        ActualizarPatrullaPasillos();
        ActualizarCinematicaSegmentos();
        ActualizarAnimacionPatas();
        ProcesarMonturaJugador();
    }

    void ActualizarPatrullaPasillos()
    {
        if (rutaPasillos == null || rutaPasillos.Count == 0)
        {
            if (Time.time >= tiempoProximaBusquedaRuta)
            {
                tiempoProximaBusquedaRuta = Time.time + 3f;
                GenerarRutaPasillosLargos();
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            waypointActual = (waypointActual + 1) % rutaPasillos.Count;
            agent.SetDestination(rutaPasillos[waypointActual]);
        }
    }

    public void GenerarRutaPasillosLargos()
    {
        rutaPasillos.Clear();

        // 1. Buscar cuartos y pasillos grandes a través de RoomData
        RoomData[] rooms = Object.FindObjectsByType<RoomData>(FindObjectsSortMode.None);
        List<Vector3> puntosCandidatos = new List<Vector3>();

        if (rooms != null && rooms.Length > 0)
        {
            // Ordenar por tamaño de área para seleccionar las salas más grandes y pasillos principales
            List<RoomData> salasGrandes = new List<RoomData>(rooms);
            salasGrandes.Sort((a, b) => b.areaSize.CompareTo(a.areaSize));

            int maxSalas = Mathf.Min(8, salasGrandes.Count);
            for (int i = 0; i < maxSalas; i++)
            {
                if (salasGrandes[i].archetype != RoomArchetype.SafeRoom)
                {
                    puntosCandidatos.Add(salasGrandes[i].FloorCenter);
                }
            }
        }

        // 2. Si no hay suficientes salas o estamos en escena de prueba, generar puntos a lo largo del NavMesh
        if (puntosCandidatos.Count < 3)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * (Mathf.PI * 2f / 8f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 25f;
                Vector3 samplePos = transform.position + offset;
                if (NavMesh.SamplePosition(samplePos, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                {
                    puntosCandidatos.Add(hit.position);
                }
            }
        }

        // 3. Validar alcanzabilidad con el NavMeshAgent
        foreach (Vector3 p in puntosCandidatos)
        {
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    rutaPasillos.Add(hit.position);
                }
            }
        }

        if (rutaPasillos.Count > 0)
        {
            waypointActual = 0;
            agent.SetDestination(rutaPasillos[0]);
        }
    }

    void ActualizarCinematicaSegmentos()
    {
        Transform lider = transform;
        float dt = Time.deltaTime;
        float velocidadActual = agent.velocity.magnitude;

        for (int i = 0; i < segmentos.Count; i++)
        {
            Transform seg = segmentos[i];
            if (seg == null) continue;

            // Dirección hacia el líder o segmento anterior
            Vector3 offset = lider.position - seg.position;
            offset.y = 0f; // Mantener altura uniforme

            float distancia = offset.magnitude;
            if (distancia > 0.001f)
            {
                // Mantener distancia fija con el segmento anterior
                Vector3 direccion = offset.normalized;
                Vector3 posDeseada = lider.position - direccion * distanciaSegmentos;

                // Ondulación sinusoidal lateral
                float onda = Mathf.Sin(Time.time * frecuenciaOndulacion - (i * faseOndulacion)) * amplitudOndulacion;
                Vector3 vectorLateral = Vector3.Cross(direccion, Vector3.up).normalized * onda;

                // Aplicar solo si está en movimiento
                if (velocidadActual > 0.1f)
                {
                    posDeseada += vectorLateral;
                }

                // Ajustar al suelo NavMesh si es necesario
                posDeseada.y = lider.position.y;

                seg.position = Vector3.Lerp(seg.position, posDeseada, dt * 15f);

                // Rotar hacia el líder
                Quaternion rotDeseada = Quaternion.LookRotation(offset);
                seg.rotation = Quaternion.Slerp(seg.rotation, rotDeseada, dt * 12f);
            }

            lider = seg;
        }
    }

    void ActualizarAnimacionPatas()
    {
        float speed = agent.velocity.magnitude;
        float cycle = Time.time * frecuenciaOndulacion * (speed > 0.1f ? 1.5f : 0.3f);

        for (int i = 0; i < patasIzquierdas.Count; i++)
        {
            float wave = Mathf.Sin(cycle + i * 0.6f);
            if (patasIzquierdas[i] != null)
            {
                patasIzquierdas[i].localRotation = Quaternion.Euler(wave * 25f, 0f, 45f + wave * 10f);
            }
            if (patasDerechas[i] != null)
            {
                patasDerechas[i].localRotation = Quaternion.Euler(-wave * 25f, 0f, -45f - wave * 10f);
            }
        }
    }

    void ProcesarMonturaJugador()
    {
        if (playerTransform == null || playerController == null) return;

        bool montadoEsteFrame = false;
        int segmentoDetectado = -1;
        Vector3 deltaMovimiento = Vector3.zero;

        // Comprobar la cabeza y todos los segmentos
        List<Transform> todosLosSegmentos = new List<Transform> { transform };
        todosLosSegmentos.AddRange(segmentos);

        for (int i = 0; i < todosLosSegmentos.Count; i++)
        {
            Transform s = todosLosSegmentos[i];
            if (s == null) continue;

            // Calcular distancia horizontal y altura
            Vector3 diff = playerTransform.position - s.position;
            float distHorizontal = new Vector2(diff.x, diff.z).magnitude;
            float diffAltura = diff.y;

            // Si el jugador está sobre el lomo (dentro del radio y un poco por encima)
            if (distHorizontal <= radioDeteccionMontura && diffAltura >= 0.2f && diffAltura <= alturaSegmento + 1.2f)
            {
                montadoEsteFrame = true;
                segmentoDetectado = i;

                // Calcular delta de movimiento del segmento montado
                if (i < posicionesAnteriores.Count)
                {
                    deltaMovimiento = s.position - posicionesAnteriores[i];
                }
                break;
            }
        }

        // Mover al jugador junto con el ciempiés
        if (montadoEsteFrame)
        {
            jugadorEstaMontado = true;
            indiceSegmentoJugador = segmentoDetectado;

            if (deltaMovimiento.sqrMagnitude > 0.000001f && deltaMovimiento.magnitude < 2f)
            {
                playerController.Move(deltaMovimiento);
            }
        }
        else
        {
            jugadorEstaMontado = false;
            indiceSegmentoJugador = -1;
        }

        // Actualizar registro de posiciones para el próximo frame
        posicionesAnteriores[0] = transform.position;
        for (int i = 0; i < segmentos.Count; i++)
        {
            if (i + 1 < posicionesAnteriores.Count && segmentos[i] != null)
            {
                posicionesAnteriores[i + 1] = segmentos[i].position;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.4f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radioDeteccionMontura);

        if (rutaPasillos != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < rutaPasillos.Count; i++)
            {
                Gizmos.DrawSphere(rutaPasillos[i], 0.7f);
                if (i + 1 < rutaPasillos.Count)
                    Gizmos.DrawLine(rutaPasillos[i], rutaPasillos[i + 1]);
                else if (rutaPasillos.Count > 1)
                    Gizmos.DrawLine(rutaPasillos[i], rutaPasillos[0]);
            }
        }
    }
}
