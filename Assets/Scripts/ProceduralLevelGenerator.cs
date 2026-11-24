using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

[System.Serializable]
public class ArchetypeSettings
{
    public RoomArchetype archetype;
    [Range(0f, 1f), Tooltip("Probabilidad base de aparición.")]
    public float baseProbability = 0.1f;
    
    [Range(0f, 1f), Tooltip("Pico de similitud: Qué tan extremo es el arquetipo (ej. densidad de pilares o cantidad de props).")]
    public float similarityPeak = 1f; 
    
    [Tooltip("Distribución en el nivel. X=0 es cerca al inicio, X=1 es cerca al final del mapa.")]
    public AnimationCurve distributionCurve = AnimationCurve.Constant(0, 1, 1);
}

[System.Serializable]
public class LevelProgressionSettings
{
    [Header("Nivel 0 (Tutorial)")]
    public int level0MapWidth = 20;
    public int level0MapDepth = 20;
    public int level0Keynotes = 1;
    public int level0Enemies = 0; // Sin enemigos en el tutorial por defecto

    [Header("Nivel Base (Nivel 1 en adelante)")]
    public int baseMapWidth = 30;
    public int baseMapDepth = 30;
    public int baseKeynotes = 5;
    public int baseEnemies = 1;

    [Header("Subida por Nivel (Escalado)")]
    [Tooltip("Cuánto crece el tamaño del laberinto por cada nivel extra después del 1")]
    public int mapSizeIncreasePerLevel = 0; // Por defecto 0 para mantener tu lógica original, pero puedes subirlo
    [Tooltip("Cuántas misiones/keynotes extra se añaden por cada nivel extra después del 1")]
    public int keynotesIncreasePerLevel = 1;
    [Tooltip("Cuántos enemigos extra se añaden por cada nivel extra después del 1")]
    public int enemiesIncreasePerLevel = 1;
    [Header("Límites del Juego")]
    [Tooltip("El nivel máximo que se puede jugar. Al pasarlo, se termina el juego.")]
    public int maxLevel = 3;
}

[System.Serializable]
public class PerlinLayersSettings
{
    [Header("Escala del Ruido (Zoom)")]
    [Tooltip("Valores bajos crean biomas enormes. Valores altos crean parches pequeños.")]
    public float spaceScale = 0.03f;
    public float illuminationScale = 0.05f;
    public float clutterScale = 0.04f;
    
    [Header("Umbrales de Espacio (Batofobia)")]
    public float batophobicThreshold = 0.75f;
    public float claustrophobicThreshold = 0.25f;
    [Tooltip("Controla cómo se escala la altura. Útil para mantener techos bajos hasta llegar al umbral batofóbico.")]
    public AnimationCurve heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Umbrales de Iluminación")]
    public float darkThreshold = 0.25f;
    public float superIlluminatedThreshold = 0.8f;
    [Tooltip("Controla la intensidad de las luces. Eje Y debe ir de 0.2 a 1.0 aprox.")]
    public AnimationCurve illuminationCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f); // Invertida por defecto

    [Header("Umbrales de Aglomeración")]
    public float emptyThreshold = 0.25f;
    public float clutteredThreshold = 0.75f;
    [Tooltip("Controla el peso de aglomeración. Curva plana abajo y pico al final hace que los muebles se concentren solo en zonas críticas.")]
    public AnimationCurve clutterCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Offsets (Generados automáticamente)")]
    public float spaceOffset;
    public float illuminationOffset;
    public float clutterOffset;
}

public class ProceduralLevelGenerator : MonoBehaviour
{
    [Header("Capas de Generación Procedural (Dimensiones)")]
    public PerlinLayersSettings perlinLayers = new PerlinLayersSettings();

    [Header("Configuración de Progresión de Niveles")]
    public LevelProgressionSettings progressionSettings = new LevelProgressionSettings();

    [Header("Generación con Semilla Determinista")]
    [Tooltip("Activa esto para generar una semilla aleatoria nueva cada vez que se inicie o regenere el nivel.")]
    public bool useRandomSeed = true;
    [Tooltip("Texto o número que actúa como semilla determinista de generación.")]
    public string levelSeed = "MI_SEMILLA";
    [Tooltip("Semilla numérica resultante calculada para el nivel actual.")]
    [SerializeField] private int seedActual;
    public int SeedActual => seedActual;

    [Header("Configuración del Mapa")]
    [Tooltip("Ancho del mapa en celdas")]
    public int mapWidth = 40;
    [Tooltip("Profundidad del mapa en celdas")]
    public int mapDepth = 40;
    [Tooltip("Tamaño mínimo de cada habitación (4 = pasillos estrechos)")]
    public int minRoomSize = 4;
    [Tooltip("Tamaño real de cada celda en unidades de Unity")]
    public float cellSize = 2f; 
    [Tooltip("Altura mínima (cuartos pequeños)")]
    public float minWallHeight = 3f;
    [Tooltip("Altura máxima (cuartos gigantes)")]
    public float maxWallHeight = 10f;

    [Header("Dimensiones 3D y Capas Verticales (3D Layers)")]
    [Tooltip("Condicionamiento de altura máxima que puede alcanzar la estructura (por defecto 80).")]
    public float maxLevelHeight = 80f;
    [Tooltip("Cantidad máxima de capas/pisos (Layers). Layer 1 equivale a que el piso es plano.")]
    [Range(1, 8)]
    public int maxLayers = 3;
    [Tooltip("Altura entre cada piso/capa vertical.")]
    public float layerHeight = 6f;
    [Tooltip("Si está activo, primero se escoge aleatoriamente la ubicación del Start Room donde aparece el jugador y luego se expande el nivel.")]
    public bool escogerStartRoomPrimero = true;

    [Header("Micro-Desniveles (Mismo Layer)")]
    [Tooltip("Desnivel vertical máximo permitido entre cuartos dentro de la misma capa.")]
    public float maxMicroDesnivel = 0.8f;
    [Tooltip("Probabilidad de que un cuarto dentro de la misma capa aplique un micro-desnivel sutil.")]
    [Range(0f, 1f)]
    public float probabilidadMicroDesnivel = 0.35f;

    [Header("Generación 3D - Multi-Layer y Rectángulos Consecutivos")]
    [Tooltip("Genera el nivel formando rectángulos consecutivos con Start/End Room pre-posicionados, Final Rooms fijos y Halls subdivididos.")]
    public bool usarRectangulosConsecutivos = true;
    [Tooltip("Distancia mínima relativa entre Start Room y End Room (0 a 1).")]
    [Range(0.2f, 0.8f)]
    public float distanciaMinimaStartEnd = 0.45f;
    [Tooltip("Tamaño mínimo de los rectángulos generados consecutivamente.")]
    public int tamanoMinimoRectangulo = 4;
    [Tooltip("Tamaño máximo de los rectángulos generados consecutivamente.")]
    public int tamanoMaximoRectangulo = 8;
    [Tooltip("Umbral de dimensión para que un cuarto sea clasificado como Hall (marco para subdivisiones).")]
    public int umbralDimensionHall = 7;

    [Header("Grosores de Paredes Aleatorios por Partición")]
    [Tooltip("Grosor mínimo de pared entre habitaciones o particiones (en celdas).")]
    [Range(1, 3)] public int grosorParedMinimo = 1;
    [Tooltip("Grosor máximo de pared entre habitaciones o particiones (en celdas).")]
    [Range(1, 4)] public int grosorParedMaximo = 3;

    [Header("Expansión Arbórea (Tree Generation)")]
    [Tooltip("Profundidad máxima de ramificación secundaria del árbol de cuartos.")]
    [Range(1, 6)] public int profundidadMaximaArbol = 4;
    [Tooltip("Probabilidad base de que una habitación genere una bifurcación o rama secundaria.")]
    [Range(0.2f, 0.95f)] public float probabilidadRamificacion = 0.65f;
    [Tooltip("Máximo de ramas hijas que pueden salir de un solo cuarto.")]
    [Range(1, 3)] public int maxHijosPorCuarto = 2;
    [Tooltip("Probabilidad de conectar dos ramas cercanas para crear rutas circulares / atajos.")]
    [Range(0f, 0.5f)] public float probabilidadConexionesCruzadas = 0.20f;

    [Header("Conexiones Verticales (Abajo y Arriba)")]
    [Tooltip("Tamaño mínimo de la entrada vertical (en celdas).")]
    public int tamanoMinimoEntradaVertical = 2;
    [Tooltip("Tamaño máximo de la entrada vertical (en celdas).")]
    public int tamanoMaximoEntradaVertical = 4;
    
    [Header("Arquetipos de Habitaciones")]
    public List<ArchetypeSettings> archetypeSettings = new List<ArchetypeSettings>();

    [Header("Variedad Procedural")]
    [Tooltip("Probabilidad de forzar un corte extremo para crear pasadizos largos (0 a 1)")]
    public float chanceCorridor = 0.25f;

    [Header("Sistema de Iluminación")]
    [Tooltip("Probabilidad (0 a 1) de que una habitación tenga luz. Valores bajos crean zonas de oscuridad total.")]
    [Range(0f, 1f)] public float lightDensity = 0.8f;

