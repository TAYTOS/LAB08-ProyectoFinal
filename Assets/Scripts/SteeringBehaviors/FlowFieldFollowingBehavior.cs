using UnityEngine;

// Simulación rápida de un campo de flujo. En un proyecto real esto sería una cuadrícula completa.
public class BaseFlowField : MonoBehaviour
{
    public virtual Vector3 GetDirectionAtPosition(Vector3 position)
    {
        // En un juego real, aquí calcularías en qué celda de la cuadrícula
        // cae la posición y devolverías el vector correspondiente.
        return Vector3.forward; 
    }
}

public class FlowFieldFollowingBehavior : SteeringBehavior
{
    public BaseFlowField flowField;

    public override Vector3 CalculateForce()
    {
        if (flowField == null) return Vector3.zero;

        // Consultar la dirección en el mapa
        Vector3 desiredDirection = flowField.GetDirectionAtPosition(transform.position);

        if (desiredDirection.sqrMagnitude > 0)
        {
            Vector3 desiredVelocity = desiredDirection.normalized * agent.maxSpeed;
            return desiredVelocity - agent.velocity;
        }

        return Vector3.zero;
    }
}
