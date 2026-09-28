using UnityEngine;
using System.Collections.Generic;

public class SeparationBehavior : SteeringBehavior
{
    public float separationRadius = 2f;
    public string tagToSeparate = "Enemy";

    public override Vector3 CalculateForce()
    {
        Vector3 force = Vector3.zero;
        int neighborCount = 0;
        
        // Para mayor rendimiento en un juego final, usa Physics.OverlapSphereNonAlloc
        GameObject[] neighbors = GameObject.FindGameObjectsWithTag(tagToSeparate);
        
        foreach (GameObject neighbor in neighbors)
        {
            if (neighbor != gameObject)
            {
                Vector3 toNeighbor = transform.position - neighbor.transform.position;
                float dist = toNeighbor.magnitude;
                
                if (dist > 0 && dist < separationRadius)
                {
                    // La fuerza de separación aumenta cuanto más cerca están
                    Vector3 pushForce = toNeighbor.normalized / dist;
                    force += pushForce;
                    neighborCount++;
                }
            }
        }
        
        if (neighborCount > 0)
        {
            force /= neighborCount;
            force = force.normalized * agent.maxSpeed;
            return force - agent.velocity;
        }

        return Vector3.zero;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, separationRadius);
    }
}
