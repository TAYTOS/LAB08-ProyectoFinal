using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>Spawn validado y reutilización de las entidades jugables.</summary>
public class EntityManager : MonoBehaviour
{
    public static EntityManager Instance;
    private readonly Dictionary<string, Queue<GameObject>> pool = new Dictionary<string, Queue<GameObject>>();
    public List<GameObject> activeEntities = new List<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public static bool TryFindNavigablePoint(Vector3 desired, RoomData room, out Vector3 point)
    {
        desired.y = 0f;
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 3f, NavMesh.AllAreas)
            && (room == null || room.ContainsMovementPoint(hit.position, 0.55f)))
        {
            point = hit.position;
            return true;
        }
        if (room != null)
        {
            Bounds bounds = room.MovementBounds;
            for (int i = 0; i < 24; i++)
            {
                Vector3 candidate = i == 0 ? room.FloorCenter : new Vector3(
                    Random.Range(bounds.min.x + 0.7f, bounds.max.x - 0.7f), 0f,
                    Random.Range(bounds.min.z + 0.7f, bounds.max.z - 0.7f));
                if (NavMesh.SamplePosition(candidate, out hit, 1.5f, NavMesh.AllAreas)
                    && room.ContainsMovementPoint(hit.position, 0.55f))
                {
                    point = hit.position;
                    return true;
                }
            }
        }
        point = desired;
        return false;
    }

    public GameObject SpawnEntity(GameObject prefab, Vector3 position, Quaternion rotation,
        Transform parent = null, RoomData homeRoom = null, bool confinedToRoom = false)
    {
        if (prefab == null || !TryFindNavigablePoint(position, confinedToRoom ? homeRoom : null, out Vector3 spawn)) return null;
        string key = prefab.name;
        GameObject entity = null;
        if (pool.TryGetValue(key, out Queue<GameObject> waiting))
            while (waiting.Count > 0 && entity == null) entity = waiting.Dequeue();
        if (entity == null)
        {
            entity = Instantiate(prefab, spawn, rotation, parent);
            entity.name = key;
        }
        else
        {
            entity.transform.SetParent(parent);
            entity.transform.SetPositionAndRotation(spawn, rotation);
        }
        foreach (Transform part in entity.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 8;
        SeguirJugador legacy = entity.GetComponent<SeguirJugador>();
        if (legacy != null) legacy.enabled = false;
        LuzWanderSeek oldLight = entity.GetComponent<LuzWanderSeek>();
        if (oldLight != null) oldLight.enabled = false;
        if (entity.GetComponentInChildren<Collider>() == null)
        {
            CapsuleCollider collider = entity.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.85f;
            collider.height = 1.7f;
            collider.radius = 0.4f;
        }
        EnemyAIController controller = entity.GetComponent<EnemyAIController>();
        if (controller == null) controller = entity.AddComponent<EnemyAIController>();
        entity.SetActive(true);
        NavMeshAgent navigation = entity.GetComponent<NavMeshAgent>();
        if (navigation != null && navigation.enabled) navigation.Warp(spawn);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        controller.Initialize(homeRoom, player != null ? player.transform : null, confinedToRoom);
        if (!activeEntities.Contains(entity)) activeEntities.Add(entity);
        return entity;
    }

    public void DespawnEntity(GameObject entity)
    {
        if (entity == null || !activeEntities.Remove(entity)) return;
        entity.SetActive(false);
        entity.transform.SetParent(transform);
        if (!pool.TryGetValue(entity.name, out Queue<GameObject> waiting))
        {
            waiting = new Queue<GameObject>();
            pool[entity.name] = waiting;
        }
        waiting.Enqueue(entity);
    }

    public void ClearAllEntities()
    {
        for (int i = activeEntities.Count - 1; i >= 0; i--) DespawnEntity(activeEntities[i]);
        activeEntities.Clear();
    }
}
