using UnityEngine;
using UnityEngine.InputSystem;

public class InteraccionLuz : MonoBehaviour
{
    [Header("Arrastra aquí tu Point Light")]
    public Light focoInteractivo;
    
    [Header("Arrastra aquí tu Botiquín")]
    public GameObject botiquin;

    [Header("Arrastra aquí tu GameManager")]
    public ControladorNivel controlador; // Conexión segura

    public float distanciaInteraccion = 3f;

    void Start()
    {
        // Apaga la luz al iniciar
        if (focoInteractivo != null)
        {
            focoInteractivo.enabled = false;
        }

        // Oculta el botiquín al iniciar
        if (botiquin != null)
        {
            botiquin.SetActive(false); 
        }
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // --- PARTE 1: TU CÓDIGO ORIGINAL (Interruptor) ---
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, distanciaInteraccion))
            {
                if (hit.transform.CompareTag("Interruptor"))
                {
                    // Prende/apaga la luz
                    if (focoInteractivo != null)
                    {
                        focoInteractivo.enabled = !focoInteractivo.enabled;
                    }
                    
                    // Muestra/oculta el botiquín
                    if (botiquin != null)
                    {
                        botiquin.SetActive(!botiquin.activeSelf);
                    }
                }
            }
        }

        // --- PARTE 2: LÓGICA PARA GANAR EL JUEGO ---
        // Revisamos continuamente si estamos mirando el botiquín
        RaycastHit hitVision;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hitVision, distanciaInteraccion))
        {
            // Si el rayo choca con el botiquín, el botiquín está visible y la luz prendida
            if (hitVision.transform.CompareTag("Botiquin") && botiquin.activeSelf && focoInteractivo.enabled)
            {
                // Le avisamos al GameManager que ganamos (solo si está conectado y no hemos ganado antes)
                if (controlador != null && !controlador.juegoTerminado)
                {
                    controlador.GanarJuego();
                }
            }
        }
    }
}
