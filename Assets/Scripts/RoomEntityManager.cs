using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(RoomData))]
public class RoomEntityManager : MonoBehaviour
{
    public GameObject[] possibleEnemies;
    public int maxEnemiesInRoom = 1;
    public bool allowSpawn = true;
    public float sleepDelay = 8f;
    private RoomData roomData;
    private readonly List<GameObject> spawnedEntities = new List<GameObject>();
    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
    private float leftAt = -1f;
    private bool clearedByPlayer;

    void Awake() { roomData = GetComponent<RoomData>(); }

    void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return; // Ignorar el sensor de proximidad; cuenta la entrada del cuerpo.
        if (other.GetComponentInParent<SimplePlayerController>() == null) return;
        bool firstCollider = playerColliders.Count == 0;
        if (!playerColliders.Add(other)) return;
        leftAt = -1f;
        if (firstCollider)
        {
            AnxietyManager fear = other.GetComponentInParent<AnxietyManager>();
            if (fear != null) fear.RecordRoomEntry(roomData);
            ActivateRoom();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;
        if (!playerColliders.Remove(other)) return;
        if (playerColliders.Count == 0)
        {
            leftAt = Time.time;
            AnxietyManager fear = other.GetComponentInParent<AnxietyManager>();
            if (fear != null) fear.RecordRoomExit(roomData);
        }
    }

    void Update()
    {
        if (leftAt >= 0f && Time.time - leftAt >= sleepDelay)
        {
            DeactivateRoom();
            leftAt = -1f;
        }
    }

    public void ActivateRoom()
    {
        if (!allowSpawn || clearedByPlayer || roomData.archetype == RoomArchetype.SafeRoom
            || EntityManager.Instance == null || possibleEnemies == null || possibleEnemies.Length == 0) return;
        if (spawnedEntities.Count > 0)
        {
            if (spawnedEntities.Exists(entity => entity != null && entity.activeInHierarchy
                && entity.GetComponent<EnemyAIController>() != null
                && entity.GetComponent<EnemyAIController>().HomeRoom == roomData)) return;
            clearedByPlayer = true;
            return;
        }
        for (int i = 0; i < Mathf.Max(0, maxEnemiesInRoom); i++)
        {
            GameObject prefab = possibleEnemies[Random.Range(0, possibleEnemies.Length)];
            Vector3 desired = roomData.FloorCenter + new Vector3(i * 1.2f, 0f, 1f);
            GameObject entity = EntityManager.Instance.SpawnEntity(prefab, desired, Quaternion.identity,
                null, roomData, true);
            if (entity != null) spawnedEntities.Add(entity);
        }
    }

    private void DeactivateRoom()
    {
        bool hadSurvivor = false;
        foreach (GameObject entity in spawnedEntities)
        {
            if (entity == null || !entity.activeInHierarchy) continue;
            EnemyAIController owner = entity.GetComponent<EnemyAIController>();
            if (owner == null || owner.HomeRoom != roomData || !owner.ConfinedToRoom) continue;
            hadSurvivor = true;
            if (EntityManager.Instance != null) EntityManager.Instance.DespawnEntity(entity);
        }
        if (!hadSurvivor && spawnedEntities.Count > 0) clearedByPlayer = true;
        spawnedEntities.Clear();
    }
}
