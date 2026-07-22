using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnxietyUI : MonoBehaviour
{
    [Header("Referencias de Ansiedad")]
    public AnxietyManager anxietyManager;

    [Header("Elementos de UI")]
    [Tooltip("Slider o Barra visual que representa la ansiedad (0 a 1).")]
    public Slider barraAnsiedad;
    [Tooltip("Imagen del relleno de la barra (para cambiar color de verde a rojo).")]
    public Image imagenRellenoAnsiedad;
    [Tooltip("Texto para mostrar el porcentaje de ansiedad (ej. 'Ansiedad: 45%').")]
    public TextMeshProUGUI textoPorcentaje;
    [Tooltip("Texto para mostrar el estado mental (ej. 'Tranquilo', 'Ansioso', '¡PÁNICO!').")]
    public TextMeshProUGUI textoEstadoMental;

    [Header("Efecto de Pulso en Pantalla (Viñeta Roja)")]
    [Tooltip("Imagen de viñeta roja que cubre los bordes de la pantalla.")]
    public Image vinetaRojaPantalla;
    [Tooltip("Icono de corazón o pulso en la UI.")]
    public RectTransform iconoCorazon;

    [Header("Gradiente de Color de la Barra")]
    public Gradient gradienteColorAnsiedad;

    private float ansiedadVisualSuave = 0f;
    private Vector3 escalaOriginalCorazon = Vector3.one;

    void Start()
    {
        if (anxietyManager == null)
        {
            anxietyManager = FindObjectOfType<AnxietyManager>();
        }

        if (iconoCorazon != null)
        {
            escalaOriginalCorazon = iconoCorazon.localScale;
        }

        // Crear gradiente por defecto si no se configuró en el inspector
        if (gradienteColorAnsiedad == null || gradienteColorAnsiedad.colorKeys.Length == 0)
        {
            gradienteColorAnsiedad = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.8f, 0.3f), 0f);   // Verde (0% Ansiedad)
            colorKeys[1] = new GradientColorKey(new Color(0.9f, 0.7f, 0.1f), 0.5f);  // Amarillo (50% Ansiedad)
            colorKeys[2] = new GradientColorKey(new Color(0.9f, 0.1f, 0.1f), 1f);    // Rojo (100% Ansiedad)

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);

            gradienteColorAnsiedad.SetKeys(colorKeys, alphaKeys);
        }
    }

    void Update()
    {
        if (anxietyManager == null)
        {
            anxietyManager = FindObjectOfType<AnxietyManager>();
            if (anxietyManager == null) return;
        }

        ActualizarUIAnsiedad();
    }

    void ActualizarUIAnsiedad()
    {
        float ansiedadReal = anxietyManager.currentAnxiety;
        float ansiedadMax = anxietyManager.maxAnxiety;
        float porcentajeNormalizado = Mathf.Clamp01(ansiedadReal / ansiedadMax);

        // Interpolación suave para la barra visual
        ansiedadVisualSuave = Mathf.Lerp(ansiedadVisualSuave, porcentajeNormalizado, Time.deltaTime * 5f);

        // 1. Actualizar Barra de Ansiedad
        if (barraAnsiedad != null)
        {
            barraAnsiedad.value = ansiedadVisualSuave;
        }

        // 2. Actualizar Color de la Barra según la Ansiedad
        if (imagenRellenoAnsiedad != null)
        {
            imagenRellenoAnsiedad.color = gradienteColorAnsiedad.Evaluate(ansiedadVisualSuave);
        }

        // 3. Actualizar Texto del Porcentaje
        if (textoPorcentaje != null)
        {
            textoPorcentaje.text = $"Ansiedad: {Mathf.RoundToInt(ansiedadVisualSuave * 100f)}%";
        }

        // 4. Actualizar Estado Mental
        if (textoEstadoMental != null)
        {
            if (ansiedadReal >= anxietyManager.criticalThreshold)
            {
                textoEstadoMental.text = "¡PÁNICO CRÍTICO!";
                textoEstadoMental.color = new Color(1f, 0.2f, 0.2f);
            }
            else if (ansiedadReal >= 50f)
            {
                textoEstadoMental.text = "Ansiedad Severa";
                textoEstadoMental.color = new Color(1f, 0.5f, 0.1f);
            }
            else if (ansiedadReal >= 25f)
            {
                textoEstadoMental.text = "Inquieto";
                textoEstadoMental.color = new Color(0.9f, 0.8f, 0.2f);
            }
            else
            {
                textoEstadoMental.text = "Tranquilo";
                textoEstadoMental.color = new Color(0.3f, 0.9f, 0.4f);
            }
        }

        // 5. Efecto de Viñeta Roja en Pantalla
        if (vinetaRojaPantalla != null)
        {
            // La transparencia (Alpha) y velocidad aumentan con la ansiedad
            float velocidadParpadeo = Mathf.Lerp(1.5f, 8f, porcentajeNormalizado);
            float pulsoVal = (Mathf.Sin(Time.time * velocidadParpadeo) + 1f) * 0.5f;

            float alphaMin = porcentajeNormalizado * 0.2f;
            float alphaMax = porcentajeNormalizado * 0.7f;
            float alphaFinal = Mathf.Lerp(alphaMin, alphaMax, pulsoVal);

            Color col = vinetaRojaPantalla.color;
            col.a = alphaFinal;
            vinetaRojaPantalla.color = col;
        }

        // 6. Efecto de Latido/Pulso en el Icono del Corazón
        if (iconoCorazon != null)
        {
            float velocidadLatido = Mathf.Lerp(2f, 10f, porcentajeNormalizado);
            float latido = Mathf.Abs(Mathf.Sin(Time.time * velocidadLatido));
            float factorEscala = 1f + (latido * 0.3f * porcentajeNormalizado);
            iconoCorazon.localScale = escalaOriginalCorazon * factorEscala;
        }
    }
}
