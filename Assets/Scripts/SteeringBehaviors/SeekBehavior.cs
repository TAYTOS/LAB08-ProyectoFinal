using UnityEngine;

public class SeekBehavior : SteeringBehavior
{
    public Transform target;
    [Tooltip("¿Usar Arrive para frenar al acercarse?")]
    public bool useArrive = true;
    public float slowRadius = 4f;
    public float stopRadius = 0.5f;

    protected override void Awake()
    {
        base.Awake();
        
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else if (Camera.main != null) target = Camera.main.transform;
        }
    }

    public override Vector3 CalculateForce()
    {
        if (target == null) return Vector3.zero;

        Vector3 offset = target.position - transform.position;
        float dist = offset.magnitude;
        
        if (dist < stopRadius)
        {
            return -agent.velocity; // Fuerza opuesta para detenerse en seco
        }
        
        float speed = agent.maxSpeed;
        if (useArrive && dist < slowRadius)
        {
            speed = agent.maxSpeed * (dist / slowRadius);
        }
        
        Vector3 desired = offset.normalized * speed;
        return desired - agent.velocity;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (useArrive)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, slowRadius);
        }
    }
}
