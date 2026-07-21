using UnityEngine;

public enum PhobiaType
{
    Claustrophobia,  // Miedo a espacios cerrados/pequeños
    Agoraphobia,     // Miedo a espacios abiertos/grandes o con muchas salidas
    Nyctophobia,     // Miedo a la oscuridad
    Automatophobia,  // Miedo a figuras humanoides (ej. estatuas, entidades)
    Monophobia       // Miedo a estar solo/aislado de áreas conocidas
}

public class PhobiaTrigger : MonoBehaviour
{
    [Header("Configuración de Fobia")]
    public PhobiaType phobiaTag;
    
    [Tooltip("Radio de efecto en el cual la entidad/objeto afecta al jugador")]
    public float effectRadius = 10f;
    
    [Tooltip("Cantidad de ansiedad por segundo que genera estar cerca")]
    public float anxietyMultiplier = 15f;

    void OnDrawGizmosSelected()
    {
        // Dibuja una esfera roja en el editor para visualizar el radio de la fobia
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawSphere(transform.position, effectRadius);
    }
}
