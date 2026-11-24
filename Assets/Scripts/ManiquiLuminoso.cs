using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum ManiquiLuminosityMode
{
    AutoDetectFromSpawn,
    ForceWhite,
    ForceBlack
}

public enum ManiquiBehaviorState
{
    Blanco_Paseando,        // Camina tranquilamente por el entorno
    Negro_BuscandoJugador,  // Jugador lejos: se aproxima manteniendo la distancia
    Negro_AcechandoCerca,   // Jugador cerca: permanece en las sombras observándolo
    Negro_HuyendoDeLaLuz    // Se encuentra en una zona iluminada: corre hacia la sombra
}

[RequireComponent(typeof(NavMeshAgent))]
public class ManiquiLuminoso : MonoBehaviour
{
    [Header("Configuración de Luminosidad")]
    [Tooltip("Modo de spawn: AutoDetect analiza la luz de la habitación/punto de spawn.")]
    public ManiquiLuminosityMode modoLuminosidad = ManiquiLuminosityMode.AutoDetectFromSpawn;

    [Tooltip("Umbral para considerar el spawn como 'con luz'. Si es mayor o igual, será blanco.")]
    public float umbralLuz = 0.15f;

    [Header("Materiales")]
    public Material materialBlanco;
    public Material materialNegro;

    [Header("Velocidades")]
    public float velocidadPaseoBlanco = 2.2f;
    public float velocidadAcechoNegro = 3.6f;
    public float velocidadHuidaLuz = 4.8f;

    [Header("Distancias (Modo Negro)")]
    [Tooltip("Distancia a partir de la cual el maniquí negro empieza a buscarte porque estás muy lejos.")]
    public float distanciaBusquedaLejana = 8.0f;

    [Tooltip("Distancia deseada para mantenerse cerca del jugador sin estorbarle.")]
    public float distanciaDeseadaJugador = 4.5f;

    [Tooltip("Distancia mínima al jugador.")]
    public float distanciaMinimaJugador = 3.0f;

    [Header("Paseo (Modo Blanco)")]
    public float radioPaseo = 12f;
    public float tiempoPausaWaypointMin = 1.5f;
    public float tiempoPausaWaypointMax = 3.5f;

    [Header("Estado Actual (Solo Lectura)")]
    [SerializeField] private bool esBlanco;
    [SerializeField] private ManiquiBehaviorState estadoActual = ManiquiBehaviorState.Blanco_Paseando;
    [SerializeField] private float luminosidadDetectada;
    [SerializeField] private float distanciaAlJugador;

    public bool EsBlanco => esBlanco;
    public ManiquiBehaviorState EstadoActual => estadoActual;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform player;
    private RoomData homeRoom;
    private bool confinedToRoom;

    private float proximaDecision;
    private float tiempoEsperaWander;
    private bool esperandoEnWaypoint;
    private Vector3 objetivoActual;
    private Renderer[] renderers;
    private bool inicializado;

    void Awake()
    {
        EliminarCamarasHijas();
        EnsureComponents();
    }

    void OnEnable()
    {
        EliminarCamarasHijas();
    }

    void Start()
    {
        EliminarCamarasHijas();
        if (!inicializado)
        {
            InicializarDesdeSpawn();
        }
    }

    /// <summary>
    /// Elimina cualquier Camera o AudioListener importado en la jerarquía del modelo FBX.
    /// Esto previene que el modelo se robe el foco o la perspectiva de la cámara principal.
    /// </summary>
    public void EliminarCamarasHijas()
    {
        Camera[] camaras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < camaras.Length; i++)
        {
            if (camaras[i] != null)
            {
                camaras[i].enabled = false;
                if (camaras[i].gameObject.CompareTag("MainCamera"))
                {
                    camaras[i].gameObject.tag = "Untagged";
                }

                if (Application.isPlaying)
                {
                    Destroy(camaras[i]);
                }
                else
                {
                    DestroyImmediate(camaras[i]);
                }
            }
        }

