using UnityEngine;

public class UnalignedCollisionAvoidanceBehavior : SteeringBehavior
{
    public float predictionTime = 2f;
    public float avoidanceRadius = 2f;
    public string agentTag = "Enemy";

    public override Vector3 CalculateForce()
    {
        GameObject[] otherAgents = GameObject.FindGameObjectsWithTag(agentTag);
        
        float shortestTime = float.MaxValue;
        GameObject firstTarget = null;
        float firstMinSeparation = 0;
        float firstDistance = 0;
        Vector3 firstRelativePos = Vector3.zero;
        Vector3 firstRelativeVel = Vector3.zero;

        // 1. Encontrar la colisión más inminente
        foreach (GameObject other in otherAgents)
        {
            if (other == gameObject) continue;
            
            SteeringAgent otherAgent = other.GetComponent<SteeringAgent>();
            if (otherAgent == null) continue;

            Vector3 relativePos = other.transform.position - transform.position;
            Vector3 relativeVel = otherAgent.velocity - agent.velocity;
            
            float relativeSpeed = relativeVel.magnitude;
            if (relativeSpeed == 0) continue;

            // Tiempo hasta el punto más cercano de aproximación
            float timeToCollision = -Vector3.Dot(relativePos, relativeVel) / (relativeSpeed * relativeSpeed);
            
            if (timeToCollision <= 0 || timeToCollision > predictionTime) continue;

            // Distancia en ese punto futuro
            float distance = relativePos.magnitude;
            float minSeparation = distance - relativeSpeed * timeToCollision;
            
            if (minSeparation > avoidanceRadius) continue;

            if (timeToCollision > 0 && timeToCollision < shortestTime)
            {
                shortestTime = timeToCollision;
                firstTarget = other;
                firstMinSeparation = minSeparation;
                firstDistance = distance;
                firstRelativePos = relativePos;
                firstRelativeVel = relativeVel;
            }
        }

        // 2. Calcular fuerza de evasión predictiva
        if (firstTarget != null)
        {
            Vector3 evasionDirection;
            if (firstMinSeparation <= 0 || firstDistance < avoidanceRadius)
            {
                // Si ya estamos muy cerca, simplemente separarse
                evasionDirection = transform.position - firstTarget.transform.position;
            }
            else
            {
                // Evadir la posición futura calculada
                Vector3 futureRelativePos = firstRelativePos + firstRelativeVel * shortestTime;
                evasionDirection = -futureRelativePos;
            }

            Vector3 desired = evasionDirection.normalized * agent.maxSpeed;
            return desired - agent.velocity;
        }

        return Vector3.zero;
    }
}
