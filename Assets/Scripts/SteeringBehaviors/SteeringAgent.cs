using UnityEngine;
using System.Collections.Generic;

public class SteeringAgent : MonoBehaviour
{
    [Header("Agent Configuration")]
    public float maxSpeed = 5f;
    public float maxForce = 15f;
    public float mass = 1f;

    [Header("State (Read Only)")]
    public Vector3 velocity;
    
    private SteeringBehavior[] behaviors;

    void Start()
    {
        // Obtiene todos los comportamientos unidos al GameObject
        behaviors = GetComponents<SteeringBehavior>();
    }

    void Update()
    {
        Vector3 steeringForce = Vector3.zero;

        // 1. Recolectar y sumar las fuerzas
        foreach (SteeringBehavior behavior in behaviors)
        {
            if (behavior.enabled)
            {
                steeringForce += behavior.CalculateForce() * behavior.weight;
            }
        }

        // 2. Limitar la fuerza total
        steeringForce = Vector3.ClampMagnitude(steeringForce, maxForce);

        // 3. Aplicar aceleración (F = m * a)
        Vector3 acceleration = steeringForce / mass;

        // 4. Integrar velocidad y posición
        velocity += acceleration * Time.deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        
        transform.position += velocity * Time.deltaTime;

        // 5. Orientar el objeto hacia donde se mueve
        if (velocity.sqrMagnitude > 0.01f)
        {
            transform.forward = velocity.normalized;
        }
    }
}
