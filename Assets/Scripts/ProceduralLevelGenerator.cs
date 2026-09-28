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

    [Header("Generación con Semilla")]
    [Tooltip("Activa esto para generar un mapa aleatorio cada vez.")]
    public bool useRandomSeed = true;
    [Tooltip("Si la casilla anterior está desactivada, el mapa siempre se generará igual a base de este texto.")]
    public string levelSeed = "MI_SEMILLA";

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

    [Header("Enemigos y Entidades (Global)")]
    [Tooltip("Prefabs de los enemigos o monstruos (ej. La Luz, Anomalías).")]
    public GameObject[] enemyPrefabs;
    [Tooltip("Total de enemigos a instanciar en todo el laberinto al iniciar la partida.")]
    public int totalEnemiesInLevel = 1;

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

    public enum CellType { Empty, Floor, Wall, Door }
    public CellType[,] grid;
    private List<BSPNode> leafNodes;
    private GameObject levelParent;

    private class BSPNode
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
    }

    public static ProceduralLevelGenerator Instance;
    
    [Header("Progresión del Juego")]
    public static int currentLevel = 0; // Se mantiene entre recargas de escena

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    void Start()
    {
        if (useRandomSeed || string.IsNullOrEmpty(levelSeed))
        {
            levelSeed = System.DateTime.Now.Ticks.ToString();
        }
        Random.InitState(levelSeed.GetHashCode());

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

        // 3. Forzar que TODAS las cámaras rendericen un fondo negro en vez del cielo
        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
        
        // 4. Asegurar que la niebla esté apagada o sea negra, ya que el color por defecto es azulado
        RenderSettings.fog = false;
        RenderSettings.fogColor = Color.black;
    }

    public void GenerateLevel()
    {
        // Limpiamos el nivel anterior si volvemos a generar
        if (levelParent != null) Destroy(levelParent);
        levelParent = new GameObject("Environment_Backrooms");

        grid = new CellType[mapWidth, mapDepth];
        for (int x = 0; x < mapWidth; x++)
            for (int z = 0; z < mapDepth; z++)
                grid[x, z] = CellType.Empty;

        leafNodes = new List<BSPNode>();

        // 1. Ejecutamos la Partición Espacial (BSP)
        BSPNode root = new BSPNode { space = new RectInt(0, 0, mapWidth, mapDepth) };
        SplitNode(root);

        // 2. Definimos qué celdas son Suelo y cuáles Pared
        CreateRooms(root);
        
        // 3. Conectamos las habitaciones excavando Puertas en las paredes
        CreateCorridors(root);
        
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
        
        // 5. Spawn de Trampas, Llaves y zonas especiales (Start/End)
        SpawnElements();

        // 6. Optimización de mallas y construcción de NavMesh
        // Aplicamos el Batching Estático SOLO a los contenedores de geometría por chunk
        for (int i = 0; i < leafNodes.Count; i++)
        {
            if (leafNodes[i].staticContainer != null)
                StaticBatchingUtility.Combine(leafNodes[i].staticContainer);
        }
        BuildNavMesh();
        
        // 7. --- SPAWN GLOBAL DE ENTIDADES AL FINALIZAR EL NIVEL ---
        SpawnGlobalEnemies();
    }

    void SpawnGlobalEnemies()
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
            
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.SpawnEntity(prefab, pos, Quaternion.identity, levelParent.transform);
            }
            else
            {
                Instantiate(prefab, pos, Quaternion.identity, levelParent.transform);
            }
            spawned++;
        }
    }

    void BuildNavMesh()
    {
        NavMeshSurface surface = levelParent.AddComponent<NavMeshSurface>();
        surface.BuildNavMesh();
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
        // Generar semillas (offsets) únicas para cada capa de ruido
        perlinLayers.spaceOffset = Random.Range(0f, 10000f);
        perlinLayers.illuminationOffset = Random.Range(0f, 10000f);
        perlinLayers.clutterOffset = Random.Range(0f, 10000f);

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
            node.roomHeight = Mathf.Lerp(minWallHeight, maxWallHeight, heightMultiplier);

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
            if (node.space.Contains(pos)) return node;
        }
        return null;
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

                Vector3 pos = new Vector3(x * cellSize, 0, z * cellSize);
                
                // Encontrar a qué habitación (Chunk) pertenece esta celda
                BSPNode node = GetNodeForCell(x, z);
                Transform parentTransform = node != null ? node.staticContainer.transform : levelParent.transform;
                float currentHeight = node != null ? node.roomHeight : minWallHeight;

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
                        // Suelo
                        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        floor.transform.position = pos + Vector3.down * 0.5f;
                        floor.transform.localScale = new Vector3(cellSize, 1f, cellSize);
                        floor.transform.SetParent(parentTransform);
                        floor.GetComponent<Renderer>().sharedMaterial = sharedFloorMat; // Uso de material compartido
                        floor.isStatic = true;
                        floor.layer = 6; // Asignar capa 'Room'
                        
                        // Techo (Quad apuntando hacia abajo, invisible desde arriba)
                        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        ceiling.transform.position = pos + Vector3.up * currentHeight;
                        ceiling.transform.localScale = new Vector3(cellSize, cellSize, 1f); // Quad se escala en XY local
                        ceiling.transform.rotation = Quaternion.Euler(-90, 0, 0); // Apuntar hacia abajo (-90 en X hace que Z apunte arriba y la normal -Z apunte abajo)
                        ceiling.transform.SetParent(parentTransform);
                        ceiling.GetComponent<Renderer>().sharedMaterial = sharedCeilingMat; // Uso de material compartido
                        ceiling.isStatic = true;
                        ceiling.layer = 6; // Asignar capa 'Room'
                    }
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
                        // Cubo invisible para mantener colisiones perfectas y evitar bugs físicos
                        GameObject wallCollider = new GameObject("WallCollider");
                        wallCollider.transform.position = pos + Vector3.up * (currentHeight / 2f);
                        wallCollider.transform.SetParent(parentTransform);
                        wallCollider.isStatic = true;
                        wallCollider.layer = 6; // Asignar capa 'Room'
                        BoxCollider box = wallCollider.AddComponent<BoxCollider>();
                        box.size = new Vector3(cellSize, currentHeight, cellSize);

                        // Caras visuales (Quads) solo apuntando al interior
                        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
                        int[][] offsets = { new int[]{0,1}, new int[]{0,-1}, new int[]{-1,0}, new int[]{1,0} };

                        for (int i = 0; i < 4; i++)
                        {
                            int nx = x + offsets[i][0];
                            int nz = z + offsets[i][1];

                            if (nx >= 0 && nx < mapWidth && nz >= 0 && nz < mapDepth)
                            {
                                if (grid[nx, nz] == CellType.Floor || grid[nx, nz] == CellType.Door)
                                {
                                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                                    Destroy(quad.GetComponent<Collider>()); // Quitamos el MeshCollider, ya tenemos el Box
                                    
                                    // Desplazamos el quad hacia el borde de la pared que toca el pasillo
                                    Vector3 quadPos = pos + Vector3.up * (currentHeight / 2f) + dirs[i] * (cellSize / 2f);
                                    quad.transform.position = quadPos;
                                    quad.transform.localScale = new Vector3(cellSize, currentHeight, 1f);
                                    
                                    // Corregimos la rotación: El Quad por defecto mira hacia -Z, así que debemos rotarlo 
                                    // usando la dirección opuesta (-dirs[i]) para que su cara normal apunte hacia el piso.
                                    quad.transform.rotation = Quaternion.LookRotation(-dirs[i]);
                                    
                                    quad.transform.SetParent(wallCollider.transform);
                                    quad.GetComponent<Renderer>().sharedMaterial = sharedWallMat;
                                    quad.isStatic = true;
                                    quad.layer = 6; // Asignar capa 'Room'
                                }
                            }
                        }
                    }

                    // La lógica de Keynotes en las paredes aleatorias ha sido eliminada para evitar generación excesiva.
                    // Ahora solo se generan en SpawnKeynotes() al final de la rutina de generación de nivel.
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

    void SelectSpecialRooms()
    {
        if (leafNodes.Count < 2) return;

        List<BSPNode> deadEnds = new List<BSPNode>();
        List<BSPNode> others = new List<BSPNode>();
        foreach (var node in leafNodes)
        {
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

        if (deadEnds.Count >= 2) {
            startRoom = deadEnds[0];
            endRoom = deadEnds[1];
        } else if (deadEnds.Count == 1) {
            startRoom = deadEnds[0];
            endRoom = others[0];
        } else {
            startRoom = others[0];
            endRoom = others[1];
        }
        
        startRoom.archetype = RoomArchetype.SafeRoom;
        endRoom.archetype = RoomArchetype.SafeRoom;

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
            
            // Hacemos que el Trigger ocupe todo el Espacio BSP, y le sumamos un margen (overshoot) 
            // para que los bordes de los triggers se solapen y el jugador nunca quede "fuera" de un cuarto.
            Vector3 spaceCenter = new Vector3((s.x + s.width/2f) * cellSize, leafNodes[i].roomHeight / 2f, (s.y + s.height/2f) * cellSize);
            roomTrigger.transform.position = spaceCenter;
            box.size = new Vector3(s.width * cellSize + 1.5f, leafNodes[i].roomHeight + 2f, s.height * cellSize + 1.5f);
            
            RoomData rd = roomTrigger.AddComponent<RoomData>();
            rd.areaSize = r.width * r.height;
            
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

        // Conectar adyacencias verificando si los Espacios BSP se tocan (garantiza adyacencia perfecta sin huecos)
        for (int i = 0; i < allRoomData.Count; i++)
        {
            for (int j = i + 1; j < allRoomData.Count; j++)
            {
                RectInt a = leafNodes[i].space;
                RectInt b = leafNodes[j].space;

                bool touchX = (a.xMax == b.xMin || a.xMin == b.xMax) && (a.yMin < b.yMax && a.yMax > b.yMin);
                bool touchY = (a.yMax == b.yMin || a.yMin == b.yMax) && (a.xMin < b.xMax && a.xMax > b.xMin);

                if (touchX || touchY)
                {
                    allRoomData[i].adjacentRooms.Add(allRoomData[j]);
                    allRoomData[j].adjacentRooms.Add(allRoomData[i]);
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
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                
                player.transform.position = center + Vector3.up * 1f; // spawn un poco por encima del suelo
                
                if (cc != null) cc.enabled = true;
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
