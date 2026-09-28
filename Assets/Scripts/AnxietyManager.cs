using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AnxietyManager : MonoBehaviour
{
    [Header("Estado de Ansiedad")]
    [Range(0, 100)]
    public float currentAnxiety = 0f;
    public float maxAnxiety = 100f;
    
    [Tooltip("Tasa a la que se recupera la ansiedad (se reduce) por segundo en estado de relajación.")]
    public float baseRecoveryRate = 8f;
    
    [Header("Fobias del Jugador")]
    [Tooltip("Añade aquí las fobias que sufre el jugador para que afecten su nivel de ansiedad.")]
    public List<PhobiaType> activePhobias = new List<PhobiaType>();
    
    [Header("Configuración de Consecuencias")]
    [Tooltip("Umbral (0-100) en el cual el jugador sufre un evento de pánico (Alteración de entorno).")]
    public float criticalThreshold = 80f;
    [Tooltip("Umbral (0-100) en el cual se materializa una entidad física.")]
    public float entitySpawnThreshold = 95f;
    [Tooltip("Prefab de la entidad que spawnea (Ej. El Gato Tétrico)")]
    public GameObject entityPrefabToSpawn;
    
    [Header("Rendimiento y Visibilidad")]
    [Tooltip("Radio de profundidad de renderizado. 1 = Cuarto actual + Adyacentes. 2 = Adyacentes de adyacentes.")]
    [Range(1, 4)]
    public int visibilityDepth = 1;

    [Header("Muerte por Ansiedad")]
    [Tooltip("Sprite (PNG) para renderizar el jugador muerto en el piso")]
    public Sprite deadPlayerSprite;
    [Tooltip("Segundos consecutivos en 100% de ansiedad antes de morir")]
    public float timeToDie = 15f;
    private float deathTimer = 0f;
    public bool isDead = false;

    private RoomData currentRoom;
    private bool hasTriggeredConsequence = false;
    private bool hasTriggeredEntity = false;
    private HashSet<RoomData> currentlyVisibleRooms = new HashSet<RoomData>();
    private RoomData[] cachedRooms = null;
    private float updateTimer = 0f;
    private float logTimer = 0f;
    private float visibilityTimer = 0f;

    // Variables para el filtro visual
    private Volume anxietyVolume;
    private ColorAdjustments colorAdjustments;
    private float smoothedAnxietyRate = 0f;

    void Start()
    {
        AssignRandomPhobias();
        SetupAnxietyVisuals();
    }

    void AssignRandomPhobias()
    {
        activePhobias.Clear();
        
        System.Array phobiaValues = System.Enum.GetValues(typeof(PhobiaType));
        List<PhobiaType> allPhobias = new List<PhobiaType>();
        foreach (PhobiaType p in phobiaValues)
        {
            allPhobias.Add(p);
        }

        int numPhobias = Random.Range(2, 4); // 2 a 3 fobias
        
        // Mezclar lista (Fisher-Yates)
        for (int i = 0; i < allPhobias.Count; i++)
        {
            PhobiaType temp = allPhobias[i];
            int randomIndex = Random.Range(i, allPhobias.Count);
            allPhobias[i] = allPhobias[randomIndex];
            allPhobias[randomIndex] = temp;
        }

        // Asignar fobias
        for (int i = 0; i < numPhobias && i < allPhobias.Count; i++)
        {
            activePhobias.Add(allPhobias[i]);
        }
        
        Debug.Log("Fobias aleatorias asignadas: " + string.Join(", ", activePhobias));
    }

    void SetupAnxietyVisuals()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            var cameraData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            cameraData.renderPostProcessing = true;
        }

        // Crear un objeto hijo para el volumen de post-procesado
        GameObject volumeObj = new GameObject("AnxietyPostProcessing");
        volumeObj.transform.SetParent(transform);
        
        anxietyVolume = volumeObj.AddComponent<Volume>();
        anxietyVolume.isGlobal = true;
        anxietyVolume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        anxietyVolume.profile = profile;

        colorAdjustments = profile.Add<ColorAdjustments>(false);
        if (colorAdjustments != null)
        {
            colorAdjustments.active = true;
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.colorFilter.overrideState = true;
            colorAdjustments.postExposure.overrideState = true;
        }
    }


    void Update()
    {
        // Forzar detección inicial del cuarto si el jugador teletransportó y OnTriggerEnter falló
        if (currentRoom == null)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 1f);
            foreach (var h in hits) {
                RoomData rd = h.GetComponent<RoomData>();
                if (rd != null) {
                    currentRoom = rd;
                    UpdateRoomVisibility();
                    break;
                }
            }
        }

        float previousAnxiety = currentAnxiety;

        if (!isDead)
        {
            if (currentAnxiety >= maxAnxiety)
            {
                deathTimer += Time.deltaTime;
                if (deathTimer >= timeToDie)
                {
                    StartCoroutine(DeathRoutine());
                }
            }
            else
            {
                deathTimer = 0f;
            }
        }

        // Se ejecuta cada frame, es ligero
        CalculateEnvironmentalAnxiety();
        CheckConsequences();

        // Throttle para cálculos pesados de físicas (solo 5 veces por segundo en vez de 60+)
        updateTimer += Time.deltaTime;
        if (updateTimer >= 0.2f)
        {
            CalculateProximityAnxiety();
            updateTimer = 0f;
        }

        // Calcular la tasa de cambio de ansiedad y actualizar los efectos visuales
        float currentRate = 0f;
        if (Time.deltaTime > 0f)
        {
            currentRate = (currentAnxiety - previousAnxiety) / Time.deltaTime;
        }
        
        smoothedAnxietyRate = Mathf.Lerp(smoothedAnxietyRate, currentRate, Time.deltaTime * 5f);
        UpdateAnxietyVisuals(smoothedAnxietyRate);

        // Mostrar un log cada segundo para monitorear el estado
        logTimer += Time.deltaTime;
        if (logTimer >= 1f)
        {
            Debug.Log($"[Ansiedad] Nivel Actual: {currentAnxiety:F1} / 100 | Ganancia: {smoothedAnxietyRate:F1} por seg");
            logTimer = 0f;
        }

        // Actualizar el Culling de Frustum/Distancia periódicamente
        visibilityTimer += Time.deltaTime;
        if (visibilityTimer >= 0.3f)
        {
            if (currentRoom != null) UpdateRoomVisibility();
            visibilityTimer = 0f;
        }
    }

    void UpdateAnxietyVisuals(float rate)
    {
        if (colorAdjustments == null || anxietyVolume == null) return;

        float targetSaturation = 0f; 
        Color targetColor = Color.white;
        float targetExposure = 0f;
        float targetWeight = 0f;

        if (currentAnxiety >= criticalThreshold)
        {
            // Ansiedad crítica: Filtro rojizo constante, menos colores y apagado
            targetWeight = 1f;
            targetSaturation = -30f;
            targetColor = new Color(1f, 0.2f, 0.2f); 
            targetExposure = -1.2f;
        }
        else if (rate > 0f || currentAnxiety > 10f) // Si gana ansiedad O ya tiene algo acumulado
        {
            // Combinar la tasa de ganancia con la ansiedad acumulada para que no desaparezca de golpe
            float rateIntensity = Mathf.Clamp01(rate / 15f); 
            float accumIntensity = Mathf.Clamp01(currentAnxiety / criticalThreshold);
            
            float intensity = Mathf.Max(rateIntensity * 0.5f, accumIntensity);

            targetWeight = Mathf.Clamp01(intensity + 0.3f); // Siempre encendido si hay ansiedad
            targetSaturation = Mathf.Lerp(0f, -60f, intensity);
            targetColor = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), intensity); // Rojo más notorio
            targetExposure = Mathf.Lerp(0f, -0.8f, intensity); // Apagado
        }
        else
        {
            // Relajándose: Sin filtro
            targetWeight = 0f;
            targetExposure = 0f;
        }

        // Transición suave del post-procesado
        anxietyVolume.weight = Mathf.Lerp(anxietyVolume.weight, targetWeight, Time.deltaTime * 2f);
        colorAdjustments.saturation.value = Mathf.Lerp(colorAdjustments.saturation.value, targetSaturation, Time.deltaTime * 2f);
        colorAdjustments.colorFilter.value = Color.Lerp(colorAdjustments.colorFilter.value, targetColor, Time.deltaTime * 2f);
        colorAdjustments.postExposure.value = Mathf.Lerp(colorAdjustments.postExposure.value, targetExposure, Time.deltaTime * 2f);
    }

    void OnTriggerEnter(Collider other)
    {
        RoomData room = other.GetComponent<RoomData>();
        if (room != null && currentRoom != room)
        {
            currentRoom = room;
            UpdateRoomVisibility();
        }
    }

    void UpdateRoomVisibility()
    {
        if (cachedRooms == null || cachedRooms.Length == 0)
        {
            cachedRooms = FindObjectsOfType<RoomData>();
        }
        
        // 1. Apagar todas las geometrías (excepto las que siempre deben renderizarse)
        foreach(var r in cachedRooms)
        {
            if (r.geometryContainer != null) 
            {
                if (r.isAlwaysRendered) r.geometryContainer.SetActive(true);
                else r.geometryContainer.SetActive(false);
            }
        }

        if (currentRoom == null) return;

        // 2. Usar BFS (Búsqueda en anchura) para encontrar habitaciones hasta la profundidad deseada
        HashSet<RoomData> roomsToShow = new HashSet<RoomData>();
        Queue<KeyValuePair<RoomData, int>> queue = new Queue<KeyValuePair<RoomData, int>>();
        
        queue.Enqueue(new KeyValuePair<RoomData, int>(currentRoom, 0));
        roomsToShow.Add(currentRoom);

        while (queue.Count > 0)
        {
            var pair = queue.Dequeue();
            RoomData node = pair.Key;
            int depth = pair.Value;

            // Continuar explorando si no hemos alcanzado la profundidad máxima,
            // O si la habitación está físicamente muy cerca (para evitar voids en pasillos rectos)
            if (depth < visibilityDepth || Vector3.Distance(transform.position, node.transform.position) < 50f)
            {
                foreach(var adj in node.adjacentRooms)
                {
                    if (adj != null && !roomsToShow.Contains(adj))
                    {
                        roomsToShow.Add(adj);
                        queue.Enqueue(new KeyValuePair<RoomData, int>(adj, depth + 1));
                    }
                }
            }
        }

        // 3. Encender las habitaciones visibles y aplicar cambios de paranoia si aplica
        foreach(var roomToShow in roomsToShow)
        {
            // MECÁNICA DE TERROR PSICOLÓGICO DINÁMICO:
            // Si la ansiedad es alta, y este cuarto estaba apagado (el jugador no lo estaba viendo),
            // hay una posibilidad de que su geometría se reconstruya/corrompa justo antes de que lo vea.
            if (currentAnxiety >= criticalThreshold && !currentlyVisibleRooms.Contains(roomToShow) && roomToShow.isVisited)
            {
                // 15% de probabilidad de reconstruir la arquitectura del cuarto a sus espaldas
                if (Random.value < 0.15f && roomToShow.illuminationLevel > 0f) 
                {
                    CorruptRoom(roomToShow);
                }
            }

            if (roomToShow.geometryContainer != null) 
            {
                roomToShow.geometryContainer.SetActive(true);
            }
        }

        currentlyVisibleRooms = roomsToShow;
    }

    void CalculateEnvironmentalAnxiety()
    {
        if (currentRoom == null) return;

        // Si estamos en un espacio seguro, la ansiedad baja rápidamente y no se activan fobias
        if (currentRoom.archetype == RoomArchetype.SafeRoom)
        {
            float safeRoomAnxietyChange = -50f; 
            currentAnxiety = Mathf.Clamp(currentAnxiety + safeRoomAnxietyChange * Time.deltaTime, 0f, maxAnxiety);
            return;
        }

        // Por defecto, si el ambiente es normal, el jugador se recupera según su tasa base
        float anxietyChange = -baseRecoveryRate; 

        // Evaluamos fobias basadas en el cuarto
        if (activePhobias.Contains(PhobiaType.Claustrophobia))
        {
            // Cuartos con área pequeña (menor a 40 unidades cuadradas, por ejemplo 6x6 aprox)
            if (currentRoom.areaSize < 40f) 
                anxietyChange += 8f; 
        }

        if (activePhobias.Contains(PhobiaType.Nyctophobia))
        {
            // Cuartos oscuros
            if (currentRoom.illuminationLevel < 0.4f)
                anxietyChange += 10f;
        }

        if (activePhobias.Contains(PhobiaType.Agoraphobia))
        {
            // Cuartos muy grandes o con muchas puertas (sentimiento de exposición)
            if (currentRoom.areaSize > 150f || currentRoom.entranceCount >= 3)
                anxietyChange += 6f;
        }

        // Aplicamos el cambio
        currentAnxiety = Mathf.Clamp(currentAnxiety + anxietyChange * Time.deltaTime, 0f, maxAnxiety);
    }

    void CalculateProximityAnxiety()
    {
        // En un espacio seguro somos inmunes a la presencia de entidades
        if (currentRoom != null && currentRoom.archetype == RoomArchetype.SafeRoom) return;

        // Reservado para futuras fobias de proximidad (ej. entidades)
        
        currentAnxiety = Mathf.Clamp(currentAnxiety, 0f, maxAnxiety);
    }

    void CheckConsequences()
    {
        if (currentAnxiety >= criticalThreshold && !hasTriggeredConsequence)
        {
            hasTriggeredConsequence = true;
            TriggerPanicEvent();
        }
        else if (currentAnxiety < criticalThreshold - 30f)
        {
            // Si la ansiedad baja significativamente, el jugador puede volver a sufrir otro evento más adelante
            hasTriggeredConsequence = false;
        }

        if (currentAnxiety >= entitySpawnThreshold && !hasTriggeredEntity)
        {
            hasTriggeredEntity = true;
            SpawnChasingEntity();
        }
        else if (currentAnxiety < entitySpawnThreshold - 20f)
        {
            hasTriggeredEntity = false;
        }
    }

    void TriggerPanicEvent()
    {
        Debug.Log("¡ANSIEDAD CRÍTICA! Iniciando evento de pánico por fobias...");
        
        // Geometría no euclidiana / Alucinación
        // Cambiamos aleatoriamente un cuarto que el jugador ya visitó (no en el que está)
        if (cachedRooms == null || cachedRooms.Length == 0) cachedRooms = FindObjectsOfType<RoomData>();
        
        List<RoomData> visitedRooms = new List<RoomData>();
        foreach (var r in cachedRooms) {
            if (r.isVisited && r != currentRoom) visitedRooms.Add(r);
        }

        if (visitedRooms.Count > 0)
        {
            RoomData roomToCorrupt = visitedRooms[Random.Range(0, visitedRooms.Count)];
            CorruptRoom(roomToCorrupt);
        }
    }

    void SpawnChasingEntity()
    {
        if (entityPrefabToSpawn != null)
        {
            // Spawnear entidad en un punto "ciego" (detrás del jugador)
            Vector3 spawnPos = transform.position - transform.forward * 8f;
            
            // Ajustamos al Y de la sala
            spawnPos.y = currentRoom != null ? currentRoom.transform.position.y : transform.position.y; 
            
            Instantiate(entityPrefabToSpawn, spawnPos, Quaternion.identity);
            Debug.Log("¡Una entidad ha aparecido a tus espaldas debido al terror extremo!");
        }
    }

    void CorruptRoom(RoomData room)
    {
        Debug.Log("La paranoia altera tus recuerdos. La arquitectura de un cuarto anterior ha cambiado.");
        
        BoxCollider box = room.GetComponent<BoxCollider>();
        if (box != null)
        {
            // Alterar arquitectura: crear un muro negro gigante en medio del cuarto
            // que bloquee el paso y cambie la forma de navegarlo
            GameObject glitchWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glitchWall.transform.position = room.transform.position; 
            
            // Lo hacemos casi tan largo como la habitación, pero delgado
            bool horizontal = Random.value > 0.5f;
            if (horizontal) {
                glitchWall.transform.localScale = new Vector3(box.size.x * 0.9f, box.size.y, 2f);
            } else {
                glitchWall.transform.localScale = new Vector3(2f, box.size.y, box.size.z * 0.9f);
            }
            
            glitchWall.GetComponent<Renderer>().material.color = new Color(0.05f, 0f, 0f); // Casi negro oscuro
            
            // Generar algunos pilares aleatorios de distorsión
            int pillars = Random.Range(2, 6);
            for(int i = 0; i < pillars; i++)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Vector3 randomPos = room.transform.position + new Vector3(
                    Random.Range(-box.size.x * 0.4f, box.size.x * 0.4f),
                    0f,
                    Random.Range(-box.size.z * 0.4f, box.size.z * 0.4f)
                );
                pillar.transform.position = randomPos;
                pillar.transform.localScale = new Vector3(Random.Range(1f, 3f), box.size.y, Random.Range(1f, 3f));
                pillar.GetComponent<Renderer>().material.color = new Color(0.05f, 0f, 0f);
            }
            
            // --- NUEVO: Apagón / Parpadeo Agresivo ---
            if (room.geometryContainer != null)
            {
                Light[] lights = room.geometryContainer.GetComponentsInChildren<Light>();
                foreach (Light l in lights)
                {
                    FlickeringLight fl = l.GetComponent<FlickeringLight>();
                    if (fl == null) fl = l.gameObject.AddComponent<FlickeringLight>();
                    
                    if (Random.value > 0.5f)
                    {
                        fl.isCorrupted = true;
                    }
                    else
                    {
                        fl.ForceOff();
                    }
                }
            }
            
            // Cambiamos el estado interno
            room.illuminationLevel = 0f; 
        }
    }

    // --- SECUENCIA DE MUERTE ---
    IEnumerator DeathRoutine()
    {
        isDead = true;

        // 1. Desactivar movimiento y vista
        SimplePlayerController playerController = GetComponent<SimplePlayerController>();
        if (playerController != null) playerController.enabled = false;

        // (Se removió la desactivación del HUD de Ansiedad para mantener la viñeta original)

        // 2. Desaparecer al jugador y generar su cadáver en el piso (usando Sprite 2D)
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.enabled = false;
        }

        GameObject deadBody = new GameObject("Dead_player_visual");
        deadBody.transform.position = transform.position + Vector3.up * 0.05f; // Pegado al piso
        deadBody.transform.rotation = Quaternion.Euler(90, transform.eulerAngles.y, 0); // Acostado cara arriba
        // Ajustamos la escala (los sprites suelen ser más pequeños que un quad de 1x1 dependiendo del PPU)
        deadBody.transform.localScale = new Vector3(3f, 3f, 3f);
        
        if (deadPlayerSprite != null)
        {
            SpriteRenderer sr = deadBody.AddComponent<SpriteRenderer>();
            sr.sprite = deadPlayerSprite;
        }

        // Despegar la cámara del jugador para moverla a 3ra persona
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.SetParent(null);
        }

        // 3. Crear un Canvas completamente nuevo para el Fade a Negro y Texto de Muerte
        GameObject fadeCanvasObj = new GameObject("DeathCanvas");
        Canvas fadeCanvas = fadeCanvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 999; // Siempre por encima de la viñeta roja y HUD
        
        GameObject flashObj = new GameObject("BlackFade");
        flashObj.transform.SetParent(fadeCanvasObj.transform, false);
        Image flashImage = flashObj.AddComponent<Image>();
        flashImage.color = new Color(0f, 0f, 0f, 0f); // Empieza transparente
        RectTransform flashRT = flashImage.GetComponent<RectTransform>();
        flashRT.anchorMin = Vector2.zero;
        flashRT.anchorMax = Vector2.one;
        flashRT.offsetMin = Vector2.zero;
        flashRT.offsetMax = Vector2.zero;

        // Crear el texto de muerte dinámicamente
        GameObject textObj = new GameObject("DeathText");
        textObj.transform.SetParent(fadeCanvasObj.transform, false);
        TMPro.TextMeshProUGUI deathText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        deathText.text = "HAS MUERTO\n\nPresiona 'R' para reiniciar";
        deathText.alignment = TMPro.TextAlignmentOptions.Center;
        deathText.color = new Color(1f, 0.2f, 0.2f, 0f); // Rojo, pero empieza transparente
        deathText.fontSize = 40;
        RectTransform textRT = deathText.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        // 4. Mover la cámara a 3ra persona y oscurecer la pantalla
        mainCam = Camera.main;
        SimplePlayerController playerCtrl = GetComponent<SimplePlayerController>();
        if (mainCam == null && playerCtrl != null && playerCtrl.playerCamera != null)
        {
            mainCam = playerCtrl.playerCamera.GetComponent<Camera>();
        }

        if (mainCam != null)
        {
            Vector3 targetCamPos = transform.position + Vector3.up * 4f - transform.forward * 4f;
            float camMoveTime = 4.5f; // Transición mucho más lenta (4.5 segundos)
            float camElapsed = 0f;
            Vector3 startCamPos = mainCam.transform.position;

            while (camElapsed < camMoveTime)
            {
                camElapsed += Time.unscaledDeltaTime;
                float t = camElapsed / camMoveTime;
                mainCam.transform.position = Vector3.Lerp(startCamPos, targetCamPos, t);
                mainCam.transform.LookAt(transform.position);

                // Transición a negro semitransparente (alfa = 0.75f) para que se siga viendo Yamcha al fondo
                if (flashImage != null) 
                {
                    flashImage.color = Color.Lerp(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.75f), t);
                }
                
                // Mostrar texto a la misma velocidad suave
                if (deathText != null)
                {
                    deathText.color = new Color(1f, 0.2f, 0.2f, Mathf.Lerp(0f, 1f, t));
                }
                yield return null;
            }
        }

        // (El Canvas de derrota ya se creó dinámicamente arriba)

        // 5. Orbitar la cámara alrededor del jugador
        while (true)
        {
            if (mainCam != null)
            {
                mainCam.transform.RotateAround(transform.position, Vector3.up, 20f * Time.deltaTime);
                mainCam.transform.LookAt(transform.position);
            }

            // Reiniciar con R
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
            {
                Time.timeScale = 1f; // Restaurar por si acaso
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            yield return null;
        }
    }
}