    [Header("Prefabs (Opcional)")]
    [Tooltip("Si están vacíos, se usarán Cubos generados por código.")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;

    [Header("Materiales (Opcional)")]
    [Tooltip("Si usas los cubos por código, aquí puedes asignarles materiales (texturas, normal maps, etc.)")]
    public Material wallMaterial;
    public Material floorMaterial;
    public Material ceilingMaterial;

    [Header("Misiones (Keynotes)")]
    [Tooltip("Prefab del Panel de Misiones para pegar en la pared")]
    public GameObject keynotePrefab;
    [Tooltip("Cantidad exacta de Keynotes que se generarán en el laberinto.")]
    public int numberOfKeynotes = 5;

    [Header("Entidades del Nivel (Global)")]
    [Tooltip("Prefabs de las entidades que habitan el laberinto (ej. La Luz, Anomalías, Maniquíes).")]
    public GameObject[] enemyPrefabs;
    [Tooltip("Total de entidades a instanciar en todo el laberinto al iniciar la partida.")]
    public int totalEnemiesInLevel = 1;
    public bool roomEntitiesEnabled = true;
    public int enemiesPerRoom = 1;
    private NavMeshSurface navigationSurface;
    private bool rebuildPending;

    [Header("Ciempiés (Entidades Especiales)")]
    public GameObject cienpiesPeludoPrefab;
    public GameObject cienpiesPequenoPrefab;
    public int cantidadCienpiesPequenos = 8;
    public bool spawnCienpiesPeludo = true;
    public bool spawnCienpiesPequenos = true;

    [Header("Maniquí Luminoso (Entidad Sensible a la Luz)")]
    public GameObject maniquiLuminosoPrefab;
    public bool spawnManiquiLuminoso = true;

    [Header("Arquitectura 3D (Rampas, Puentes, Plataformas y Desfiladeros)")]
    [Tooltip("Activa la generación procedural de habitaciones con desniveles, desfiladeros, puentes, plataformas elevadas y rampas.")]
    public bool generarArquitectura3D = true;
    [Tooltip("Probabilidad (0 a 1) de que una habitación elegible tenga arquitectura 3D (reducido para priorizar rampitas sutiles).")]
    [Range(0f, 1f)] public float probabilidadHabitacion3D = 0.10f;
    [Tooltip("Cantidad mínima garantizada de habitaciones con arquitectura 3D.")]
    public int minimoHabitaciones3D = 0;

    [Header("Objetos de Entorno Aleatorios")]
    [Tooltip("Prefabs de props (sillas, mesas, monitores, etc.)")]
    public GameObject[] objectPrefabs;
    [Tooltip("Cantidad MÍNIMA TOTAL de objetos que aparecerán en todo el nivel")]
    public int minTotalObjects = 15;
    [Tooltip("Cantidad MÁXIMA TOTAL de objetos que aparecerán en todo el nivel")]
    public int maxTotalObjects = 40;
    [Tooltip("Escala mínima aleatoria de los objetos generados")]
    public float minObjectScale = 0.5f;
    [Tooltip("Escala máxima aleatoria de los objetos generados")]
    public float maxObjectScale = 4f;
    
    [Header("Marcadores Visuales")]
    public Color startRoomColor = Color.green;
    public Color safeZoneColor = Color.blue;

    [Header("Debug")]
    [Tooltip("Activa para ver los volúmenes de las habitaciones en la ventana de Scene (amarillo = luz, gris = oscuridad)")]
    public bool showDebugZones = false;

    private BSPNode startRoom;
    private BSPNode endRoom;

    public enum CellType { Empty, Floor, Wall, Door, Chasm, Bridge }
    public enum RoomStructureType { FinalRoom, Hall }
    public CellType[,] grid;
    // Grilla segregada por capa para visualización precisa del mapa/minimapa por piso
    public CellType[,,] gridPorLayer;
    private List<BSPNode> leafNodes;
    private GameObject levelParent;

    public class BSPNode
    {
        public RectInt space;
        public RectInt room;
        public BSPNode left, right;
        public bool splitHorizontal;
        public int splitPoint;
        public GameObject geometryContainer; // Raíz del chunk (para el culling general)
        public GameObject staticContainer;   // Paredes, Suelos, Techos (Marcados como Static)
        public GameObject dynamicContainer;  // Props, Luces, Entidades
        
        // --- Capas de Ruido de Perlin ---
        public float noiseSpace;
        public float noiseIllumination;
        public float noiseClutter;

        public float roomHeight;
        public RoomArchetype archetype = RoomArchetype.Normal;
        public float archetypeIntensity = 0f;

        // --- Atributos 3D Multi-Layer ---
        public int layerIndex = 0;       // 0 = Layer 1 (Plano base y=0)
        public float floorElevation = 0f; // Elevación en Y del suelo

        // --- Atributos Estructurales (Fase 2: FinalRooms y Halls) ---
        public RoomStructureType structureType = RoomStructureType.FinalRoom;
        public List<BSPNode> subRooms = new List<BSPNode>();

        // --- Atributos de Expansión Arbórea (Tree Generation) ---
        public int treeDepth = 0;
        public int branchDepth = 0; // 0 = Tronco principal, 1 = Rama secundaria, 2 = Sub-rama, etc.
        public bool isTrunk = false;
        public List<BSPNode> treeChildren = new List<BSPNode>();
    }

    public class ConexionVertical
    {
        public BSPNode cuartoInferior;
        public BSPNode cuartoSuperior;
        public RectInt zonaEntrada; // Posición y tamaño calculados aleatoriamente
        public float anchoRampa;
    }
    public List<ConexionVertical> conexionesVerticales = new List<ConexionVertical>();

    public class RampitaInfo
    {
        public Vector3 inicio;
        public Vector3 fin;
        public float ancho;
        public Transform parent;
    }
    public List<RampitaInfo> rampitasDeUnion = new List<RampitaInfo>();

    public void RegistrarRampita(BSPNode nodoRef, Vector3 pInicio, Vector3 pFin, float ancho)
    {
        Transform pTransform = (nodoRef != null && nodoRef.staticContainer != null)
            ? nodoRef.staticContainer.transform
            : (levelParent != null ? levelParent.transform : null);

        rampitasDeUnion.Add(new RampitaInfo
        {
            inicio = pInicio,
            fin = pFin,
            ancho = ancho,
            parent = pTransform
        });
    }

    public static ProceduralLevelGenerator Instance;
    
    [Header("Progresión del Juego")]
    public static int currentLevel = 0; // Se mantiene entre recargas de escena

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    /// <summary>
    /// Calcula un hash entero determinista de 32 bits a partir de cualquier texto usando FNV-1a.
    /// Es completamente estable e idéntico en todas las plataformas y sesiones (evita la aleatoriedad de string.GetHashCode()).
    /// </summary>
    public static int ObtenerHashDeterminista(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return 0;
        if (int.TryParse(texto, out int num)) return num;

        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < texto.Length; i++)
            {
                hash ^= texto[i];
                hash *= 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    /// <summary>
    /// Inicializa la semilla determinista del generador pseudoaleatorio y deriva offsets únicos para Perlin Noise.
    /// </summary>
    public void InicializarSemilla()
    {
        if (useRandomSeed || string.IsNullOrEmpty(levelSeed))
        {
            seedActual = (int)(System.DateTime.Now.Ticks & 0x7FFFFFFF);
            levelSeed = seedActual.ToString();
        }
        else
        {
            seedActual = ObtenerHashDeterminista(levelSeed);
        }

        // Semilla efectiva combinada con el nivel de progresión (reproducibilidad por nivel)
        int seedEfectiva = seedActual + (currentLevel * 10007);

        // Inicializar el PRNG de Unity
        UnityEngine.Random.InitState(seedEfectiva);

        // Derivar offsets de ruido de Perlin de forma determinista para la semilla
        System.Random prng = new System.Random(seedEfectiva);
        perlinLayers.spaceOffset = (float)(prng.NextDouble() * 10000.0);
        perlinLayers.illuminationOffset = (float)(prng.NextDouble() * 10000.0);
        perlinLayers.clutterOffset = (float)(prng.NextDouble() * 10000.0);

        Debug.Log($"<b>[Semilla de Generación]</b> Nivel {currentLevel} inicializado con semilla '{levelSeed}' (Hash={seedActual}, SeedEfectiva={seedEfectiva}). Reproducibilidad 100% garantizada.");
    }

    [ContextMenu("Regenerar con Semilla Actual")]
    public void RegenerarConSemillaActual()
    {
        useRandomSeed = false;
        GenerateLevel();
    }

    [ContextMenu("Regenerar con Nueva Semilla Aleatoria")]
    public void RegenerarConNuevaSemilla()
    {
        useRandomSeed = true;
        levelSeed = "";
        GenerateLevel();
    }

    public void SetSeed(string newSeed)
    {
        levelSeed = newSeed;
        useRandomSeed = false;
        GenerateLevel();
    }

    void Start()
    {
        GameplayFeatureInstaller.Install();
        ConfigurarDificultadNivel();
        ConfigurarEntornoOscuro();
        GenerateLevel();
    }

    void ConfigurarDificultadNivel()
    {
        Debug.Log("Iniciando Nivel " + currentLevel);
        
        if (currentLevel == 0)
        {
            // Nivel 0 (Tutorial/Introducción)
            mapWidth = progressionSettings.level0MapWidth;
            mapDepth = progressionSettings.level0MapDepth;
            numberOfKeynotes = progressionSettings.level0Keynotes;
            totalEnemiesInLevel = progressionSettings.level0Enemies;
        }
        else
        {
            // Nivel 1 en adelante (Niveles reales)
            int levelScale = (currentLevel - 1);
            
            mapWidth = progressionSettings.baseMapWidth + (levelScale * progressionSettings.mapSizeIncreasePerLevel);
            mapDepth = progressionSettings.baseMapDepth + (levelScale * progressionSettings.mapSizeIncreasePerLevel);
            
            // Aumentar las keynotes y enemigos según el nivel
            numberOfKeynotes = progressionSettings.baseKeynotes + (levelScale * progressionSettings.keynotesIncreasePerLevel); 
            totalEnemiesInLevel = progressionSettings.baseEnemies + (levelScale * progressionSettings.enemiesIncreasePerLevel);
        }

        // Asegurar que el Maniquí Luminoso esté disponible en la lista de entidades
        GameObject maniquiPrefab = Resources.Load<GameObject>("ManiquiLuminoso");
        if (maniquiPrefab != null)
        {
            bool found = false;
            if (enemyPrefabs != null)
            {
                foreach (var p in enemyPrefabs)
                {
                    if (p != null && (p == maniquiPrefab || p.name == "ManiquiLuminoso"))
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (!found)
            {
                List<GameObject> list = enemyPrefabs != null ? new List<GameObject>(enemyPrefabs) : new List<GameObject>();
                list.Add(maniquiPrefab);
                enemyPrefabs = list.ToArray();
            }
        }
    }

    void ConfigurarEntornoOscuro()
    {
        // 1. Apagar la iluminación ambiental
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.skybox = null; // Quitar el material del cielo

        // 2. Apagar cualquier Luz Direccional (El Sol de Unity)
        Light[] todasLasLuces = FindObjectsOfType<Light>();
        foreach (Light luz in todasLasLuces)
        {
            if (luz.type == LightType.Directional)
            {
                luz.enabled = false;
                Debug.Log("Luz Direccional apagada automáticamente para asegurar la oscuridad.");
            }
        }

        // 3. Forzar que TODAS las cámaras rendericen un fondo negro en vez del cielo y rango amplio de visión
        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            if (cam.farClipPlane < 180f) cam.farClipPlane = 180f;
        }
        
        // 4. Asegurar que la niebla esté apagada o sea negra, ya que el color por defecto es azulado
        RenderSettings.fog = false;
        RenderSettings.fogColor = Color.black;
    }

    public void GenerateLevel()
    {
        InicializarSemilla();
        if (EntityManager.Instance != null) EntityManager.Instance.ClearAllEntities();
        // Limpiamos el nivel anterior si volvemos a generar
        if (levelParent != null) { levelParent.SetActive(false); Destroy(levelParent); }
        navigationSurface = null;
        levelParent = new GameObject("Environment_Backrooms");

        grid = new CellType[mapWidth, mapDepth];
        for (int x = 0; x < mapWidth; x++)
            for (int z = 0; z < mapDepth; z++)
                grid[x, z] = CellType.Empty;

        leafNodes = new List<BSPNode>();
        conexionesVerticales.Clear();
        rampitasDeUnion.Clear();

        if (usarRectangulosConsecutivos)
        {
            // NUEVO PROCEDIMIENTO 3D (Fase 2):
            // 1. Start Room y End Room se posicionan al inicio (en la capa base, Layer 1 plano)
            EscogerStartEndRoomsAleatorios();

            // 2. Generación procedural en la misma capa formando rectángulos consecutivos (FinalRooms y Halls)
            GenerarNivelMismaCapaRectangulosConsecutivos();
        }
        else
        {
            // Procedimiento BSP anterior (fallback)
            if (escogerStartRoomPrimero)
            {
                EscogerStartRoomAleatorio();
            }
            else
            {
                startRoom = null;
            }

            // 1. Ejecutamos la Partición Espacial (BSP)
            BSPNode root = new BSPNode { space = new RectInt(0, 0, mapWidth, mapDepth) };
            SplitNode(root);

            // 2. Definimos qué celdas son Suelo y cuáles Pared
            CreateRooms(root);

            // Si pre-seleccionamos el startRoom, asegurar que esté integrado en leafNodes y grilla
            if (escogerStartRoomPrimero && startRoom != null)
            {
                if (!leafNodes.Contains(startRoom))
                {
                    leafNodes.Insert(0, startRoom);
                }
                for (int x = startRoom.room.x; x < startRoom.room.x + startRoom.room.width; x++)
                {
                    for (int z = startRoom.room.y; z < startRoom.room.y + startRoom.room.height; z++)
                    {
                        if (x >= 0 && x < mapWidth && z >= 0 && z < mapDepth)
                            grid[x, z] = CellType.Floor;
                    }
                }
            }
            
            // 3. Conectamos las habitaciones excavando Puertas en las paredes
            CreateCorridors(root);
        }
        
        // 3.5 Forzar Paredes de Contención en el Borde Absoluto del Mapa
        for (int x = 0; x < mapWidth; x++)
        {
            grid[x, 0] = CellType.Wall;
            grid[x, mapDepth - 1] = CellType.Wall;
        }
        for (int z = 0; z < mapDepth; z++)
        {
            grid[0, z] = CellType.Wall;
            grid[mapWidth - 1, z] = CellType.Wall;
        }
        
        // 4. Asignamos Arquetipos (y calculamos el Ruido de Perlin por cuarto)
        AssignArchetypes();

        // 4.5. FUSIONAR CUARTOS BATOFÓBICOS
        // Si dos cuartos adyacentes son colosales (Batofóbicos), eliminamos la pared que los separa
        FuseBatophobicRooms();
        
        // --- NUEVA LÓGICA: Seleccionar cuartos especiales y forzar su geometría cerrada ---
        SelectSpecialRooms();

        // --- ARQUITECTURA 3D: Planificar Desfiladeros, Puentes, Plataformas y Rampas ---
        List<InfoHabitacion3D> plan3D = new List<InfoHabitacion3D>();
        if (generarArquitectura3D)
        {
            plan3D = Procedural3DArchitecture.PlanificarHabitaciones3D(
                this, leafNodes, grid, startRoom, endRoom,
                probabilidadHabitacion3D, minimoHabitaciones3D);
        }
        
        // --- NUEVA LÓGICA: Preparar contenedores de Chunking Separados ---
        for (int i = 0; i < leafNodes.Count; i++)
        {
            GameObject container = new GameObject("Chunk_" + i);
            container.transform.SetParent(levelParent.transform);
            
            // Contenedor Estático para Geometría
            GameObject staticCont = new GameObject("StaticGeometry");
            staticCont.transform.SetParent(container.transform);
            staticCont.isStatic = true;

            // Contenedor Dinámico para Props, Luces y Objetos
            GameObject dynamicCont = new GameObject("DynamicObjects");
            dynamicCont.transform.SetParent(container.transform);

            leafNodes[i].geometryContainer = container;
            leafNodes[i].staticContainer = staticCont;
            leafNodes[i].dynamicContainer = dynamicCont;
        }

        // 4. Instanciamos los objetos 3D y los metemos en sus Chunks
        BuildPhysicalLevel();
        
        // 4.5. Construir Arquitectura 3D (Desfiladeros, Puentes, Plataformas Elevadas y Rampas)
        Procedural3DArchitecture.ConstruirGeometria3D(this, plan3D, grid, levelParent.transform);
        
        // 4.6. Construir Conexiones Verticales Físicas (Rampas y Accesos entre Capas)
        ConstruirConexionesVerticalesFisicas();
        
        // 5. Spawn de Trampas, Llaves y zonas especiales (Start/End)
        SpawnElements();
        BuildGridPorLayer();

        // 6. Optimización de mallas y construcción de NavMesh
        // Aplicamos el Batching Estático SOLO a los contenedores de geometría por chunk
        for (int i = 0; i < leafNodes.Count; i++)
        {
            if (leafNodes[i].staticContainer != null)
                StaticBatchingUtility.Combine(leafNodes[i].staticContainer);
        }
        BuildNavMesh();
        RepositionPlayerToStartRoom();
        
        // 7. --- SPAWN GLOBAL DE ENTIDADES AL FINALIZAR EL NIVEL ---
        SpawnGlobalEntities();
        SpawnCalmingPills();
        SpawnManiquiLuminoso();
        SpawnCienpiesPeludo();
        SpawnCienpiesPequenos();
    }

    void SpawnGlobalEnemies()
    {
        SpawnGlobalEntities();
    }

    void SpawnGlobalEntities()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0 || leafNodes.Count < 2) return;

        int spawned = 0;
        int attempts = 0; // Para evitar bucles infinitos

        while (spawned < totalEnemiesInLevel && attempts < 200)
        {
            attempts++;
            BSPNode node = leafNodes[Random.Range(0, leafNodes.Count)];
            
            // Evitamos spawnear en la zona de inicio o cuartos seguros
            if (node == startRoom || node.archetype == RoomArchetype.SafeRoom) continue;

            RectInt s = node.space;
            // Calcular una posición al azar dentro de ese cuarto
            Vector3 pos = new Vector3((s.x + Random.Range(1, s.width-1)) * cellSize, 1f, (s.y + Random.Range(1, s.height-1)) * cellSize);

            GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            
            GameObject spawnedEntity;
            if (EntityManager.Instance != null)
            {
                spawnedEntity = EntityManager.Instance.SpawnEntity(prefab, pos, Quaternion.identity, levelParent.transform);
            }
            else
            {
                spawnedEntity = null;
            }
            if (spawnedEntity != null) spawned++;
        }
    }

    void BuildNavMesh()
    {
        if (levelParent == null) return;
        if (navigationSurface == null) navigationSurface = levelParent.AddComponent<NavMeshSurface>();
        navigationSurface.collectObjects = CollectObjects.Children;
        navigationSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        navigationSurface.layerMask = ~((1 << 8) | (1 << 9) | (1 << 10));
        // Los FBX de muebles pueden tener Read/Write desactivado. PhysX dispone de
        // sus colisiones cocinadas, pero el constructor de NavMesh necesita vértices
        // legibles. Un collider simple conserva un obstáculo válido también en build.
        foreach (MeshCollider meshCollider in levelParent.GetComponentsInChildren<MeshCollider>(true))
        {
            if (!meshCollider.enabled || meshCollider.sharedMesh == null || meshCollider.sharedMesh.isReadable) continue;
            BoxCollider proxy = meshCollider.GetComponent<BoxCollider>();
            if (proxy == null)
            {
                proxy = meshCollider.gameObject.AddComponent<BoxCollider>();
                proxy.center = meshCollider.sharedMesh.bounds.center;
                proxy.size = meshCollider.sharedMesh.bounds.size;
                proxy.isTrigger = meshCollider.isTrigger;
            }
            meshCollider.enabled = false;
        }
        Physics.SyncTransforms();
        navigationSurface.BuildNavMesh();
    }

    public void RequestNavigationRebuild()
    {
        if (!rebuildPending && isActiveAndEnabled) StartCoroutine(RebuildNavigationAfterChanges());
    }

    System.Collections.IEnumerator RebuildNavigationAfterChanges()
    {
        rebuildPending = true;
        yield return null;
        BuildNavMesh();
        RepositionPlayerToStartRoom();
        rebuildPending = false;
        foreach (EnemyAIController enemy in FindObjectsByType<EnemyAIController>(FindObjectsSortMode.None))
            enemy.InvalidateNavigation();
    }

    void SpawnCalmingPills()
    {
        RoomData[] rooms = levelParent.GetComponentsInChildren<RoomData>();
        for (int i = 0; i < rooms.Length; i++)
        {
            if (i % 4 != 0 && rooms[i].archetype != RoomArchetype.SafeRoom) continue;
            Vector3 desired = rooms[i].FloorCenter + Vector3.right * 1.5f;
            if (EntityManager.TryFindNavigablePoint(desired, rooms[i], out Vector3 position))
            {
                PillPowerMode pillMode = (PillPowerMode)(i % 3);
                CalmingPill.Create(position, levelParent.transform, pillMode);
            }
        }
    }

    void SpawnManiquiLuminoso()
    {
        if (!spawnManiquiLuminoso || leafNodes == null || leafNodes.Count < 2) return;

        // Buscar una habitación que no sea el spawn inicial ni safe room
        List<BSPNode> validNodes = new List<BSPNode>();
        foreach (var n in leafNodes)
        {
            if (n != startRoom && n != endRoom && n.archetype != RoomArchetype.SafeRoom)
                validNodes.Add(n);
        }
        if (validNodes.Count == 0) return;

        BSPNode targetNode = validNodes[Random.Range(0, validNodes.Count)];
        RectInt s = targetNode.room;
        Vector3 pos = new Vector3((s.x + s.width * 0.5f) * cellSize, 0.5f, (s.y + s.height * 0.5f) * cellSize);
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            pos = hit.position;
        }

        GameObject prefab = maniquiLuminosoPrefab ?? Resources.Load<GameObject>("ManiquiLuminoso");
        if (prefab != null)
        {
            if (EntityManager.Instance != null)
                EntityManager.Instance.SpawnEntity(prefab, pos, Quaternion.identity, levelParent.transform);
            else
                Instantiate(prefab, pos, Quaternion.identity, levelParent.transform);
        }
    }

    void SpawnCienpiesPeludo()
    {
        if (!spawnCienpiesPeludo || leafNodes == null || leafNodes.Count < 2) return;

        // Buscar una habitación espaciosa o pasillo largo para el spawn
        BSPNode spawnNode = null;
        int maxArea = 0;
        foreach (var node in leafNodes)
        {
            if (node == startRoom || node.archetype == RoomArchetype.SafeRoom) continue;
            int area = node.room.width * node.room.height;
            if (area > maxArea)
            {
                maxArea = area;
                spawnNode = node;
            }
        }

        if (spawnNode == null) spawnNode = leafNodes[leafNodes.Count / 2];

        Vector3 spawnPos = new Vector3(
            (spawnNode.room.x + spawnNode.room.width * 0.5f) * cellSize,
            0.5f,
            (spawnNode.room.y + spawnNode.room.height * 0.5f) * cellSize
        );

        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject obj = null;
        if (cienpiesPeludoPrefab != null)
        {
            obj = Instantiate(cienpiesPeludoPrefab, spawnPos, Quaternion.identity, levelParent.transform);
        }
        else
        {
            GameObject resPrefab = Resources.Load<GameObject>("CienpiesPeludoGigante");
            if (resPrefab != null)
            {
                obj = Instantiate(resPrefab, spawnPos, Quaternion.identity, levelParent.transform);
            }
            else
            {
                obj = new GameObject("Cienpies_Peludo_Gigante");
                obj.transform.position = spawnPos;
                obj.transform.SetParent(levelParent.transform);
                obj.AddComponent<UnityEngine.AI.NavMeshAgent>();
                obj.AddComponent<CienpiesPeludoGigante>();
            }
        }
        if (obj != null && EntityManager.Instance != null)
        {
            EntityManager.Instance.RegisterEntity(obj, false);
        }
    }

    void SpawnCienpiesPequenos()
    {
        if (!spawnCienpiesPequenos || leafNodes == null || leafNodes.Count < 2) return;

        GameObject resMiniPrefab = cienpiesPequenoPrefab ?? Resources.Load<GameObject>("CienpiesPequeno");

        int creados = 0;
        for (int i = 0; i < cantidadCienpiesPequenos && i < leafNodes.Count; i++)
        {
            BSPNode node = leafNodes[Random.Range(0, leafNodes.Count)];
            if (node == startRoom || node.archetype == RoomArchetype.SafeRoom) continue;

            Vector3 pos = new Vector3(
                (node.room.x + Random.Range(1, Mathf.Max(2, node.room.width - 1))) * cellSize,
                0.2f,
                (node.room.y + Random.Range(1, Mathf.Max(2, node.room.height - 1))) * cellSize
            );

            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
            {
                GameObject mini = null;
                if (resMiniPrefab != null)
                {
                    mini = Instantiate(resMiniPrefab, hit.position, Quaternion.Euler(0, Random.Range(0, 360), 0), levelParent.transform);
                }
                else
                {
                    mini = new GameObject($"Cienpies_Pequeno_{creados + 1}");
                    mini.transform.position = hit.position;
                    mini.transform.SetParent(levelParent.transform);
                    mini.AddComponent<UnityEngine.AI.NavMeshAgent>();
                    mini.AddComponent<CienpiesPequeno>();
                }
                if (mini != null && EntityManager.Instance != null)
                {
                    EntityManager.Instance.RegisterEntity(mini, false);
                }
                creados++;
            }
        }
    }

    void SplitNode(BSPNode node)
    {
        // --- NUEVA LÓGICA: Cuarto Batofóbico (Gigante) ---
        if (node.space.width < mapWidth && node.space.height < mapDepth)
        {
            float depthProgress = (float)node.space.y / mapDepth;
            ArchetypeSettings batoSettings = archetypeSettings.Find(s => s.archetype == RoomArchetype.Batophobic);
            if (batoSettings != null)
            {
                float prob = batoSettings.baseProbability * batoSettings.distributionCurve.Evaluate(depthProgress);
                if (Random.value < prob)
                {
                    node.archetype = RoomArchetype.Batophobic;
                    node.archetypeIntensity = batoSettings.similarityPeak;
                    leafNodes.Add(node);
                    return;
                }
            }
        }

        // Si el espacio es lo suficientemente grande, lo dividimos en dos
        if (node.space.width > minRoomSize * 2 || node.space.height > minRoomSize * 2)
        {
            bool splitHorizontal = Random.value > 0.5f;
            if (node.space.width < minRoomSize * 2) splitHorizontal = true;
            else if (node.space.height < minRoomSize * 2) splitHorizontal = false;

            // --- NUEVA LÓGICA: Forzar Pasadizos ---
            bool forceCorridor = Random.value < chanceCorridor;

            node.splitHorizontal = splitHorizontal;

            if (splitHorizontal)
            {
                int split = Random.Range(minRoomSize, node.space.height - minRoomSize);
                if (forceCorridor) split = Random.value > 0.5f ? minRoomSize : node.space.height - minRoomSize;
                
                node.splitPoint = split;
                node.left = new BSPNode { space = new RectInt(node.space.x, node.space.y, node.space.width, split) };
                node.right = new BSPNode { space = new RectInt(node.space.x, node.space.y + split, node.space.width, node.space.height - split) };
            }
            else
            {
                int split = Random.Range(minRoomSize, node.space.width - minRoomSize);
                if (forceCorridor) split = Random.value > 0.5f ? minRoomSize : node.space.width - minRoomSize;
                
                node.splitPoint = split;
                node.left = new BSPNode { space = new RectInt(node.space.x, node.space.y, split, node.space.height) };
                node.right = new BSPNode { space = new RectInt(node.space.x + split, node.space.y, node.space.width - split, node.space.height) };
            }

            SplitNode(node.left);
            SplitNode(node.right);
        }
        else
        {
            leafNodes.Add(node); // Es una habitación final
        }
    }

