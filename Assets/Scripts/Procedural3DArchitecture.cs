using System.Collections.Generic;
using UnityEngine;

public enum Arquitectura3DTipo
{
    Ninguno,
    DesfiladeroConPuentes,
    PlataformasYRampas,
    Completo3D // Desfiladero + Puentes + Plataformas elevadas + Rampas
}

public class InfoHabitacion3D
{
    public ProceduralLevelGenerator.BSPNode node;
    public Arquitectura3DTipo tipo = Arquitectura3DTipo.Ninguno;
    public bool corteHorizontal = false;
    public int canalInicio = 0;
    public int canalFin = 0;
    public List<int> posicionesPuentes = new List<int>();
    public RectInt areaPlataforma;
    public float alturaPlataforma = 2.6f;
    public float profundidadDesfiladero = 3.2f;
}

public static class Procedural3DArchitecture
{
    private static Material matMetal;
    private static Material matRampa;
    private static Material matDesfiladero;
    private static Material matPuente;

    public static void CargarMateriales()
    {
        if (matMetal == null) matMetal = Resources.Load<Material>("Materials/Arquitectura3D_Metal") ?? CargarMaterialPorRuta("Assets/Materials/Arquitectura3D_Metal.mat");
        if (matRampa == null) matRampa = Resources.Load<Material>("Materials/Arquitectura3D_Rampa") ?? CargarMaterialPorRuta("Assets/Materials/Arquitectura3D_Rampa.mat");
        if (matDesfiladero == null) matDesfiladero = Resources.Load<Material>("Materials/Arquitectura3D_Desfiladero") ?? CargarMaterialPorRuta("Assets/Materials/Arquitectura3D_Desfiladero.mat");
        if (matPuente == null) matPuente = Resources.Load<Material>("Materials/Arquitectura3D_Puente") ?? CargarMaterialPorRuta("Assets/Materials/Arquitectura3D_Puente.mat");
    }

