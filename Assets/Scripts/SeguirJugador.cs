using UnityEngine;

public class SeguirJugador : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Qué tan rápido caminará hacia el jugador.")]
    public float velocidad = 2f;
    
    [Tooltip("A qué distancia se detendrá para no atravesar al jugador.")]
    public float distanciaFrenado = 1.5f;

    [Tooltip("Opcional: Si el gato está flotando, ajusta este valor en negativo para bajarlo.")]
    public float ajusteAltura = 0f;

    private Transform jugador;

    void Start()
    {
        // Busca al jugador usando la cámara principal, que suele ser hija del jugador en primera persona
        if (Camera.main != null)
        {
            jugador = Camera.main.transform; 
        }
        else
        {
            GameObject goJugador = GameObject.FindGameObjectWithTag("Player");
            if (goJugador != null) jugador = goJugador.transform;
        }
        
        if (jugador == null)
        {
            Debug.LogWarning("SeguirJugador: No se encontró al jugador. Asegúrate de tener un objeto con tag 'Player' o una Main Camera.");
        }
    }

    void Update()
    {
        if (jugador == null) return;

        // Calcular la posición objetivo ignorando la altura de la cámara para que el gato no mire hacia arriba ni vuele
        Vector3 posicionObjetivo = new Vector3(jugador.position.x, transform.position.y, jugador.position.z);
        float distancia = Vector3.Distance(transform.position, posicionObjetivo);

        if (distancia > distanciaFrenado)
        {
            // Girar suavemente hacia el jugador
            Vector3 direccion = (posicionObjetivo - transform.position).normalized;
            if (direccion != Vector3.zero)
            {
                Quaternion rotacionDeseada = Quaternion.LookRotation(direccion);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacionDeseada, Time.deltaTime * 5f);
            }
            
            // Moverse hacia adelante en dirección al jugador
            transform.position = Vector3.MoveTowards(transform.position, posicionObjetivo, velocidad * Time.deltaTime);
        }

        // Ajustar altura si es necesario (mantenerlo pegado al piso si se desvía)
        transform.position = new Vector3(transform.position.x, transform.position.y + ajusteAltura, transform.position.z);
    }
}
