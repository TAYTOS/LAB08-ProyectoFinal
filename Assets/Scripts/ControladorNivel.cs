using UnityEngine;
using TMPro;
using System.Collections;

public class ControladorNivel : MonoBehaviour
{
    [Header("Textos del Tutorial (GameObjects)")]
    public GameObject textoMision;
    public GameObject textoInstruccion;
    public GameObject textoVictoria;
    public GameObject textoDerrota;

    [Header("Reloj Dinámico")]
    public TextMeshProUGUI textoReloj;

    [Header("Configuración de Tiempo")]
    public float tiempoRestante = 30f;
    public float tiempoVisible = 5f; // para cada texto en pantalla

    public bool juegoTerminado { get; private set; } = false;

    void Start()
    {
        // 1. Apagamos todo por si acaso quedaron prendidos en Unity
        ApagarTodosLosTextos();
        
        // 2. Iniciamos tu secuencia de tutorial
        StartCoroutine(SecuenciaTextos());
    }

    void Update()
    {
        if (juegoTerminado) return;

        // Lógica del temporizador
        tiempoRestante -= Time.deltaTime;
        ActualizarReloj();

        if (tiempoRestante <= 0)
        {
            ApagarTodosLosTextos();
            if (textoDerrota != null) textoDerrota.SetActive(true);
            juegoTerminado = true;
        }
    }

    IEnumerator SecuenciaTextos()
    {
        // Mostramos el primer mensaje
        if (textoMision != null) textoMision.SetActive(true);
        yield return new WaitForSeconds(tiempoVisible);
        if (textoMision != null) textoMision.SetActive(false);

        // Mostramos el segundo mensaje
        if (textoInstruccion != null) textoInstruccion.SetActive(true);
        yield return new WaitForSeconds(tiempoVisible);
        if (textoInstruccion != null) textoInstruccion.SetActive(false);
    }

    void ActualizarReloj()
    {
        if (textoReloj != null)
        {
            int minutos = Mathf.FloorToInt(tiempoRestante / 60);
            int segundos = Mathf.FloorToInt(tiempoRestante % 60);
            textoReloj.text = string.Format("{0:00}:{1:00}", minutos, segundos);
        }
    }

    private void ApagarTodosLosTextos()
    {
        if (textoMision != null) textoMision.SetActive(false);
        if (textoInstruccion != null) textoInstruccion.SetActive(false);
        if (textoVictoria != null) textoVictoria.SetActive(false);
        if (textoDerrota != null) textoDerrota.SetActive(false);
    }

    // El script de interacción llamará aquí cuando veas el botiquín
    public void GanarJuego()
    {
        ApagarTodosLosTextos();
        if (textoVictoria != null) textoVictoria.SetActive(true);
        juegoTerminado = true;
    }
}