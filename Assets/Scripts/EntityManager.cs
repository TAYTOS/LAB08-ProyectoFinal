using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Mánager Global de Entidades.
/// Usa un sistema de "Object Pooling" para no destruir y crear objetos repetidamente, 
/// ahorrando muchísima memoria y CPU, algo vital en juegos generados proceduralmente.
/// </summary>
public class EntityManager : MonoBehaviour
{
    public static EntityManager Instance;

    // Diccionario de Pools. Llave = Nombre del Prefab, Valor = Cola de objetos inactivos listos para usar
    private Dictionary<string, Queue<GameObject>> pool = new Dictionary<string, Queue<GameObject>>();
    
    // Lista de todas las entidades activas en el mundo en este instante
    [Header("Debug - Entidades Activas")]
    public List<GameObject> activeEntities = new List<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Pide una entidad. Si hay una en el "cementerio" (pool), la revive. Si no, la instancia.
    /// </summary>
    public GameObject SpawnEntity(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        string key = prefab.name;
        GameObject entity = null;

        if (pool.ContainsKey(key) && pool[key].Count > 0)
        {
            // Reutilizar
            entity = pool[key].Dequeue();
            entity.transform.position = position;
            entity.transform.rotation = rotation;
            if (parent != null) entity.transform.SetParent(parent);
            entity.SetActive(true);
        }
        else
        {
            // Crear nuevo
            entity = Instantiate(prefab, position, rotation, parent);
            entity.name = key; // Mantenemos el nombre sin "(Clone)" para que la llave coincida
        }

        activeEntities.Add(entity);
        return entity;
    }

    /// <summary>
    /// Elimina una entidad del mundo activo y la manda a dormir al pool.
    /// </summary>
    public void DespawnEntity(GameObject entity)
    {
        if (entity == null) return;

        entity.SetActive(false);
        activeEntities.Remove(entity);

        string key = entity.name;
        if (!pool.ContainsKey(key))
        {
            pool[key] = new Queue<GameObject>();
        }
        pool[key].Enqueue(entity);
    }

    /// <summary>
    /// Elimina TODAS las entidades activas (útil al cambiar de nivel)
    /// </summary>
    public void ClearAllEntities()
    {
        for (int i = activeEntities.Count - 1; i >= 0; i--)
        {
            DespawnEntity(activeEntities[i]);
        }
    }
}
