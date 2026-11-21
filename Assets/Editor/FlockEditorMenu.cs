#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para crear y configurar en 1 clic un enjambre de mini bolitas con el algoritmo de Flocking.
/// Disponible en el menú Tools > IA - Steering y en el menú contextual de la Jerarquía.
/// </summary>
public static class FlockEditorMenu
{
    [MenuItem("Tools/IA - Steering/Crear Enjambre de Mini Bolitas (Flocking)", false, 10)]
    [MenuItem("GameObject/3D Object/Enjambre Flocking (Mini Bolitas)", false, 20)]
    public static void CreateFlockObject()
    {
        GameObject flockObj = new GameObject("Flock_MiniBolitas");

        // Posicionar en el centro de la vista del editor o en la posición del jugador
        Vector3 spawnPos = Vector3.up * 1.5f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            spawnPos = player.transform.position + Vector3.forward * 4f + Vector3.up * 1.5f;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            spawnPos = SceneView.lastActiveSceneView.pivot;
        }

        flockObj.transform.position = spawnPos;

        // Agregar FlockManager
        FlockManager manager = flockObj.AddComponent<FlockManager>();
        manager.flockSize = 40;
        manager.bolitaScale = 0.25f;
        manager.bolitaColor = new Color(0.1f, 0.85f, 1f, 1f); // Cian brillante
        manager.enableTrails = true;
        manager.trailTime = 0.35f;
        manager.is3D = true;
        manager.boundsRadius = 9f;
        manager.boundsBoxSize = new Vector3(16f, 4.5f, 16f);
        manager.targetMode = FlockManager.TargetMode.ScatterOnApproach;

        if (player != null)
        {
            manager.target = player.transform;
        }

        // Registrar acción para permitir Ctrl+Z (Undo)
        Undo.RegisterCreatedObjectUndo(flockObj, "Crear Enjambre de Mini Bolitas");
        Selection.activeGameObject = flockObj;

        Debug.Log("<b>[Flocking]</b> ¡Enjambre de Mini Bolitas creado exitosamente! Dale al botón <b>PLAY</b> para verlas volar y dispersarse.");
    }
}
#endif