    void AssignArchetypes()
    {
        // Los offsets de Perlin se derivan determinísticamente de la semilla en InicializarSemilla()
        if (perlinLayers.spaceOffset == 0f && perlinLayers.illuminationOffset == 0f)
        {
            perlinLayers.spaceOffset = Random.Range(0f, 10000f);
            perlinLayers.illuminationOffset = Random.Range(0f, 10000f);
            perlinLayers.clutterOffset = Random.Range(0f, 10000f);
        }

        foreach (BSPNode node in leafNodes)
        {
            // Coordenadas absolutas del centro del cuarto (usado para muestrear el ruido contínuo)
            float centerX = (node.room.x + node.room.width / 2f) * cellSize;
            float centerZ = (node.room.y + node.room.height / 2f) * cellSize;

            // 1. Muestrear las 3 dimensiones (capas) de Ruido de Perlin [0.0 - 1.0]
            node.noiseSpace = Mathf.PerlinNoise(centerX * perlinLayers.spaceScale + perlinLayers.spaceOffset, centerZ * perlinLayers.spaceScale + perlinLayers.spaceOffset);
            node.noiseIllumination = Mathf.PerlinNoise(centerX * perlinLayers.illuminationScale + perlinLayers.illuminationOffset, centerZ * perlinLayers.illuminationScale + perlinLayers.illuminationOffset);
            node.noiseClutter = Mathf.PerlinNoise(centerX * perlinLayers.clutterScale + perlinLayers.clutterOffset, centerZ * perlinLayers.clutterScale + perlinLayers.clutterOffset);

            // 2. Aplicar la Dimensión de Espacio (Altura del Techo) usando la Curva
            float heightMultiplier = perlinLayers.heightCurve.Evaluate(node.noiseSpace);
            node.roomHeight = Mathf.Clamp(Mathf.Lerp(minWallHeight, maxWallHeight, heightMultiplier), minWallHeight, maxLevelHeight);

            // 2.5 Configuración de Capa Vertical 3D (Layer 1 = Plano base)
            if (!usarRectangulosConsecutivos)
            {
                if (maxLayers > 1)
                {
                    if (node == startRoom)
                    {
                        node.layerIndex = 0;
                    }
                    else
                    {
                        node.layerIndex = Mathf.FloorToInt(node.noiseSpace * maxLayers) % maxLayers;
                    }
                    float hCapa = Mathf.Min(layerHeight, maxLevelHeight / maxLayers);
                    node.floorElevation = node.layerIndex * hCapa;
                }
                else
                {
                    node.layerIndex = 0;
                    node.floorElevation = 0f;
                }
            }

            // 3. Asignar el Arquetipo Clásico dominante (por compatibilidad con triggers de ansiedad y el mapa)
            if (node.archetype == RoomArchetype.Normal)
            {
                if (node.noiseSpace > perlinLayers.batophobicThreshold)
                {
                    node.archetype = RoomArchetype.Batophobic;
                    node.archetypeIntensity = (node.noiseSpace - perlinLayers.batophobicThreshold) / (1f - perlinLayers.batophobicThreshold);
                }
                else if (node.noiseSpace < perlinLayers.claustrophobicThreshold)
                {
                    node.archetype = RoomArchetype.Claustrophobic;
                    node.archetypeIntensity = (perlinLayers.claustrophobicThreshold - node.noiseSpace) / perlinLayers.claustrophobicThreshold;
                }
                else if (node.noiseClutter > perlinLayers.clutteredThreshold)
                {
                    node.archetype = RoomArchetype.Cluttered;
                    node.archetypeIntensity = (node.noiseClutter - perlinLayers.clutteredThreshold) / (1f - perlinLayers.clutteredThreshold);
                }
                else if (node.noiseClutter < perlinLayers.emptyThreshold)
                {
                    node.archetype = RoomArchetype.Empty;
                    node.archetypeIntensity = 1f;
                }
                else if (node.noiseIllumination < perlinLayers.darkThreshold)
                {
                    node.archetype = RoomArchetype.Dark;
                    node.archetypeIntensity = 1f;
                }
                else if (node.noiseIllumination > perlinLayers.superIlluminatedThreshold)
                {
                    node.archetype = RoomArchetype.SuperIlluminated;
                    node.archetypeIntensity = 1f;
                }
            }
        }
    }

    void FuseBatophobicRooms()
    {
        for (int x = 1; x < mapWidth - 1; x++)
        {
            for (int z = 1; z < mapDepth - 1; z++)
            {
                if (grid[x, z] == CellType.Wall)
                {
                    // Comprobar Horizontal (Izquierda a Derecha)
                    if (grid[x - 1, z] == CellType.Floor && grid[x + 1, z] == CellType.Floor)
                    {
                        BSPNode leftNode = GetNodeAt(x - 1, z);
                        BSPNode rightNode = GetNodeAt(x + 1, z);
                        if (leftNode != null && rightNode != null && leftNode.archetype == RoomArchetype.Batophobic && rightNode.archetype == RoomArchetype.Batophobic)
                        {
                            grid[x, z] = CellType.Floor;
                            continue; // Ya lo convertimos a suelo, pasamos al siguiente
                        }
                    }
                    // Comprobar Vertical (Arriba a Abajo)
                    if (grid[x, z - 1] == CellType.Floor && grid[x, z + 1] == CellType.Floor)
                    {
                        BSPNode downNode = GetNodeAt(x, z - 1);
                        BSPNode upNode = GetNodeAt(x, z + 1);
                        if (downNode != null && upNode != null && downNode.archetype == RoomArchetype.Batophobic && upNode.archetype == RoomArchetype.Batophobic)
                        {
                            grid[x, z] = CellType.Floor;
                        }
                    }
                }
            }
        }
    }

    private BSPNode GetNodeAt(int x, int z)
    {
        foreach (BSPNode node in leafNodes)
        {
            if (x >= node.room.x && x < node.room.x + node.room.width &&
                z >= node.room.y && z < node.room.y + node.room.height)
            {
                return node;
            }
            if (node.subRooms != null)
            {
                foreach (BSPNode sub in node.subRooms)
                {
                    if (x >= sub.room.x && x < sub.room.x + sub.room.width &&
                        z >= sub.room.y && z < sub.room.y + sub.room.height)
                    {
                        return sub;
                    }
                }
            }
        }
        return null;
    }

    void CreateRooms(BSPNode node)
    {
        if (node.left == null && node.right == null)
        {
            int roomWidth = Random.Range(minRoomSize, node.space.width - 1);
            int roomDepth = Random.Range(minRoomSize, node.space.height - 1);
            int roomX = node.space.x + Random.Range(1, node.space.width - roomWidth);
            int roomY = node.space.y + Random.Range(1, node.space.height - roomDepth);
            
            node.room = new RectInt(roomX, roomY, roomWidth, roomDepth);
            
            // Función de normalización: [minArea, maxArea] -> [minHeight, maxHeight]
            float minArea = minRoomSize * minRoomSize;
            float maxArea = (mapWidth / 2f) * (mapDepth / 2f); // heurística de salón gigante
            float area = roomWidth * roomDepth;
            float t = Mathf.Clamp01((area - minArea) / (maxArea - minArea));
            node.roomHeight = Mathf.Lerp(minWallHeight, maxWallHeight, t);

            for (int x = node.space.x; x < node.space.x + node.space.width; x++)
            {
                for (int z = node.space.y; z < node.space.y + node.space.height; z++)
                {
                    if (x < 0 || x >= mapWidth || z < 0 || z >= mapDepth) continue;

                    if (x < node.room.x || x >= node.room.x + node.room.width ||
                        z < node.room.y || z >= node.room.y + node.room.height)
                    {
                        grid[x, z] = CellType.Wall;
                    }
                    else
                    {
                        grid[x, z] = CellType.Floor;
                    }
                }
            }
        }
        else
        {
            CreateRooms(node.left);
            CreateRooms(node.right);
        }
    }

    void CreateCorridors(BSPNode node)
    {
        if (node.left != null && node.right != null)
        {
            CreateCorridors(node.left);
            CreateCorridors(node.right);

            Vector2Int centerA = GetRoomCenter(node.left);
            Vector2Int centerB = GetRoomCenter(node.right);

            // Elegir aleatoriamente si vamos primero en X y luego en Z, o al revés
            if (Random.value > 0.5f)
            {
                CarveCorridor(centerA.x, centerB.x, centerA.y, true); // X primero
                CarveCorridor(centerA.y, centerB.y, centerB.x, false); // Z despues
            }
            else
            {
                CarveCorridor(centerA.y, centerB.y, centerA.x, false); // Z primero
                CarveCorridor(centerA.x, centerB.x, centerB.y, true); // X despues
            }
        }
    }

    Vector2Int GetRoomCenter(BSPNode node)
    {
        if (node.left == null && node.right == null)
        {
            return new Vector2Int(node.room.x + node.room.width / 2, node.room.y + node.room.height / 2);
        }
        else
        {
            // Tomamos el centro de una de sus hojas
            return Random.value > 0.5f ? GetRoomCenter(node.left) : GetRoomCenter(node.right);
        }
    }

    void CarveCorridor(int start, int end, int constant, bool isX)
    {
        int min = Mathf.Min(start, end);
        int max = Mathf.Max(start, end);
        
        // El grosor del pasillo (2 o 3 para que no sean tan estrechos y el NavMesh funcione bien)
        int corridorWidth = 2;

        for (int i = min; i <= max; i++)
        {
            for (int w = 0; w < corridorWidth; w++)
            {
                int x = isX ? i : constant + w;
                int z = isX ? constant + w : i;

                // Evitar que los pasillos rompan las paredes exteriores del mapa
                x = Mathf.Clamp(x, 1, mapWidth - 2);
                z = Mathf.Clamp(z, 1, mapDepth - 2);

                if (x >= 0 && x < mapWidth && z >= 0 && z < mapDepth)
                {
                    // Convertir muros en puertas/pasillos
                    if (grid[x, z] == CellType.Wall || grid[x, z] == CellType.Empty)
                    {
                        grid[x, z] = CellType.Door;
                    }
                }
            }
        }
    }

    BSPNode GetNodeForCell(int x, int z)
    {
        Vector2Int pos = new Vector2Int(x, z);
        foreach (var node in leafNodes)
        {
            if (node.subRooms != null && node.subRooms.Count > 0)
            {
                foreach (var sub in node.subRooms)
                {
                    if (sub.space.Contains(pos) || sub.room.Contains(pos)) return sub;
                }
            }
            if (node.space.Contains(pos) || node.room.Contains(pos)) return node;
        }

        // Si la celda es un pasillo o conexión externa a los cuartos, asociar a la habitación más cercana
        BSPNode nearest = null;
        float minDst = float.MaxValue;
        foreach (var node in leafNodes)
        {
            Vector2 center = new Vector2(node.room.center.x, node.room.center.y);
            float dst = (center - new Vector2(x, z)).sqrMagnitude;
            if (dst < minDst)
            {
                minDst = dst;
                nearest = node;
            }
        }
        return nearest;
    }

