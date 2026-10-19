using UnityEngine;
public class FlowFieldFollowingBehavior : SteeringBehavior
{
    public BaseFlowField flowField;
    public override Vector3 CalculateForce()
    {
        if (flowField == null) return Vector3.zero;
        Vector3 direction = flowField.GetDirectionAtPosition(transform.position); direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return Vector3.zero;
        float arrival = Mathf.Clamp01(SteeringAgent.PlanarDistance(transform.position, agent.RouteGoal) / 1.4f);
        return direction.normalized * agent.maxSpeed * arrival - agent.velocity;
    }
}
