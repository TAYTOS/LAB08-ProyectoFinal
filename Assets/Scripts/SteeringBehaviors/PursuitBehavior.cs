using UnityEngine;
public class PursuitBehavior : SteeringBehavior
{
    public Transform target;
    public float maximumPrediction = 1.5f;
    private Vector3 lastPosition;
    private Vector3 targetVelocity;
    private Transform trackedTarget;
    private int sampledFrame = -1;
    public void ResetPrediction()
    {
        trackedTarget = target;
        lastPosition = target != null ? target.position : Vector3.zero;
        targetVelocity = Vector3.zero;
        sampledFrame = -1;
    }
    void OnEnable() { ResetPrediction(); }
    public Vector3 PredictedTargetPosition()
    {
        if (target == null) return transform.position;
        if (trackedTarget != target) ResetPrediction();
        if (sampledFrame != Time.frameCount && Time.deltaTime > 0f)
        {
            targetVelocity = Vector3.ClampMagnitude((target.position - lastPosition) / Time.deltaTime, 12f);
            targetVelocity.y = 0f;
            lastPosition = target.position;
            sampledFrame = Time.frameCount;
        }
        float prediction = Mathf.Min(maximumPrediction,
            SteeringAgent.PlanarDistance(transform.position, target.position) / Mathf.Max(0.1f, agent.maxSpeed));
        return target.position + targetVelocity * prediction;
    }
    public override Vector3 CalculateForce()
    {
        if (target == null) return Vector3.zero;
        return agent.GetNavigableDesiredVelocity(PredictedTargetPosition(), 1.2f) - agent.velocity;
    }
}
