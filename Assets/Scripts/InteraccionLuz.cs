using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; // Necesario para Corrutinas

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

    public float distanciaInteraccion = 3f;

    private int etapaActual = 0;

    void Start()
    {
        // Apaga todas las luces y objetos
        foreach (var etapa in etapas)
        {
            if (etapa.objetoOculto != null) etapa.objetoOculto.SetActive(false);
            if (etapa.mensajeCompletado != null) etapa.mensajeCompletado.SetActive(false);
            
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
                    // Detecta si es el objeto, hijo del objeto, o si tiene el Tag correcto
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
        yield return new WaitForSeconds(3f); // El mensaje dura 3 segundos
        mensaje.SetActive(false);
    }
}
