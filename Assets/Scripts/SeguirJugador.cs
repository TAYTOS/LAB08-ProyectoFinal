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

    private EnemyAIController aiController;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        agente.stoppingDistance = distanciaFrenado;
        agente.baseOffset = ajusteAltura;

        // Si ya existe el controlador completo FSM, permitirle gobernar
        aiController = GetComponent<EnemyAIController>();

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
        // Si EnemyAIController está presente y activo, delega la lógica de navegación completa a él
        if (aiController != null && aiController.enabled) return;

        if (jugador == null || agente == null) return;

        // Fallback: seguimiento directo original
        agente.SetDestination(jugador.position);
    }
}
