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

public class ProceduralLevelGenerator : MonoBehaviour
{
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
    public GameObject trapPrefab;
    public GameObject keyPrefab;
    public GameObject padPrefab;
    
    [Header("Marcadores Visuales")]
    public Color startRoomColor = Color.green;
    public Color safeZoneColor = Color.blue;

    [Header("Debug")]
    [Tooltip("Activa para ver los volúmenes de las habitaciones en la ventana de Scene (amarillo = luz, gris = oscuridad)")]
    public bool showDebugZones = false;

    private BSPNode startRoom;
    private BSPNode endRoom;

    private enum CellType { Empty, Floor, Wall, Door }
    private CellType[,] grid;
    private List<BSPNode> leafNodes;
    private GameObject levelParent;

    private class BSPNode
    {
        public RectInt space;
        public RectInt room;
        public BSPNode left, right;
        public bool splitHorizontal;
        public int splitPoint;
        public GameObject geometryContainer;
        public float roomHeight;
        public RoomArchetype archetype = RoomArchetype.Normal;
        public float archetypeIntensity = 0f;
    }

    void Start()
    {
        ConfigurarEntornoOscuro();
        GenerateLevel();
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
        
        // 4. Asignamos Arquetipos a las habitaciones según la curva de distribución
        AssignArchetypes();
        
        // --- NUEVA LÓGICA: Seleccionar cuartos especiales y forzar su geometría cerrada ---
        SelectSpecialRooms();
        
        // --- NUEVA LÓGICA: Preparar contenedores de Chunking ---
        for (int i = 0; i < leafNodes.Count; i++)
        {
            GameObject container = new GameObject("GeometryChunk_" + i);
            container.transform.SetParent(levelParent.transform);
            leafNodes[i].geometryContainer = container;
        }

        // 4. Instanciamos los objetos 3D y los metemos en sus Chunks
        BuildPhysicalLevel();
        
        // 5. Spawn de Trampas, Llaves y zonas especiales (Start/End)
        SpawnElements();

        // 6. Construir NavMesh
        BuildNavMesh();
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
        foreach (BSPNode node in leafNodes)
        {
            if (node.archetype != RoomArchetype.Normal) continue;

            float depthProgress = (float)node.room.y / mapDepth;
            List<ArchetypeSettings> validSettings = archetypeSettings.FindAll(s => s.archetype != RoomArchetype.Batophobic && s.archetype != RoomArchetype.Normal);
            validSettings.Sort((a, b) => Random.value.CompareTo(0.5f)); // Mezclar

            foreach (var setting in validSettings)
            {
                float prob = setting.baseProbability * setting.distributionCurve.Evaluate(depthProgress);
                if (Random.value < prob)
                {
                    node.archetype = setting.archetype;
                    node.archetypeIntensity = setting.similarityPeak;
                    break;
                }
            }
        }
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

            int minDoorSize = 1;
            
            if (node.splitHorizontal)
            {
                // El corte fue horizontal, así que comparten una pared en Z
                int sharedXMin = node.space.x + 1;
                int sharedXMax = node.space.x + node.space.width - 2;
                
                if (sharedXMax >= sharedXMin) 
                {
                    int maxDoorSize = (sharedXMax - sharedXMin) + 1;
                    // Tamaño aleatorio de la puerta
                    int doorSize = Random.Range(minDoorSize, maxDoorSize + 1);
                    
                    // 30% de probabilidad de que la puerta sea GIGANTE (abarque toda la pared)
                    // Esto ayuda a crear pasadizos reales que se fusionan con otros cuartos
                    if (Random.value < 0.3f) doorSize = maxDoorSize;
                    
                    // Posición aleatoria a lo largo de la pared compartida
                    int doorStartX = Random.Range(sharedXMin, sharedXMax - doorSize + 2);
                    
                    int wallZ1 = node.space.y + node.splitPoint - 1;
                    int wallZ2 = node.space.y + node.splitPoint;
                    
                    for (int x = doorStartX; x < doorStartX + doorSize; x++)
                    {
                        if (grid[x, wallZ1] == CellType.Wall) grid[x, wallZ1] = CellType.Door;
                        if (grid[x, wallZ2] == CellType.Wall) grid[x, wallZ2] = CellType.Door;
                    }
                }
            }
            else
            {
                // El corte fue vertical, comparten pared en X
                int sharedZMin = node.space.y + 1;
                int sharedZMax = node.space.y + node.space.height - 2;
                
                if (sharedZMax >= sharedZMin)
                {
                    int maxDoorSize = (sharedZMax - sharedZMin) + 1;
                    int doorSize = Random.Range(minDoorSize, maxDoorSize + 1);
                    
                    if (Random.value < 0.3f) doorSize = maxDoorSize;
                    
                    int doorStartZ = Random.Range(sharedZMin, sharedZMax - doorSize + 2);
                    
                    int wallX1 = node.space.x + node.splitPoint - 1;
                    int wallX2 = node.space.x + node.splitPoint;
                    
                    for (int z = doorStartZ; z < doorStartZ + doorSize; z++)
                    {
                        if (grid[wallX1, z] == CellType.Wall) grid[wallX1, z] = CellType.Door;
                        if (grid[wallX2, z] == CellType.Wall) grid[wallX2, z] = CellType.Door;
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
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapDepth; z++)
            {
                if (grid[x, z] == CellType.Empty) continue;

                Vector3 pos = new Vector3(x * cellSize, 0, z * cellSize);
                
                // Encontrar a qué habitación (Chunk) pertenece esta celda
                BSPNode node = GetNodeForCell(x, z);
                Transform parentTransform = node != null ? node.geometryContainer.transform : levelParent.transform;
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
                        Instantiate(floorPrefab, pos, Quaternion.identity, parentTransform);
                    }
                    else
                    {
                        // Suelo
                        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        floor.transform.position = pos + Vector3.down * 0.5f;
                        floor.transform.localScale = new Vector3(cellSize, 1f, cellSize);
                        floor.transform.SetParent(parentTransform);
                        Material floorMat = floor.GetComponent<Renderer>().material;
                        floorMat.color = new Color(0.8f, 0.8f, 0.7f);
                        floorMat.SetFloat("_Smoothness", 0f);
                        floorMat.SetFloat("_Glossiness", 0f);
                        floorMat.SetFloat("_Metallic", 0f);
                        
                        // Techo
                        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        ceiling.transform.position = pos + Vector3.up * currentHeight;
                        ceiling.transform.localScale = new Vector3(cellSize, 1f, cellSize);
                        ceiling.transform.SetParent(parentTransform);
                        Material ceilingMat = ceiling.GetComponent<Renderer>().material;
                        ceilingMat.color = new Color(0.2f, 0.2f, 0.2f);
                        ceilingMat.SetFloat("_Smoothness", 0f);
                        ceilingMat.SetFloat("_Glossiness", 0f);
                        ceilingMat.SetFloat("_Metallic", 0f);
                    }
                }
                
                if (isWall)
                {
                    if (wallPrefab != null)
                    {
                        Instantiate(wallPrefab, pos + Vector3.up * (currentHeight/2f), Quaternion.identity, parentTransform);
                    }
                    else
                    {
                        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        wall.transform.position = pos + Vector3.up * (currentHeight / 2f);
                        wall.transform.localScale = new Vector3(cellSize, currentHeight, cellSize);
                        wall.transform.SetParent(parentTransform);
                        Material wallMat = wall.GetComponent<Renderer>().material;
                        wallMat.color = new Color(0.9f, 0.8f, 0.6f); 
                        wallMat.SetFloat("_Smoothness", 0f);
                        wallMat.SetFloat("_Glossiness", 0f);
                        wallMat.SetFloat("_Metallic", 0f);
                    }
                }
            }
        }
    }

    int GetEntranceCount(BSPNode node)
    {
        RectInt r = node.room;
        int count = 0;
        for(int x = r.x; x < r.x + r.width; x++) {
            if (x >= 0 && x < mapWidth) {
                if (r.y >= 0 && r.y < mapDepth && grid[x, r.y] == CellType.Door) count++;
                if (r.y + r.height - 1 >= 0 && r.y + r.height - 1 < mapDepth && grid[x, r.y + r.height - 1] == CellType.Door) count++;
            }
        }
        for(int z = r.y; z < r.y + r.height; z++) {
            if (z >= 0 && z < mapDepth) {
                if (r.x >= 0 && r.x < mapWidth && grid[r.x, z] == CellType.Door) count++;
                if (r.x + r.width - 1 >= 0 && r.x + r.width - 1 < mapWidth && grid[r.x + r.width - 1, z] == CellType.Door) count++;
            }
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
        List<Vector2Int> connections = new List<Vector2Int>();
        if (node.space.y > 0) {
            for (int x = node.space.x; x < node.space.x + node.space.width; x++) {
                if (grid[x, node.space.y] == CellType.Door) connections.Add(new Vector2Int(x, node.space.y));
            }
        }
        if (node.space.y + node.space.height < mapDepth) {
            for (int x = node.space.x; x < node.space.x + node.space.width; x++) {
                if (grid[x, node.space.y + node.space.height - 1] == CellType.Door) connections.Add(new Vector2Int(x, node.space.y + node.space.height - 1));
            }
        }
        if (node.space.x > 0) {
            for (int z = node.space.y; z < node.space.y + node.space.height; z++) {
                if (grid[node.space.x, z] == CellType.Door) connections.Add(new Vector2Int(node.space.x, z));
            }
        }
        if (node.space.x + node.space.width < mapWidth) {
            for (int z = node.space.y; z < node.space.y + node.space.height; z++) {
                if (grid[node.space.x + node.space.width - 1, z] == CellType.Door) connections.Add(new Vector2Int(node.space.x + node.space.width - 1, z));
            }
        }

        if (connections.Count == 0) {
            for (int x = node.space.x; x < node.space.x + node.space.width; x++) {
                for (int z = node.space.y; z < node.space.y + node.space.height; z++) {
                    if (grid[x, z] == CellType.Door) connections.Add(new Vector2Int(x, z));
                }
            }
        }

        for (int x = node.space.x; x < node.space.x + node.space.width; x++) {
            for (int z = node.space.y; z < node.space.y + node.space.height; z++) {
                grid[x, z] = CellType.Wall;
            }
        }

        int size = Mathf.Min(node.space.width, node.space.height) - 2;
        if (size < 3) size = 3;
        int rx = node.space.x + (node.space.width - size) / 2;
        int rz = node.space.y + (node.space.height - size) / 2;
        
        if (rx < 1) rx = 1;
        if (rz < 1) rz = 1;
        if (rx + size >= mapWidth) size = mapWidth - rx - 1;
        if (rz + size >= mapDepth) size = mapDepth - rz - 1;
        
        node.room = new RectInt(rx, rz, size, size);

        for (int x = rx; x < rx + size; x++) {
            for (int z = rz; z < rz + size; z++) {
                grid[x, z] = CellType.Floor;
            }
        }

        if (connections.Count > 0) {
            Vector2Int chosen = connections[Random.Range(0, connections.Count)];
            int cx = chosen.x;
            int cz = chosen.y;
            
            cx = Mathf.Clamp(cx, node.space.x, node.space.x + node.space.width - 1);
            cz = Mathf.Clamp(cz, node.space.y, node.space.y + node.space.height - 1);

            int failsafe = 100;
            while(failsafe-- > 0) {
                grid[cx, cz] = CellType.Door;
                if (cx >= rx && cx < rx + size && cz >= rz && cz < rz + size) break;
                
                int targetX = rx + size/2;
                int targetZ = rz + size/2;
                if (Mathf.Abs(targetX - cx) > Mathf.Abs(targetZ - cz)) {
                    cx += (targetX > cx) ? 1 : -1;
                } else {
                    cz += (targetZ > cz) ? 1 : -1;
                }
            }
        }
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
            
            rd.entranceCount = 0;
            for(int x = r.x; x < r.x + r.width; x++) {
                if (x >= 0 && x < mapWidth) {
                    if (r.y >= 0 && r.y < mapDepth && grid[x, r.y] == CellType.Door) rd.entranceCount++;
                    if (r.y + r.height - 1 >= 0 && r.y + r.height - 1 < mapDepth && grid[x, r.y + r.height - 1] == CellType.Door) rd.entranceCount++;
                }
            }
            for(int z = r.y; z < r.y + r.height; z++) {
                if (z >= 0 && z < mapDepth) {
                    if (r.x >= 0 && r.x < mapWidth && grid[r.x, z] == CellType.Door) rd.entranceCount++;
                    if (r.x + r.width - 1 >= 0 && r.x + r.width - 1 < mapWidth && grid[r.x + r.width - 1, z] == CellType.Door) rd.entranceCount++;
                }
            }
            
            if (leafNodes[i] == endRoom)
            {
                // Instanciar barreras en las puertas
                for(int x = r.x; x < r.x + r.width; x++) {
                    if (r.y >= 0 && r.y < mapDepth && grid[x, r.y] == CellType.Door) InstanciarBarrera(x, r.y, rd.geometryContainer != null ? rd.geometryContainer.transform : levelParent.transform);
                    if (r.y + r.height - 1 >= 0 && r.y + r.height - 1 < mapDepth && grid[x, r.y + r.height - 1] == CellType.Door) InstanciarBarrera(x, r.y + r.height - 1, rd.geometryContainer != null ? rd.geometryContainer.transform : levelParent.transform);
                }
                for(int z = r.y; z < r.y + r.height; z++) {
                    if (r.x >= 0 && r.x < mapWidth && grid[r.x, z] == CellType.Door) InstanciarBarrera(r.x, z, rd.geometryContainer != null ? rd.geometryContainer.transform : levelParent.transform);
                    if (r.x + r.width - 1 >= 0 && r.x + r.width - 1 < mapWidth && grid[r.x + r.width - 1, z] == CellType.Door) InstanciarBarrera(r.x + r.width - 1, z, rd.geometryContainer != null ? rd.geometryContainer.transform : levelParent.transform);
                }
            }

            if (leafNodes[i] == startRoom || leafNodes[i] == endRoom)
            {
                rd.isAlwaysRendered = true;
            }

            // Convertir a cuarto seguro de manera obligatoria si tiene 1 puerta (Dead End) y era Normal
            if (rd.entranceCount == 1 && leafNodes[i].archetype == RoomArchetype.Normal)
            {
                leafNodes[i].archetype = RoomArchetype.SafeRoom;
                rd.archetype = RoomArchetype.SafeRoom;
            }

            float customLightDensity = lightDensity;
            if (leafNodes[i].archetype == RoomArchetype.SuperIlluminated || leafNodes[i].archetype == RoomArchetype.SafeRoom) customLightDensity = 1.0f;
            else if (leafNodes[i].archetype == RoomArchetype.Dark) customLightDensity = 0.0f;

            if (Random.value < customLightDensity)
            {
                rd.illuminationLevel = Random.Range(0.2f, 1.0f);
                if (leafNodes[i].archetype == RoomArchetype.SuperIlluminated) rd.illuminationLevel = 1.0f;
                else if (leafNodes[i].archetype == RoomArchetype.SafeRoom) rd.illuminationLevel = 0.2f; // Luz tenue (sistema lo ve tenue)
                
                // Instanciar luz física en el cuarto
                GameObject roomLightObj = new GameObject("RoomLight_" + i);
                roomLightObj.transform.position = spaceCenter + Vector3.up * (leafNodes[i].roomHeight * 0.40f); 
                roomLightObj.transform.SetParent(rd.geometryContainer != null ? rd.geometryContainer.transform : levelParent.transform);
                
                Light pointLight = roomLightObj.AddComponent<Light>();
                pointLight.type = LightType.Point;
                pointLight.range = s.width * cellSize * 0.8f; 
                pointLight.intensity = rd.illuminationLevel * 8f;
                
                if (leafNodes[i].archetype == RoomArchetype.SuperIlluminated) 
                {
                    pointLight.intensity *= (1f + leafNodes[i].archetypeIntensity * 2f); // Más intensa según el pico
                }
                
                if (leafNodes[i].archetype == RoomArchetype.SafeRoom)
                {
                    // Color aleatorio, vivaz y tranquilizante (Tonos Fríos/Naturales: Verde a Azul a Morado)
                    pointLight.color = Color.HSVToRGB(Random.Range(0.3f, 0.85f), 0.8f, 1f);
                    pointLight.intensity = 5f; // Fuerte visualmente
                }
                // Si la iluminación es muy baja, la luz es defectuosa y parpadea (excepto cuarto seguro)
                else if (rd.illuminationLevel < 0.4f)
                {
                    FlickeringLight fl = roomLightObj.AddComponent<FlickeringLight>();
                    fl.isDefective = true;
                    pointLight.color = new Color(0.9f, 0.9f, 0.8f);
                }
                else
                {
                    pointLight.color = Color.white;
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

        // Instanciar props en el resto de habitaciones
        foreach (BSPNode node in leafNodes)
        {
            if (node == startRoom || node == endRoom) continue;

            RectInt r = node.room;
            Transform chunkParent = node.geometryContainer != null ? node.geometryContainer.transform : levelParent.transform;

            if (node.archetype == RoomArchetype.Empty) continue;

            int propIterations = 1;
            if (node.archetype == RoomArchetype.Cluttered)
            {
                // Multiplicar props según la intensidad
                propIterations = 1 + Mathf.FloorToInt(node.archetypeIntensity * 4f); 
            }

            for (int p = 0; p < propIterations; p++)
            {
                // Instanciar Trampa (50% prob) en posición aleatoria
                if (Random.value > 0.5f)
                {
                    Vector3 centerPos = new Vector3((r.x + Random.Range(1, r.width-1)) * cellSize, 0.5f, (r.y + Random.Range(1, r.height-1)) * cellSize);
                    if (trapPrefab != null) Instantiate(trapPrefab, centerPos, Quaternion.identity, chunkParent);
                    else CreatePlaceholder("Trampa", centerPos, Color.red, Vector3.one * 0.5f, chunkParent);
                }

                // Instanciar Llave (Item) 
                if (Random.value > 0.3f)
                {
                    Vector3 cornerPos = new Vector3((r.x + Random.Range(1, r.width-1)) * cellSize, 1f, (r.y + Random.Range(1, r.height-1)) * cellSize);
                    if (keyPrefab != null) Instantiate(keyPrefab, cornerPos, Quaternion.identity, chunkParent);
                    else CreatePlaceholder("Item/Key", cornerPos, Color.yellow, Vector3.one * 0.3f, chunkParent);
                }

                // Instanciar Pad cerca de una puerta (o random)
                if (Random.value > 0.5f)
                {
                    Vector3 padPos = new Vector3((r.x + Random.Range(1, r.width-1)) * cellSize, 0.1f, (r.y + Random.Range(1, r.height-1)) * cellSize);
                    if (padPrefab != null) Instantiate(padPrefab, padPos, Quaternion.identity, chunkParent);
                    else CreatePlaceholder("Entry Pad", padPos, Color.cyan, new Vector3(1f, 0.1f, 1f), chunkParent);
                }
            }
        }
    }

    void MarkRoomSpecial(BSPNode node, string label, Color col)
    {
        RectInt r = node.room;
        Vector3 center = new Vector3((r.x + r.width / 2f) * cellSize, 0.1f, (r.y + r.height / 2f) * cellSize);
        
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.transform.position = center;
        marker.transform.localScale = new Vector3(2f, 0.05f, 2f);
        marker.GetComponent<Renderer>().material.color = col;
        marker.name = label;
        
        Transform chunkParent = node.geometryContainer != null ? node.geometryContainer.transform : levelParent.transform;
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
