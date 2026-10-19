using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Separate from the synthetic fixture: loads the actual GameScene at level 1.
[InitializeOnLoad]
public static class CourseSceneSmoke
{
    const string ActiveKey = "CourseSceneSmoke.Active", PathKey = "CourseSceneSmoke.Path", StartKey = "CourseSceneSmoke.Start";
    const string IntroSavedKey = "CourseSceneSmoke.IntroSaved", IntroExistsKey = "CourseSceneSmoke.IntroExists",
        IntroValueKey = "CourseSceneSmoke.IntroValue";
    [Serializable] public class Check { public string name; public bool passed; public string detail; }
    [Serializable] public class Report
    {
        public string status, unityVersion, generatedUtc;
        public List<Check> checks = new List<Check>();
        public List<string> errors = new List<string>();
        public string[] limitations = {
            "Loads actual GameScene without saving changes; level 1 only.",
            "Checks runtime wiring and generated objects, not full player input, visual quality or all procedural seeds.",
            "Seven steering behaviors are attached; mutually exclusive behaviors need not all be enabled simultaneously.",
            "Does not validate actual jump traversal or complete every mission."
        };
    }
    static Report report;
    static IEnumerator sequence;
    static bool finished;
    static int lastFrame = -1;
    static CourseSceneSmoke() { EditorApplication.update += Tick; }

