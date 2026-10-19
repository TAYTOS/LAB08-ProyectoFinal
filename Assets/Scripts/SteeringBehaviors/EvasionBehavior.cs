using UnityEngine;
public class EvasionBehavior : SteeringBehavior
{
    public Transform target;
    public float evasionRadius = 15f;
    public float maximumPrediction = 1f;
    private Vector3 lastPosition;
    private Vector3 targetVelocity;
    private Transform trackedTarget;
    public void ResetPrediction()
    {
        trackedTarget = target;
        lastPosition = target != null ? target.position : Vector3.zero;
        targetVelocity = Vector3.zero;
    }
    void OnEnable() { ResetPrediction(); }
    public override Vector3 CalculateForce()
    {
        if (target == null || Time.deltaTime <= 0f) return Vector3.zero;
        if (trackedTarget != target) ResetPrediction();
        targetVelocity = Vector3.ClampMagnitude((target.position - lastPosition) / Time.deltaTime, 12f);
        targetVelocity.y = 0f;
        lastPosition = target.position;
        float distance = SteeringAgent.PlanarDistance(transform.position, target.position);
        if (distance > evasionRadius) return Vector3.zero;
        Vector3 future = target.position + targetVelocity * Mathf.Min(maximumPrediction, distance / Mathf.Max(0.1f, agent.maxSpeed));
        Vector3 away = transform.position - future; away.y = 0f;
        if (away.sqrMagnitude < 0.001f) away = -target.forward;
        return agent.GetNavigableDesiredVelocity(transform.position + away.normalized * 5f) - agent.velocity;
    }
}
