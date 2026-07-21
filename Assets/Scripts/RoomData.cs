using UnityEngine;
using System.Collections.Generic;

public enum RoomArchetype
{
    Normal,
    Claustrophobic,
    Batophobic,
    SuperIlluminated,
    Dark,
    Empty,
    Cluttered,
    SafeRoom
}

public class RoomData : MonoBehaviour
{
    [Header("Arquetipo")]
    public RoomArchetype archetype = RoomArchetype.Normal;
    [Range(0f, 1f)]
    public float archetypeIntensity = 0f;

    [Header("Propiedades de la Habitación")]
    public float areaSize;
    public int entranceCount;
    [Range(0f, 1f)]
    public float illuminationLevel = 1.0f; // 1 = Brillante, 0 = Oscuridad total
    
    [Header("Estado del Jugador")]
    public bool isVisited = false;
    
    [Header("Optimización")]
    public GameObject geometryContainer;
    public bool isAlwaysRendered = false;
    
    [HideInInspector]
    public List<RoomData> adjacentRooms = new List<RoomData>();

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.name.Contains("Player") || other.name.Contains("Jugador"))
        {
            isVisited = true;
            // Si la habitación se visita, los cuartos adyacentes se consideran "conocidos"
        }
    }
}
