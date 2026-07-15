using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.UI;

[System.Serializable]
public class EtapaNivel
{
    [Header("1. Interruptor de esta etapa")]
    [Tooltip("Arrastra aquí el GameObject del interruptor que el jugador debe presionar.")]
    public Transform interruptor;

    [Header("2. Luces a encender")]
    [Tooltip("Arrastra aquí las luces específicas para este interruptor.")]
    public Light[] luces;

    [Header("3. Objeto Oculto")]
    [Tooltip("Arrastra aquí el objeto a encontrar (Botiquín, Muñeca, Oso, etc.).")]
    public GameObject objetoOculto;
    
    [Header("4. Feedback de éxito (Opcional)")]
    [Tooltip("Un GameObject (ej. un Texto en la UI) que aparecerá temporalmente diciendo '¡Encontrado!'.")]
    public GameObject mensajeCompletado;

    [Header("5. Etiqueta / Tag (Opcional)")]
    [Tooltip("Si usaste un Tag (ej. 'Botiquin') ponlo aquí. Ayuda a detectarlo mejor.")]
    public string tagObjeto = "";
}

public class InteraccionLuz : MonoBehaviour
{
    [Header("Secuencia de Objetivos")]
    public EtapaNivel[] etapas;

    [Header("Conexión al GameManager")]
    public ControladorNivel controlador;

    [Header("Configuración Automática")]
    [Tooltip("Si marcas esto, se creará un panel automáticamente listando los objetos a la derecha.")]
    public bool generarListaUI = true;

    public float distanciaInteraccion = 3f;

    private int etapaActual = 0;
    private Text textoObjetivoActualUI; // Referencia al único texto de la UI

