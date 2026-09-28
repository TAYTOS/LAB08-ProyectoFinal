using UnityEngine;

public class EvasionBehavior : SteeringBehavior
{
    public Transform target;
    [Tooltip("El radio dentro del cual empezamos a evadir")]
    public float evasionRadius = 15f; 
    
    private Vector3 targetLastPos;

    protected override void Awake()
    {
        base.Awake();
        
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else if (Camera.main != null) target = Camera.main.transform;
        }

        if (target != null) targetLastPos = target.position;
    }

    public override Vector3 CalculateForce()
    {
        if (target == null) return Vector3.zero;

        Vector3 toTarget = target.position - transform.position;
        float distance = toTarget.magnitude;

        // Si el objetivo está muy lejos, no hay necesidad de evadir
        if (distance > evasionRadius) return Vector3.zero;

        // Estimar velocidad del objetivo
        Vector3 targetVelocity = (target.position - targetLastPos) / Time.deltaTime;
        targetLastPos = target.position;

        // Limitar la predicción
        float lookAheadTime = distance / agent.maxSpeed;
        Vector3 futurePosition = target.position + targetVelocity * lookAheadTime;

        // Flee (huir) desde la posición futura
        Vector3 desired = (transform.position - futurePosition).normalized * agent.maxSpeed;
        return desired - agent.velocity;
    }
}
