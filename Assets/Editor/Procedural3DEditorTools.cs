#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class Procedural3DEditorTools
{
    private static Vector3 ObtenerPuntoFrenteJugador(float distancia = 6f)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player.transform.position + player.transform.forward * distancia;
        }
        if (SceneView.lastActiveSceneView != null)
        {
            return SceneView.lastActiveSceneView.pivot;
        }
        return Vector3.zero;
    }

    [MenuItem("Tools/Arquitectura 3D/1. Instanciar Plataforma y Rampa de Prueba", false, 1)]
    public static void SpawnTestPlataformaYRampa()
    {
        Vector3 basePos = ObtenerPuntoFrenteJugador(7f);
        basePos.y = 0f;

        GameObject root = new GameObject("Test_Plataforma_Y_Rampa_3D");
        root.transform.position = basePos;

        Material matMetal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Arquitectura3D_Metal.mat");
        Material matRampa = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Arquitectura3D_Rampa.mat");

        float altura = 2.8f;
        Vector3 platCenter = basePos + Vector3.forward * 5f + Vector3.up * altura;
        Vector3 platSize = new Vector3(6f, 0.25f, 6f);

        // Plataforma superior
        GameObject plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plat.name = "Plataforma_Superior";
        plat.transform.position = platCenter;
        plat.transform.localScale = platSize;
        plat.transform.SetParent(root.transform);
        if (matMetal != null) plat.GetComponent<Renderer>().sharedMaterial = matMetal;

        // Columnas
        Vector3[] corners = {
            platCenter + new Vector3(-2.6f, -altura * 0.5f, -2.6f),
            platCenter + new Vector3(2.6f, -altura * 0.5f, -2.6f),
            platCenter + new Vector3(-2.6f, -altura * 0.5f, 2.6f),
            platCenter + new Vector3(2.6f, -altura * 0.5f, 2.6f)
        };
        foreach (var c in corners)
        {
            GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            col.name = "Columna_Soporte";
            col.transform.position = c;
            col.transform.localScale = new Vector3(0.5f, altura * 0.5f, 0.5f);
            col.transform.SetParent(root.transform);
            if (matMetal != null) col.GetComponent<Renderer>().sharedMaterial = matMetal;
        }

        // Rampa de acceso
        Vector3 rampaInicio = basePos + Vector3.forward * 0.5f + Vector3.up * 0.05f;
        Vector3 rampaFin = platCenter - Vector3.forward * (platSize.z * 0.5f);
        Procedural3DArchitecture.CrearRampa(root.transform, rampaInicio, rampaFin, 2.2f, matRampa);

        Undo.RegisterCreatedObjectUndo(root, "Instanciar Plataforma y Rampa de Prueba");
        Selection.activeGameObject = root;

        Debug.Log("<b>[Arquitectura 3D]</b> ¡Plataforma y rampa de prueba instanciadas! Puedes subir corriendo por la rampa o saltar (ESPACIO).");
    }

    [MenuItem("Tools/Arquitectura 3D/2. Instanciar Desfiladero y Puente de Prueba", false, 2)]
    public static void SpawnTestDesfiladeroYPuente()
    {
        Vector3 basePos = ObtenerPuntoFrenteJugador(8f);
        basePos.y = 0f;

        GameObject root = new GameObject("Test_Desfiladero_Y_Puente_3D");
        root.transform.position = basePos;

        Material matMetal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Arquitectura3D_Metal.mat");
        Material matPuente = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Arquitectura3D_Puente.mat");
        Material matDesfiladero = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Arquitectura3D_Desfiladero.mat");

        float prof = 4.0f;
        float anchoFosa = 6f;
        float largoFosa = 12f;

        // Suelo del abismo
        GameObject sueloFosa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sueloFosa.name = "Suelo_Abismo";
        sueloFosa.transform.position = basePos + Vector3.down * (prof + 0.5f);
        sueloFosa.transform.localScale = new Vector3(anchoFosa, 1f, largoFosa);
        sueloFosa.transform.SetParent(root.transform);
        if (matDesfiladero != null) sueloFosa.GetComponent<Renderer>().sharedMaterial = matDesfiladero;

        // Paredes del desfiladero (Barrancos)
        GameObject paredIzq = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paredIzq.name = "Barranco_Izquierdo";
        paredIzq.transform.position = basePos + Vector3.left * (anchoFosa * 0.5f + 0.1f) + Vector3.down * (prof * 0.5f);
        paredIzq.transform.localScale = new Vector3(0.2f, prof, largoFosa);
        paredIzq.transform.SetParent(root.transform);
        if (matDesfiladero != null) paredIzq.GetComponent<Renderer>().sharedMaterial = matDesfiladero;

        GameObject paredDer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paredDer.name = "Barranco_Derecho";
        paredDer.transform.position = basePos + Vector3.right * (anchoFosa * 0.5f + 0.1f) + Vector3.down * (prof * 0.5f);
        paredDer.transform.localScale = new Vector3(0.2f, prof, largoFosa);
        paredDer.transform.SetParent(root.transform);
        if (matDesfiladero != null) paredDer.GetComponent<Renderer>().sharedMaterial = matDesfiladero;

        // Puente suspendido cruzando la fosa
        GameObject puente = GameObject.CreatePrimitive(PrimitiveType.Cube);
        puente.name = "Puente_Suspendido";
        puente.transform.position = basePos + Vector3.down * 0.12f;
        puente.transform.localScale = new Vector3(anchoFosa + 1f, 0.24f, 2.5f);
        puente.transform.SetParent(root.transform);
        if (matPuente != null) puente.GetComponent<Renderer>().sharedMaterial = matPuente;

        // Pilares del puente
        GameObject pilarPuente = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pilarPuente.name = "Pilar_Puente";
        pilarPuente.transform.position = basePos + Vector3.down * (prof * 0.5f);
        pilarPuente.transform.localScale = new Vector3(0.4f, prof * 0.5f, 0.4f);
        pilarPuente.transform.SetParent(root.transform);
        if (matMetal != null) pilarPuente.GetComponent<Renderer>().sharedMaterial = matMetal;

        Undo.RegisterCreatedObjectUndo(root, "Instanciar Desfiladero y Puente de Prueba");
        Selection.activeGameObject = root;

        Debug.Log("<b>[Arquitectura 3D]</b> ¡Desfiladero abismal y puente instanciados! Cruza el puente con cuidado o salta sobre el abismo.");
    }

    [MenuItem("Tools/Arquitectura 3D/3. Regenerar Nivel con Semilla Actual", false, 10)]
    public static void RegenerarNivel()
    {
        if (Application.isPlaying && ProceduralLevelGenerator.Instance != null)
        {
            ProceduralLevelGenerator.Instance.RegenerarConSemillaActual();
            Debug.Log($"<b>[Arquitectura 3D]</b> ¡Nivel regenerado determinísticamente con la semilla: '{ProceduralLevelGenerator.Instance.levelSeed}'!");
        }
        else
        {
            Debug.Log("<b>[Arquitectura 3D]</b> Entra en modo PLAY y se generará automáticamente el mapa 3D completo.");
        }
    }

    [MenuItem("Tools/Arquitectura 3D/4. Regenerar Nivel con Nueva Semilla Aleatoria", false, 11)]
    public static void RegenerarNivelNuevaSemilla()
    {
        if (Application.isPlaying && ProceduralLevelGenerator.Instance != null)
        {
            ProceduralLevelGenerator.Instance.RegenerarConNuevaSemilla();
            Debug.Log($"<b>[Arquitectura 3D]</b> ¡Nivel regenerado con nueva semilla aleatoria: '{ProceduralLevelGenerator.Instance.levelSeed}'!");
        }
        else
        {
            Debug.Log("<b>[Arquitectura 3D]</b> Entra en modo PLAY y se generará automáticamente el mapa 3D completo.");
        }
    }
}
#endif
