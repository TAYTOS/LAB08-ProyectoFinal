using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Se adjunta al mismo objeto que el RoomData (el Trigger de la habitación).
/// Administra qué entidades deben "despertar" cuando el jugador entra al área.
/// </summary>
[RequireComponent(typeof(RoomData))]
public class RoomEntityManager : MonoBehaviour
{
    private RoomData roomData;
    
    [Header("Configuración de Entidades")]
    public GameObject[] possibleEnemies;
    public int maxEnemiesInRoom = 1;
    
    // Lista de entidades que actualmente están vivas en ESTE cuarto
    private List<GameObject> spawnedEntities = new List<GameObject>();
    private bool isPlayerNearby = false;

    void Awake()
    {
        roomData = GetComponent<RoomData>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isPlayerNearby)
        {
            isPlayerNearby = true;
            ActivateRoom();
            
            // Opcional: Si quieres que el monstruo te persiga desde lejos, 
            // puedes llamar a un método que active las habitaciones adyacentes también:
            // ActivarHabitacionesVecinas();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && isPlayerNearby)
        {
            isPlayerNearby = false;
            // Cuando el jugador sale, devolvemos las entidades al Pool para ahorrar recursos.
            DeactivateRoom();
        }
    }

    private void ActivateRoom()
    {
        if (EntityManager.Instance == null || possibleEnemies == null || possibleEnemies.Length == 0) return;
        if (spawnedEntities.Count > 0) return; // Ya hay entidades activas

        // Lógica de juego: En los Backrooms, quizá solo queremos enemigos en cuartos oscuros o batofóbicos
        if (roomData.archetype == RoomArchetype.Dark || roomData.archetype == RoomArchetype.Batophobic)
        {
            int toSpawn = Random.Range(0, maxEnemiesInRoom + 1); // Puede que spawnee 0 (miedo a lo desconocido)
            
            for (int i = 0; i < toSpawn; i++)
            {
                GameObject prefab = possibleEnemies[Random.Range(0, possibleEnemies.Length)];
                
                // Variar un poco la posición dentro de los límites del cuarto
                Vector3 spawnPos = transform.position + new Vector3(Random.Range(-2f, 2f), 1f, Random.Range(-2f, 2f));
                
                // Pedimos al Manager Global que nos preste una entidad
                GameObject entity = EntityManager.Instance.SpawnEntity(prefab, spawnPos, Quaternion.identity);
                spawnedEntities.Add(entity);
            }
        }
    }

    private void DeactivateRoom()
    {
        if (EntityManager.Instance == null) return;

        // Limpiar el cuarto (mandar todo a dormir)
        foreach (GameObject entity in spawnedEntities)
        {
            if (entity != null)
            {
                EntityManager.Instance.DespawnEntity(entity);
            }
        }
        spawnedEntities.Clear();
    }
}
