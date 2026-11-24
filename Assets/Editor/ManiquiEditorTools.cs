#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using System.IO;

[InitializeOnLoad]
public static class ManiquiEditorTools
{
    private const string FbxPath = "Assets/Models/Goalkeeper Idle.fbx";
    private const string PrefabFolder = "Assets/Prefabs/Entities";
    private const string PrefabPath = "Assets/Prefabs/Entities/ManiquiLuminoso.prefab";
    private const string ResourcesFolder = "Assets/Resources";
    private const string ResourcesPrefabPath = "Assets/Resources/ManiquiLuminoso.prefab";
    private const string MatBlancoPath = "Assets/Materials/Maniqui_Blanco.mat";
    private const string MatNegroPath = "Assets/Materials/Maniqui_Negro.mat";
    private const string ControllerPath = "Assets/Prefabs/Entities/ControladorManiqui.controller";

    static ManiquiEditorTools()
    {
        EditorApplication.delayCall += AutoEnsurePrefab;
    }

    private static void AutoEnsurePrefab()
    {
        if (!File.Exists(PrefabPath) || !File.Exists(ResourcesPrefabPath))
        {
            CreateOrUpdatePrefab(false);
        }
    }

    [MenuItem("Tools/IA - Maniquí Luminoso/1. Crear o Actualizar Prefab del Maniquí", false, 1)]
    public static void MenuItemCreatePrefab()
    {
        CreateOrUpdatePrefab(true);
    }

    public static GameObject CreateOrUpdatePrefab(bool showDialog)
    {
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxAsset == null)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Error", "No se encontró el modelo en: " + FbxPath, "OK");
            return null;
        }

        // Crear carpetas si no existen
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Entities");
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // Instanciar temporalmente el modelo
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
        instance.name = "ManiquiLuminoso";

        // Configurar capa 8 (Entidades)
        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 8;
        }

        // Configurar NavMeshAgent
        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
        if (agent == null) agent = instance.AddComponent<NavMeshAgent>();
        agent.speed = 2.5f;
        agent.angularSpeed = 240f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 1.0f;
        agent.radius = 0.35f;
        agent.height = 1.8f;

        // Configurar Collider
        CapsuleCollider col = instance.GetComponent<CapsuleCollider>();
        if (col == null) col = instance.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.35f;

        // Configurar Animator
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller != null) animator.runtimeAnimatorController = controller;

        // Configurar ManiquiLuminoso
        ManiquiLuminoso maniqui = instance.GetComponent<ManiquiLuminoso>();
        if (maniqui == null) maniqui = instance.AddComponent<ManiquiLuminoso>();

        maniqui.materialBlanco = AssetDatabase.LoadAssetAtPath<Material>(MatBlancoPath);
        maniqui.materialNegro = AssetDatabase.LoadAssetAtPath<Material>(MatNegroPath);
        maniqui.modoLuminosidad = ManiquiLuminosityMode.AutoDetectFromSpawn;

        // Configurar Salud
        EnemyHealth health = instance.GetComponent<EnemyHealth>();
        if (health == null) health = instance.AddComponent<EnemyHealth>();
        health.maxHealth = 100f;
        health.alturaBarra = 2.1f;

        // Eliminar cualquier cámara o listener que venga del FBX para que no se robe el foco de la escena
        maniqui.EliminarCamarasHijas();

        // Guardar Prefab
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(instance, ResourcesPrefabPath);

        Object.DestroyImmediate(instance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Éxito",
                "¡Prefab ManiquiLuminoso creado y guardado en:\n- " + PrefabPath + "\n- " + ResourcesPrefabPath, "Excelente");
        }
        Debug.Log("<b>[ManiquiLuminoso]</b> Prefab creado con éxito.");
        return savedPrefab;
    }

    [MenuItem("Tools/IA - Maniquí Luminoso/2. Instanciar en Escena (Auto: Detecta Luz del Spawn)", false, 10)]
    public static void SpawnAutoInScene()
    {
        SpawnInSceneWithMode(ManiquiLuminosityMode.AutoDetectFromSpawn);
    }

    [MenuItem("Tools/IA - Maniquí Luminoso/3. Instanciar en Escena (Forzar Blanco: Paseo)", false, 11)]
    public static void SpawnWhiteInScene()
    {
        SpawnInSceneWithMode(ManiquiLuminosityMode.ForceWhite);
    }

    [MenuItem("Tools/IA - Maniquí Luminoso/4. Instanciar en Escena (Forzar Negro: Acecho en Sombras)", false, 12)]
    public static void SpawnBlackInScene()
    {
        SpawnInSceneWithMode(ManiquiLuminosityMode.ForceBlack);
    }

    private static void SpawnInSceneWithMode(ManiquiLuminosityMode mode)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            prefab = CreateOrUpdatePrefab(false);
            if (prefab == null) return;
        }

        Vector3 spawnPos = Vector3.up * 0.1f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            spawnPos = player.transform.position + player.transform.forward * 4f;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            spawnPos = SceneView.lastActiveSceneView.pivot;
        }

        // Alinear a NavMesh si es posible
        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        obj.transform.position = spawnPos;

        ManiquiLuminoso ml = obj.GetComponent<ManiquiLuminoso>();
        if (ml != null)
        {
            ml.modoLuminosidad = mode;
        }

        Undo.RegisterCreatedObjectUndo(obj, "Instanciar ManiquiLuminoso");
        Selection.activeGameObject = obj;
        Debug.Log("<b>[ManiquiLuminoso]</b> Instanciado en escena (" + mode + ") en " + spawnPos);
    }

    [MenuItem("Tools/IA - Maniquí Luminoso/5. Añadir Maniquí al Generador de Nivel Actual", false, 20)]
    public static void AddToCurrentLevelGenerator()
    {
        ProceduralLevelGenerator gen = Object.FindFirstObjectByType<ProceduralLevelGenerator>();
        if (gen == null)
        {
            EditorUtility.DisplayDialog("Aviso", "No se encontró ningún ProceduralLevelGenerator en la escena actual.", "OK");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = CreateOrUpdatePrefab(false);

        if (prefab == null) return;

        bool yaExiste = false;
        if (gen.enemyPrefabs != null)
        {
            foreach (var p in gen.enemyPrefabs)
            {
                if (p == prefab || (p != null && p.name == prefab.name))
                {
                    yaExiste = true;
                    break;
                }
            }
        }

        if (!yaExiste)
        {
            Undo.RecordObject(gen, "Añadir ManiquiLuminoso a Generador");
            var lista = gen.enemyPrefabs != null ? new System.Collections.Generic.List<GameObject>(gen.enemyPrefabs) : new System.Collections.Generic.List<GameObject>();
            lista.Add(prefab);
            gen.enemyPrefabs = lista.ToArray();
            EditorUtility.SetDirty(gen);
            Debug.Log("<b>[ManiquiLuminoso]</b> Añadido exitosamente a la lista enemyPrefabs del generador de nivel.");
            EditorUtility.DisplayDialog("Completado", "¡Maniquí Luminoso añadido a la lista de enemigos del nivel!", "Genial");
        }
        else
        {
            EditorUtility.DisplayDialog("Información", "El Maniquí Luminoso ya está en la lista de enemigos del generador.", "OK");
        }
    }
}
#endif