    public static void Run()
    {
        SaveIntroductionPreference();
        string path = Path.Combine(Path.GetTempPath(), "course-scene-smoke.json");
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-courseValidationReport") path = Path.GetFullPath(args[i + 1]);
        SessionState.SetString(PathKey, path);
        SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
        SessionState.SetBool(ActiveKey, true);
        report = null; sequence = null; finished = false; lastFrame = -1;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void SaveIntroductionPreference()
    {
        // SessionState survives the domain reload when EnterPlaymode is called.
        if (!SessionState.GetBool(IntroSavedKey, false))
        {
            SessionState.SetBool(IntroExistsKey, PlayerPrefs.HasKey(GameIntroductionUI.SeenKey));
            SessionState.SetInt(IntroValueKey, PlayerPrefs.GetInt(GameIntroductionUI.SeenKey, 0));
            SessionState.SetBool(IntroSavedKey, true);
        }
        PlayerPrefs.SetInt(GameIntroductionUI.SeenKey, 1);
        PlayerPrefs.Save();
    }
    static void RestoreIntroductionPreference()
    {
        if (!SessionState.GetBool(IntroSavedKey, false)) return;
        if (SessionState.GetBool(IntroExistsKey, false))
            PlayerPrefs.SetInt(GameIntroductionUI.SeenKey, SessionState.GetInt(IntroValueKey, 0));
        else PlayerPrefs.DeleteKey(GameIntroductionUI.SeenKey);
        PlayerPrefs.Save();
        SessionState.SetBool(IntroSavedKey, false);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false) || finished) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f) > 70f)
        {
            EnsureReport(); Record("smoke.timeout", false, "Exceeded 70 seconds."); Finish(); return;
        }
        if (!EditorApplication.isPlaying) return;
        if (sequence == null)
        {
            EnsureReport(); Application.logMessageReceived += CaptureLog; sequence = CheckScene();
        }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try { if (!sequence.MoveNext()) Finish(); }
        catch (Exception e) { Record("smoke.exception", false, e.ToString()); Finish(); }
    }
    static void EnsureReport()
    {
        if (report == null) report = new Report { status = "running", unityVersion = Application.unityVersion,
            generatedUtc = DateTime.UtcNow.ToString("o") };
    }
    static void Record(string name, bool passed, string detail = "")
    {
        report.checks.Add(new Check { name = name, passed = passed, detail = detail });
    }
    static void CaptureLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.errors.Add(message + "\n" + stack);
    }
    static void Finish()
    {
        if (finished) return;
        finished = true; Application.logMessageReceived -= CaptureLog;
        try { RestoreIntroductionPreference(); }
        catch (Exception e) { report.errors.Add("Could not restore introduction preference: " + e); }
        bool passed = report.errors.Count == 0;
        foreach (Check check in report.checks) passed &= check.passed;
        report.status = passed ? "passed" : "failed";
        try
        {
            string path = SessionState.GetString(PathKey, "");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
        catch (Exception e) { Debug.LogException(e); passed = false; }
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static IEnumerator CheckScene()
    {
        Time.timeScale = 1;
        ProceduralLevelGenerator.currentLevel = 1;
        SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        yield return null;
        double deadline = EditorApplication.timeSinceStartup + 45;
        GameObject player = null;
        RoomData[] rooms = new RoomData[0];
        EnemyAIController[] enemies = new EnemyAIController[0];
        CalmingPill[] pills = new CalmingPill[0];
        bool ready = false;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            rooms = UnityEngine.Object.FindObjectsByType<RoomData>(FindObjectsSortMode.None);
            enemies = UnityEngine.Object.FindObjectsByType<EnemyAIController>(FindObjectsSortMode.None);
            pills = UnityEngine.Object.FindObjectsByType<CalmingPill>(FindObjectsSortMode.None);
            bool readyEnemy = false;
            foreach (EnemyAIController enemy in enemies) readyEnemy |= enemy.IsReady;
            ready = player != null && rooms.Length > 0 && pills.Length > 0 && readyEnemy && EntityManager.Instance != null;
            if (ready) break;
            yield return null;
        }
        Record("scene.loadedGameScene", SceneManager.GetActiveScene().name == "GameScene");
        Record("scene.levelOne", ProceduralLevelGenerator.currentLevel == 1);
        Record("scene.initializationWithin45Seconds", ready);
        Record("scene.playerExists", player != null);
        if (player == null) yield break;
        Record("player.powerInstalled", player.GetComponent<PlayerPowerState>() != null);
        Record("player.throwerInstalled", player.GetComponent<PredictiveThrower>() != null);
        Record("player.predictorInstalled", player.GetComponent<BallisticPredictor>() != null);
        Record("scene.managerInstalled", EntityManager.Instance != null);
        Record("scene.generatedRooms", rooms.Length > 0, "count=" + rooms.Length);
        Record("scene.generatedPills", pills.Length > 0, "count=" + pills.Length);
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        Record("navigation.meshExists", triangulation.vertices.Length > 0, "vertices=" + triangulation.vertices.Length);
        Vector3 playerFloor = player.transform.position; playerFloor.y = 0;
        Record("navigation.playerFloorReachable", NavMesh.SamplePosition(playerFloor, out NavMeshHit _, 3, NavMesh.AllAreas));
        int safeRooms = 0, validBounds = 0, populationManagers = 0;
        foreach (RoomData room in rooms)
        {
            if (room.archetype == RoomArchetype.SafeRoom) safeRooms++;
            if (room.MovementBounds.size.x > 0 && room.MovementBounds.size.z > 0 && Mathf.Abs(room.FloorCenter.y) < 0.001f) validBounds++;
            if (room.GetComponent<RoomEntityManager>() != null) populationManagers++;
        }
        Record("rooms.safeArchetypesPreserved", safeRooms >= 2, "safeRooms=" + safeRooms);
        Record("rooms.movementBoundsConfigured", rooms.Length > 0 && validBounds == rooms.Length);
        Record("rooms.populationManagersInstalled", rooms.Length > 0 && populationManagers == rooms.Length);
        EnemyAIController smartLight = null;
        foreach (EnemyAIController enemy in enemies)
            if (enemy.name.StartsWith("Smart Light", StringComparison.Ordinal)) { smartLight = enemy; break; }
        Record("entity.smartLightSpawned", smartLight != null);
        if (smartLight != null)
        {
            Record("entity.smartLightReady", smartLight.IsReady);
            Type[] required = { typeof(PursuitBehavior), typeof(EvasionBehavior), typeof(ObstacleAvoidanceBehavior),
                typeof(WallFollowingBehavior), typeof(FlowFieldFollowingBehavior),
                typeof(UnalignedCollisionAvoidanceBehavior), typeof(SeparationBehavior) };
            foreach (Type type in required) Record("entity.smartLight.attached." + type.Name, smartLight.GetComponent(type) != null);
            var avoid = smartLight.GetComponent<ObstacleAvoidanceBehavior>();
            var crossing = smartLight.GetComponent<UnalignedCollisionAvoidanceBehavior>();
            var separation = smartLight.GetComponent<SeparationBehavior>();
            Record("entity.smartLight.localAvoidanceEnabled", avoid != null && avoid.enabled
                && crossing != null && crossing.enabled && separation != null && separation.enabled);
        }
        RoomData activatedRoom = null;
        EnemyAIController roomEnemy = null;
        foreach (RoomData room in rooms)
        {
            RoomEntityManager population = room.GetComponent<RoomEntityManager>();
            if (population == null || !population.allowSpawn || population.maxEnemiesInRoom <= 0
                || room.archetype == RoomArchetype.SafeRoom || population.possibleEnemies == null
                || population.possibleEnemies.Length == 0) continue;
            population.ActivateRoom();
            foreach (EnemyAIController enemy in UnityEngine.Object.FindObjectsByType<EnemyAIController>(FindObjectsSortMode.None))
                if (enemy.HomeRoom == room && enemy.ConfinedToRoom) { roomEnemy = enemy; activatedRoom = room; break; }
            if (roomEnemy != null) break;
        }
        yield return null;
        Record("roomPopulation.activationSpawns", roomEnemy != null);
        if (roomEnemy != null)
        {
            Record("roomPopulation.homeAssigned", roomEnemy.HomeRoom == activatedRoom && roomEnemy.ConfinedToRoom);
            Record("roomPopulation.ready", roomEnemy.IsReady);
            Record("roomPopulation.spawnWithinBounds", activatedRoom.ContainsMovementPoint(roomEnemy.transform.position, 0.1f));
        }
    }
}

