using UnityEngine;

public abstract class BaseFlowField : MonoBehaviour
{
    public abstract Vector3 GetDirectionAtPosition(Vector3 position);
}
