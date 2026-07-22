using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    [Header("UI Panel de Pausa")]
    [Tooltip("El GameObject del panel de pausa que contiene los botones y controles de UI.")]
    public GameObject panelPausa;

    [Header("Controles de Sonido")]
    [Tooltip("Slider de volumen general (opcional).")]
    public Slider sliderVolumen;

    [Header("Navegación de Escenas")]
    [Tooltip("Nombre exacto de la escena del Menú Principal.")]
    public string nombreEscenaMenu = "MainMenu";

    public bool juegoPausado { get; private set; } = false;

    void Awake()
    {
        // Si no se asignó en el Inspector, intentar usar este mismo GameObject
        if (panelPausa == null)
        {
            panelPausa = gameObject;
        }

        // Ocultar de inmediato el panel en Awake antes de que la escena se renderice
        if (panelPausa != null)
        {
            panelPausa.SetActive(false);
        }

        // Asegurar tiempo normal al iniciar
        Time.timeScale = 1f;
        juegoPausado = false;
    }

    void Start()
    {
        // Cargar y aplicar volumen guardado previamente o poner 1 por defecto
        float volumenGuardado = PlayerPrefs.GetFloat("VolumenGeneral", 1f);
        AudioListener.volume = volumenGuardado;

        if (sliderVolumen != null)
        {
            sliderVolumen.value = volumenGuardado;
            sliderVolumen.onValueChanged.AddListener(CambiarVolumenGeneral);
        }
    }

    void Update()
    {
        // Detectar presionado de la tecla Escape usando el Nuevo Sistema de Input
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (juegoPausado)
            {
                Reanudar();
            }
            else
            {
                Pausar();
            }
        }
    }

    public void Pausar()
    {
        juegoPausado = true;
        if (panelPausa != null) panelPausa.SetActive(true);

        Time.timeScale = 0f; // Pausa la física y el tiempo del juego

        // Liberar el cursor para interactuar con el menú
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Reanudar()
    {
        juegoPausado = false;
        if (panelPausa != null) panelPausa.SetActive(false);

        Time.timeScale = 1f; // Reanudar el tiempo del juego

        // Bloquear y ocultar el cursor para continuar jugando
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Cambiar volumen desde un Slider (0 a 1)
    public void CambiarVolumenGeneral(float volumen)
    {
        AudioListener.volume = Mathf.Clamp01(volumen);
        PlayerPrefs.SetFloat("VolumenGeneral", AudioListener.volume);
        PlayerPrefs.Save();

        if (sliderVolumen != null && sliderVolumen.value != AudioListener.volume)
        {
            sliderVolumen.value = AudioListener.volume;
        }
    }

    // Botón Aumentar Sonido (+10%)
    public void AumentarSonido()
    {
        float nuevoVolumen = Mathf.Clamp01(AudioListener.volume + 0.1f);
        CambiarVolumenGeneral(nuevoVolumen);
    }

    // Botón Bajar Sonido (-10%)
    public void BajarSonido()
    {
        float nuevoVolumen = Mathf.Clamp01(AudioListener.volume - 0.1f);
        CambiarVolumenGeneral(nuevoVolumen);
    }

    // Volver al Menú Principal
    public void VolverAlMenuPrincipal()
    {
        Time.timeScale = 1f; // IMPORTANTE: Restaurar tiempo antes de cambiar de escena
        SceneManager.LoadScene(nombreEscenaMenu);
    }
}