    void Start()
    {
        // Crear la interfaz automáticamente si está marcado
        if (generarListaUI && etapas.Length > 0)
        {
            CrearInterfazObjetivos();
            ActualizarTextoUI();
        }

        // Apaga todas las luces, objetos e interruptores futuros
        for (int i = 0; i < etapas.Length; i++)
        {
            var etapa = etapas[i];

            // 1. Ocultar interruptores futuros (solo se ve el de la etapa 0)
            if (etapa.interruptor != null)
            {
                etapa.interruptor.gameObject.SetActive(i == 0);
            }

            // 2. Apagar objeto oculto y asegurarse de que tenga Collider
            if (etapa.objetoOculto != null)
            {
                etapa.objetoOculto.SetActive(false);
                
                // Agregamos un MeshCollider automáticamente para que el raycast sea preciso al modelo 3D
                if (etapa.objetoOculto.GetComponentInChildren<Collider>() == null)
                {
                    MeshRenderer[] renderers = etapa.objetoOculto.GetComponentsInChildren<MeshRenderer>();
                    foreach (var rend in renderers)
                    {
                        rend.gameObject.AddComponent<MeshCollider>();
                    }

                    SkinnedMeshRenderer[] skinnedRenderers = etapa.objetoOculto.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var sRend in skinnedRenderers)
                    {
                        MeshCollider mc = sRend.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = sRend.sharedMesh;
                    }

                    // Si por algún motivo no tiene mallas, le ponemos uno básico al objeto padre
                    if (renderers.Length == 0 && skinnedRenderers.Length == 0)
                    {
                        etapa.objetoOculto.AddComponent<BoxCollider>();
                    }
                }
            }

            // 3. Apagar mensajes de éxito
            if (etapa.mensajeCompletado != null) etapa.mensajeCompletado.SetActive(false);
            
            // 4. Apagar las luces
            foreach (var luz in etapa.luces)
            {
                if (luz != null) luz.enabled = false;
            }
        }
    }

    void Update()
    {
        if (Keyboard.current == null || etapaActual >= etapas.Length) return;

        EtapaNivel etapa = etapas[etapaActual];

        // --- PARTE 1: INTERRUPTOR ---
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, distanciaInteraccion))
            {
                if (hit.transform == etapa.interruptor)
                {
                    foreach (var luz in etapa.luces)
                    {
                        if (luz != null) luz.enabled = !luz.enabled;
                    }
                    
                    if (etapa.objetoOculto != null)
                    {
                        etapa.objetoOculto.SetActive(!etapa.objetoOculto.activeSelf);
                    }
                }
            }
        }

        // --- PARTE 2: PROGRESO ---
        if (etapa.objetoOculto != null && etapa.objetoOculto.activeSelf)
        {
            bool algunaLuzEncendida = false;
            foreach (var luz in etapa.luces)
            {
                if (luz != null && luz.enabled)
                {
                    algunaLuzEncendida = true;
                    break;
                }
            }

            if (algunaLuzEncendida)
            {
                RaycastHit hitVision;
                if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hitVision, distanciaInteraccion))
                {
                    bool detectadoPorTransform = hitVision.transform.IsChildOf(etapa.objetoOculto.transform) || hitVision.transform.gameObject == etapa.objetoOculto;
                    bool detectadoPorTag = (!string.IsNullOrEmpty(etapa.tagObjeto) && hitVision.transform.CompareTag(etapa.tagObjeto));

                    if (detectadoPorTransform || detectadoPorTag)
                    {
                        Debug.Log("¡Objeto de la etapa " + etapaActual + " encontrado!");
                        
                        // Mostrar mensaje de feedback
                        if (etapa.mensajeCompletado != null)
                        {
                            StartCoroutine(MostrarMensajeTemporal(etapa.mensajeCompletado));
                        }
                        
                        etapaActual++;

                        // Hacer aparecer el interruptor de la SIGUIENTE etapa
                        if (etapaActual < etapas.Length && etapas[etapaActual].interruptor != null)
                        {
                            etapas[etapaActual].interruptor.gameObject.SetActive(true);
                        }

                        // Actualizar UI para el siguiente objetivo
                        if (generarListaUI)
                        {
                            ActualizarTextoUI();
                        }

                        if (etapaActual >= etapas.Length)
                        {
                            if (controlador != null && !controlador.juegoTerminado)
                            {
                                controlador.GanarJuego();
                            }
                        }
                    }
                }
            }
        }
    }

    private IEnumerator MostrarMensajeTemporal(GameObject mensaje)
    {
        mensaje.SetActive(true);
        yield return new WaitForSeconds(7f); // El mensaje dura 7 segundos
        mensaje.SetActive(false);
    }

    private void ActualizarTextoUI()
    {
        if (textoObjetivoActualUI == null) return;

        if (etapaActual < etapas.Length)
        {
            string nombreObjeto = "Objeto Oculto";
            if (!string.IsNullOrEmpty(etapas[etapaActual].tagObjeto))
                nombreObjeto = etapas[etapaActual].tagObjeto;
            else if (etapas[etapaActual].objetoOculto != null)
                nombreObjeto = etapas[etapaActual].objetoOculto.name;

            textoObjetivoActualUI.text = "Misión: Encuentra el " + nombreObjeto;
        }
        else
        {
            textoObjetivoActualUI.color = Color.green;
            textoObjetivoActualUI.text = "¡Todas las misiones completadas!";
        }
    }

    // --- FUNCIÓN QUE CREA EL CANVAS Y EL PANEL AUTOMÁTICAMENTE ---
    private void CrearInterfazObjetivos()
    {
        // 1. Crear Canvas
        GameObject canvasObj = new GameObject("Canvas_Objetivos_Generado");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Crear Panel de Fondo pequeño arriba a la derecha
        GameObject panelObj = new GameObject("Panel_Objetivos");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f); // Negro semitransparente

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 1); // Anclado arriba a la derecha
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.anchoredPosition = new Vector2(-20, -20); // Separado 20px de la esquina
        panelRect.sizeDelta = new Vector2(300, 50); // Mucho más pequeño y menos invasivo

        // Intentar usar fuente Legacy o Arial
        Font fontUsada = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fontUsada == null) fontUsada = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 3. Crear Texto del Objetivo Actual
        GameObject itemObj = new GameObject("Texto_Objetivo_Unico");
        itemObj.transform.SetParent(panelObj.transform, false);
        textoObjetivoActualUI = itemObj.AddComponent<Text>();
        textoObjetivoActualUI.font = fontUsada;
        textoObjetivoActualUI.fontSize = 20;
        textoObjetivoActualUI.color = Color.yellow; 
        textoObjetivoActualUI.alignment = TextAnchor.MiddleCenter;
        
        RectTransform itemRect = itemObj.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 0);
        itemRect.anchorMax = new Vector2(1, 1);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.anchoredPosition = new Vector2(0, 0);
        itemRect.sizeDelta = new Vector2(-10, -10); // Margen interior
    }
}