    private static Material CargarMaterialPorRuta(string ruta)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(ruta);
#else
        return null;
#endif
    }

    public static List<InfoHabitacion3D> PlanificarHabitaciones3D(
        ProceduralLevelGenerator generator,
        List<ProceduralLevelGenerator.BSPNode> leafNodes,
        ProceduralLevelGenerator.CellType[,] grid,
        ProceduralLevelGenerator.BSPNode startRoom,
        ProceduralLevelGenerator.BSPNode endRoom,
        float probabilidad = 0.85f,
        int minimoGarantizado = 2)
    {
        CargarMateriales();
        List<InfoHabitacion3D> plan = new List<InfoHabitacion3D>();

        // 1. Filtrar habitaciones candidatas válidas (mínimo 4x4 celdas)
        List<ProceduralLevelGenerator.BSPNode> candidatos = new List<ProceduralLevelGenerator.BSPNode>();
        foreach (var node in leafNodes)
        {
            if (node == null || node == startRoom || node == endRoom || node.archetype == RoomArchetype.SafeRoom)
                continue;

            RectInt r = node.room;
            if (r.width >= 4 && r.height >= 4)
            {
                candidatos.Add(node);
            }
        }

        if (candidatos.Count == 0)
        {
            Debug.LogWarning("[Arquitectura 3D] No se encontraron habitaciones con tamaño suficiente (>= 4x4) para arquitectura 3D.");
            return plan;
        }

        // 2. Ordenar candidatos: habitaciones batofóbicas y de mayor superficie primero
        candidatos.Sort((a, b) =>
        {
            int scoreA = (a.archetype == RoomArchetype.Batophobic ? 500 : 0) + (a.room.width * a.room.height);
            int scoreB = (b.archetype == RoomArchetype.Batophobic ? 500 : 0) + (b.room.width * b.room.height);
            return scoreB.CompareTo(scoreA);
        });

        int habitacionesCon3D = 0;
        int maxHabitaciones3D = Mathf.Max(minimoGarantizado, Mathf.CeilToInt(candidatos.Count * probabilidad));

        for (int i = 0; i < candidatos.Count; i++)
        {
            var node = candidatos[i];
            RectInt r = node.room;

            // Garantizar que al menos los primeros 'minimoGarantizado' se construyen sí o sí
            bool forzar = (habitacionesCon3D < minimoGarantizado);
            if (!forzar && Random.value > probabilidad)
                continue;

            if (habitacionesCon3D >= maxHabitaciones3D)
                break;

            InfoHabitacion3D info = new InfoHabitacion3D { node = node };
            bool esBatofobica = node.archetype == RoomArchetype.Batophobic || node.roomHeight >= 5f;

            if (esBatofobica && r.width >= 6 && r.height >= 6)
            {
                info.tipo = (r.width >= 7 && r.height >= 7 && Random.value < 0.6f)
                    ? Arquitectura3DTipo.Completo3D
                    : Arquitectura3DTipo.DesfiladeroConPuentes;
            }
            else
            {
                // Alternar entre Desfiladeros con Puentes y Plataformas con Rampas
                info.tipo = (habitacionesCon3D % 2 == 0)
                    ? Arquitectura3DTipo.DesfiladeroConPuentes
                    : Arquitectura3DTipo.PlataformasYRampas;
            }

            // Planificar Desfiladero y Puentes
            if (info.tipo == Arquitectura3DTipo.DesfiladeroConPuentes || info.tipo == Arquitectura3DTipo.Completo3D)
            {
                info.corteHorizontal = (r.width >= r.height);
                int anchoCanal = (Mathf.Min(r.width, r.height) >= 6) ? 2 : 1;

                if (info.corteHorizontal)
                {
                    int centroZ = r.y + (r.height / 2);
                    info.canalInicio = Mathf.Clamp(centroZ, r.y + 1, r.y + r.height - 2);
                    info.canalFin = Mathf.Clamp(info.canalInicio + anchoCanal - 1, r.y + 1, r.y + r.height - 2);

                    for (int x = r.x + 1; x < r.x + r.width - 1; x++)
                    {
                        for (int z = info.canalInicio; z <= info.canalFin; z++)
                        {
                            if (grid[x, z] == ProceduralLevelGenerator.CellType.Floor)
                            {
                                grid[x, z] = ProceduralLevelGenerator.CellType.Chasm;
                            }
                        }
                    }

                    // 1 o 2 puentes
                    int puente1 = r.x + (r.width / 2);
                    info.posicionesPuentes.Add(puente1);
                    if (r.width >= 7)
                    {
                        info.posicionesPuentes.Clear();
                        info.posicionesPuentes.Add(r.x + 2);
                        info.posicionesPuentes.Add(r.x + r.width - 3);
                    }

                    foreach (int px in info.posicionesPuentes)
                    {
                        for (int z = info.canalInicio; z <= info.canalFin; z++)
                        {
                            grid[px, z] = ProceduralLevelGenerator.CellType.Bridge;
                        }
                    }
                }
                else
                {
                    int centroX = r.x + (r.width / 2);
                    info.canalInicio = Mathf.Clamp(centroX, r.x + 1, r.x + r.width - 2);
                    info.canalFin = Mathf.Clamp(info.canalInicio + anchoCanal - 1, r.x + 1, r.x + r.width - 2);

                    for (int z = r.y + 1; z < r.y + r.height - 1; z++)
                    {
                        for (int x = info.canalInicio; x <= info.canalFin; x++)
                        {
                            if (grid[x, z] == ProceduralLevelGenerator.CellType.Floor)
                            {
                                grid[x, z] = ProceduralLevelGenerator.CellType.Chasm;
                            }
                        }
                    }

                    // 1 o 2 puentes
                    int puente1 = r.y + (r.height / 2);
                    info.posicionesPuentes.Add(puente1);
                    if (r.height >= 7)
                    {
                        info.posicionesPuentes.Clear();
                        info.posicionesPuentes.Add(r.y + 2);
                        info.posicionesPuentes.Add(r.y + r.height - 3);
                    }

                    foreach (int pz in info.posicionesPuentes)
                    {
                        for (int x = info.canalInicio; x <= info.canalFin; x++)
                        {
                            grid[x, pz] = ProceduralLevelGenerator.CellType.Bridge;
                        }
                    }
                }
            }

            // Planificar Plataforma y Rampa
            if (info.tipo == Arquitectura3DTipo.PlataformasYRampas || info.tipo == Arquitectura3DTipo.Completo3D)
            {
                int platW = Mathf.Clamp(r.width / 2, 2, 3);
                int platH = Mathf.Clamp(r.height / 2, 2, 3);
                int platX = r.x + 1;
                int platZ = r.y + 1;

                if (info.tipo == Arquitectura3DTipo.Completo3D)
                {
                    if (info.corteHorizontal)
                        platZ = Mathf.Min(r.y + r.height - platH - 1, info.canalFin + 1);
                    else
                        platX = Mathf.Min(r.x + r.width - platW - 1, info.canalFin + 1);
                }

                info.areaPlataforma = new RectInt(platX, platZ, platW, platH);
                info.alturaPlataforma = Mathf.Clamp(node.roomHeight * 0.45f, 2.2f, 3.5f);
            }

            plan.Add(info);
            habitacionesCon3D++;
        }

        Debug.Log($"<b>[Arquitectura 3D]</b> {plan.Count} habitaciones 3D planificadas (Desfiladeros, Puentes, Plataformas y Rampas).");
        return plan;
    }

    public static void ConstruirGeometria3D(
        ProceduralLevelGenerator generator,
        List<InfoHabitacion3D> plan3D,
        ProceduralLevelGenerator.CellType[,] grid,
        Transform levelParent)
    {
        CargarMateriales();
        float cellSize = generator.cellSize;

        foreach (var info in plan3D)
        {
            Transform parentStatic = info.node.staticContainer != null ? info.node.staticContainer.transform : levelParent;
            Transform parentDynamic = info.node.dynamicContainer != null ? info.node.dynamicContainer.transform : levelParent;

            // 1. Construir Desfiladeros y Puentes
            if (info.tipo == Arquitectura3DTipo.DesfiladeroConPuentes || info.tipo == Arquitectura3DTipo.Completo3D)
            {
                ConstruirDesfiladeroFisico(generator, info, grid, parentStatic, parentDynamic);
            }

            // 2. Construir Plataformas Elevadas y Rampas
            if (info.tipo == Arquitectura3DTipo.PlataformasYRampas || info.tipo == Arquitectura3DTipo.Completo3D)
            {
                ConstruirPlataformaYRampasFisicas(generator, info, grid, parentStatic, parentDynamic);
            }
        }

        Debug.Log($"<b>[Arquitectura 3D]</b> Geometría 3D construida exitosamente en {plan3D.Count} habitaciones.");
    }

    private static void ConstruirDesfiladeroFisico(
        ProceduralLevelGenerator generator,
        InfoHabitacion3D info,
        ProceduralLevelGenerator.CellType[,] grid,
        Transform parentStatic,
        Transform parentDynamic)
    {
        float cellSize = generator.cellSize;
        RectInt r = info.node.room;
        float prof = info.profundidadDesfiladero;

        GameObject chasmGroup = new GameObject("Desfiladero_3D");
        chasmGroup.transform.SetParent(parentStatic);
        chasmGroup.isStatic = true;

        List<Vector2Int> celdasFosa = new List<Vector2Int>();
        List<Vector2Int> celdasPuente = new List<Vector2Int>();

        int xMin = r.x;
        int xMax = r.x + r.width;
        int zMin = r.y;
        int zMax = r.y + r.height;

        for (int x = xMin; x < xMax; x++)
        {
            for (int z = zMin; z < zMax; z++)
            {
                if (grid[x, z] == ProceduralLevelGenerator.CellType.Chasm)
                    celdasFosa.Add(new Vector2Int(x, z));
                else if (grid[x, z] == ProceduralLevelGenerator.CellType.Bridge)
                    celdasPuente.Add(new Vector2Int(x, z));
            }
        }

        // A. Suelo hundido del Desfiladero (con material abismal y colisión)
        foreach (var c in celdasFosa)
        {
            Vector3 posAbismo = new Vector3(c.x * cellSize, -prof, c.y * cellSize);

            GameObject floorBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorBottom.name = $"Suelo_Fosa_{c.x}_{c.y}";
            floorBottom.transform.position = posAbismo + Vector3.down * 0.5f;
            floorBottom.transform.localScale = new Vector3(cellSize, 1f, cellSize);
            floorBottom.transform.SetParent(chasmGroup.transform);
            floorBottom.isStatic = true;
            floorBottom.layer = 6;

            if (matDesfiladero != null) floorBottom.GetComponent<Renderer>().sharedMaterial = matDesfiladero;
        }

        // B. Paredes Verticales del Desfiladero (donde el piso alto cae hacia la fosa)
        int[][] dirs = { new int[] { 0, 1 }, new int[] { 0, -1 }, new int[] { -1, 0 }, new int[] { 1, 0 } };
        foreach (var c in celdasFosa)
        {
            for (int d = 0; d < 4; d++)
            {
                int nx = c.x + dirs[d][0];
                int nz = c.y + dirs[d][1];

                if (nx >= 0 && nx < generator.mapWidth && nz >= 0 && nz < generator.mapDepth)
                {
                    ProceduralLevelGenerator.CellType vecino = grid[nx, nz];
                    if (vecino == ProceduralLevelGenerator.CellType.Floor || vecino == ProceduralLevelGenerator.CellType.Door)
                    {
                        Vector3 dirVec = new Vector3(dirs[d][0], 0, dirs[d][1]);
                        Vector3 paredPos = new Vector3(c.x * cellSize, -prof * 0.5f, c.y * cellSize) + dirVec * (cellSize * 0.5f);

                        GameObject cliffWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cliffWall.name = $"Pared_Desfiladero_{c.x}_{c.y}";
                        cliffWall.transform.position = paredPos;

                        Vector3 escala = (dirs[d][0] != 0)
                            ? new Vector3(0.2f, prof, cellSize)
                            : new Vector3(cellSize, prof, 0.2f);
                        cliffWall.transform.localScale = escala;
                        cliffWall.transform.SetParent(chasmGroup.transform);
                        cliffWall.isStatic = true;
                        cliffWall.layer = 6;

                        if (matDesfiladero != null) cliffWall.GetComponent<Renderer>().sharedMaterial = matDesfiladero;
                    }
                }
            }
        }

        // C. Puentes Físicos (Tableros, Pilares de soporte y Barandillas de seguridad)
        GameObject bridgesGroup = new GameObject("Puentes_Sobre_Desfiladero");
        bridgesGroup.transform.SetParent(chasmGroup.transform);
        bridgesGroup.isStatic = true;

        foreach (var c in celdasPuente)
        {
            Vector3 posPuente = new Vector3(c.x * cellSize, 0f, c.y * cellSize);

            // Tablero del puente
            GameObject puente = GameObject.CreatePrimitive(PrimitiveType.Cube);
            puente.name = $"Tablero_Puente_{c.x}_{c.y}";
            puente.transform.position = posPuente + Vector3.down * 0.12f;
            puente.transform.localScale = new Vector3(cellSize, 0.24f, cellSize);
            puente.transform.SetParent(bridgesGroup.transform);
            puente.isStatic = true;
            puente.layer = 6;
            if (matPuente != null) puente.GetComponent<Renderer>().sharedMaterial = matPuente;

            // Pilares de soporte bajo el puente
            GameObject pilar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pilar.name = "Pilar_Puente";
            pilar.transform.position = posPuente + Vector3.down * (prof * 0.5f);
            pilar.transform.localScale = new Vector3(0.35f, prof * 0.5f, 0.35f);
            pilar.transform.SetParent(bridgesGroup.transform);
            pilar.isStatic = true;
            pilar.layer = 6;
            if (matMetal != null) pilar.GetComponent<Renderer>().sharedMaterial = matMetal;

            // Barandillas a los lados del puente donde hay fosa
            for (int d = 0; d < 4; d++)
            {
                int nx = c.x + dirs[d][0];
                int nz = c.y + dirs[d][1];
                if (nx >= 0 && nx < generator.mapWidth && nz >= 0 && nz < generator.mapDepth)
                {
                    if (grid[nx, nz] == ProceduralLevelGenerator.CellType.Chasm)
                    {
                        Vector3 dirVec = new Vector3(dirs[d][0], 0, dirs[d][1]);
                        Vector3 railPos = posPuente + dirVec * (cellSize * 0.46f) + Vector3.up * 0.45f;
                        CrearBarandilla(bridgesGroup.transform, railPos, dirs[d][0] != 0, cellSize);
                    }
                }
            }
        }

        // D. Rampa de Acceso al fondo del Desfiladero (conecta el fondo de la fosa con el suelo alto)
        if (celdasFosa.Count > 0)
        {
            // Tomamos una celda extrema de la fosa
            Vector2Int cFosa = celdasFosa[0];
            Vector3 pBaja = new Vector3(cFosa.x * cellSize, -prof + 0.05f, cFosa.y * cellSize);

            // La rampa sube a lo largo de la fosa hacia una celda interior
            Vector3 dirRampa = info.corteHorizontal ? Vector3.right : Vector3.forward;
            Vector3 pAlta = pBaja + dirRampa * (cellSize * 1.5f) + Vector3.up * prof;

            CrearRampa(chasmGroup.transform, pBaja, pAlta, 1.4f, matRampa);
        }

        // E. Plataforma de Salto (Pilar de salto en medio del abismo)
        if (celdasFosa.Count >= 3)
        {
            Vector2Int cMedio = celdasFosa[celdasFosa.Count / 2];
            Vector3 posPilar = new Vector3(cMedio.x * cellSize, -0.2f, cMedio.y * cellSize);

            GameObject pilarSalto = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pilarSalto.name = "Plataforma_Salto_Pilar";
            pilarSalto.transform.position = posPilar + Vector3.down * (prof * 0.5f);
            pilarSalto.transform.localScale = new Vector3(1.2f, prof * 0.5f, 1.2f);
            pilarSalto.transform.SetParent(chasmGroup.transform);
            pilarSalto.isStatic = true;
            pilarSalto.layer = 6;
            if (matMetal != null) pilarSalto.GetComponent<Renderer>().sharedMaterial = matMetal;
        }

        // F. Iluminación atmosférica en la fosa
        if (celdasFosa.Count > 0 && parentDynamic != null)
        {
            Vector2Int cLuz = celdasFosa[celdasFosa.Count / 2];
            GameObject luzFosa = new GameObject("Luz_Fosa_Atmosferica");
            luzFosa.transform.position = new Vector3(cLuz.x * cellSize, -prof * 0.3f, cLuz.y * cellSize);
            luzFosa.transform.SetParent(parentDynamic);

            Light l = luzFosa.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.4f, 0.1f);
            l.range = cellSize * 5f;
            l.intensity = 2.5f;
        }
    }

    private static void ConstruirPlataformaYRampasFisicas(
        ProceduralLevelGenerator generator,
        InfoHabitacion3D info,
        ProceduralLevelGenerator.CellType[,] grid,
        Transform parentStatic,
        Transform parentDynamic)
    {
        float cellSize = generator.cellSize;
        RectInt r = info.areaPlataforma;
        float h = info.alturaPlataforma;

        GameObject platGroup = new GameObject("Plataforma_Elevada_3D");
        platGroup.transform.SetParent(parentStatic);
        platGroup.isStatic = true;

        Vector3 centroPlat = new Vector3(
            (r.x + r.width * 0.5f) * cellSize,
            h,
            (r.y + r.height * 0.5f) * cellSize
        );
        Vector3 tamPlat = new Vector3(r.width * cellSize, 0.25f, r.height * cellSize);

        // A. Tablero de la Plataforma Elevada
        GameObject pisoPlataforma = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pisoPlataforma.name = "Losa_Plataforma";
        pisoPlataforma.transform.position = centroPlat;
        pisoPlataforma.transform.localScale = tamPlat;
        pisoPlataforma.transform.SetParent(platGroup.transform);
        pisoPlataforma.isStatic = true;
        pisoPlataforma.layer = 6;
        if (matMetal != null) pisoPlataforma.GetComponent<Renderer>().sharedMaterial = matMetal;

        // B. Columnas de Soporte Estructural
        Vector3[] esquinas = {
            new Vector3(r.x * cellSize + 0.35f, h * 0.5f, r.y * cellSize + 0.35f),
            new Vector3((r.x + r.width) * cellSize - 0.35f, h * 0.5f, r.y * cellSize + 0.35f),
            new Vector3(r.x * cellSize + 0.35f, h * 0.5f, (r.y + r.height) * cellSize - 0.35f),
            new Vector3((r.x + r.width) * cellSize - 0.35f, h * 0.5f, (r.y + r.height) * cellSize - 0.35f)
        };

        foreach (var esq in esquinas)
        {
            GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            col.name = "Columna_Soporte";
            col.transform.position = esq;
            col.transform.localScale = new Vector3(0.4f, h * 0.5f, 0.4f);
            col.transform.SetParent(platGroup.transform);
            col.isStatic = true;
            col.layer = 6;
            if (matMetal != null) col.GetComponent<Renderer>().sharedMaterial = matMetal;
        }

        // C. Rampa Inclinada conectando el Suelo (y=0) con la Plataforma (y=h)
        // La rampa se posiciona hacia el interior abierto de la habitación
        Vector3 finRampa = new Vector3(
            (r.x + r.width * 0.5f) * cellSize,
            h,
            (r.y + r.height) * cellSize
        );
        Vector3 inicioRampa = finRampa + Vector3.forward * (cellSize * 1.6f) + Vector3.down * h + Vector3.up * 0.05f;

        CrearRampa(platGroup.transform, inicioRampa, finRampa, 1.8f, matRampa);

        // D. Barandillas de Seguridad en los 3 bordes de la plataforma (dejando abierto el borde de la rampa)
        Vector3 railIzq = new Vector3(r.x * cellSize, h + 0.45f, (r.y + r.height * 0.5f) * cellSize);
        CrearBarandilla(platGroup.transform, railIzq, true, r.height * cellSize);

        Vector3 railDer = new Vector3((r.x + r.width) * cellSize, h + 0.45f, (r.y + r.height * 0.5f) * cellSize);
        CrearBarandilla(platGroup.transform, railDer, true, r.height * cellSize);

        Vector3 railAtras = new Vector3((r.x + r.width * 0.5f) * cellSize, h + 0.45f, r.y * cellSize);
        CrearBarandilla(platGroup.transform, railAtras, false, r.width * cellSize);
    }

    public static void CrearRampa(Transform parent, Vector3 inicio, Vector3 fin, float ancho, Material material, bool conBordillos = true)
    {
        Vector3 delta = fin - inicio;
        float longitudPlana = new Vector2(delta.x, delta.z).magnitude;
        if (longitudPlana < 0.1f) return;

        float longitudPendiente = delta.magnitude;
        Vector3 centro = (inicio + fin) * 0.5f;

        GameObject rampa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rampa.name = "Rampa_Inclinada";
        rampa.transform.position = centro;
        rampa.transform.localScale = new Vector3(ancho, 0.15f, longitudPendiente);

        Quaternion rotacionHorizontal = Quaternion.LookRotation(new Vector3(delta.x, 0, delta.z));
        float anguloInclinacion = Mathf.Atan2(delta.y, longitudPlana) * Mathf.Rad2Deg;
        rampa.transform.rotation = rotacionHorizontal * Quaternion.Euler(-anguloInclinacion, 0, 0);

        rampa.transform.SetParent(parent);
        rampa.isStatic = true;
        rampa.layer = 6;
        if (material != null) rampa.GetComponent<Renderer>().sharedMaterial = material;

        // Bordillos guía laterales (solo si se solicitan, se omiten para rampitas de puerta/pasillo)
        if (conBordillos)
        {
            CrearBordilloRampa(rampa.transform, ancho * 0.5f, longitudPendiente);
            CrearBordilloRampa(rampa.transform, -ancho * 0.5f, longitudPendiente);
        }
    }

    private static void CrearBordilloRampa(Transform rampa, float localX, float longitud)
    {
        GameObject bordillo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bordillo.name = "Bordillo_Guia";
        bordillo.transform.SetParent(rampa, false);
        bordillo.transform.localPosition = new Vector3(localX, 0.2f, 0);
        bordillo.transform.localScale = new Vector3(0.12f, 0.35f, 1f);
        bordillo.isStatic = true;
        bordillo.layer = 6;
        if (matMetal != null) bordillo.GetComponent<Renderer>().sharedMaterial = matMetal;
    }

    private static void CrearBarandilla(Transform parent, Vector3 pos, bool alineadoZ, float longitud)
    {
        GameObject railGroup = new GameObject("Barandilla");
        railGroup.transform.position = pos;
        railGroup.transform.SetParent(parent);
        railGroup.isStatic = true;

        GameObject pasamanos = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pasamanos.name = "Pasamanos";
        pasamanos.transform.position = pos + Vector3.up * 0.45f;
        pasamanos.transform.localScale = alineadoZ
            ? new Vector3(0.08f, 0.08f, longitud)
            : new Vector3(longitud, 0.08f, 0.08f);
        pasamanos.transform.SetParent(railGroup.transform);
        pasamanos.isStatic = true;
        pasamanos.layer = 6;
        if (matMetal != null) pasamanos.GetComponent<Renderer>().sharedMaterial = matMetal;

        int postes = Mathf.Max(2, Mathf.RoundToInt(longitud / 2.0f) + 1);
        float paso = longitud / (postes - 1);
        float inicioOffset = -longitud * 0.5f;

        for (int i = 0; i < postes; i++)
        {
            float offset = inicioOffset + (i * paso);
            Vector3 posPoste = pos + (alineadoZ ? new Vector3(0, 0, offset) : new Vector3(offset, 0, 0));

            GameObject poste = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            poste.name = $"Poste_{i}";
            poste.transform.position = posPoste + Vector3.up * 0.22f;
            poste.transform.localScale = new Vector3(0.08f, 0.45f, 0.08f);
            poste.transform.SetParent(railGroup.transform);
            poste.isStatic = true;
            poste.layer = 6;
            if (matMetal != null) poste.GetComponent<Renderer>().sharedMaterial = matMetal;
        }
    }
}