        AudioListener[] listeners = GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null)
            {
                listeners[i].enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(listeners[i]);
                }
                else
                {
                    DestroyImmediate(listeners[i]);
                }
            }
        }
    }

    void EnsureComponents()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>(true);

        if (GetComponentInChildren<Collider>() == null)
        {
            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.center = Vector3.up * 0.9f;
            col.height = 1.8f;
            col.radius = 0.35f;
        }

        if (GetComponent<EnemyHealth>() == null)
        {
            EnemyHealth health = gameObject.AddComponent<EnemyHealth>();
            health.maxHealth = 100f;
            health.alturaBarra = 2.1f;
        }

        // Cargar materiales por defecto si no están asignados en el inspector
        if (materialBlanco == null)
            materialBlanco = Resources.Load<Material>("Materials/Maniqui_Blanco");
        if (materialNegro == null)
            materialNegro = Resources.Load<Material>("Materials/Maniqui_Negro");
    }

    public void Initialize(RoomData room, Transform targetPlayer, bool confined)
    {
        EnsureComponents();
        homeRoom = room;
        confinedToRoom = confined && room != null;
        player = targetPlayer;
        InicializarDesdeSpawn();
    }

    void InicializarDesdeSpawn()
    {
        EnsureComponents();
        ResolverJugador();

        // 1. Evaluar luminosidad del spawn
        luminosidadDetectada = EvaluarLuminosidadEnPosicion(transform.position);

        switch (modoLuminosidad)
        {
            case ManiquiLuminosityMode.ForceWhite:
                esBlanco = true;
                break;
            case ManiquiLuminosityMode.ForceBlack:
                esBlanco = false;
                break;
            case ManiquiLuminosityMode.AutoDetectFromSpawn:
            default:
                esBlanco = (luminosidadDetectada >= umbralLuz);
                break;
        }

        // 2. Aplicar color visual
        AplicarApariencia();

        // 3. Configurar estado y parámetros iniciales
        if (esBlanco)
        {
            estadoActual = ManiquiBehaviorState.Blanco_Paseando;
            agent.speed = velocidadPaseoBlanco;
            agent.stoppingDistance = 0.6f;
            ElegirNuevoDestinoPaseo();
        }
        else
        {
            estadoActual = ManiquiBehaviorState.Negro_BuscandoJugador;
            agent.speed = velocidadAcechoNegro;
            agent.stoppingDistance = distanciaMinimaJugador;
        }

        inicializado = true;
    }

    void ResolverJugador()
    {
        if (player != null) return;
        GameObject goPlayer = GameObject.FindGameObjectWithTag("Player");
        if (goPlayer != null)
        {
            player = goPlayer.transform;
        }
        else if (Camera.main != null)
        {
            player = Camera.main.transform;
        }
    }

    void AplicarApariencia()
    {
        Color colorFinal = esBlanco ? new Color(0.94f, 0.94f, 0.94f, 1f) : new Color(0.04f, 0.04f, 0.04f, 1f);
        Material matAsignar = esBlanco ? materialBlanco : materialNegro;

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;

            if (matAsignar != null)
            {
                rend.material = matAsignar;
            }
            else
            {
                // Fallback dinámico sobre el material existente
                if (rend.material != null)
                {
                    if (rend.material.HasProperty("_BaseColor"))
                        rend.material.SetColor("_BaseColor", colorFinal);
                    if (rend.material.HasProperty("_Color"))
                        rend.material.color = colorFinal;

                    if (!esBlanco)
                    {
                        if (rend.material.HasProperty("_Metallic"))
                            rend.material.SetFloat("_Metallic", 0.25f);
                        if (rend.material.HasProperty("_Smoothness"))
                            rend.material.SetFloat("_Smoothness", 0.72f);
                    }
                }
            }
        }
    }

    void Update()
    {
        if (!inicializado) return;

        ResolverJugador();
        ActualizarAnimacion();

        if (esBlanco)
        {
            ActualizarComportamientoBlanco();
        }
        else
        {
            ActualizarComportamientoNegro();
        }
    }

    void ActualizarAnimacion()
    {
        if (animator == null) return;
        float currentSpeed = agent != null ? agent.velocity.magnitude : 0f;
        animator.SetFloat("Speed", currentSpeed);
    }

    // =========================================================================
    // COMPORTAMIENTO BLANCO: "Si sale en spawn con luz, blanco, y solo camina, se pasea"
    // =========================================================================
    void ActualizarComportamientoBlanco()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        agent.speed = velocidadPaseoBlanco;
        estadoActual = ManiquiBehaviorState.Blanco_Paseando;

        if (esperandoEnWaypoint)
        {
            if (Time.time >= tiempoEsperaWander)
            {
                esperandoEnWaypoint = false;
                ElegirNuevoDestinoPaseo();
            }
            return;
        }

        // Si ha llegado a su destino o no tiene camino
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
        {
            esperandoEnWaypoint = true;
            tiempoEsperaWander = Time.time + Random.Range(tiempoPausaWaypointMin, tiempoPausaWaypointMax);
        }
    }

    void ElegirNuevoDestinoPaseo()
    {
        for (int i = 0; i < 20; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * radioPaseo;
            randomDirection += transform.position;
            randomDirection.y = transform.position.y;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, radioPaseo * 0.5f, NavMesh.AllAreas))
            {
                // Si está confinado a un cuarto específico, asegurarse de no salir de él
                if (confinedToRoom && homeRoom != null && !homeRoom.ContainsMovementPoint(hit.position, 0.4f))
                    continue;

                objetivoActual = hit.position;
                agent.SetDestination(objetivoActual);
                return;
            }
        }
    }

    // =========================================================================
    // COMPORTAMIENTO NEGRO: "Si sale en negro, Te busca si estas muy lejos y siempre
    // busca estar cerca de ti, pero lejos de la luz de las habitaciones"
    // =========================================================================
    void ActualizarComportamientoNegro()
    {
        if (agent == null || !agent.isOnNavMesh || player == null) return;

        distanciaAlJugador = Vector3.Distance(transform.position, player.position);

        if (Time.time < proximaDecision) return;
        proximaDecision = Time.time + 0.2f;

        // 1. ¿Está el maniquí actualmente bajo una luz?
        float luzEnManiqui = EvaluarLuminosidadEnPosicion(transform.position);
        bool estaEnLuz = luzEnManiqui >= umbralLuz;

        if (estaEnLuz)
        {
            // FOTOFOBIA: ¡Huir inmediatamente hacia la sombra más cercana!
            estadoActual = ManiquiBehaviorState.Negro_HuyendoDeLaLuz;
            agent.speed = velocidadHuidaLuz;
            HuirHaciaLaSombra();
            return;
        }

        // 2. Dinámica con el jugador
        if (distanciaAlJugador > distanciaBusquedaLejana)
        {
            // "Te busca si estas muy lejos"
            estadoActual = ManiquiBehaviorState.Negro_BuscandoJugador;
            agent.speed = velocidadAcechoNegro;
            AvanzarHaciaJugadorEnSombras();
        }
        else if (distanciaAlJugador > distanciaDeseadaJugador)
        {
            // Ajustar posición para acercarse a la distancia ideal manteniéndose en sombra
            estadoActual = ManiquiBehaviorState.Negro_AcechandoCerca;
            agent.speed = velocidadPaseoBlanco * 1.2f;
            AvanzarHaciaJugadorEnSombras();
        }
        else if (distanciaAlJugador < distanciaMinimaJugador)
        {
            // Demasiado cerca: retroceder un paso hacia la sombra si es posible para mantener el acecho
            estadoActual = ManiquiBehaviorState.Negro_AcechandoCerca;
            agent.ResetPath();
            MirarHacia(player.position);
        }
        else
        {
            // En distancia ideal: acecha pacientemente desde la sombra mirando al jugador
            estadoActual = ManiquiBehaviorState.Negro_AcechandoCerca;
            agent.ResetPath();
            MirarHacia(player.position);
        }
    }

    void AvanzarHaciaJugadorEnSombras()
    {
        Vector3 mejorPuntoSombra = EncontrarMejorPuntoSombraCercaDeJugador();

        if (mejorPuntoSombra != Vector3.zero)
        {
            objetivoActual = mejorPuntoSombra;
            agent.SetDestination(mejorPuntoSombra);
        }
        else
        {
            // Si todo alrededor del jugador está hiperiluminado, quedarse en la posición oscura actual y esperar
            agent.ResetPath();
            MirarHacia(player.position);
        }
    }

    void HuirHaciaLaSombra()
    {
        // Buscar un punto navegable a corta distancia que tenga la menor cantidad de luz posible
        Vector3 mejorEscape = transform.position;
        float menorLuz = float.MaxValue;

        for (int i = 0; i < 16; i++)
        {
            float angulo = i * (Mathf.PI * 2f / 16f);
            Vector3 direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
            Vector3 candidato = transform.position + direccion * Random.Range(3.5f, 7.5f);

            if (NavMesh.SamplePosition(candidato, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                float luz = EvaluarLuminosidadEnPosicion(hit.position);
                if (luz < menorLuz)
                {
                    menorLuz = luz;
                    mejorEscape = hit.position;
                }
            }
        }

        if (menorLuz < float.MaxValue)
        {
            objetivoActual = mejorEscape;
            agent.SetDestination(mejorEscape);
        }
    }

    Vector3 EncontrarMejorPuntoSombraCercaDeJugador()
    {
        Vector3 mejorPunto = Vector3.zero;
        float mejorPuntaje = float.NegativeInfinity;

        int numMuestras = 16;
        float radioMuestreo = distanciaDeseadaJugador;

        for (int i = 0; i < numMuestras; i++)
        {
            float angulo = i * (Mathf.PI * 2f / numMuestras);
            Vector3 offset = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * radioMuestreo;
            Vector3 puntoMuestra = player.position + offset;

            if (NavMesh.SamplePosition(puntoMuestra, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Vector3 pos = hit.position;
                float luz = EvaluarLuminosidadEnPosicion(pos);

                // REGLA CRÍTICA: "lejos de la luz de las habitaciones"
                // Descartar o penalizar drásticamente cualquier punto que esté iluminado
                if (luz >= umbralLuz)
                {
                    continue; // Punto iluminado, descartado por fotofobia
                }

                float distAlJugador = Vector3.Distance(pos, player.position);
                float distAlManiqui = Vector3.Distance(pos, transform.position);

                // Puntaje: preferir puntos con 0 luz, que estén a la distancia ideal del jugador y accesibles
                float puntaje = - (luz * 100f) - Mathf.Abs(distAlJugador - distanciaDeseadaJugador) * 2f - distAlManiqui * 0.5f;

                // Bonificación si el maniquí puede ver al jugador desde ese punto de sombra
                if (!Physics.Linecast(pos + Vector3.up * 1.5f, player.position + Vector3.up * 1.5f, ~((1 << 8) | (1 << 2))))
                {
                    puntaje += 10f;
                }

                if (puntaje > mejorPuntaje)
                {
                    mejorPuntaje = puntaje;
                    mejorPunto = pos;
                }
            }
        }

        // Si todos los puntos a distanciaDeseada están iluminados, probar a mayor distancia (ej. en el umbral del pasillo oscuro)
        if (mejorPunto == Vector3.zero)
        {
            for (int i = 0; i < 16; i++)
            {
                float angulo = i * (Mathf.PI * 2f / 16f);
                Vector3 offset = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * (distanciaDeseadaJugador + 4f);
                Vector3 puntoMuestra = player.position + offset;

                if (NavMesh.SamplePosition(puntoMuestra, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    float luz = EvaluarLuminosidadEnPosicion(hit.position);
                    if (luz < umbralLuz)
                    {
                        float dist = Vector3.Distance(hit.position, transform.position);
                        float puntaje = -(luz * 100f) - dist;
                        if (puntaje > mejorPuntaje)
                        {
                            mejorPuntaje = puntaje;
                            mejorPunto = hit.position;
                        }
                    }
                }
            }
        }

        return mejorPunto;
    }

    void MirarHacia(Vector3 targetPosition)
    {
        Vector3 direccion = (targetPosition - transform.position);
        direccion.y = 0f;
        if (direccion.sqrMagnitude > 0.01f)
        {
            Quaternion rotDeseada = Quaternion.LookRotation(direccion);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotDeseada, Time.deltaTime * 5f);
        }
    }

    // =========================================================================
    // CÁLCULO DE LUMINOSIDAD FÍSICA Y POR HABITACIÓN
    // =========================================================================
    public float EvaluarLuminosidadEnPosicion(Vector3 pos)
    {
        float luzTotal = 0f;

        // 1. Revisar si la posición cae en alguna RoomData
        RoomData cuarto = ObtenerCuartoEnPosicion(pos);
        if (cuarto != null)
        {
            if (cuarto.archetype == RoomArchetype.Dark)
            {
                return 0f; // Oscuridad total obligada
            }

            if (cuarto.illuminationLevel > 0.05f)
            {
                luzTotal += cuarto.illuminationLevel;
            }
        }

        // 2. Muestrear fuentes de luz activas en la escena cerca de la posición
        Light[] luces = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light luz in luces)
        {
            if (luz == null || !luz.enabled || !luz.gameObject.activeInHierarchy || luz.intensity <= 0f) continue;
            if (luz.type == LightType.Directional)
            {
                luzTotal += luz.intensity * 0.5f;
                continue;
            }

            float dist = Vector3.Distance(pos, luz.transform.position);
            if (dist < luz.range)
            {
                float factorAtenuacion = 1f - (dist / luz.range);

                if (luz.type == LightType.Spot)
                {
                    Vector3 dirHaciaPunto = (pos - luz.transform.position).normalized;
                    float angulo = Vector3.Angle(luz.transform.forward, dirHaciaPunto);
                    if (angulo > luz.spotAngle * 0.5f)
                    {
                        continue; // Fuera del cono de luz
                    }
                    factorAtenuacion *= Mathf.Clamp01((luz.spotAngle * 0.5f - angulo) / (luz.spotAngle * 0.2f));
                }

                luzTotal += (luz.intensity * factorAtenuacion * factorAtenuacion) * 0.25f;
            }
        }

        return Mathf.Clamp01(luzTotal);
    }

    RoomData ObtenerCuartoEnPosicion(Vector3 pos)
    {
        if (homeRoom != null && homeRoom.ContainsMovementPoint(pos))
            return homeRoom;

        foreach (RoomData rd in Object.FindObjectsByType<RoomData>(FindObjectsSortMode.None))
        {
            if (rd != null && rd.ContainsMovementPoint(pos))
                return rd;
        }
        return null;
    }

    // =========================================================================
    // INTERACCIÓN CON SISTEMA DE JUEGO (PROYECTILES Y PODER)
    // =========================================================================
    public void ReceiveProjectileImpact()
    {
        EnemyHealth health = GetComponent<EnemyHealth>();
        if (health != null && health.IsDead)
        {
            Defeat();
            return;
        }

        // Si recibe un impacto, si es negro se asusta y huye a las sombras
        if (!esBlanco)
        {
            HuirHaciaLaSombra();
        }
    }

    public void Defeat()
    {
        if (PlayerPowerState.Instance != null)
            PlayerPowerState.Instance.RegisterDefeat();

        if (EntityManager.Instance != null)
            EntityManager.Instance.DespawnEntity(gameObject);
        else
            gameObject.SetActive(false);
    }

    // =========================================================================
    // GIZMOS Y DEBUG EN EL EDITOR
    // =========================================================================
    void OnDrawGizmosSelected()
    {
        if (esBlanco)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, radioPaseo);
            Gizmos.DrawLine(transform.position, objetivoActual);
        }
        else
        {
            // Círculo de distancia deseada al jugador
            if (player != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(player.position, distanciaDeseadaJugador);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(player.position, distanciaMinimaJugador);
            }

            // Línea hacia objetivo de sombra actual
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(objetivoActual, 0.3f);
            Gizmos.DrawLine(transform.position, objetivoActual);
        }
    }
}
