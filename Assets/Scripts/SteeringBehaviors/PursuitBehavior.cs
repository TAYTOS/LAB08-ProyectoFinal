using UnityEngine;

public class PursuitBehavior : SteeringBehavior
{
    public Transform target;
    
    // Asumimos que target tiene un Rigidbody o guardamos su pos anterior
    private Vector3 targetLastPos;

    protected override void Awake()
    {
        base.Awake();
        
        // Si no se asignó un objetivo en el inspector (porque es prefab), buscamos al jugador automáticamente
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else if (Camera.main != null) target = Camera.main.transform; // Fallback
        }

        if (target != null) targetLastPos = target.position;
    }

    public override Vector3 CalculateForce()
    {
        if (target == null) return Vector3.zero;

        // Estimar velocidad
        Vector3 targetVelocity = (target.position - targetLastPos) / Time.deltaTime;
        targetLastPos = target.position;

        Vector3 toTarget = target.position - transform.position;
        float distance = toTarget.magnitude;

        // Limitar la predicción para que no apunte al infinito
        float lookAheadTime = distance / agent.maxSpeed;
        
        Vector3 futurePosition = target.position + targetVelocity * lookAheadTime;

        // Seek hacia futurePosition
        Vector3 desired = (futurePosition - transform.position).normalized * agent.maxSpeed;
        return desired - agent.velocity;
    }
}
