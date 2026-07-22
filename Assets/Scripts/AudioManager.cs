using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instancia;

    [Header("Música de Fondo")]
    [Tooltip("Clip de audio para la música ambiental de fondo.")]
    public AudioClip musicaFondoClip;
    [Range(0f, 1f)]
    public float volumenMusicaFondo = 0.5f;

    [Header("Efecto de Latidos por Ansiedad")]
    [Tooltip("Clip de audio con los latidos del corazón del jugador.")]
    public AudioClip latidosClip;
    [Tooltip("Nivel de ansiedad (0-100) en el que comienzan a escucharse los latidos.")]
    public float umbralLatidos = 30f;
    [Range(0f, 1f)]
    public float volumenMaxLatidos = 0.8f;
    [Range(0.8f, 2.0f)]
    public float pitchMinLatidos = 0.9f;
    [Range(0.8f, 2.5f)]
    public float pitchMaxLatidos = 1.6f;

    [Header("Referencias")]
    public AnxietyManager anxietyManager;

    private AudioSource bgmSource;
    private AudioSource latidosSource;

    void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
        }
        else if (Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Si no se asignó AnxietyManager, buscarlo en la escena
        if (anxietyManager == null)
        {
            anxietyManager = FindObjectOfType<AnxietyManager>();
        }

        SetupAudioSources();
    }

    void SetupAudioSources()
    {
        // 1. Configurar AudioSource para la Música de Fondo
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.clip = musicaFondoClip;
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = volumenMusicaFondo;

        if (musicaFondoClip != null)
        {
            bgmSource.Play();
        }

        // 2. Configurar AudioSource para los Latidos
        latidosSource = gameObject.AddComponent<AudioSource>();
        latidosSource.clip = latidosClip;
        latidosSource.loop = true;
        latidosSource.playOnAwake = false;
        latidosSource.volume = 0f;
        latidosSource.pitch = pitchMinLatidos;
    }

    void Update()
    {
        // Si no se encontró AnxietyManager durante el Start, reintentar encontrarlo
        if (anxietyManager == null)
        {
            anxietyManager = FindObjectOfType<AnxietyManager>();
            if (anxietyManager == null) return;
        }

        ActualizarLatidosPorAnsiedad();
    }

    void ActualizarLatidosPorAnsiedad()
    {
        float anxiety = anxietyManager.currentAnxiety;
        float maxAnxiety = anxietyManager.maxAnxiety;

        if (anxiety >= umbralLatidos)
        {
            // Iniciar reproducción si no está sonando
            if (!latidosSource.isPlaying && latidosClip != null)
            {
                latidosSource.Play();
            }

            // Normalizar intensidad entre umbral (0.0) y ansiedad máxima (1.0)
            float t = Mathf.InverseLerp(umbralLatidos, maxAnxiety, anxiety);

            // Calcular volumen y pitch objetivo
            float targetVolume = Mathf.Lerp(0.1f, volumenMaxLatidos, t);
            float targetPitch = Mathf.Lerp(pitchMinLatidos, pitchMaxLatidos, t);

            // Suavizar la transición del audio
            latidosSource.volume = Mathf.Lerp(latidosSource.volume, targetVolume, Time.deltaTime * 3f);
            latidosSource.pitch = Mathf.Lerp(latidosSource.pitch, targetPitch, Time.deltaTime * 3f);
        }
        else
        {
            // Reducir suavemente el volumen si la ansiedad cae por debajo del umbral
            if (latidosSource.isPlaying)
            {
                latidosSource.volume = Mathf.Lerp(latidosSource.volume, 0f, Time.deltaTime * 3f);
                if (latidosSource.volume <= 0.01f)
                {
                    latidosSource.Stop();
                }
            }
        }
    }

    // Métodos públicos auxiliares
    public void CambiarMusicaFondo(AudioClip nuevaMusica)
    {
        musicaFondoClip = nuevaMusica;
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = nuevaMusica;
            if (nuevaMusica != null)
            {
                bgmSource.Play();
            }
        }
    }
}
