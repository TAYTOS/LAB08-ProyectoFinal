#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class CienpiesEditorTools
{
    private const string PrefabFolder = "Assets/Prefabs/Entities";
    private const string ResourcesFolder = "Assets/Resources";
    private const string PeludoPrefabPath = "Assets/Prefabs/Entities/CienpiesPeludoGigante.prefab";
    private const string PeludoResourcesPath = "Assets/Resources/CienpiesPeludoGigante.prefab";
    private const string PequenoPrefabPath = "Assets/Prefabs/Entities/CienpiesPequeno.prefab";
    private const string PequenoResourcesPath = "Assets/Resources/CienpiesPequeno.prefab";

    [MenuItem("Tools/IA - Ciempiés/1. Instanciar Ciempiés Peludo Gigante (Montable)", false, 1)]
    public static void SpawnPeludoGigante()
    {
        Vector3 spawnPos = Vector3.up * 0.2f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            spawnPos = player.transform.position + player.transform.forward * 4f;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            spawnPos = SceneView.lastActiveSceneView.pivot;
        }

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject cienpies = new GameObject("Cienpies_Peludo_Gigante");
        cienpies.transform.position = spawnPos;

        NavMeshAgent agent = cienpies.AddComponent<NavMeshAgent>();
        agent.speed = 3.8f;
        agent.angularSpeed = 140f;
        agent.acceleration = 8f;
        agent.radius = 0.8f;
        agent.height = 1.2f;

        CienpiesPeludoGigante script = cienpies.AddComponent<CienpiesPeludoGigante>();
        script.materialPeludo = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CienpiesPeludo_Mat.mat");

        Undo.RegisterCreatedObjectUndo(cienpies, "Crear Ciempiés Peludo Gigante");
        Selection.activeGameObject = cienpies;

        Debug.Log("<b>[Ciempiés Peludo Gigante]</b> ¡Instanciado exitosamente! Dale al botón PLAY y salta encima de su lomo para transportarte por los pasillos.");
    }

    [MenuItem("Tools/IA - Ciempiés/2. Instanciar Grupo de Ciempiés Pequeños (Huidizos)", false, 2)]
    public static void SpawnGrupoPequenos()
    {
        Vector3 center = Vector3.zero;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            center = player.transform.position + player.transform.forward * 3f;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            center = SceneView.lastActiveSceneView.pivot;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CienpiesPequeno_Mat.mat");

        for (int i = 0; i < 5; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 4f;
            offset.y = 0;
            Vector3 pos = center + offset;

            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            GameObject mini = new GameObject($"Cienpies_Pequeno_{i + 1}");
            mini.transform.position = pos;

            NavMeshAgent agent = mini.AddComponent<NavMeshAgent>();
            agent.speed = 3.2f;
            agent.radius = 0.2f;
            agent.height = 0.3f;

            CienpiesPequeno cp = mini.AddComponent<CienpiesPequeno>();
            cp.materialCienpies = mat;

            Undo.RegisterCreatedObjectUndo(mini, "Crear Ciempiés Pequeño");
        }

        Debug.Log("<b>[Ciempiés Pequeños]</b> 5 ciempiés instanciados correteando por el suelo. ¡Acércate para verlos huir!");
    }

    [MenuItem("Tools/IA - Ciempiés/3. Guardar Prefabs en Resources", false, 10)]
    public static void SavePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Entities");
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // Prefab Peludo
        GameObject tempPeludo = new GameObject("CienpiesPeludoGigante");
        tempPeludo.AddComponent<NavMeshAgent>();
        CienpiesPeludoGigante pScript = tempPeludo.AddComponent<CienpiesPeludoGigante>();
        pScript.materialPeludo = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CienpiesPeludo_Mat.mat");
        PrefabUtility.SaveAsPrefabAsset(tempPeludo, PeludoPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(tempPeludo, PeludoResourcesPath);
        Object.DestroyImmediate(tempPeludo);

        // Prefab Pequeño
        GameObject tempPequeno = new GameObject("CienpiesPequeno");
        tempPequeno.AddComponent<NavMeshAgent>();
        CienpiesPequeno minScript = tempPequeno.AddComponent<CienpiesPequeno>();
        minScript.materialCienpies = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CienpiesPequeno_Mat.mat");
        PrefabUtility.SaveAsPrefabAsset(tempPequeno, PequenoPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(tempPequeno, PequenoResourcesPath);
        Object.DestroyImmediate(tempPequeno);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<b>[Ciempiés]</b> Prefabs guardados correctamente en Assets/Resources/.");
    }
}
#endif
