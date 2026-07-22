using UnityEngine;

public class Keynote : MonoBehaviour
{
    [Header("Configuración de Misión")]
    [Tooltip("El Tag del objeto que el jugador debe encontrar (Ej: 'Hammer', 'Prop')")]
    public string requiredObjectTag = "Prop";
    
    [Tooltip("Mensaje que aparecerá en pantalla al interactuar")]
    public string missionText = "Misión: Encuentra un objeto perdido.";
    
    [Tooltip("Mensaje al completar")]
    public string completionText = "¡Misión completada!";
}
