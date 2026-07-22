using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class MissionManager : MonoBehaviour
{
    [Header("Configuración de Interacción")]
    public float interactionDistance = 3f;
    
    [HideInInspector] public string currentMissionText = "";
    [HideInInspector] public Color currentMissionColor = Color.white;
    [HideInInspector] public bool isMissionUIActive = false;
    
    [HideInInspector] public int collectedKeynotes = 0;
    [HideInInspector] public int totalKeynotes = 0;

    private SimplePlayerController player;

    void Start()
    {
        player = FindObjectOfType<SimplePlayerController>();
        if (player == null) Debug.LogError("MissionManager: No se encontró al jugador.");
        
        // Esperamos 1 frame para asegurar que el LevelGenerator ya spawneó las keynotes
        StartCoroutine(InitKeynoteCount());
    }

    IEnumerator InitKeynoteCount()
    {
        yield return new WaitForEndOfFrame();
        ProceduralLevelGenerator generator = FindObjectOfType<ProceduralLevelGenerator>();
        if (generator != null)
        {
            totalKeynotes = generator.numberOfKeynotes;
        }
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // Usar la cámara del jugador explícitamente para evitar problemas con la cámara del minimapa
        if (player == null) player = FindObjectOfType<SimplePlayerController>();
        if (player == null || player.playerCamera == null) return;

        Transform camTransform = player.playerCamera;

        // Log cada cierto tiempo para asegurarnos de que el script sí está corriendo
        if (Time.frameCount % 120 == 0) Debug.Log("MissionManager está corriendo y apuntando desde: " + camTransform.name);

        RaycastHit hit;
        bool isLookingAtSomething = Physics.Raycast(camTransform.position, camTransform.forward, out hit, interactionDistance);

        // Dibujar el Raycast en la ventana de Escena (Scene View) para depuración
        if (isLookingAtSomething)
        {
            Debug.DrawRay(camTransform.position, camTransform.forward * hit.distance, Color.green);
        }
        else
        {
            Debug.DrawRay(camTransform.position, camTransform.forward * interactionDistance, Color.red);
        }

        // Recoger Keynote (E sobre Keynote)
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log("Tecla E presionada.");
            
            if (isLookingAtSomething)
            {
                Debug.Log("Mirando al objeto: " + hit.transform.name);
                
                Keynote keynote = hit.transform.GetComponent<Keynote>();
                if (keynote == null) keynote = hit.transform.GetComponentInParent<Keynote>();
                if (keynote == null) keynote = hit.transform.GetComponentInChildren<Keynote>(); // Por si acaso está en un hijo

                if (keynote != null)
                {
                    RecogerKeynote(keynote);
                }
                else
                {
                    Debug.LogWarning("El objeto mirado no tiene el script Keynote adjunto.");
                }
            }
        }
    }

    void RecogerKeynote(Keynote keynote)
    {
        collectedKeynotes++;
        
        currentMissionText = "RECOLECTADO: " + keynote.completionText;
        currentMissionColor = new Color(0.3f, 0.9f, 0.3f); // Verde
        isMissionUIActive = true;
        
        Debug.Log("Keynote recogida: " + keynote.completionText + " (" + collectedKeynotes + "/" + totalKeynotes + ")");
        
        // Destruir el keynote
        Destroy(keynote.gameObject);
        
        if (collectedKeynotes >= totalKeynotes && totalKeynotes > 0)
        {
            // Nivel Superado mostrado en la sección de misiones
            currentMissionText = "¡NIVEL " + ProceduralLevelGenerator.currentLevel + " SUPERADO! CARGANDO...";
            currentMissionColor = new Color(0.1f, 1f, 0.1f); // Verde brillante
            isMissionUIActive = true;
            
            // 1. Desactivar movimiento del jugador
            if (player != null) player.enabled = false;

            // Iniciar la rutina para cargar el siguiente nivel
            StopAllCoroutines();
            StartCoroutine(CargarSiguienteNivel());
        }
        else
        {
            // Reiniciar el contador de ocultamiento si recoge varios seguidos
            StopAllCoroutines();
            StartCoroutine(OcultarPanel());
        }
    }

    IEnumerator OcultarPanel()
    {
        yield return new WaitForSeconds(4f);
        isMissionUIActive = false;
    }

    IEnumerator CargarSiguienteNivel()
    {
        // Esperar unos segundos para que el jugador lea el mensaje
        yield return new WaitForSeconds(3f);

        // Avanzar de nivel y recargar escena
        ProceduralLevelGenerator.currentLevel++;
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneName);
    }
}
