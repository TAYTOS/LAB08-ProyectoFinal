using UnityEngine;

public class LuzWanderSeek : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    public float maxSpeed = 5f;
    public float maxForce = 15f;
    
    [Header("Radios de Detección (SEEK/ARRIVE)")]
    public float detectionRadius = 10f;
    public float slowRadius = 4f;
    
    [Header("Configuración de WANDER")]
    public float wanderDistance = 5f;
    public float wanderRadius = 3f;
    public float wanderJitter = 45f;
    
    private Vector3 velocity;
    private Vector3 acceleration;
    private float wanderAngle;
    
    private Transform player;
    
    public enum State { WANDER, SEEK }
    [Header("Estado Actual (Debug)")]
    public State currentState = State.WANDER;
    
    // Variables para Debug
    private Vector3 debugDesired;
    private Vector3 debugWanderTarget;

    void Start()
    {
        wanderAngle = Random.Range(0f, 360f);
        velocity = transform.forward * maxSpeed;
        
        // Buscar al jugador. Como es un juego en primera persona, la cámara principal o el objeto con tag "Player" sirven.
        if (Camera.main != null)
        {
            player = Camera.main.transform;
        }
        else
        {
            GameObject goPlayer = GameObject.FindGameObjectWithTag("Player");
            if (goPlayer != null) player = goPlayer.transform;
        }
        
        if (player == null)
        {
            Debug.LogWarning("LuzWanderSeek: No se encontró al jugador.");
        }
    }

    void Update()
    {
        if (player == null) return;
        
        float dt = Time.deltaTime;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if (distanceToPlayer <= detectionRadius)
        {
            currentState = State.SEEK;
            acceleration = SeekArrive(player.position);
        }
        else
        {
            currentState = State.WANDER;
            acceleration = Wander(dt);
        }
        
        // Integración de Euler (Velocity += Acc * dt; Pos += Vel * dt)
        velocity += acceleration * dt;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        
        transform.position += velocity * dt;
        
        // Rotar el objeto hacia la dirección de movimiento para que "mire" hacia donde va
        if (velocity.sqrMagnitude > 0.01f)
        {
            transform.forward = velocity.normalized;
        }
    }

    // Comportamiento de Perseguir (Seek) y Llegar (Arrive)
    Vector3 SeekArrive(Vector3 target)
    {
        Vector3 offset = target - transform.position;
        float dist = offset.magnitude;
        
        if (dist < 0.001f)
        {
            return -velocity; // Detenerse por completo
        }
        
        float speed = maxSpeed;
        if (dist < slowRadius) // Zona de frenado (Arrive)
        {
            speed = maxSpeed * (dist / slowRadius);
        }
        
        Vector3 desired = offset.normalized * speed;
        debugDesired = desired; // Guardar para Gizmos
        Vector3 steer = desired - velocity;
        return Vector3.ClampMagnitude(steer, maxForce);
    }
    
    // Comportamiento de Deambular (Wander)
    Vector3 Wander(float dt)
    {
        // Pequeño cambio aleatorio acumulativo
        wanderAngle += Random.Range(-wanderJitter, wanderJitter) * dt;
        
        Vector3 forward = velocity.sqrMagnitude > 0.1f ? velocity.normalized : transform.forward;
        Vector3 circleCenter = transform.position + forward * wanderDistance;
        
        // Calculamos el desplazamiento asumiendo un movimiento principal en el plano XZ (horizontal)
        Vector3 displacement = new Vector3(Mathf.Cos(wanderAngle * Mathf.Deg2Rad), 0, Mathf.Sin(wanderAngle * Mathf.Deg2Rad)) * wanderRadius;
        
        Vector3 wanderTarget = circleCenter + displacement;
        
        // Opcional: mantener la altura constante durante el wander
        wanderTarget.y = transform.position.y;
        
        debugWanderTarget = wanderTarget; // Guardar para Gizmos
        
        Vector3 desired = wanderTarget - transform.position;
        if (desired.sqrMagnitude > 0)
        {
            desired = desired.normalized * maxSpeed;
        }
        
        debugDesired = desired; // Guardar para Gizmos
        
        Vector3 steer = desired - velocity;
        return Vector3.ClampMagnitude(steer, maxForce);
    }

    // Dibujar Gizmos para visualizar los radios en el Editor de Unity (similar al debug en pygame)
    private void OnDrawGizmosSelected()
    {
        // Radio de Detección
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Radio de Frenado (Arrive)
        Gizmos.color = new Color(1f, 0.5f, 0f); // Naranja
        Gizmos.DrawWireSphere(transform.position, slowRadius);
        
        if (!Application.isPlaying) return;

        // Vector Velocity (Verde)
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + velocity);

        // Vector Desired (Rojo)
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + debugDesired);

        // Información de Wander
        if (currentState == State.WANDER)
        {
            Gizmos.color = Color.cyan;
            Vector3 forward = velocity.sqrMagnitude > 0.1f ? velocity.normalized : transform.forward;
            Vector3 circleCenter = transform.position + forward * wanderDistance;
            
            // Círculo de Wander
            Gizmos.DrawWireSphere(circleCenter, wanderRadius);
            
            // Punto objetivo de Wander y línea hacia él
            Gizmos.DrawSphere(debugWanderTarget, 0.2f);
            Gizmos.DrawLine(circleCenter, debugWanderTarget);
        }
    }
}
