using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MinimapUI : MonoBehaviour
{
    [Header("Configuración del Minimapa")]
    public int minimapSize = 200; // Tamaño en píxeles del mapa en UI
    public UnityEngine.InputSystem.Key toggleKey = UnityEngine.InputSystem.Key.M; // Tecla para ocultar/mostrar
    
    private RawImage minimapImage;
    private GameObject playerDot;
    private Texture2D mapTexture;
    private ProceduralLevelGenerator generator;
    private bool mapGenerated = false;
    private GameObject minimapPanel;
    private Transform playerTransform;

    void Start()
    {
        // Esperar un segundo para asegurar que el nivel terminó de generarse
        Invoke("CrearMinimapa", 1.0f);
    }

    void CrearMinimapa()
    {
        generator = ProceduralLevelGenerator.Instance;
        if (generator == null || generator.grid == null)
        {
            Debug.LogWarning("Minimapa: No se encontró el ProceduralLevelGenerator o la grilla.");
            return;
        }

        // 1. Crear Canvas
        GameObject canvasObj = new GameObject("Canvas_Minimap");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; 
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // 2. Crear Textura Circular para la Máscara
        Texture2D circleTex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int x = 0; x < 128; x++) {
            for (int y = 0; y < 128; y++) {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64));
                circleTex.SetPixel(x, y, dist < 64 ? Color.white : Color.clear);
            }
        }
        circleTex.Apply();
        Sprite circleSprite = Sprite.Create(circleTex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));

        // 3. Crear Panel Contenedor con Máscara Circular
        minimapPanel = new GameObject("Panel_Minimap");
        minimapPanel.transform.SetParent(canvasObj.transform, false);
        
        RectTransform panelRect = minimapPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f); 
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-20, -100); 
        panelRect.sizeDelta = new Vector2(minimapSize, minimapSize);

        Image bg = minimapPanel.AddComponent<Image>();
        bg.sprite = circleSprite;
        bg.color = new Color(0, 0, 0, 0.8f); 

        Mask mask = minimapPanel.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // 4. Generar Textura del Mapa
        int width = generator.mapWidth;
        int depth = generator.mapDepth;
        mapTexture = new Texture2D(width, depth, TextureFormat.RGBA32, false);
        mapTexture.filterMode = FilterMode.Point; 

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                mapTexture.SetPixel(x, z, Color.clear); // Inicialmente todo oculto (Fog of War)
            }
        }
        mapTexture.Apply();

        // 5. Asignar Textura a la UI (El mapa se moverá dentro de la máscara)
        GameObject rawImgObj = new GameObject("RawImage_Map");
        rawImgObj.transform.SetParent(minimapPanel.transform, false);
        minimapImage = rawImgObj.AddComponent<RawImage>();
        minimapImage.texture = mapTexture;
        
        RectTransform rawImgRect = rawImgObj.GetComponent<RectTransform>();
        
        // Cada celda será de 10x10 píxeles en la UI (puedes ajustar esto para más o menos zoom)
        float uiCellSize = 10f; 
        rawImgRect.sizeDelta = new Vector2(width * uiCellSize, depth * uiCellSize); 

        // 6. Crear Marcador del Jugador (Flecha/Triángulo)
        playerDot = new GameObject("PlayerDot");
        playerDot.transform.SetParent(minimapPanel.transform, false); 
        
        Text dotText = playerDot.AddComponent<Text>();
        dotText.text = "▲";
        dotText.color = Color.red;
        dotText.fontSize = 20;
        dotText.alignment = TextAnchor.MiddleCenter;
        dotText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (dotText.font == null) dotText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        RectTransform dotRect = playerDot.GetComponent<RectTransform>();
        dotRect.sizeDelta = new Vector2(24, 24); 
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = new Vector2(0, 2); // Un poco arriba para centrar el triángulo visualmente

        mapGenerated = true;
    }

    void Update()
    {
        // Alternar visualización del mapa
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame && minimapPanel != null)
        {
            minimapPanel.SetActive(!minimapPanel.activeSelf);
        }

        if (!mapGenerated || playerDot == null) return;

        // Buscar al jugador fiablemente usando el AnxietyManager que sabemos que está en el Player
        if (playerTransform == null)
        {
            AnxietyManager am = FindObjectOfType<AnxietyManager>();
            if (am != null) playerTransform = am.transform;
            else return; // Si no hay jugador, no podemos actualizar
        }

        // Actualizar posición del punto rojo
        Vector3 playerPos = playerTransform.position; 
        
        // Convertir posición del mundo (X, Z) a índices de la grilla
        float gridX = playerPos.x / generator.cellSize;
        float gridZ = playerPos.z / generator.cellSize;

        // Normalizar los índices (0 a 1) para posicionarlos en el RawImage.
        // Sumamos 0.5f para que el pivot caiga exactamente en el centro del píxel/celda, y no en la esquina inferior izquierda.
        float normalizedX = (gridX + 0.5f) / (float)generator.mapWidth;
        float normalizedY = (gridZ + 0.5f) / (float)generator.mapDepth;

        RectTransform mapRect = minimapImage.GetComponent<RectTransform>();
        
        // 1. Mover el Pivot del mapa exactamente a donde está el jugador
        mapRect.pivot = new Vector2(normalizedX, normalizedY);
        
        // 2. Anclar el mapa al centro de la máscara circular
        mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = Vector2.zero; // Esto hace que el pivot (el jugador) quede al centro exacto

        // 3. Rotar el mapa en dirección contraria a la cámara del jugador
        // Buscamos la cámara real dentro del jugador para obtener la rotación exacta de su cabeza
        Camera playerCam = playerTransform.GetComponentInChildren<Camera>();
        float yRotation = 0f;
        
        if (playerCam != null) yRotation = playerCam.transform.eulerAngles.y;
        else if (Camera.main != null) yRotation = Camera.main.transform.eulerAngles.y;
        else yRotation = playerTransform.eulerAngles.y;

        mapRect.localEulerAngles = new Vector3(0, 0, yRotation);

        // 4. Fog of War: Revelar área alrededor del jugador
        RevelarAreaEnMinimapa(Mathf.RoundToInt(gridX), Mathf.RoundToInt(gridZ));
    }

    void RevelarAreaEnMinimapa(int px, int pz)
    {
        int visionRadius = 8; // Radio de celdas que el jugador puede ver en el minimapa
        bool mapModified = false;

        for (int x = px - visionRadius; x <= px + visionRadius; x++)
        {
            for (int z = pz - visionRadius; z <= pz + visionRadius; z++)
            {
                // Verificar límites del mapa
                if (x >= 0 && x < generator.mapWidth && z >= 0 && z < generator.mapDepth)
                {
                    // Si el píxel ya tiene color, no necesitamos repintarlo
                    if (mapTexture.GetPixel(x, z).a > 0) continue;

                    // Si está dentro del radio circular visual
                    if (Vector2.Distance(new Vector2(px, pz), new Vector2(x, z)) <= visionRadius)
                    {
                        Color pixelColor = Color.clear;
                        if (generator.grid[x, z] == ProceduralLevelGenerator.CellType.Wall) 
                            pixelColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                        else if (generator.grid[x, z] == ProceduralLevelGenerator.CellType.Floor) 
                            pixelColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
                        else if (generator.grid[x, z] == ProceduralLevelGenerator.CellType.Door) 
                            pixelColor = new Color(0.2f, 0.8f, 0.2f, 1f);
                        
                        if (pixelColor != Color.clear)
                        {
                            mapTexture.SetPixel(x, z, pixelColor);
                            mapModified = true;
                        }
                    }
                }
            }
        }

        if (mapModified)
        {
            mapTexture.Apply();
        }
    }
}