    void BuildPhysicalLevel()
    {
        // OPTIMIZACIÓN: Crear los materiales UNA SOLA VEZ o usar los asignados en el inspector
        Material sharedFloorMat = floorMaterial != null ? floorMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        if (floorMaterial == null) {
            sharedFloorMat.color = new Color(0.8f, 0.8f, 0.7f);
            sharedFloorMat.SetFloat("_Smoothness", 0f);
        }
        
        Material sharedCeilingMat = ceilingMaterial != null ? ceilingMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        if (ceilingMaterial == null) {
            sharedCeilingMat.color = new Color(0.2f, 0.2f, 0.2f);
            sharedCeilingMat.SetFloat("_Smoothness", 0f);
        }

        Material sharedWallMat = wallMaterial != null ? wallMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        if (wallMaterial == null) {
            sharedWallMat.color = new Color(0.9f, 0.8f, 0.6f);
            sharedWallMat.SetFloat("_Smoothness", 0f);
        }

        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapDepth; z++)
            {
                if (grid[x, z] == CellType.Empty) continue;

                // Encontrar a qué habitación (Chunk) pertenece esta celda
                BSPNode node = GetNodeForCell(x, z);
                Transform parentTransform = node != null ? node.staticContainer.transform : levelParent.transform;
                float currentHeight = node != null ? node.roomHeight : minWallHeight;
                float currentFloorY = node != null ? node.floorElevation : 0f;
                Vector3 pos = new Vector3(x * cellSize, currentFloorY, z * cellSize);

                bool isWall = (grid[x, z] == CellType.Wall);
                bool isFloorOrDoor = (grid[x, z] == CellType.Floor || grid[x, z] == CellType.Door);

                if (grid[x, z] == CellType.Floor && node != null && node.archetype == RoomArchetype.Claustrophobic)
                {
                    // Crear patrón de pilares densos basado en archetypeIntensity (ej. grilla cada 2 celdas)
                    if (x % 2 == 0 && z % 2 == 0)
                    {
                        if (Random.value < node.archetypeIntensity)
                        {
                            isWall = true;
                            // Envolver en pared pero dejar que tenga suelo abajo por si acaso (isFloor = true se mantiene)
                        }
                    }
                }

                if (isFloorOrDoor)
                {
                    if (floorPrefab != null)
                    {
                        GameObject inst = Instantiate(floorPrefab, pos, Quaternion.identity, parentTransform);
                        inst.isStatic = true;
                        inst.layer = 6; // Asignar capa 'Room'
                    }
                    else
                    {
                        // Suelo continuo (omitiendo el hueco donde sube la rampa al cuarto superior)
                        bool esHuecoSueloVertical = EsCeldaDeConexionVertical(x, z, node, true);
                        if (!esHuecoSueloVertical)
                        {
                            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            floor.transform.position = pos + Vector3.down * 0.5f;
                            floor.transform.localScale = new Vector3(cellSize, 1f, cellSize);
                            floor.transform.SetParent(parentTransform);
                            floor.GetComponent<Renderer>().sharedMaterial = sharedFloorMat; // Uso de material compartido
                            floor.isStatic = true;
                            floor.layer = 6; // Asignar capa 'Room'
                        }

                        // Si el suelo está en una capa elevada (Y > 0), generar faldón de apoyo en bordes con desnivel
                        // para evitar huecos abiertos al vacío debajo de las habitaciones altas
                        if (currentFloorY > 0.1f)
                        {
                            int[][] faldonOffsets = { new int[]{0,1}, new int[]{0,-1}, new int[]{-1,0}, new int[]{1,0} };
                            for (int f = 0; f < 4; f++)
                            {
                                int fnx = x + faldonOffsets[f][0];
                                int fnz = z + faldonOffsets[f][1];
                                float vecinoFloorY = 0f;
                                bool requiereFaldon = false;

                                if (fnx >= 0 && fnx < mapWidth && fnz >= 0 && fnz < mapDepth)
                                {
                                    if (grid[fnx, fnz] == CellType.Empty)
                                    {
                                        requiereFaldon = true;
                                        vecinoFloorY = 0f;
                                    }
                                    else
                                    {
                                        BSPNode vecinoNode = GetNodeForCell(fnx, fnz);
                                        if (vecinoNode != null && vecinoNode.floorElevation < currentFloorY - 0.2f)
                                        {
                                            requiereFaldon = true;
                                            vecinoFloorY = vecinoNode.floorElevation;
                                        }
                                    }
                                }
                                else
                                {
                                    requiereFaldon = true;
                                    vecinoFloorY = 0f;
                                }

                                if (requiereFaldon && currentFloorY > vecinoFloorY)
                                {
                                    float hFaldon = currentFloorY - vecinoFloorY;
                                    Vector3 dirFaldon = new Vector3(faldonOffsets[f][0], 0, faldonOffsets[f][1]);
                                    Vector3 posFaldon = pos + dirFaldon * (cellSize * 0.5f) + Vector3.down * (hFaldon * 0.5f);
                                    Vector3 escalaFaldon = faldonOffsets[f][0] != 0
                                        ? new Vector3(0.4f, hFaldon, cellSize)
                                        : new Vector3(cellSize, hFaldon, 0.4f);

                                    GameObject faldon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                    faldon.name = $"FloorSkirt_{x}_{z}_{f}";
                                    faldon.transform.position = posFaldon;
                                    faldon.transform.localScale = escalaFaldon;
                                    // Emparentar a levelParent para que sea estructura permanente entre capas
                                    faldon.transform.SetParent(levelParent.transform);
                                    faldon.GetComponent<Renderer>().sharedMaterial = sharedWallMat;
                                    faldon.isStatic = true;
                                    faldon.layer = 6;
                                }
                            }
                        }
                        
                        // Techo sólido (Cubo delgado con caras superior e inferior visibles; se omite en el hueco de subida de la rampa)
                        bool esHuecoEntradaVertical = EsCeldaDeConexionVertical(x, z, node, false);
                        if (!esHuecoEntradaVertical)
                        {
                            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            ceiling.transform.position = pos + Vector3.up * (currentHeight + 0.1f);
                            ceiling.transform.localScale = new Vector3(cellSize, 0.2f, cellSize);
                            ceiling.transform.SetParent(parentTransform);
                            ceiling.GetComponent<Renderer>().sharedMaterial = sharedCeilingMat;
                            ceiling.isStatic = true;
                            ceiling.layer = 6; // Asignar capa 'Room'
                        }
                    }

                    // MARCO Y DINTEL SÓLIDO PARA PUERTAS (especialmente en Halls y conexiones entre habitaciones):
                    // Construye una viga/pared sólida sobre el vano de paso, cerrando cualquier espacio abierto hasta el techo
                    // e impidiendo físicamente que el jugador o entidades puedan saltar o pasar por encima del marco del hall.
                    if (grid[x, z] == CellType.Door)
                    {
                        float doorClearance = 2.5f; // Altura libre para paso del jugador
                        float dintelMinY = currentFloorY + doorClearance;
                        float dintelMaxY = currentFloorY + currentHeight;

                        // Si hay cuartos vecinos (o halls con techos más altos o desniveles de capa), extender hasta el techo más alto
                        int[][] doorOffsets = { new int[]{0,1}, new int[]{0,-1}, new int[]{-1,0}, new int[]{1,0} };
                        for (int d = 0; d < 4; d++)
                        {
                            int dnx = x + doorOffsets[d][0];
                            int dnz = z + doorOffsets[d][1];
                            if (dnx >= 0 && dnx < mapWidth && dnz >= 0 && dnz < mapDepth)
                            {
                                BSPNode neighborNode = GetNodeForCell(dnx, dnz);
                                if (neighborNode != null)
                                {
                                    dintelMaxY = Mathf.Max(dintelMaxY, neighborNode.floorElevation + neighborNode.roomHeight);
                                }
                            }
                        }

                        float dintelTotalHeight = dintelMaxY - dintelMinY;
                        if (dintelTotalHeight > 0.05f)
                        {
                            GameObject lintelObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            lintelObj.name = $"DoorLintel_{x}_{z}";
                            lintelObj.transform.position = new Vector3(x * cellSize, dintelMinY + (dintelTotalHeight * 0.5f), z * cellSize);
                            lintelObj.transform.localScale = new Vector3(cellSize, dintelTotalHeight, cellSize);
                            lintelObj.transform.SetParent(levelParent.transform);
                            lintelObj.GetComponent<Renderer>().sharedMaterial = sharedWallMat;
                            lintelObj.isStatic = true;
                            lintelObj.layer = 6; // Capa 'Room'
                        }
                    }
                }
                else if (grid[x, z] == CellType.Chasm || grid[x, z] == CellType.Bridge)
                {
                    // Techo superior sobre zonas 3D (Desfiladeros y Puentes)
                    GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ceiling.transform.position = pos + Vector3.up * (currentHeight + 0.1f);
                    ceiling.transform.localScale = new Vector3(cellSize, 0.2f, cellSize);
                    ceiling.transform.SetParent(parentTransform);
                    ceiling.GetComponent<Renderer>().sharedMaterial = sharedCeilingMat;
                    ceiling.isStatic = true;
                    ceiling.layer = 6;
                }
                
                if (isWall)
                {
                    if (wallPrefab != null)
                    {
                        GameObject inst = Instantiate(wallPrefab, pos + Vector3.up * (currentHeight/2f), Quaternion.identity, parentTransform);
                        inst.isStatic = true;
                        inst.layer = 6; // Asignar capa 'Room'
                    }
                    else
                    {
                        // Muro sólido continuo con PrimitiveType.Cube para garantizar caras completas (frente, laterales y parte superior)
                        // y cubrir cualquier desnivel vertical entre habitaciones de distintas capas sin huecos visuales.
                        float minY = currentFloorY;
                        float maxY = currentFloorY + currentHeight;

                        int[][] wallOffsets = { new int[]{0,1}, new int[]{0,-1}, new int[]{-1,0}, new int[]{1,0} };
                        for (int i = 0; i < 4; i++)
                        {
                            int nx = x + wallOffsets[i][0];
                            int nz = z + wallOffsets[i][1];
                            if (nx >= 0 && nx < mapWidth && nz >= 0 && nz < mapDepth)
                            {
                                if (grid[nx, nz] == CellType.Floor || grid[nx, nz] == CellType.Door || grid[nx, nz] == CellType.Bridge || grid[nx, nz] == CellType.Chasm)
                                {
                                    BSPNode neighborNode = GetNodeForCell(nx, nz);
                                    if (neighborNode != null)
                                    {
                                        minY = Mathf.Min(minY, neighborNode.floorElevation);
                                        maxY = Mathf.Max(maxY, neighborNode.floorElevation + neighborNode.roomHeight);
                                    }
                                }
                            }
                        }

                        float wallTotalHeight = Mathf.Max(currentHeight, maxY - minY);
                        Vector3 wallPos = new Vector3(x * cellSize, minY + (wallTotalHeight / 2f), z * cellSize);

                        GameObject wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        wallObj.name = $"Wall_{x}_{z}";
                        wallObj.transform.position = wallPos;
                        wallObj.transform.localScale = new Vector3(cellSize, wallTotalHeight, cellSize);
                        // Si la pared divide habitaciones de diferente elevación/capa, emparentar a levelParent para que nunca desaparezca
                        bool esMuroDivisorioCapas = (minY < currentFloorY - 0.2f || maxY > currentFloorY + currentHeight + 0.2f);
                        wallObj.transform.SetParent(esMuroDivisorioCapas ? levelParent.transform : parentTransform);
                        wallObj.GetComponent<Renderer>().sharedMaterial = sharedWallMat;
                        wallObj.isStatic = true;
                        wallObj.layer = 6; // Capa 'Room'
                    }
                }
            }
        }
    }

    int GetEntranceCount(BSPNode node)
    {
        RectInt r = node.room;
        int count = 0;

        // Borde Inferior (y)
        bool inDoor = false;
        for(int x = r.x; x < r.x + r.width; x++) {
            if (x >= 0 && x < mapWidth && r.y >= 0 && r.y < mapDepth && grid[x, r.y] == CellType.Door) {
                if (!inDoor) { count++; inDoor = true; }
            } else inDoor = false;
        }

        // Borde Superior (y + height - 1)
        inDoor = false;
        for(int x = r.x; x < r.x + r.width; x++) {
            if (x >= 0 && x < mapWidth && r.y + r.height - 1 >= 0 && r.y + r.height - 1 < mapDepth && grid[x, r.y + r.height - 1] == CellType.Door) {
                if (!inDoor) { count++; inDoor = true; }
            } else inDoor = false;
        }

        // Borde Izquierdo (x)
        inDoor = false;
        for(int z = r.y; z < r.y + r.height; z++) {
            if (z >= 0 && z < mapDepth && r.x >= 0 && r.x < mapWidth && grid[r.x, z] == CellType.Door) {
                if (!inDoor) { count++; inDoor = true; }
            } else inDoor = false;
        }

        // Borde Derecho (x + width - 1)
        inDoor = false;
        for(int z = r.y; z < r.y + r.height; z++) {
            if (z >= 0 && z < mapDepth && r.x + r.width - 1 >= 0 && r.x + r.width - 1 < mapWidth && grid[r.x + r.width - 1, z] == CellType.Door) {
                if (!inDoor) { count++; inDoor = true; }
            } else inDoor = false;
        }

        return count;
    }

    /// <summary>
    /// Reposiciona al jugador de forma segura en el centro del Start Room sobre el suelo firme.
    /// </summary>
    public void RepositionPlayerToStartRoom()
    {
        if (startRoom == null) return;
        RectInt sr = startRoom.room;
        Vector3 playerSpawnCenter = new Vector3(
            (sr.x + (sr.width - 1) / 2f) * cellSize,
            startRoom.floorElevation + 0.8f,
            (sr.y + (sr.height - 1) / 2f) * cellSize
        );

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            SimplePlayerController spc = player.GetComponent<SimplePlayerController>();
            if (spc != null)
            {
                spc.TeleportTo(playerSpawnCenter);
            }
            else
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = playerSpawnCenter;
                Physics.SyncTransforms();
                if (cc != null) cc.enabled = true;
            }
        }
    }

    /// <summary>
    /// NUEVO PROCEDIMIENTO 3D (Fase 2): Posiciona tanto el Start Room como el End Room
    /// aleatoriamente al inicio en la misma capa base (Layer 1, Y=0), garantizando que sean Final Rooms fijos,
    /// sin solapamiento destructivo y con una distancia de separación adecuada para generar el nivel entre ellos.
    /// </summary>
    void EscogerStartEndRoomsAleatorios()
    {
        int margen = 3;

        // 1. Elegir Start Room (Final Room fijo de spawn para el jugador)
        int startW = Random.Range(tamanoMinimoRectangulo, Mathf.Min(tamanoMaximoRectangulo, 6) + 1);
        int startD = Random.Range(tamanoMinimoRectangulo, Mathf.Min(tamanoMaximoRectangulo, 6) + 1);
        int maxStartX = Mathf.Max(margen + 1, mapWidth - startW - margen);
        int maxStartZ = Mathf.Max(margen + 1, mapDepth - startD - margen);

        int startX = Random.Range(margen, maxStartX);
        int startZ = Random.Range(margen, maxStartZ);

        startRoom = new BSPNode
        {
            space = new RectInt(startX - 1, startZ - 1, startW + 2, startD + 2),
            room = new RectInt(startX, startZ, startW, startD),
            layerIndex = 0,
            floorElevation = 0f,
            roomHeight = Mathf.Clamp(minWallHeight + 1.5f, 3.5f, maxLevelHeight),
            archetype = RoomArchetype.SafeRoom,
            structureType = RoomStructureType.FinalRoom
        };

        // 2. Elegir End Room (Final Room fijo de meta)
        int endW = Random.Range(tamanoMinimoRectangulo, Mathf.Min(tamanoMaximoRectangulo, 6) + 1);
        int endD = Random.Range(tamanoMinimoRectangulo, Mathf.Min(tamanoMaximoRectangulo, 6) + 1);
        float minSeparacion = Mathf.Max(8f, Mathf.Min(mapWidth, mapDepth) * distanciaMinimaStartEnd);

        int bestEndX = margen;
        int bestEndZ = margen;
        float bestDist = -1f;

        // Intentar hasta 60 posiciones aleatorias que respeten la distancia y no se solapen
        bool posicionValida = false;
        for (int intento = 0; intento < 60; intento++)
        {
            int testX = Random.Range(margen, Mathf.Max(margen + 1, mapWidth - endW - margen));
            int testZ = Random.Range(margen, Mathf.Max(margen + 1, mapDepth - endD - margen));
            RectInt testRect = new RectInt(testX, testZ, endW, endD);

            // Validar que no se solape con startRoom (con margen extra de 2 celdas de separación)
            RectInt startMarginRect = new RectInt(startX - 2, startZ - 2, startW + 4, startD + 4);
            if (testRect.Overlaps(startMarginRect)) continue;

            float dist = Vector2.Distance(
                new Vector2(startX + startW * 0.5f, startZ + startD * 0.5f),
                new Vector2(testX + endW * 0.5f, testZ + endD * 0.5f)
            );

            if (dist > bestDist)
            {
                bestDist = dist;
                bestEndX = testX;
                bestEndZ = testZ;
            }

            if (dist >= minSeparacion)
            {
                bestEndX = testX;
                bestEndZ = testZ;
                posicionValida = true;
                break;
            }
        }

        if (!posicionValida)
        {
            // Ubicar en la esquina más lejana a startRoom
            int cornerX = (startX > mapWidth / 2) ? margen : mapWidth - endW - margen;
            int cornerZ = (startZ > mapDepth / 2) ? margen : mapDepth - endD - margen;
            bestEndX = Mathf.Clamp(cornerX, margen, mapWidth - endW - margen);
            bestEndZ = Mathf.Clamp(cornerZ, margen, mapDepth - endD - margen);
        }

        endRoom = new BSPNode
        {
            space = new RectInt(bestEndX - 1, bestEndZ - 1, endW + 2, endD + 2),
            room = new RectInt(bestEndX, bestEndZ, endW, endD),
            layerIndex = 0,
            floorElevation = 0f,
            roomHeight = Mathf.Clamp(minWallHeight + 1.5f, 3.5f, maxLevelHeight),
            archetype = RoomArchetype.SafeRoom,
            structureType = RoomStructureType.FinalRoom
        };

        // 3. Registrar ambos cuartos especiales
        leafNodes.Add(startRoom);
        leafNodes.Add(endRoom);

        // 4. Marcar celdas de suelo en la grilla para ambos cuartos
        MarcarHabitacionEnGrilla(startRoom.room);
        MarcarHabitacionEnGrilla(endRoom.room);

        // 5. Posicionar de inmediato al jugador en el centro de Start Room
        RepositionPlayerToStartRoom();

        Debug.Log($"<b>[Generación 3D - Fase 2]</b> Start Room ({startX},{startZ} [{startW}x{startD}]) y End Room ({bestEndX},{bestEndZ} [{endW}x{endD}]) posicionados al inicio en Layer 1 (Y=0). Distancia={Vector2.Distance(new Vector2(startX,startZ), new Vector2(bestEndX,bestEndZ)):F1} celdas.");
    }

    /// <summary>
    /// Determina la capa vertical (Layer 1 .. N) de un nuevo cuarto dependiendo del layer de su cuarto raíz/padre.
    /// Garantiza de forma estricta que jamás supere la cantidad máxima de layers configurada (maxLayers).
    /// </summary>
    int DeterminarLayerHijo(int layerPadre)
    {
        if (maxLayers <= 1) return 0;

        int targetLayer = layerPadre;
        float r = Random.value;
        // Reducción drástica de saltos de capa: 80% de las habitaciones permanecen en el mismo layer
        // Solo un 10% sube 1 piso y 10% baja 1 piso de forma controlada y gradual
        if (r < 0.10f)
        {
            targetLayer = layerPadre + 1; // Subir un piso
        }
        else if (r < 0.20f)
        {
            targetLayer = layerPadre - 1; // Bajar un piso
        }
        else
        {
            targetLayer = layerPadre; // Mismo layer
        }

        // REGLA ESTRICTA: No existe forma en que un cuarto supere la cantidad máxima de layers
        return Mathf.Clamp(targetLayer, 0, maxLayers - 1);
    }

    /// <summary>
    /// Comprueba si una celda (x, z) corresponde a la abertura/entrada de una conexión vertical.
    /// Si esParaSuelo es true, comprueba si debe omitirse el suelo en el cuarto superior para no tapar la rampa.
    /// Si esParaSuelo es false, comprueba si debe omitirse el techo en el cuarto inferior para no chocar con la rampa.
    /// </summary>
    bool EsCeldaDeConexionVertical(int x, int z, BSPNode node, bool esParaSuelo = false)
    {
        if (conexionesVerticales == null || conexionesVerticales.Count == 0) return false;
        Vector2Int p = new Vector2Int(x, z);
        foreach (var cv in conexionesVerticales)
        {
            if (cv.zonaEntrada.Contains(p))
            {
                if (esParaSuelo && (node == null || cv.cuartoSuperior == node || (node.floorElevation >= cv.cuartoSuperior.floorElevation - 0.5f)))
                    return true;
                if (!esParaSuelo && (node == null || cv.cuartoInferior == node || (node.floorElevation <= cv.cuartoInferior.floorElevation + 0.5f)))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Crea una conexión navegable vertical entre un cuarto inferior y uno superior.
    /// La posición y el tamaño de la entrada se calculan de forma aleatoria dentro de las dimensiones disponibles.
    /// </summary>
    void CrearConexionVerticalEntreCuartos(BSPNode a, BSPNode b)
    {
        BSPNode bajo = (a.floorElevation <= b.floorElevation) ? a : b;
        BSPNode alto = (a.floorElevation > b.floorElevation) ? a : b;

        // 1. Tamaño aleatorio de la entrada vertical
        int maxW = Mathf.Clamp(Mathf.Min(bajo.room.width, alto.room.width) - 1, 2, tamanoMaximoEntradaVertical);
        int maxD = Mathf.Clamp(Mathf.Min(bajo.room.height, alto.room.height) - 1, 2, tamanoMaximoEntradaVertical);
        int minW = Mathf.Clamp(tamanoMinimoEntradaVertical, 2, maxW);
        int minD = Mathf.Clamp(tamanoMinimoEntradaVertical, 2, maxD);

        int entradaW = Random.Range(minW, maxW + 1);
        int entradaD = Random.Range(minD, maxD + 1);

        // 2. Posición aleatoria de la entrada vertical en la interfaz entre ambos cuartos
        int minX = Mathf.Max(bajo.room.x, alto.room.x);
        int maxX = Mathf.Min(bajo.room.xMax - entradaW, alto.room.xMax - entradaW);
        int entradaX;
        if (maxX >= minX)
        {
            entradaX = Random.Range(minX, maxX + 1);
        }
        else
        {
            entradaX = Mathf.Clamp(bajo.room.x + Random.Range(0, Mathf.Max(1, bajo.room.width - entradaW)), 1, mapWidth - entradaW - 1);
        }

        int minZ = Mathf.Max(bajo.room.y, alto.room.y);
        int maxZ = Mathf.Min(bajo.room.yMax - entradaD, alto.room.yMax - entradaD);
        int entradaZ;
        if (maxZ >= minZ)
        {
            entradaZ = Random.Range(minZ, maxZ + 1);
        }
        else
        {
            entradaZ = Mathf.Clamp(bajo.room.y + Random.Range(0, Mathf.Max(1, bajo.room.height - entradaD)), 1, mapDepth - entradaD - 1);
        }

        RectInt zonaEntrada = new RectInt(entradaX, entradaZ, entradaW, entradaD);

        // 3. Registrar conexión vertical
        ConexionVertical cv = new ConexionVertical
        {
            cuartoInferior = bajo,
            cuartoSuperior = alto,
            zonaEntrada = zonaEntrada,
            anchoRampa = Mathf.Clamp(entradaW * cellSize * 0.8f, 1.8f, 3.2f)
        };
        conexionesVerticales.Add(cv);

        // 4. Marcar celdas de la entrada como transitables en la grilla
        for (int x = zonaEntrada.x; x < zonaEntrada.xMax; x++)
        {
            for (int z = zonaEntrada.y; z < zonaEntrada.yMax; z++)
            {
                if (x >= 0 && x < mapWidth && z >= 0 && z < mapDepth)
                {
                    grid[x, z] = CellType.Door;
                }
            }
        }

        // 5. Trazar pasillos de enlace conectando el centro de la habitación baja con la entrada y la alta
        Vector2Int cBajo = new Vector2Int(bajo.room.x + bajo.room.width / 2, bajo.room.y + bajo.room.height / 2);
        Vector2Int cEntrada = new Vector2Int(zonaEntrada.x + zonaEntrada.width / 2, zonaEntrada.y + zonaEntrada.height / 2);
        Vector2Int cAlto = new Vector2Int(alto.room.x + alto.room.width / 2, alto.room.y + alto.room.height / 2);

        CarveCorridor(cBajo.x, cEntrada.x, cBajo.y, true);
        CarveCorridor(cBajo.y, cEntrada.y, cEntrada.x, false);
        CarveCorridor(cEntrada.x, cAlto.x, cEntrada.y, true);
        CarveCorridor(cEntrada.y, cAlto.y, cAlto.x, false);

        Debug.Log($"<b>[Conexión Vertical 3D]</b> Entre {bajo.room} (Layer {bajo.layerIndex + 1}) y {alto.room} (Layer {alto.layerIndex + 1}). Entrada aleatoria en ({entradaX}, {entradaZ}) de tamaño {entradaW}x{entradaD}.");
    }

    /// <summary>
    /// Instancia físicamente las rampas inclinadas entre pisos y las rampitas suaves en micro-desniveles del mismo layer.
    /// </summary>
    void ConstruirConexionesVerticalesFisicas()
    {
        Material matRampa = Resources.Load<Material>("Materials/Arquitectura3D_Rampa");
        if (matRampa == null) matRampa = floorMaterial != null ? floorMaterial : wallMaterial;

        // 1. Construir rampas grandes entre capas principales (si existen)
        if (conexionesVerticales != null)
        {
            foreach (var cv in conexionesVerticales)
            {
                if (cv.cuartoInferior == null || cv.cuartoSuperior == null) continue;

                // Las rampas conectan ambas capas y deben pertenecer a levelParent para que nunca sean destruidas/ocultadas por culling
                Transform parent = levelParent.transform;

                // Punto bajo: en el centro de la zona de entrada en la capa inferior
                Vector3 pBajo = new Vector3(
                    (cv.zonaEntrada.x + cv.zonaEntrada.width * 0.5f) * cellSize,
                    cv.cuartoInferior.floorElevation,
                    (cv.zonaEntrada.y + cv.zonaEntrada.height * 0.5f) * cellSize
                );

                // Punto alto: hacia el interior de la habitación superior
                Vector3 pAlto = new Vector3(
                    (cv.cuartoSuperior.room.center.x) * cellSize,
                    cv.cuartoSuperior.floorElevation,
                    (cv.cuartoSuperior.room.center.y) * cellSize
                );

                // Evitar que la rampa sea excesivamente larga limitando la distancia horizontal
                Vector3 offsetHoriz = pAlto - pBajo;
                offsetHoriz.y = 0f;
                float maxDistRampa = Mathf.Max(cv.zonaEntrada.width, cv.zonaEntrada.height) * cellSize * 2.2f;
                if (offsetHoriz.magnitude > maxDistRampa && offsetHoriz.sqrMagnitude > 0.01f)
                {
                    pAlto = pBajo + offsetHoriz.normalized * maxDistRampa + Vector3.up * (cv.cuartoSuperior.floorElevation - cv.cuartoInferior.floorElevation);
                }
                else
                {
                    pAlto.y = cv.cuartoSuperior.floorElevation;
                }

                // Construir la rampa navegable
                Procedural3DArchitecture.CrearRampa(parent, pBajo, pAlto, cv.anchoRampa, matRampa, true);
            }
        }

        // 2. Construir pequeñas rampitas de unión en micro-desniveles del mismo piso
        if (rampitasDeUnion != null)
        {
            foreach (var r in rampitasDeUnion)
            {
                // Emparentar a levelParent para que las rampas de conexión entre cuartos nunca se oculten por culling
                Transform parent = levelParent != null ? levelParent.transform : (r.parent != null ? r.parent : transform);
                Procedural3DArchitecture.CrearRampa(parent, r.inicio, r.fin, r.ancho, matRampa, false);
            }
        }
    }

    /// <summary>
    /// NUEVO PROCEDIMIENTO 3D (Fase 2): Genera el nivel como un árbol jerárquico (Tree Expansion).
    /// El tronco principal conecta Start Room hacia End Room, y a partir de este se expanden ramas secundarias
    /// y sub-ramas con capas de 1..N Layers, micro-desniveles con rampitas y grosores aleatorios por partición.
    /// </summary>
    void GenerarNivelMismaCapaRectangulosConsecutivos()
    {
        int margen = 2;
        startRoom.treeDepth = 0;
        startRoom.branchDepth = 0;
        startRoom.isTrunk = true;

        List<BSPNode> cadenaPrincipal = new List<BSPNode> { startRoom };
        BSPNode habitacionActual = startRoom;

        int maxPasos = 25;
        int pasos = 0;

        Vector2 objetivoEnd = new Vector2(endRoom.room.center.x, endRoom.room.center.y);

        while (pasos < maxPasos)
        {
            pasos++;
            Vector2 posActual = new Vector2(habitacionActual.room.center.x, habitacionActual.room.center.y);
            float distEnd = Vector2.Distance(posActual, objetivoEnd);

            // Si estamos lo suficientemente cerca de End Room, nos conectamos a él directamente
            if (distEnd <= Mathf.Max(habitacionActual.room.width, habitacionActual.room.height) + 5f)
            {
                ConectarHabitacionesGarantizado(habitacionActual, endRoom);
                habitacionActual.treeChildren.Add(endRoom);
                endRoom.treeDepth = habitacionActual.treeDepth + 1;
                endRoom.branchDepth = 0;
                endRoom.isTrunk = true;
                break;
            }

            Vector2 dirHaciaEnd = (objetivoEnd - posActual).normalized;
            List<Vector2Int> direcciones = ObtenerDireccionesCandidatas(dirHaciaEnd);

            bool colocado = false;
            foreach (Vector2Int dir in direcciones)
            {
                BSPNode nuevoNodo = IntentarColocarHabitacionHija(habitacionActual, dir, margen, true);
                if (nuevoNodo != null)
                {
                    habitacionActual.treeChildren.Add(nuevoNodo);
                    cadenaPrincipal.Add(nuevoNodo);
                    habitacionActual = nuevoNodo;
                    colocado = true;
                    break;
                }
            }

            if (!colocado)
            {
                // Si no hubo dirección libre contigua, trazar un enlace navegable hacia End Room
                ConectarHabitacionesGarantizado(habitacionActual, endRoom);
                habitacionActual.treeChildren.Add(endRoom);
                endRoom.treeDepth = habitacionActual.treeDepth + 1;
                endRoom.branchDepth = 0;
                endRoom.isTrunk = true;
                break;
            }
        }

        // Conectar siempre el último cuarto del tronco con End Room para garantizar continuidad del recorrido
        if (cadenaPrincipal.Count > 0 && !cadenaPrincipal[cadenaPrincipal.Count - 1].treeChildren.Contains(endRoom))
        {
            BSPNode ultimoTronco = cadenaPrincipal[cadenaPrincipal.Count - 1];
            ConectarHabitacionesGarantizado(ultimoTronco, endRoom);
            ultimoTronco.treeChildren.Add(endRoom);
            endRoom.treeDepth = ultimoTronco.treeDepth + 1;
            endRoom.branchDepth = 0;
            endRoom.isTrunk = true;
        }

        // EXPANDIR EL NIVEL COMO UN ÁRBOL (Tree Branching: ramas secundarias, sub-ramas y atajos)
        GenerarExpansionArborea(cadenaPrincipal, margen);

        // Construir paredes de cierre en el perímetro de cada habitación y bordes del mapa
        ConstruirParedesPerimetrales();
    }

    /// <summary>
    /// Intenta colocar una habitación adyacente a un padre en una dirección dada.
    /// Aplica alineación coordinada en el eje perpendicular (solapamiento mínimo de 4 celdas),
    /// asignación de Layer (1..N sin superar maxLayers), micro-desnivel, tipo estructural y conexión navegable.
    /// </summary>
    BSPNode IntentarColocarHabitacionHija(BSPNode padre, Vector2Int dir, int margen, bool esTrunk = false)
    {
        if (padre == null) return null;

        int w = Random.Range(tamanoMinimoRectangulo, tamanoMaximoRectangulo + 1);
        int h = Random.Range(tamanoMinimoRectangulo, tamanoMaximoRectangulo + 1);

        // Grosor aleatorio de pared por partición (entre cuartos)
        int grosorParedParticion = Random.Range(grosorParedMinimo, grosorParedMaximo + 1);
        int separacionExtra = grosorParedParticion - 1;

        int nuevoX = padre.room.x;
        int nuevoZ = padre.room.y;

        if (dir.x > 0)
        {
            nuevoX = padre.room.xMax + separacionExtra;
            // Coordinar alineación en el eje Z para garantizar solapamiento amplio y evitar desfases
            int centroZ = padre.room.y + (padre.room.height - h) / 2;
            int maxJitter = Mathf.Max(0, (Mathf.Min(padre.room.height, h) - 4) / 2);
            int jitter = maxJitter > 0 ? Random.Range(-maxJitter, maxJitter + 1) : 0;
            int minPermitidoZ = Mathf.Min(padre.room.y - h + 4, padre.room.yMax - 4);
            int maxPermitidoZ = Mathf.Max(padre.room.y - h + 4, padre.room.yMax - 4);
            nuevoZ = Mathf.Clamp(centroZ + jitter, minPermitidoZ, maxPermitidoZ);
        }
        else if (dir.x < 0)
        {
            nuevoX = padre.room.x - w - separacionExtra;
            // Coordinar alineación en el eje Z para garantizar solapamiento amplio y evitar desfases
            int centroZ = padre.room.y + (padre.room.height - h) / 2;
            int maxJitter = Mathf.Max(0, (Mathf.Min(padre.room.height, h) - 4) / 2);
            int jitter = maxJitter > 0 ? Random.Range(-maxJitter, maxJitter + 1) : 0;
            int minPermitidoZ = Mathf.Min(padre.room.y - h + 4, padre.room.yMax - 4);
            int maxPermitidoZ = Mathf.Max(padre.room.y - h + 4, padre.room.yMax - 4);
            nuevoZ = Mathf.Clamp(centroZ + jitter, minPermitidoZ, maxPermitidoZ);
        }
        else if (dir.y > 0)
        {
            nuevoZ = padre.room.yMax + separacionExtra;
            // Coordinar alineación en el eje X para garantizar solapamiento amplio y evitar desfases
            int centroX = padre.room.x + (padre.room.width - w) / 2;
            int maxJitter = Mathf.Max(0, (Mathf.Min(padre.room.width, w) - 4) / 2);
            int jitter = maxJitter > 0 ? Random.Range(-maxJitter, maxJitter + 1) : 0;
            int minPermitidoX = Mathf.Min(padre.room.x - w + 4, padre.room.xMax - 4);
            int maxPermitidoX = Mathf.Max(padre.room.x - w + 4, padre.room.xMax - 4);
            nuevoX = Mathf.Clamp(centroX + jitter, minPermitidoX, maxPermitidoX);
        }
        else if (dir.y < 0)
        {
            nuevoZ = padre.room.y - h - separacionExtra;
            // Coordinar alineación en el eje X para garantizar solapamiento amplio y evitar desfases
            int centroX = padre.room.x + (padre.room.width - w) / 2;
            int maxJitter = Mathf.Max(0, (Mathf.Min(padre.room.width, w) - 4) / 2);
            int jitter = maxJitter > 0 ? Random.Range(-maxJitter, maxJitter + 1) : 0;
            int minPermitidoX = Mathf.Min(padre.room.x - w + 4, padre.room.xMax - 4);
            int maxPermitidoX = Mathf.Max(padre.room.x - w + 4, padre.room.xMax - 4);
            nuevoX = Mathf.Clamp(centroX + jitter, minPermitidoX, maxPermitidoX);
        }

        nuevoX = Mathf.Clamp(nuevoX, margen, mapWidth - w - margen);
        nuevoZ = Mathf.Clamp(nuevoZ, margen, mapDepth - h - margen);

        RectInt rectCandidato = new RectInt(nuevoX, nuevoZ, w, h);
        if (rectCandidato.width < tamanoMinimoRectangulo || rectCandidato.height < tamanoMinimoRectangulo) return null;
        if (HaySolapamientoDestructivo(rectCandidato)) return null;

        // 1..N Layers dependiendo del layer actual de raíz (clamp estricto a [0, maxLayers - 1])
        int nuevoLayer = DeterminarLayerHijo(padre.layerIndex);
        float hCapa = Mathf.Min(layerHeight, maxLevelHeight / Mathf.Max(1, maxLayers));
        
        // Micro-desnivel sutil dentro del mismo layer (ej. 0m, 0.4m, 0.8m)
        float microDesnivel = (Random.value < probabilidadMicroDesnivel) ? (Mathf.Round(Random.Range(-maxMicroDesnivel, maxMicroDesnivel) * 2f) / 2f) : 0f;
        float elevacionY = Mathf.Max(0f, nuevoLayer * hCapa + microDesnivel);

        // Clasificación: Final Rooms vs Halls
        bool esHall = (w >= umbralDimensionHall && h >= umbralDimensionHall) || (w * h >= 38);
        RoomStructureType tipo = esHall ? RoomStructureType.Hall : RoomStructureType.FinalRoom;

        int nuevoBranchDepth = esTrunk ? 0 : (padre.isTrunk ? 1 : padre.branchDepth + 1);

        BSPNode nuevoNodo = new BSPNode
        {
            space = new RectInt(nuevoX - 1, nuevoZ - 1, w + 2, h + 2),
            room = rectCandidato,
            layerIndex = nuevoLayer,
            floorElevation = elevacionY,
            roomHeight = Mathf.Clamp(minWallHeight + (esHall ? 2.5f : 1.0f), 3.5f, maxLevelHeight - elevacionY),
            archetype = RoomArchetype.Normal,
            structureType = tipo,
            treeDepth = padre.treeDepth + 1,
            branchDepth = nuevoBranchDepth,
            isTrunk = esTrunk
        };

        if (tipo == RoomStructureType.Hall)
        {
            SubdividirHall(nuevoNodo);
        }
        else
        {
            MarcarHabitacionEnGrilla(nuevoNodo.room);
        }

        // Conexión garantizada entre el cuarto padre y el nuevo
        ConectarHabitacionesGarantizado(padre, nuevoNodo);

        leafNodes.Add(nuevoNodo);
        return nuevoNodo;
    }

    /// <summary>
    /// Subdivide un Hall utilizando sus dimensiones como marco exterior.
    /// Crea sub-habitaciones clasificadas como FinalRooms con al menos una puerta interior de interconexión.
    /// El grosor de la pared divisoria se determina aleatoriamente por partición.
    /// </summary>
    void SubdividirHall(BSPNode hallNode)
    {
        RectInt r = hallNode.room;
        bool splitHorizontal = r.height >= r.width;

        // Grosor aleatorio de pared por partición (tabique interior)
        int grosorTabique = Random.Range(grosorParedMinimo, grosorParedMaximo + 1);

        int minSubSize = 3;
        if (splitHorizontal)
        {
            if (r.height < minSubSize * 2 + grosorTabique)
                grosorTabique = Mathf.Max(1, r.height - minSubSize * 2);

            if (r.height < minSubSize * 2 + grosorTabique)
            {
                // Espacio interior insuficiente: se conserva íntegro como FinalRoom
                hallNode.structureType = RoomStructureType.FinalRoom;
                MarcarHabitacionEnGrilla(r);
                return;
            }

            int splitY = r.y + (r.height - grosorTabique) / 2;

            RectInt sub1 = new RectInt(r.x, r.y, r.width, splitY - r.y);
            RectInt sub2 = new RectInt(r.x, splitY + grosorTabique, r.width, r.yMax - (splitY + grosorTabique));

            // Micro-desnivel opcional entre las subdivisiones de un Hall en el mismo piso
            float microDesnivelSubH = (Random.value < probabilidadMicroDesnivel) ? (Random.value < 0.5f ? 0.4f : -0.4f) : 0f;
            float elevSub2H = Mathf.Max(0f, hallNode.floorElevation + microDesnivelSubH);

            BSPNode nodeSub1 = new BSPNode
            {
                space = sub1,
                room = sub1,
                layerIndex = hallNode.layerIndex,
                floorElevation = hallNode.floorElevation,
                roomHeight = hallNode.roomHeight,
                structureType = RoomStructureType.FinalRoom
            };
            BSPNode nodeSub2 = new BSPNode
            {
                space = sub2,
                room = sub2,
                layerIndex = hallNode.layerIndex,
                floorElevation = elevSub2H,
                roomHeight = hallNode.roomHeight,
                structureType = RoomStructureType.FinalRoom
            };

            hallNode.subRooms.Add(nodeSub1);
            hallNode.subRooms.Add(nodeSub2);

            MarcarHabitacionEnGrilla(sub1);
            MarcarHabitacionEnGrilla(sub2);

            // Tabique divisorio con grosor aleatorio y puerta de paso ancha centrada
            int doorWidthH = Mathf.Clamp(r.width >= 6 ? 3 : 2, 2, Mathf.Max(2, r.width - 2));
            int doorX = r.x + (r.width - doorWidthH) / 2;
            for (int t = 0; t < grosorTabique; t++)
            {
                int yActual = splitY + t;
                for (int x = r.x; x < r.xMax; x++)
                {
                    if (x >= doorX && x < doorX + doorWidthH)
                        grid[x, yActual] = CellType.Door;
                    else
                        grid[x, yActual] = CellType.Wall;
                }
            }

            // Si hay micro-desnivel entre ambas sub-habitaciones, registrar rampita en la puerta
            if (Mathf.Abs(nodeSub1.floorElevation - nodeSub2.floorElevation) >= 0.12f)
            {
                float doorXCentro = (doorX + doorWidthH * 0.5f) * cellSize;
                Vector3 pA = new Vector3(doorXCentro, nodeSub1.floorElevation, (splitY - 0.2f) * cellSize);
                Vector3 pB = new Vector3(doorXCentro, nodeSub2.floorElevation, (splitY + grosorTabique + 0.2f) * cellSize);
                RegistrarRampita(nodeSub1, pA, pB, doorWidthH * cellSize * 0.92f);
            }
        }
        else
        {
            if (r.width < minSubSize * 2 + grosorTabique)
                grosorTabique = Mathf.Max(1, r.width - minSubSize * 2);

            if (r.width < minSubSize * 2 + grosorTabique)
            {
                hallNode.structureType = RoomStructureType.FinalRoom;
                MarcarHabitacionEnGrilla(r);
                return;
            }

            int splitX = r.x + (r.width - grosorTabique) / 2;

            RectInt sub1 = new RectInt(r.x, r.y, splitX - r.x, r.height);
            RectInt sub2 = new RectInt(splitX + grosorTabique, r.y, r.xMax - (splitX + grosorTabique), r.height);

            // Micro-desnivel opcional entre las subdivisiones de un Hall en el mismo piso
            float microDesnivelSubV = (Random.value < probabilidadMicroDesnivel) ? (Random.value < 0.5f ? 0.4f : -0.4f) : 0f;
            float elevSub2V = Mathf.Max(0f, hallNode.floorElevation + microDesnivelSubV);

            BSPNode nodeSub1 = new BSPNode
            {
                space = sub1,
                room = sub1,
                layerIndex = hallNode.layerIndex,
                floorElevation = hallNode.floorElevation,
                roomHeight = hallNode.roomHeight,
                structureType = RoomStructureType.FinalRoom
            };
            BSPNode nodeSub2 = new BSPNode
            {
                space = sub2,
                room = sub2,
                layerIndex = hallNode.layerIndex,
                floorElevation = elevSub2V,
                roomHeight = hallNode.roomHeight,
                structureType = RoomStructureType.FinalRoom
            };

            hallNode.subRooms.Add(nodeSub1);
            hallNode.subRooms.Add(nodeSub2);

            MarcarHabitacionEnGrilla(sub1);
            MarcarHabitacionEnGrilla(sub2);

            // Tabique divisorio con grosor aleatorio y puerta de paso ancha centrada
            int doorWidthV = Mathf.Clamp(r.height >= 6 ? 3 : 2, 2, Mathf.Max(2, r.height - 2));
            int doorZ = r.y + (r.height - doorWidthV) / 2;
            for (int t = 0; t < grosorTabique; t++)
            {
                int xActual = splitX + t;
                for (int z = r.y; z < r.yMax; z++)
                {
                    if (z >= doorZ && z < doorZ + doorWidthV)
                        grid[xActual, z] = CellType.Door;
                    else
                        grid[xActual, z] = CellType.Wall;
                }
            }

            // Si hay micro-desnivel entre ambas sub-habitaciones, registrar rampita en la puerta
            if (Mathf.Abs(nodeSub1.floorElevation - nodeSub2.floorElevation) >= 0.12f)
            {
                float doorZCentro = (doorZ + doorWidthV * 0.5f) * cellSize;
                Vector3 pA = new Vector3((splitX - 0.2f) * cellSize, nodeSub1.floorElevation, doorZCentro);
                Vector3 pB = new Vector3((splitX + grosorTabique + 0.2f) * cellSize, nodeSub2.floorElevation, doorZCentro);
                RegistrarRampita(nodeSub1, pA, pB, doorWidthV * cellSize * 0.92f);
            }
        }
    }

    /// <summary>
    /// Garantiza al menos una conexión directa y navegable entre dos habitaciones consecutivas.
    /// Si comparten diferente layerIndex con desnivel mayor o igual a 2.5m, crea una conexión vertical.
    /// Si están en el mismo layerIndex o tienen micro-desniveles, traza puertas con rampitas suaves.
    /// </summary>
    void ConectarHabitacionesGarantizado(BSPNode a, BSPNode b)
    {
        if (a == null || b == null) return;

        float deltaElevacion = Mathf.Abs(a.floorElevation - b.floorElevation);

        // Si la diferencia vertical es grande (pisos distintos >= 2.5m), crear conexión vertical clásica con rampa grande
        if (deltaElevacion >= 2.5f)
        {
            CrearConexionVerticalEntreCuartos(a, b);
            return;
        }

        RectInt rA = a.room;
        RectInt rB = b.room;

        // 1. Revisar si comparten pared vertical contigua o cercana (A a la izquierda de B, o B a la izquierda de A)
        bool aIzquierdaB = (rA.xMax <= rB.x && rB.x - rA.xMax <= 4);
        bool bIzquierdaA = (rB.xMax <= rA.x && rA.x - rB.xMax <= 4);

        if (aIzquierdaB || bIzquierdaA)
        {
            int overlapMinZ = Mathf.Max(rA.y, rB.y);
            int overlapMaxZ = Mathf.Min(rA.yMax, rB.yMax);
            int overlapLen = overlapMaxZ - overlapMinZ;

            if (overlapLen >= 2)
            {
                // Puerta generosa y ancha (2 a 3 celdas = 8m a 12m)
                int doorWidth = Mathf.Clamp(overlapLen >= 5 ? 3 : 2, 2, overlapLen);
                int doorStart = overlapMinZ + (overlapLen - doorWidth) / 2;
                // Si hay holgura suficiente, alejar la puerta de las esquinas exteriores
                if (overlapLen >= doorWidth + 2)
                {
                    doorStart = Mathf.Clamp(doorStart, overlapMinZ + 1, overlapMaxZ - doorWidth - 1);
                }

                int minX = aIzquierdaB ? (rA.xMax - 1) : (rB.xMax - 1);
                int maxX = aIzquierdaB ? rB.x : rA.x;

                for (int x = minX; x <= maxX; x++)
                {
                    for (int dz = 0; dz < doorWidth; dz++)
                    {
                        int z = doorStart + dz;
                        if (x >= 0 && x < mapWidth && z >= 0 && z < mapDepth)
                        {
                            grid[x, z] = CellType.Door;
                            if (a.layerIndex >= 0 && a.layerIndex < maxLayers && gridPorLayer != null)
                                gridPorLayer[a.layerIndex, x, z] = CellType.Door;
                            if (b.layerIndex >= 0 && b.layerIndex < maxLayers && gridPorLayer != null)
                                gridPorLayer[b.layerIndex, x, z] = CellType.Door;
                        }
                    }
                }

                // Si hay micro-desnivel en la misma capa, trazar rampita suave centrada en la puerta
                if (deltaElevacion >= 0.12f)
                {
                    float zCentro = (doorStart + doorWidth * 0.5f) * cellSize;
                    float xA = (aIzquierdaB ? minX - 0.2f : maxX + 1.2f) * cellSize;
                    float xB = (aIzquierdaB ? maxX + 1.2f : minX - 0.2f) * cellSize;
                    float ancho = doorWidth * cellSize * 0.92f;
                    RegistrarRampita(a, new Vector3(xA, a.floorElevation, zCentro), new Vector3(xB, b.floorElevation, zCentro), ancho);
                }
                return;
            }
        }

        // 2. Revisar si comparten pared horizontal contigua o cercana (A debajo de B, o B debajo de A)
        bool aAbajoB = (rA.yMax <= rB.y && rB.y - rA.yMax <= 4);
        bool bAbajoA = (rB.yMax <= rA.y && rA.y - rB.yMax <= 4);

        if (aAbajoB || bAbajoA)
        {
            int overlapMinX = Mathf.Max(rA.x, rB.x);
            int overlapMaxX = Mathf.Min(rA.xMax, rB.xMax);
            int overlapLen = overlapMaxX - overlapMinX;

            if (overlapLen >= 2)
            {
                // Puerta generosa y ancha (2 a 3 celdas = 8m a 12m)
                int doorWidth = Mathf.Clamp(overlapLen >= 5 ? 3 : 2, 2, overlapLen);
                int doorStart = overlapMinX + (overlapLen - doorWidth) / 2;
                // Si hay holgura suficiente, alejar la puerta de las esquinas exteriores
                if (overlapLen >= doorWidth + 2)
                {
                    doorStart = Mathf.Clamp(doorStart, overlapMinX + 1, overlapMaxX - doorWidth - 1);
                }

                int minZ = aAbajoB ? (rA.yMax - 1) : (rB.yMax - 1);
                int maxZ = aAbajoB ? rB.y : rA.y;

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int dx = 0; dx < doorWidth; dx++)
                    {
                        int x = doorStart + dx;
                        if (x >= 0 && x < mapWidth && z >= 0 && z < mapDepth)
                        {
                            grid[x, z] = CellType.Door;
                            if (a.layerIndex >= 0 && a.layerIndex < maxLayers && gridPorLayer != null)
                                gridPorLayer[a.layerIndex, x, z] = CellType.Door;
                            if (b.layerIndex >= 0 && b.layerIndex < maxLayers && gridPorLayer != null)
                                gridPorLayer[b.layerIndex, x, z] = CellType.Door;
                        }
                    }
                }

                // Si hay micro-desnivel en la misma capa, trazar rampita suave centrada en la puerta
                if (deltaElevacion >= 0.12f)
                {
                    float xCentro = (doorStart + doorWidth * 0.5f) * cellSize;
                    float zA = (aAbajoB ? minZ - 0.2f : maxZ + 1.2f) * cellSize;
                    float zB = (aAbajoB ? maxZ + 1.2f : minZ - 0.2f) * cellSize;
                    float ancho = doorWidth * cellSize * 0.92f;
                    RegistrarRampita(a, new Vector3(xCentro, a.floorElevation, zA), new Vector3(xCentro, b.floorElevation, zB), ancho);
                }
                return;
            }
        }

        // 3. Si no son contiguas, trazar un pasillo directo entre sus centros
        Vector2Int centerA = new Vector2Int(rA.x + rA.width / 2, rA.y + rA.height / 2);
        Vector2Int centerB = new Vector2Int(rB.x + rB.width / 2, rB.y + rB.height / 2);

        if (Random.value > 0.5f)
        {
            CarveCorridor(centerA.x, centerB.x, centerA.y, true);
            CarveCorridor(centerA.y, centerB.y, centerB.x, false);
        }
        else
        {
            CarveCorridor(centerA.y, centerB.y, centerA.x, false);
            CarveCorridor(centerA.x, centerB.x, centerB.y, true);
        }

        if (deltaElevacion >= 0.12f)
        {
            Vector3 pA = new Vector3(centerA.x * cellSize, a.floorElevation, centerA.y * cellSize);
            Vector3 pB = new Vector3(centerB.x * cellSize, b.floorElevation, centerB.y * cellSize);
            Vector3 dir = (pB - pA);
            dir.y = 0f;
            if (dir.sqrMagnitude > 4f)
            {
                Vector3 mitad = (pA + pB) * 0.5f;
                Vector3 rStart = mitad - dir.normalized * cellSize * 1.2f;
                rStart.y = a.floorElevation;
                Vector3 rEnd = mitad + dir.normalized * cellSize * 1.2f;
                rEnd.y = b.floorElevation;
                RegistrarRampita(a, rStart, rEnd, cellSize * 1.8f);
            }
        }
    }

    bool HaySolapamientoDestructivo(RectInt cand, BSPNode excludeNode = null)
    {
        foreach (BSPNode n in leafNodes)
        {
            if (n == excludeNode) continue;
            RectInt r = n.room;
            int xOverlap = Mathf.Max(0, Mathf.Min(cand.xMax, r.xMax) - Mathf.Max(cand.x, r.x));
            int yOverlap = Mathf.Max(0, Mathf.Min(cand.yMax, r.yMax) - Mathf.Max(cand.y, r.y));

            // Solapamiento interior estricto (> 0 en ambos ejes)
            if (xOverlap > 0 && yOverlap > 0)
            {
                return true;
            }
        }
        return false;
    }

    void MarcarHabitacionEnGrilla(RectInt r)
    {
        for (int x = r.x; x < r.xMax; x++)
        {
            for (int z = r.y; z < r.yMax; z++)
            {
                if (x >= 0 && x < mapWidth && z >= 0 && z < mapDepth)
                {
                    grid[x, z] = CellType.Floor;
                }
            }
        }
    }

    List<Vector2Int> ObtenerDireccionesCandidatas(Vector2 dirPrincipal)
    {
        List<Vector2Int> dirs = new List<Vector2Int>();
        if (Mathf.Abs(dirPrincipal.x) >= Mathf.Abs(dirPrincipal.y))
        {
            dirs.Add(new Vector2Int(dirPrincipal.x > 0 ? 1 : -1, 0));
            dirs.Add(new Vector2Int(0, dirPrincipal.y >= 0 ? 1 : -1));
            dirs.Add(new Vector2Int(0, dirPrincipal.y >= 0 ? -1 : 1));
            dirs.Add(new Vector2Int(dirPrincipal.x > 0 ? -1 : 1, 0));
        }
        else
        {
            dirs.Add(new Vector2Int(0, dirPrincipal.y > 0 ? 1 : -1));
            dirs.Add(new Vector2Int(dirPrincipal.x >= 0 ? 1 : -1, 0));
            dirs.Add(new Vector2Int(dirPrincipal.x >= 0 ? -1 : 1, 0));
            dirs.Add(new Vector2Int(0, dirPrincipal.y > 0 ? -1 : 1));
        }
        return dirs;
    }

    /// <summary>
    /// Expande el nivel como una estructura arbórea orgánica (Tree Branching).
    /// El tronco principal conecta Start con End, y desde cualquier cuarto se pueden ramificar
    /// ramas secundarias y sub-ramas con profundidad progresiva, capas 1..N y atajos cruzados opcionales.
    /// </summary>
    void GenerarExpansionArborea(List<BSPNode> tronco, int margen)
    {
        Queue<BSPNode> colaExpansion = new Queue<BSPNode>();

        // Encolar habitaciones del tronco principal como puntos de origen de ramas secundarias
        foreach (var n in tronco)
        {
            if (n != endRoom) colaExpansion.Enqueue(n);
        }

        // Capacidad dinámica de cuartos basada en el tamaño del mapa
        int maxHabitacionesArbol = Mathf.Clamp((mapWidth * mapDepth) / 45, 18, 36);

        while (colaExpansion.Count > 0 && leafNodes.Count < maxHabitacionesArbol)
        {
            BSPNode padre = colaExpansion.Dequeue();
            if (padre == null || padre.branchDepth >= profundidadMaximaArbol) continue;

            // Determinar cuántas ramas hijas pueden brotar de este nodo
            int numHijosDeseados = 0;
            if (padre.isTrunk)
            {
                if (Random.value < probabilidadRamificacion)
                    numHijosDeseados = Random.Range(1, maxHijosPorCuarto + 1);
            }
            else
            {
                // La probabilidad de ramificar disminuye progresivamente con la profundidad de la rama
                float probProf = probabilidadRamificacion / (1f + (padre.branchDepth - 1) * 0.45f);
                if (Random.value < probProf)
                    numHijosDeseados = (padre.structureType == RoomStructureType.Hall) ? Random.Range(1, maxHijosPorCuarto + 1) : 1;
            }

            if (numHijosDeseados <= 0) continue;

            // Barajar direcciones ortogonales para una ramificación orgánica
            Vector2Int[] dirs = new Vector2Int[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            for (int d = 0; d < dirs.Length; d++)
            {
                int r = Random.Range(d, dirs.Length);
                Vector2Int tmp = dirs[d]; dirs[d] = dirs[r]; dirs[r] = tmp;
            }

            int hijosColocados = 0;
            foreach (Vector2Int dir in dirs)
            {
                if (hijosColocados >= numHijosDeseados || leafNodes.Count >= maxHabitacionesArbol) break;

                BSPNode hijo = IntentarColocarHabitacionHija(padre, dir, margen, false);
                if (hijo != null)
                {
                    padre.treeChildren.Add(hijo);
                    colaExpansion.Enqueue(hijo);
                    hijosColocados++;
                }
            }
        }

        // Conexiones cruzadas opcionales (Loops / Atajos) entre ramas adyacentes para enriquecer la navegación
        if (probabilidadConexionesCruzadas > 0.01f && leafNodes.Count >= 4)
        {
            for (int i = 0; i < leafNodes.Count; i++)
            {
                BSPNode nodeA = leafNodes[i];
                if (nodeA == startRoom || nodeA == endRoom) continue;

                for (int j = i + 1; j < leafNodes.Count; j++)
                {
                    BSPNode nodeB = leafNodes[j];
                    if (nodeB == startRoom || nodeB == endRoom) continue;

                    // Si ya están directamente conectadas en el árbol, saltar
                    if (nodeA.treeChildren.Contains(nodeB) || nodeB.treeChildren.Contains(nodeA)) continue;

                    // Comprobar si comparten frontera contigua o cercana
                    RectInt rA = nodeA.room;
                    RectInt rB = nodeB.room;
                    bool adyacenteX = (Mathf.Abs(rA.xMax - rB.x) <= 3 || Mathf.Abs(rB.xMax - rA.x) <= 3) 
                                      && (Mathf.Min(rA.yMax, rB.yMax) - Mathf.Max(rA.y, rB.y) >= 2);
                    bool adyacenteY = (Mathf.Abs(rA.yMax - rB.y) <= 3 || Mathf.Abs(rB.yMax - rA.y) <= 3) 
                                      && (Mathf.Min(rA.xMax, rB.xMax) - Mathf.Max(rA.x, rB.x) >= 2);

                    if ((adyacenteX || adyacenteY) && Random.value < probabilidadConexionesCruzadas)
                    {
                        ConectarHabitacionesGarantizado(nodeA, nodeB);
                    }
                }
            }
        }
    }

    void ConstruirParedesPerimetrales()
    {
        // 1. Toda celda vacía con vecino ortogonal transitable, o esquina diagonal de suelo, se convierte en pared
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapDepth; z++)
            {
                if (grid[x, z] == CellType.Empty)
                {
                    bool tieneVecinoWalkable = false;
                    for (int dx = -1; dx <= 1 && !tieneVecinoWalkable; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;

                            int nx = x + dx;
                            int nz = z + dz;
                            if (nx >= 0 && nx < mapWidth && nz >= 0 && nz < mapDepth)
                            {
                                CellType ct = grid[nx, nz];
                                bool esOrtogonal = (dx == 0 || dz == 0);

                                if (esOrtogonal)
                                {
                                    // Vecino ortogonal transitable: siempre requiere pared perimetral
                                    if (ct == CellType.Floor || ct == CellType.Door || ct == CellType.Bridge || ct == CellType.Chasm)
                                    {
                                        tieneVecinoWalkable = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    // Vecino diagonal: solo si es Floor para sellar esquinas exteriores de cuartos.
                                    // NUNCA generar paredes en diagonales de celdas Door para no obstruir ni estrechar la entrada.
                                    if (ct == CellType.Floor)
                                    {
                                        tieneVecinoWalkable = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (tieneVecinoWalkable)
                    {
                        grid[x, z] = CellType.Wall;
                    }
                }
            }
        }

        // 2. Bordes absolutos del mapa cerrados como pared
        for (int x = 0; x < mapWidth; x++)
        {
            grid[x, 0] = CellType.Wall;
            grid[x, mapDepth - 1] = CellType.Wall;
        }
        for (int z = 0; z < mapDepth; z++)
        {
            grid[0, z] = CellType.Wall;
            grid[mapWidth - 1, z] = CellType.Wall;
        }
    }

    /// <summary>
    /// Procedimiento tradicional de respaldo para Start Room.
    /// </summary>
    void EscogerStartRoomAleatorio()
    {
        int startW = Mathf.Clamp(minRoomSize + 1, 4, 6);
        int startD = Mathf.Clamp(minRoomSize + 1, 4, 6);
        int margen = 3;
        int maxStartX = Mathf.Max(margen + 1, mapWidth - startW - margen);
        int maxStartZ = Mathf.Max(margen + 1, mapDepth - startD - margen);

        int startX = Random.Range(margen, maxStartX);
        int startZ = Random.Range(margen, maxStartZ);

        startRoom = new BSPNode
        {
            space = new RectInt(startX - 1, startZ - 1, startW + 2, startD + 2),
            room = new RectInt(startX, startZ, startW, startD),
            layerIndex = 0,
            floorElevation = 0f,
            roomHeight = Mathf.Clamp(minWallHeight + 1.5f, 3.5f, maxLevelHeight),
            archetype = RoomArchetype.SafeRoom,
            structureType = RoomStructureType.FinalRoom
        };

        MarcarHabitacionEnGrilla(startRoom.room);
        RepositionPlayerToStartRoom();
    }

    void SelectSpecialRooms()
    {
        if (leafNodes.Count < 2) return;

        // Si startRoom y endRoom ya fueron pre-fijados al inicio, preservarlos directamente
        if (startRoom != null && endRoom != null)
        {
            startRoom.archetype = RoomArchetype.SafeRoom;
            endRoom.archetype = RoomArchetype.SafeRoom;
            return;
        }

        List<BSPNode> deadEnds = new List<BSPNode>();
        List<BSPNode> others = new List<BSPNode>();
        foreach (var node in leafNodes)
        {
            if (node == startRoom) continue;
            if (GetEntranceCount(node) == 1) deadEnds.Add(node);
            else others.Add(node);
        }

        // Shuffle both
        for (int i = 0; i < deadEnds.Count; i++) {
            BSPNode temp = deadEnds[i]; int rIdx = Random.Range(i, deadEnds.Count); deadEnds[i] = deadEnds[rIdx]; deadEnds[rIdx] = temp;
        }
        for (int i = 0; i < others.Count; i++) {
            BSPNode temp = others[i]; int rIdx = Random.Range(i, others.Count); others[i] = others[rIdx]; others[rIdx] = temp;
        }

        if (startRoom == null)
        {
            if (deadEnds.Count >= 2) {
                startRoom = deadEnds[0];
                endRoom = deadEnds[1];
            } else if (deadEnds.Count == 1) {
                startRoom = deadEnds[0];
                endRoom = others[0];
            } else {
                startRoom = others[0];
                endRoom = others.Count > 1 ? others[1] : others[0];
            }
        }
        else
        {
            if (endRoom == null)
            {
                endRoom = deadEnds.Count > 0 ? deadEnds[0] : (others.Count > 0 ? others[0] : leafNodes[leafNodes.Count - 1]);
            }
        }
        
        startRoom.archetype = RoomArchetype.SafeRoom;
        if (endRoom != null) endRoom.archetype = RoomArchetype.SafeRoom;

        foreach (BSPNode node in leafNodes) {
            if (node.archetype == RoomArchetype.SafeRoom) {
                ForceSafeRoomGeometry(node);
            }
        }
    }

    void ForceSafeRoomGeometry(BSPNode node)
    {
        // Se eliminó la reescritura de geometría para evitar islas.
    }

    void SpawnElements()
    {
        if (leafNodes.Count < 2) return;

        // Start y End room ya fueron calculados en SelectSpecialRooms()
        List<RoomData> allRoomData = new List<RoomData>();

        // 1. Generar los Triggers de RoomData para cada habitación
        for (int i = 0; i < leafNodes.Count; i++)
        {
            RectInt r = leafNodes[i].room;
            RectInt s = leafNodes[i].space;
            
            GameObject roomTrigger = new GameObject("RoomTrigger_" + i);
            roomTrigger.transform.SetParent(levelParent.transform);
            
            BoxCollider box = roomTrigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            
            // Hacemos que el Trigger ocupe todo el Espacio BSP, centrado verticalmente según la elevación del suelo
            Vector3 spaceCenter = new Vector3((s.x + s.width/2f) * cellSize, leafNodes[i].floorElevation + leafNodes[i].roomHeight / 2f, (s.y + s.height/2f) * cellSize);
            roomTrigger.transform.position = spaceCenter;
            box.size = new Vector3(s.width * cellSize + 1.5f, leafNodes[i].roomHeight + 2f, s.height * cellSize + 1.5f);
            
            // Añadir Rigidbody cinemático al trigger para garantizar que CharacterController dispare OnTriggerEnter/Exit en Unity
            Rigidbody rbTrigger = roomTrigger.AddComponent<Rigidbody>();
            rbTrigger.isKinematic = true;
            rbTrigger.useGravity = false;
            
            RoomData rd = roomTrigger.AddComponent<RoomData>();
            rd.areaSize = r.width * r.height;
            rd.archetype = leafNodes[i].archetype;
            rd.archetypeIntensity = leafNodes[i].archetypeIntensity;
            rd.layerIndex = leafNodes[i].layerIndex;
            rd.floorElevation = leafNodes[i].floorElevation;
            rd.ConfigureMovementBounds(new Bounds(
                new Vector3((r.x + (r.width - 1) * 0.5f) * cellSize, leafNodes[i].floorElevation + leafNodes[i].roomHeight * 0.5f,
                    (r.y + (r.height - 1) * 0.5f) * cellSize),
                new Vector3(Mathf.Max(cellSize, (r.width - 2) * cellSize), leafNodes[i].roomHeight,
                    Mathf.Max(cellSize, (r.height - 2) * cellSize))));
            RoomEntityManager population = roomTrigger.AddComponent<RoomEntityManager>();
            population.possibleEnemies = enemyPrefabs;
            population.maxEnemiesInRoom = enemiesPerRoom;
            population.allowSpawn = roomEntitiesEnabled && currentLevel > 0 && leafNodes[i] != startRoom
                && leafNodes[i] != endRoom && rd.archetype != RoomArchetype.SafeRoom;
            
            // Asignar y emparentar el contenedor de geometría para culling
            rd.geometryContainer = leafNodes[i].geometryContainer;
            if (rd.geometryContainer != null) {
                rd.geometryContainer.transform.SetParent(roomTrigger.transform);
            }
            
            rd.entranceCount = GetEntranceCount(leafNodes[i]);
            
            if (leafNodes[i] == endRoom)
            {
                // Instanciar barreras en las puertas con validación de límites
                for(int x = r.x; x < r.x + r.width; x++) {
                    if (x >= 0 && x < mapWidth && r.y >= 0 && r.y < mapDepth && grid[x, r.y] == CellType.Door) InstanciarBarrera(x, r.y, leafNodes[i].dynamicContainer != null ? leafNodes[i].dynamicContainer.transform : levelParent.transform);
                    if (x >= 0 && x < mapWidth && r.y + r.height - 1 >= 0 && r.y + r.height - 1 < mapDepth && grid[x, r.y + r.height - 1] == CellType.Door) InstanciarBarrera(x, r.y + r.height - 1, leafNodes[i].dynamicContainer != null ? leafNodes[i].dynamicContainer.transform : levelParent.transform);
                }
                for(int z = r.y; z < r.y + r.height; z++) {
                    if (z >= 0 && z < mapDepth && r.x >= 0 && r.x < mapWidth && grid[r.x, z] == CellType.Door) InstanciarBarrera(r.x, z, leafNodes[i].dynamicContainer != null ? leafNodes[i].dynamicContainer.transform : levelParent.transform);
                    if (z >= 0 && z < mapDepth && r.x + r.width - 1 >= 0 && r.x + r.width - 1 < mapWidth && grid[r.x + r.width - 1, z] == CellType.Door) InstanciarBarrera(r.x + r.width - 1, z, leafNodes[i].dynamicContainer != null ? leafNodes[i].dynamicContainer.transform : levelParent.transform);
                }
            }

            if (leafNodes[i] == startRoom || leafNodes[i] == endRoom)
            {
                rd.isAlwaysRendered = true;
            }

            // NOTA: Eliminamos la lógica de convertir automáticamente todos los caminos muertos en cuartos seguros.
            // Ahora, los dead ends normales seguirán siendo terroríficos.

            // La decisión de si hay luz ahora usa la capa de Perlin (noiseIllumination)
            // Zonas con ruido alto (> lightDensity) serán oscuras.
            bool hasLight = (leafNodes[i].noiseIllumination < lightDensity);

            if (leafNodes[i].archetype == RoomArchetype.SuperIlluminated || leafNodes[i].archetype == RoomArchetype.SafeRoom) hasLight = true;
            else if (leafNodes[i].archetype == RoomArchetype.Dark) hasLight = false;

            if (hasLight)
            {
                // Intensidad basada en la Curva de Iluminación configurada en el Inspector
                rd.illuminationLevel = ProceduralLevelGenerator.Instance.perlinLayers.illuminationCurve.Evaluate(leafNodes[i].noiseIllumination);
                
                if (leafNodes[i].archetype == RoomArchetype.SuperIlluminated) rd.illuminationLevel = 1.0f;
                else if (leafNodes[i].archetype == RoomArchetype.SafeRoom) rd.illuminationLevel = 0.2f; // Luz tenue (sistema lo ve tenue)
                
                // Instanciar luz física en el cuarto (capa dinámica)
                GameObject roomLightObj = new GameObject("RoomLight_" + i);
                roomLightObj.transform.position = spaceCenter + Vector3.up * (leafNodes[i].roomHeight * 0.40f); 
                roomLightObj.transform.SetParent(leafNodes[i].dynamicContainer != null ? leafNodes[i].dynamicContainer.transform : levelParent.transform);
                
                Light pointLight = roomLightObj.AddComponent<Light>();
                pointLight.type = LightType.Point;
                
                // Asegurar que la luz alcance el suelo incluso en techos altos
                float widthRange = s.width * cellSize * 0.8f;
                float heightRange = leafNodes[i].roomHeight * 1.5f;
                pointLight.range = Mathf.Max(widthRange, heightRange); 
                
                pointLight.intensity = rd.illuminationLevel * 8f;

                // Compensar la atenuación si el techo es muy alto
                if (leafNodes[i].roomHeight > 5f)
                {
                    pointLight.intensity *= 1.5f;
                }
                
                // --- NUEVA LUZ EN CONO (SPOTLIGHT) ---
                GameObject spotLightObj = new GameObject("SpotLight");
                spotLightObj.transform.SetParent(roomLightObj.transform);
                spotLightObj.transform.localPosition = Vector3.zero;
                spotLightObj.transform.localRotation = Quaternion.Euler(90, 0, 0); // Apuntando directo al suelo
                
                Light spotLight = spotLightObj.AddComponent<Light>();
                spotLight.type = LightType.Spot;
                spotLight.spotAngle = 75f; // Cono amplio para iluminar bien el suelo
                spotLight.range = heightRange * 1.5f; // Mucha más distancia de caída para techos altos
                spotLight.intensity = pointLight.intensity * 2.5f; // Los SpotLights necesitan más intensidad

                if (leafNodes[i].archetype == RoomArchetype.SuperIlluminated) 
                {
                    float boost = (1f + leafNodes[i].archetypeIntensity * 2f);
                    pointLight.intensity *= boost; 
                    spotLight.intensity *= boost;
                }
                
                if (leafNodes[i].archetype == RoomArchetype.SafeRoom)
                {
                    // Color aleatorio, vivaz y tranquilizante (Tonos Fríos/Naturales: Verde a Azul a Morado)
                    Color safeColor = Color.HSVToRGB(Random.Range(0.3f, 0.85f), 0.8f, 1f);
                    pointLight.color = safeColor;
                    spotLight.color = safeColor;
                    pointLight.intensity = 5f; 
                    spotLight.intensity = 12f;
                }
                // Si la iluminación es muy baja, la luz es defectuosa y parpadea (excepto cuarto seguro)
                else if (rd.illuminationLevel < 0.4f)
                {
                    FlickeringLight fl = roomLightObj.AddComponent<FlickeringLight>();
                    fl.isDefective = true;
                    Color defectColor = new Color(0.9f, 0.9f, 0.8f);
                    pointLight.color = defectColor;
                    spotLight.color = defectColor;
                }
                else
                {
                    pointLight.color = Color.white;
                    spotLight.color = Color.white;
                }
            }
            else
            {
                // Cuarto sumido en la oscuridad total
                rd.illuminationLevel = 0f;
            }
            
            allRoomData.Add(rd);
        }

        // Conectar adyacencias considerando grosor de paredes entre cuartos consecutivos y conexiones verticales
        int maxDistAdyacencia = Mathf.Max(grosorParedMaximo + 4, 6);
        for (int i = 0; i < allRoomData.Count; i++)
        {
            for (int j = i + 1; j < allRoomData.Count; j++)
            {
                RectInt a = leafNodes[i].room;
                RectInt b = leafNodes[j].room;

                // Revisar si están separados por pared o pasillo cercano
                bool cercaX = (Mathf.Abs(a.xMax - b.xMin) <= maxDistAdyacencia || Mathf.Abs(a.xMin - b.xMax) <= maxDistAdyacencia)
                              && (Mathf.Max(a.yMin, b.yMin) < Mathf.Min(a.yMax, b.yMax) + maxDistAdyacencia);
                bool cercaY = (Mathf.Abs(a.yMax - b.yMin) <= maxDistAdyacencia || Mathf.Abs(a.yMin - b.yMax) <= maxDistAdyacencia)
                              && (Mathf.Max(a.xMin, b.xMin) < Mathf.Min(a.xMax, b.xMax) + maxDistAdyacencia);
                bool solapa = a.xMin < b.xMax + maxDistAdyacencia && a.xMax > b.xMin - maxDistAdyacencia
                           && a.yMin < b.yMax + maxDistAdyacencia && a.yMax > b.yMin - maxDistAdyacencia;

                if (cercaX || cercaY || solapa)
                {
                    allRoomData[i].adjacentRooms.Add(allRoomData[j]);
                    allRoomData[j].adjacentRooms.Add(allRoomData[i]);
                }
            }
        }

        // Conectar adyacencias explícitas para cuartos conectados por rampas verticales
        if (conexionesVerticales != null)
        {
            foreach (var cv in conexionesVerticales)
            {
                if (cv.cuartoInferior == null || cv.cuartoSuperior == null) continue;
                int idxBajo = leafNodes.IndexOf(cv.cuartoInferior);
                int idxAlto = leafNodes.IndexOf(cv.cuartoSuperior);
                if (idxBajo >= 0 && idxAlto >= 0 && idxBajo < allRoomData.Count && idxAlto < allRoomData.Count)
                {
                    allRoomData[idxBajo].isAlwaysRendered = true;
                    allRoomData[idxAlto].isAlwaysRendered = true;
                    if (!allRoomData[idxBajo].adjacentRooms.Contains(allRoomData[idxAlto]))
                        allRoomData[idxBajo].adjacentRooms.Add(allRoomData[idxAlto]);
                    if (!allRoomData[idxAlto].adjacentRooms.Contains(allRoomData[idxBajo]))
                        allRoomData[idxAlto].adjacentRooms.Add(allRoomData[idxBajo]);
                }
            }
        }

        MarkRoomSpecial(startRoom, "START ZONE", startRoomColor);
        MarkRoomSpecial(endRoom, "SAFE ZONE", safeZoneColor);
        
        SpawnScatteredObjects();
        SpawnKeynotes();
    }

    void SpawnScatteredObjects()
    {
        if (objectPrefabs == null || objectPrefabs.Length == 0) return;

        // Filtrar habitaciones válidas (que no sean vacías)
        System.Collections.Generic.List<BSPNode> validRooms = new System.Collections.Generic.List<BSPNode>();
        float totalClutterWeight = 0f;
        foreach (var node in leafNodes)
        {
            if (node.archetype != RoomArchetype.Empty && node.room.width > 2 && node.room.height > 2)
            {
                validRooms.Add(node);
                totalClutterWeight += ProceduralLevelGenerator.Instance.perlinLayers.clutterCurve.Evaluate(node.noiseClutter); // Sumar peso según curva
            }
        }

        if (validRooms.Count == 0 || totalClutterWeight <= 0f) return;

        int totalToSpawn = Random.Range(minTotalObjects, maxTotalObjects + 1);

        foreach (BSPNode randomNode in validRooms)
        {
            // Distribuir la cantidad de objetos proporcionalmente al peso de la curva de Aglomeración
            float weight = ProceduralLevelGenerator.Instance.perlinLayers.clutterCurve.Evaluate(randomNode.noiseClutter);
            int spawnCount = Mathf.RoundToInt(totalToSpawn * (weight / totalClutterWeight));

            for (int i = 0; i < spawnCount; i++)
            {
                RectInt r = randomNode.room;

                // Elegir celda aleatoria dentro de la habitación
                int rx = r.x + Random.Range(1, r.width - 1);
                int rz = r.y + Random.Range(1, r.height - 1);

            // Evitar generar objetos pegados a las puertas (conexiones)
            bool isNearDoor = false;
            if (rx == r.x + 1 && r.x >= 0 && grid[r.x, rz] == CellType.Floor) isNearDoor = true;
            if (rx == r.x + r.width - 2 && r.x + r.width - 1 < mapWidth && grid[r.x + r.width - 1, rz] == CellType.Floor) isNearDoor = true;
            if (rz == r.y + 1 && r.y >= 0 && grid[rx, r.y] == CellType.Floor) isNearDoor = true;
            if (rz == r.y + r.height - 2 && r.y + r.height - 1 < mapDepth && grid[rx, r.y + r.height - 1] == CellType.Floor) isNearDoor = true;

            if (isNearDoor) continue; // Si bloquea la entrada, abortamos este spawn

            // Le damos una pequeña variación para que no estén perfectamente alineados a la grilla
            float spawnX = (rx + Random.Range(-0.3f, 0.3f)) * cellSize;
            float spawnZ = (rz + Random.Range(-0.3f, 0.3f)) * cellSize;
            
            // Usamos y=0 para que queden pegados al suelo
            Vector3 spawnPos = new Vector3(spawnX, 0f, spawnZ);

            // SAFE WARD: Evitar generar objetos en un radio de 3 unidades del Spawn y la Salida
            if (startRoom != null)
            {
                RectInt sr = startRoom.room;
                Vector3 startCenter = new Vector3((sr.x + (sr.width - 1) / 2f) * cellSize, 0f, (sr.y + (sr.height - 1) / 2f) * cellSize);
                if (Vector3.Distance(spawnPos, startCenter) <= 3.0f) continue;
            }
            if (endRoom != null)
            {
                RectInt er = endRoom.room;
                Vector3 endCenter = new Vector3((er.x + (er.width - 1) / 2f) * cellSize, 0f, (er.y + (er.height - 1) / 2f) * cellSize);
                if (Vector3.Distance(spawnPos, endCenter) <= 3.0f) continue;
            }

            // Elegir un prefab aleatorio
            GameObject selectedPrefab = objectPrefabs[Random.Range(0, objectPrefabs.Length)];

            if (selectedPrefab != null)
            {
                // Emparentar al dynamicContainer
                Transform parentTransform = randomNode.dynamicContainer != null ? randomNode.dynamicContainer.transform : levelParent.transform;
                GameObject inst = Instantiate(selectedPrefab, spawnPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                inst.transform.SetParent(parentTransform);

                // Variación de escala aleatoria configurable
                float randomScale = Random.Range(minObjectScale, maxObjectScale);
                inst.transform.localScale = inst.transform.localScale * randomScale;
            }
            }
        }
    }

    void SpawnKeynotes()
    {
        if (keynotePrefab == null) return;
        
        int numKeynotes = numberOfKeynotes * 2; // Se genera el doble de la misión por si acaso
        int spawned = 0;
        
        // Barajar cuartos
        List<BSPNode> shuffledNodes = new List<BSPNode>(leafNodes);
        for(int i = 0; i < shuffledNodes.Count; i++) {
            BSPNode temp = shuffledNodes[i]; int rIdx = Random.Range(i, shuffledNodes.Count);
            shuffledNodes[i] = shuffledNodes[rIdx]; shuffledNodes[rIdx] = temp;
        }

        foreach(var node in shuffledNodes)
        {
            if (spawned >= numKeynotes) break;
            if (node == startRoom || node == endRoom) continue; // No misiones en zonas seguras

            RectInt r = node.room;
            List<KeyValuePair<Vector2Int, Vector3>> possibleWalls = new List<KeyValuePair<Vector2Int, Vector3>>();
            
            // Buscar celdas de piso adyacentes a paredes sólidas
            int startX = Mathf.Max(0, r.x);
            int endX = Mathf.Min(mapWidth, r.x + r.width);
            int startZ = Mathf.Max(0, r.y);
            int endZ = Mathf.Min(mapDepth, r.y + r.height);

            for (int x = startX; x < endX; x++) {
                for (int z = startZ; z < endZ; z++) {
                    if (grid[x, z] == CellType.Floor) {
                        if (x - 1 >= 0 && grid[x - 1, z] == CellType.Wall) 
                            possibleWalls.Add(new KeyValuePair<Vector2Int, Vector3>(new Vector2Int(x, z), Vector3.right)); // Pared izquierda, mirar a la derecha
                        else if (x + 1 < mapWidth && grid[x + 1, z] == CellType.Wall) 
                            possibleWalls.Add(new KeyValuePair<Vector2Int, Vector3>(new Vector2Int(x, z), Vector3.left));
                        else if (z - 1 >= 0 && grid[x, z - 1] == CellType.Wall) 
                            possibleWalls.Add(new KeyValuePair<Vector2Int, Vector3>(new Vector2Int(x, z), Vector3.forward));
                        else if (z + 1 < mapDepth && grid[x, z + 1] == CellType.Wall) 
                            possibleWalls.Add(new KeyValuePair<Vector2Int, Vector3>(new Vector2Int(x, z), Vector3.back));
                    }
                }
            }

            if (possibleWalls.Count > 0)
            {
                // Elegir una pared aleatoria
                var chosen = possibleWalls[Random.Range(0, possibleWalls.Count)];
                Vector2Int cell = chosen.Key;
                Vector3 normal = chosen.Value; // Hacia adentro del cuarto

                // El centro de la celda de piso
                Vector3 cellCenter = new Vector3(cell.x * cellSize, 1.5f, cell.y * cellSize);
                
                // Mover hacia la pared (en dirección opuesta a la normal). 
                // Le restamos casi la mitad de cellSize para que quede pegado a la pared
                Vector3 spawnPos = cellCenter - normal * (cellSize * 0.48f);
                Quaternion rot = Quaternion.LookRotation(normal);

                GameObject keynote = Instantiate(keynotePrefab, spawnPos, rot);
                keynote.transform.SetParent(node.dynamicContainer != null ? node.dynamicContainer.transform : levelParent.transform);
                
                spawned++;
            }
        }
    }

    void MarkRoomSpecial(BSPNode node, string label, Color col)
    {
        RectInt r = node.room;
        // El verdadero centro geométrico de los cuartos considera que los índices van de 0 a (size - 1)
        Vector3 center = new Vector3((r.x + (r.width - 1) / 2f) * cellSize, 0.1f, (r.y + (r.height - 1) / 2f) * cellSize);
        
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.transform.position = center;
        marker.transform.localScale = new Vector3(2f, 0.05f, 2f);
        marker.GetComponent<Renderer>().material.color = col;
        marker.name = label;
        
        Transform chunkParent = node.dynamicContainer != null ? node.dynamicContainer.transform : levelParent.transform;
        marker.transform.SetParent(chunkParent);
        
        if (label == "START ZONE")
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Vector3 spawnPos = center + Vector3.up * 0.8f;
                if (UnityEngine.AI.NavMesh.SamplePosition(center, out UnityEngine.AI.NavMeshHit hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    spawnPos = hit.position + Vector3.up * 0.6f;
                }

                SimplePlayerController spc = player.GetComponent<SimplePlayerController>();
                if (spc != null)
                {
                    spc.TeleportTo(spawnPos);
                }
                else
                {
                    CharacterController cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.transform.position = spawnPos;
                    Physics.SyncTransforms();
                    if (cc != null) cc.enabled = true;
                }
            }
        }
        else if (label == "SAFE ZONE")
        {
            BoxCollider trigger = marker.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2f, 10f, 2f);
            marker.AddComponent<EndZoneTrigger>();
        }
    }

    void InstanciarBarrera(int x, int z, Transform parent)
    {
        Vector3 pos = new Vector3(x * cellSize, minWallHeight / 2f, z * cellSize);
        GameObject barrera = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrera.transform.position = pos;
        barrera.transform.localScale = new Vector3(cellSize, minWallHeight, cellSize);
        barrera.transform.SetParent(parent);
        barrera.GetComponent<Renderer>().material.color = Color.red; // Barrera roja para que se note bloqueada
        barrera.layer = 7; // Asignar capa 'Wall'
        
        if (ControladorNivel.Instancia != null)
        {
            ControladorNivel.Instancia.barrerasSalida.Add(barrera);
        }
    }

    void CreatePlaceholder(string name, Vector3 pos, Color color, Vector3 scale, Transform parent)
    {
        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.name = "Placeholder_" + name;
        placeholder.transform.position = pos;
        placeholder.transform.localScale = scale;
        placeholder.GetComponent<Renderer>().material.color = color;
        placeholder.transform.SetParent(parent);
    }

    /// <summary>
    /// Construye una grilla 3D segregada por capas verticales (layers).
    /// Permite que el mapa/minimapa renderice de forma limpia y exclusiva las habitaciones y paredes del piso actual.
    /// </summary>
    public void BuildGridPorLayer()
    {
        int numLayers = Mathf.Max(1, maxLayers);
        gridPorLayer = new CellType[numLayers, mapWidth, mapDepth];

        for (int l = 0; l < numLayers; l++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                for (int z = 0; z < mapDepth; z++)
                {
                    gridPorLayer[l, x, z] = CellType.Empty;
                }
            }
        }

        if (leafNodes != null)
        {
            foreach (var node in leafNodes)
            {
                if (node == null) continue;
                int l = Mathf.Clamp(node.layerIndex, 0, numLayers - 1);

                List<RectInt> cuartos = new List<RectInt>();
                if (node.subRooms != null && node.subRooms.Count > 0)
                {
                    foreach (var sub in node.subRooms) cuartos.Add(sub.room);
                }
                else
                {
                    cuartos.Add(node.room);
                }

                foreach (var r in cuartos)
                {
                    // 1. Marcar suelo del cuarto en este layer
                    for (int x = r.x; x < r.xMax; x++)
                    {
                        for (int z = r.y; z < r.yMax; z++)
                        {
                            if (x >= 0 && x < mapWidth && z >= 0 && z < mapDepth)
                            {
                                CellType globalCell = grid[x, z];
                                gridPorLayer[l, x, z] = (globalCell == CellType.Empty) ? CellType.Floor : globalCell;
                            }
                        }
                    }

                    // 2. Marcar paredes perimetrales de la habitación en este layer respetando puertas y puentes
                    for (int x = r.x - 1; x <= r.xMax; x++)
                    {
                        if (x < 0 || x >= mapWidth) continue;
                        if (r.y - 1 >= 0 && gridPorLayer[l, x, r.y - 1] == CellType.Empty)
                        {
                            CellType g = grid[x, r.y - 1];
                            gridPorLayer[l, x, r.y - 1] = (g == CellType.Door || g == CellType.Bridge) ? g : CellType.Wall;
                        }
                        if (r.yMax < mapDepth && gridPorLayer[l, x, r.yMax] == CellType.Empty)
                        {
                            CellType g = grid[x, r.yMax];
                            gridPorLayer[l, x, r.yMax] = (g == CellType.Door || g == CellType.Bridge) ? g : CellType.Wall;
                        }
                    }
                    for (int z = r.y - 1; z <= r.yMax; z++)
                    {
                        if (z < 0 || z >= mapDepth) continue;
                        if (r.x - 1 >= 0 && gridPorLayer[l, r.x - 1, z] == CellType.Empty)
                        {
                            CellType g = grid[r.x - 1, z];
                            gridPorLayer[l, r.x - 1, z] = (g == CellType.Door || g == CellType.Bridge) ? g : CellType.Wall;
                        }
                        if (r.xMax < mapWidth && gridPorLayer[l, r.xMax, z] == CellType.Empty)
                        {
                            CellType g = grid[r.xMax, z];
                            gridPorLayer[l, r.xMax, z] = (g == CellType.Door || g == CellType.Bridge) ? g : CellType.Wall;
                        }
                    }
                }
            }
        }

        // 3. Registrar conexiones verticales (rampas) como puente en ambas capas para que el mapa muestre la salida/llegada
        if (conexionesVerticales != null)
        {
            foreach (var cv in conexionesVerticales)
            {
                if (cv.cuartoInferior == null || cv.cuartoSuperior == null) continue;
                int lInf = Mathf.Clamp(cv.cuartoInferior.layerIndex, 0, numLayers - 1);
                int lSup = Mathf.Clamp(cv.cuartoSuperior.layerIndex, 0, numLayers - 1);

                for (int x = cv.zonaEntrada.x; x < cv.zonaEntrada.xMax; x++)
                {
                    for (int z = cv.zonaEntrada.y; z < cv.zonaEntrada.yMax; z++)
                    {
                        if (x >= 0 && x < mapWidth && z >= 0 && z < mapDepth)
                        {
                            gridPorLayer[lInf, x, z] = CellType.Bridge;
                            gridPorLayer[lSup, x, z] = CellType.Bridge;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Devuelve el layer actual en el que se encuentra el jugador de forma 100% precisa.
    /// Utiliza posición espacial exacta (coordenadas X, Z y elevación Y), comprobación contra
    /// las habitaciones registradas y cálculo vertical robusto para rampas y transiciones.
    /// </summary>
    public int GetPlayerCurrentLayer(Vector3? optPlayerPos = null)
    {
        Vector3 playerPos = Vector3.zero;
        bool hasPos = false;

        if (optPlayerPos.HasValue)
        {
            playerPos = optPlayerPos.Value;
            hasPos = true;
        }
        else
        {
            AnxietyManager am = Object.FindAnyObjectByType<AnxietyManager>();
            if (am != null)
            {
                playerPos = am.transform.position;
                hasPos = true;
            }
            else
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null)
                {
                    playerPos = playerGo.transform.position;
                    hasPos = true;
                }
            }
        }

        if (!hasPos) return 0;

        int numLayers = Mathf.Max(1, maxLayers);
        float hCapa = Mathf.Min(layerHeight, maxLevelHeight / numLayers);

        // 1. Detección espacial exacta en las habitaciones (LeafNodes y SubRooms)
        if (leafNodes != null && leafNodes.Count > 0)
        {
            float gridX = playerPos.x / cellSize;
            float gridZ = playerPos.z / cellSize;

            for (int i = 0; i < leafNodes.Count; i++)
            {
                var node = leafNodes[i];
                if (node == null) continue;

                // Comprobar elevación vertical del nodo (con holgura para techos y desniveles)
                float minElevation = node.floorElevation - 1.2f;
                float maxElevation = node.floorElevation + node.roomHeight + 1.8f;
                if (playerPos.y < minElevation || playerPos.y > maxElevation) continue;

                // Comprobar si el punto (X, Z) cae dentro de la habitación o sus sub-cuartos
                bool inside = false;
                if (node.subRooms != null && node.subRooms.Count > 0)
                {
                    foreach (var sub in node.subRooms)
                    {
                        if (gridX >= sub.room.x - 0.5f && gridX <= sub.room.xMax + 0.5f &&
                            gridZ >= sub.room.y - 0.5f && gridZ <= sub.room.yMax + 0.5f)
                        {
                            inside = true;
                            break;
                        }
                    }
                }
                else
                {
                    if (gridX >= node.room.x - 0.5f && gridX <= node.room.xMax + 0.5f &&
                        gridZ >= node.room.y - 0.5f && gridZ <= node.room.yMax + 0.5f)
                    {
                        inside = true;
                    }
                }

                if (inside)
                {
                    return Mathf.Clamp(node.layerIndex, 0, numLayers - 1);
                }
            }
        }

        // 2. Si está en una rampa o transición entre capas, calcular directamente por elevación Y
        int layerCalculado = Mathf.Clamp(Mathf.FloorToInt((playerPos.y + (hCapa * 0.45f)) / Mathf.Max(1f, hCapa)), 0, numLayers - 1);
        return layerCalculado;
    }

    public CellType GetCellTypeAt(int x, int z, int layer)
    {
        if (gridPorLayer == null)
            return (grid != null && x >= 0 && x < mapWidth && z >= 0 && z < mapDepth) ? grid[x, z] : CellType.Empty;

        int l = Mathf.Clamp(layer, 0, gridPorLayer.GetLength(0) - 1);
        if (x < 0 || x >= mapWidth || z < 0 || z >= mapDepth) return CellType.Empty;
        return gridPorLayer[l, x, z];
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugZones) return;

        RoomData[] rooms = FindObjectsOfType<RoomData>();
        foreach (RoomData rd in rooms)
        {
            BoxCollider box = rd.GetComponent<BoxCollider>();
            if (box != null)
            {
                Color gizmoColor = Color.gray;

                switch (rd.archetype)
                {
                    case RoomArchetype.Normal:
                        gizmoColor = rd.illuminationLevel > 0f ? Color.yellow : Color.gray;
                        break;
                    case RoomArchetype.Claustrophobic: gizmoColor = Color.red; break;
                    case RoomArchetype.Batophobic: gizmoColor = Color.magenta; break;
                    case RoomArchetype.SuperIlluminated: gizmoColor = Color.white; break;
                    case RoomArchetype.Dark: gizmoColor = Color.black; break;
                    case RoomArchetype.Empty: gizmoColor = Color.cyan; break;
                    case RoomArchetype.Cluttered: gizmoColor = new Color(1f, 0.5f, 0f); break; // Naranja
                    case RoomArchetype.SafeRoom: gizmoColor = Color.green; break;
                }
                
                // Dibujar volumen sólido semitransparente
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
                Gizmos.DrawCube(box.bounds.center, box.bounds.size);
                
                // Dibujar contorno de alambre
                Gizmos.color = gizmoColor;
                Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
            }
        }
    }
#endif
}

public class EndZoneTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.name.Contains("Player") || other.name.Contains("Jugador"))
        {
            if (ControladorNivel.Instancia != null)
            {
                ControladorNivel.Instancia.GanarJuego();
            }
        }
    }
}
