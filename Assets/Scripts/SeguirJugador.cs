using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SeguirJugador : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("A qué distancia se detendrá para no atravesar al jugador.")]
    public float distanciaFrenado = 1.5f;

    [Tooltip("Opcional: Si el gato está flotando, ajusta este valor en negativo para bajarlo.")]
    public float ajusteAltura = 0f;

    private Transform jugador;
    private NavMeshAgent agente;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        agente.stoppingDistance = distanciaFrenado;
        agente.baseOffset = ajusteAltura;
        
        // El NavMeshAgent ya controla la rotación, pero ajustamos un poco para suavidad
        agente.updateRotation = true;

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
            Debug.LogWarning("SeguirJugador: No se encontró al jugador.");
        }
    }

    void Update()
    {
        if (jugador == null || agente == null) return;

        // Actualizamos el destino del agente hacia el jugador
        agente.SetDestination(jugador.position);
    }
}
