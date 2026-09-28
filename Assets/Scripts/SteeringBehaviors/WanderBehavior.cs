using UnityEngine;

public class WanderBehavior : SteeringBehavior
{
    public float wanderDistance = 5f;
    public float wanderRadius = 3f;
    public float wanderJitter = 45f;
    
    private float wanderAngle;
    
    protected override void Awake()
    {
        base.Awake();
        wanderAngle = Random.Range(0f, 360f);
    }

    public override Vector3 CalculateForce()
    {
        wanderAngle += Random.Range(-wanderJitter, wanderJitter) * Time.deltaTime;
        
        Vector3 forward = agent.velocity.sqrMagnitude > 0.1f ? agent.velocity.normalized : transform.forward;
        Vector3 circleCenter = transform.position + forward * wanderDistance;
        
        Vector3 displacement = new Vector3(Mathf.Cos(wanderAngle * Mathf.Deg2Rad), 0, Mathf.Sin(wanderAngle * Mathf.Deg2Rad)) * wanderRadius;
        
        Vector3 wanderTarget = circleCenter + displacement;
        wanderTarget.y = transform.position.y; // Mantener plano Y
        
        Vector3 desired = wanderTarget - transform.position;
        if (desired.sqrMagnitude > 0)
        {
            desired = desired.normalized * agent.maxSpeed;
        }
        
        return desired - agent.velocity;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || agent == null) return;
        
        Gizmos.color = Color.cyan;
        Vector3 forward = agent.velocity.sqrMagnitude > 0.1f ? agent.velocity.normalized : transform.forward;
        Vector3 circleCenter = transform.position + forward * wanderDistance;
        
        Gizmos.DrawWireSphere(circleCenter, wanderRadius);
        
        Vector3 displacement = new Vector3(Mathf.Cos(wanderAngle * Mathf.Deg2Rad), 0, Mathf.Sin(wanderAngle * Mathf.Deg2Rad)) * wanderRadius;
        Gizmos.DrawSphere(circleCenter + displacement, 0.2f);
        Gizmos.DrawLine(transform.position, circleCenter + displacement);
    }
}
