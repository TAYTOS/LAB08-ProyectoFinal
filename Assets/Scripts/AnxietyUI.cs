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
    
    [Header("Elementos Integrados (Generados Automáticamente si están vacíos)")]
    public TextMeshProUGUI textoFobias;
    public TextMeshProUGUI textoMision;
    public TextMeshProUGUI textoConteoKeynotes;

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

        // Arreglar posiciones de los textos para que no se traslapen con el minimapa (Forzar anclaje abajo a la izquierda)
        if (textoEstadoMental != null && textoPorcentaje != null)
        {
            RectTransform rtEstado = textoEstadoMental.GetComponent<RectTransform>();
            RectTransform rtPorcentaje = textoPorcentaje.GetComponent<RectTransform>();
            
            // Forzar a la esquina inferior izquierda
            rtEstado.anchorMin = new Vector2(0, 0);
            rtEstado.anchorMax = new Vector2(0, 0);
            rtEstado.pivot = new Vector2(0, 0);
            
            // Colocar el Estado Mental justo encima del texto de porcentaje
            rtEstado.anchoredPosition = new Vector2(rtPorcentaje.anchoredPosition.x, rtPorcentaje.anchoredPosition.y + 30f);
        }

        // Generar Texto de Fobias automáticamente si no existe
        if (textoFobias == null && textoEstadoMental != null)
        {
            GameObject fobiasObj = new GameObject("TextoFobias");
            fobiasObj.transform.SetParent(textoEstadoMental.transform.parent, false);
            textoFobias = fobiasObj.AddComponent<TextMeshProUGUI>();
            textoFobias.font = textoEstadoMental.font;
            textoFobias.fontSize = textoEstadoMental.fontSize * 0.7f;
            textoFobias.alignment = TextAlignmentOptions.BottomLeft;
            textoFobias.color = new Color(0.8f, 0.6f, 0.8f); // Morado tenue
            
            RectTransform rt = textoFobias.GetComponent<RectTransform>();
            RectTransform rtEstado = textoEstadoMental.GetComponent<RectTransform>();
            rt.anchorMin = rtEstado.anchorMin;
            rt.anchorMax = rtEstado.anchorMax;
            rt.pivot = rtEstado.pivot;
            // Colocar encima del Estado Mental
            rt.anchoredPosition = new Vector2(rtEstado.anchoredPosition.x, rtEstado.anchoredPosition.y + 35f);
        }

        // Generar Texto de Misión automáticamente si no existe
        if (textoMision == null && textoFobias != null)
        {
            GameObject misionObj = new GameObject("TextoMision");
            misionObj.transform.SetParent(textoFobias.transform.parent, false);
            textoMision = misionObj.AddComponent<TextMeshProUGUI>();
            textoMision.font = textoFobias.font;
            textoMision.fontSize = textoFobias.fontSize * 1.1f; // Un poquito más grande
            textoMision.alignment = TextAlignmentOptions.BottomLeft;
            
            RectTransform rt = textoMision.GetComponent<RectTransform>();
            RectTransform rtFobias = textoFobias.GetComponent<RectTransform>();
            rt.anchorMin = rtFobias.anchorMin;
            rt.anchorMax = rtFobias.anchorMax;
            rt.pivot = rtFobias.pivot;
            // Colocar encima del texto de Fobias
            rt.anchoredPosition = new Vector2(rtFobias.anchoredPosition.x, rtFobias.anchoredPosition.y + 30f);
        }

        // Generar Texto de Conteo de Keynotes automáticamente si no existe
        if (textoConteoKeynotes == null && textoMision != null)
        {
            GameObject conteoObj = new GameObject("TextoConteoKeynotes");
            conteoObj.transform.SetParent(textoMision.transform.parent, false);
            textoConteoKeynotes = conteoObj.AddComponent<TextMeshProUGUI>();
            textoConteoKeynotes.font = textoMision.font;
            textoConteoKeynotes.fontSize = textoMision.fontSize;
            textoConteoKeynotes.alignment = TextAlignmentOptions.BottomLeft;
            textoConteoKeynotes.color = new Color(0.9f, 0.9f, 0.9f);
            
            RectTransform rt = textoConteoKeynotes.GetComponent<RectTransform>();
            RectTransform rtMision = textoMision.GetComponent<RectTransform>();
            rt.anchorMin = rtMision.anchorMin;
            rt.anchorMax = rtMision.anchorMax;
            rt.pivot = rtMision.pivot;
            // Colocar encima del texto de Mision
            rt.anchoredPosition = new Vector2(rtMision.anchoredPosition.x, rtMision.anchoredPosition.y + 30f);
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

        // 5. Actualizar Texto de Fobias
        if (textoFobias != null && anxietyManager != null && anxietyManager.activePhobias.Count > 0)
        {
            string fobiasStr = string.Join(", ", anxietyManager.activePhobias);
            textoFobias.text = "Fobias Activas: " + fobiasStr;
            
            // Hacer que las fobias se pongan rojas si hay mucha ansiedad
            if (ansiedadReal >= anxietyManager.criticalThreshold)
            {
                textoFobias.color = new Color(1f, 0.3f, 0.3f); // Rojo alerta
            }
            else
            {
                textoFobias.color = new Color(0.8f, 0.6f, 0.8f); // Morado tenue
            }
        }

        // 5.5 Actualizar Texto de Mision (Leyendo del MissionManager)
        MissionManager missionManager = FindObjectOfType<MissionManager>();
        if (textoMision != null && missionManager != null)
        {
            if (missionManager.isMissionUIActive)
            {
                textoMision.enabled = true;
                textoMision.text = missionManager.currentMissionText;
                textoMision.color = missionManager.currentMissionColor;
            }
            else
            {
                textoMision.enabled = false;
            }

            // Actualizar conteo de keynotes
            if (textoConteoKeynotes != null && missionManager != null)
            {
                textoConteoKeynotes.text = "Keynotes: " + missionManager.collectedKeynotes + " / " + missionManager.totalKeynotes;
            }
        }

        // 6. Efecto de Viñeta Roja en Pantalla
        if (vinetaRojaPantalla != null)
        {
            // Usar una curva exponencial para que la viñeta no sea molesta en niveles bajos de ansiedad
            float curvaAnsiedad = Mathf.Pow(porcentajeNormalizado, 2.5f);

            // La transparencia (Alpha) y velocidad aumentan con la curva
            float velocidadParpadeo = Mathf.Lerp(0.5f, 5f, curvaAnsiedad);
            
            // Latido más orgánico usando valor absoluto del seno
            float pulsoVal = Mathf.Abs(Mathf.Sin(Time.time * velocidadParpadeo));

            // Menos opaco que antes para no cegar al jugador
            float alphaMin = curvaAnsiedad * 0.05f;
            float alphaMax = curvaAnsiedad * 0.5f; 
            float alphaFinal = Mathf.Lerp(alphaMin, alphaMax, pulsoVal);

            Color col = vinetaRojaPantalla.color;
            col.a = alphaFinal;
            vinetaRojaPantalla.color = col;
        }

        // Metodos extraídos

        // 6. Efecto de Latido/Pulso en el Icono del Corazón
        if (iconoCorazon != null)
        {
            float velocidadLatido = Mathf.Lerp(2f, 10f, porcentajeNormalizado);
            float latido = Mathf.Abs(Mathf.Sin(Time.time * velocidadLatido));
            float factorEscala = 1f + (latido * 0.3f * porcentajeNormalizado);
            iconoCorazon.localScale = escalaOriginalCorazon * factorEscala;
        }
    }

    private System.Collections.IEnumerator FadeOutVineta(float fadeDuration)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            vinetaRojaPantalla.color = new Color(vinetaRojaPantalla.color.r, vinetaRojaPantalla.color.g, vinetaRojaPantalla.color.b, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        vinetaRojaPantalla.color = new Color(vinetaRojaPantalla.color.r, vinetaRojaPantalla.color.g, vinetaRojaPantalla.color.b, 0f);
    }

    public void MostrarPantallaVictoria()
    {
        // Reemplazado por el MissionManager
    }
}
