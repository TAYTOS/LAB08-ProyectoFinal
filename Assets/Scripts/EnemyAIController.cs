using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public enum EnemyState
{
    Patrol,
    Investigate,
    Chase,
    TraverseJumpLink
}

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAIController : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform player;

    [Header("Percepción y Detección (Line of Sight)")]
    public float detectionRadius = 14f;
    [Range(0f, 360f)]
    public float fieldOfViewAngle = 110f;
    public LayerMask obstacleMask = ~0; // Por defecto todo
    public float loseTargetTime = 4f;

    [Header("Patrulla y Waypoints")]
    public float patrolSpeed = 2.5f;
    public float chaseSpeed = 5.0f;
    public float waypointStoppingDistance = 1.0f;
    public float waitTimeAtWaypoint = 2.0f;
    public List<Transform> customWaypoints = new List<Transform>();
    private List<Vector3> patrolPoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private bool isWaitingAtPoint = false;

    [Header("Orientación Suave (Cuaterniones)")]
    public float rotationSpeed = 8f;

    [Header("Off-Mesh Links / Saltos")]
    public float jumpDuration = 0.8f;
    public float jumpHeight = 1.5f;
    private bool isTraversingLink = false;

    [Header("Estado Actual (Read Only)")]
    public EnemyState currentState = EnemyState.Patrol;

    private NavMeshAgent agent;
    private Vector3 lastKnownPlayerPos;
    private float timeSinceLastSeen = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // Tomamos el control manual de la rotación para usar Quaternion.Slerp suave
        agent.updateRotation = false;
        agent.autoTraverseOffMeshLink = false; // Manejamos saltos proceduralmente
    }

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else if (Camera.main != null) player = Camera.main.transform;
        }

        InicializarPuntosDePatrulla();
        CambiarEstado(EnemyState.Patrol);
    }

    void Update()
    {
        if (isTraversingLink) return;

        // 1. Revisar si estamos cruzando un NavMeshLink (Jump / Drop)
        if (agent.isOnOffMeshLink)
        {
            StartCoroutine(TraverseOffMeshLinkRoutine());
            return;
        }

        // 2. Comprobar Visión directa del jugador
        bool canSee = CanSeePlayer();
        if (canSee)
        {
            lastKnownPlayerPos = player.position;
            timeSinceLastSeen = 0f;
            if (currentState != EnemyState.Chase)
            {
                CambiarEstado(EnemyState.Chase);
            }
        }
        else
        {
            timeSinceLastSeen += Time.deltaTime;
        }

        // 3. Ejecutar lógica según el estado actual
        switch (currentState)
        {
            case EnemyState.Patrol:
                ActualizarPatrulla();
                break;
            case EnemyState.Chase:
                ActualizarPersecucion();
                break;
            case EnemyState.Investigate:
                ActualizarInvestigacion();
                break;
        }

        // 4. Giro suave con Cuaterniones (LookRotation + Slerp)
        ActualizarRotacionSuave();
    }

    void CambiarEstado(EnemyState nuevoEstado)
    {
        currentState = nuevoEstado;
        switch (currentState)
        {
            case EnemyState.Patrol:
                agent.speed = patrolSpeed;
                agent.stoppingDistance = waypointStoppingDistance;
                IrASiguienteWaypoint();
                break;

            case EnemyState.Chase:
                agent.speed = chaseSpeed;
                agent.stoppingDistance = 1.5f;
                isWaitingAtPoint = false;
                StopAllCoroutines();
                break;

            case EnemyState.Investigate:
                agent.speed = patrolSpeed * 1.2f;
                agent.stoppingDistance = 1.0f;
                agent.SetDestination(lastKnownPlayerPos);
                break;
        }
    }

    void ActualizarPatrulla()
    {
        if (patrolPoints.Count == 0)
        {
            // Fallback: Si no hay puntos generados, crear un punto aleatorio en NavMesh
            if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance)
            {
                Vector3 randomDirection = Random.insideUnitSphere * 15f + transform.position;
                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance && !isWaitingAtPoint)
        {
            StartCoroutine(WaitAtWaypointRoutine());
        }
    }

    IEnumerator WaitAtWaypointRoutine()
    {
        isWaitingAtPoint = true;
        yield return new WaitForSeconds(waitTimeAtWaypoint);
        currentWaypointIndex = (currentWaypointIndex + 1) % patrolPoints.Count;
        IrASiguienteWaypoint();
        isWaitingAtPoint = false;
    }

    void IrASiguienteWaypoint()
    {
        if (patrolPoints.Count == 0) return;
        agent.SetDestination(patrolPoints[currentWaypointIndex]);
    }

    void ActualizarPersecucion()
    {
        if (player != null)
        {
            agent.SetDestination(player.position);
        }

        // Si perdimos de vista al jugador por más tiempo que loseTargetTime
        if (timeSinceLastSeen > loseTargetTime)
        {
            CambiarEstado(EnemyState.Investigate);
        }
    }

    void ActualizarInvestigacion()
    {
        // Llegó a la última posición conocida
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            CambiarEstado(EnemyState.Patrol);
        }
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.2f;
        Vector3 targetPos = player.position + Vector3.up * 1.0f;
        Vector3 dirToTarget = targetPos - eyePos;
        float distToTarget = dirToTarget.magnitude;

        if (distToTarget > detectionRadius) return false;

        // Comprobación de ángulo de visión (FOV)
        float angle = Vector3.Angle(transform.forward, dirToTarget.normalized);
        if (angle <= fieldOfViewAngle * 0.5f)
        {
            // Raycast para verificar si no hay paredes entre enemigo y jugador
            if (Physics.Raycast(eyePos, dirToTarget.normalized, out RaycastHit hit, distToTarget, obstacleMask))
            {
                if (hit.transform.CompareTag("Player") || hit.transform == player || hit.transform.IsChildOf(player))
                {
                    return true;
                }
            }
        }
        return false;
    }

    void ActualizarRotacionSuave()
    {
        Vector3 dir = agent.desiredVelocity;
        dir.y = 0; // Mantener rotación en el plano horizontal

        if (dir.sqrMagnitude > 0.05f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    IEnumerator TraverseOffMeshLinkRoutine()
    {
        isTraversingLink = true;
        currentState = EnemyState.TraverseJumpLink;

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;
        Vector3 startPos = agent.transform.position;
        Vector3 endPos = linkData.endPos + Vector3.up * agent.baseOffset;

        float timer = 0f;
        while (timer < jumpDuration)
        {
            timer += Time.deltaTime;
            float t = timer / jumpDuration;

            // Parábola de salto: y = 4 * h * t * (1 - t)
            float verticalOffset = 4f * jumpHeight * t * (1f - t);
            Vector3 currentPos = Vector3.Lerp(startPos, endPos, t) + Vector3.up * verticalOffset;
            
            // Orientar hacia el destino del salto
            Vector3 jumpDir = (endPos - startPos);
            jumpDir.y = 0;
            if (jumpDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(jumpDir.normalized), Time.deltaTime * rotationSpeed);
            }

            transform.position = currentPos;
            yield return null;
        }

        agent.CompleteOffMeshLink();
        isTraversingLink = false;
        CambiarEstado(EnemyState.Patrol);
    }

    void InicializarPuntosDePatrulla()
    {
        patrolPoints.Clear();

        // 1. Añadir waypoints manuales si fueron asignados
        foreach (var wp in customWaypoints)
        {
            if (wp != null) patrolPoints.Add(wp.position);
        }

        // 2. Si no hay waypoints manuales, extraer automáticamente los centros de las habitaciones
        if (patrolPoints.Count == 0)
        {
            RoomData[] rooms = Object.FindObjectsByType<RoomData>(FindObjectsSortMode.None);
            foreach (var r in rooms)
            {
                if (r != null)
                {
                    patrolPoints.Add(r.transform.position);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualizar rango de detección y FOV en el editor de Unity
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Vector3 fovLine1 = Quaternion.AngleAxis(fieldOfViewAngle * 0.5f, Vector3.up) * transform.forward * detectionRadius;
        Vector3 fovLine2 = Quaternion.AngleAxis(-fieldOfViewAngle * 0.5f, Vector3.up) * transform.forward * detectionRadius;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.2f, fovLine1);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.2f, fovLine2);

        // Visualizar waypoints
        Gizmos.color = Color.magenta;
        foreach (var p in patrolPoints)
        {
            Gizmos.DrawSphere(p, 0.4f);
        }
    }
}
