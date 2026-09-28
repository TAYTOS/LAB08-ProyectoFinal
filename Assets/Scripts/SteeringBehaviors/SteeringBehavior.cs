using UnityEngine;

[RequireComponent(typeof(SteeringAgent))]
public abstract class SteeringBehavior : MonoBehaviour
{
    [Tooltip("El peso (prioridad) de este comportamiento")]
    public float weight = 1f;

    protected SteeringAgent agent;

    protected virtual void Awake()
    {
        agent = GetComponent<SteeringAgent>();
    }

    // La función que todos los comportamientos deben implementar
    public abstract Vector3 CalculateForce();
}
