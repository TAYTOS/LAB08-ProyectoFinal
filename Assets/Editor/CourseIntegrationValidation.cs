using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Batch entry point. Uses a new, unsaved scene and reports runtime invariants.
[InitializeOnLoad]
public static class CourseIntegrationValidation
{
    const string ActiveKey = "CourseValidation.Active", PathKey = "CourseValidation.Path", StartKey = "CourseValidation.Start";
    const string IntroSavedKey = "CourseValidation.IntroSaved", IntroExistsKey = "CourseValidation.IntroExists",
        IntroValueKey = "CourseValidation.IntroValue";
    [Serializable] public class Check { public string name; public bool passed; public string detail; }
    [Serializable] public class Report
    {
        public string status, unityVersion, generatedUtc;
        public List<Check> checks = new List<Check>();
        public List<string> errors = new List<string>();
        public string[] limitations = {
            "Isolated runtime fixture; user scenes and prefabs are not saved or tested.",
            "Flat NavMesh: does not test jump links, landing or procedural connectivity.",
            "Public APIs: does not test keyboard/mouse input, visual layout or player experience.",
            "Lead assumes constant target velocity; moving geometry may change real impacts."
        };
    }
    static Report report;
    static IEnumerator sequence;
    static bool finished;
    static int lastFrame = -1;

    static CourseIntegrationValidation() { EditorApplication.update += Tick; }

    public static void Run()
    {
        SaveIntroductionPreference();
        string path = Path.Combine(Path.GetTempPath(), "course-validation.json");
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
            EnsureReport(); Record("harness.timeout", false, "Exceeded 70 seconds."); Finish(); return;
        }
        if (!EditorApplication.isPlaying) return;
        if (sequence == null)
        {
            EnsureReport(); Application.logMessageReceived += CaptureLog; sequence = RunChecks();
        }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try { if (!sequence.MoveNext()) Finish(); }
        catch (Exception e) { Record("harness.exception", false, e.ToString()); Finish(); }
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
    static void CaptureLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.errors.Add(condition + "\n" + stack);
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
    static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 size)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name; cube.transform.SetParent(parent);
        cube.transform.position = position; cube.transform.localScale = size;
        return cube;
    }
    static bool Enabled<T>(GameObject entity) where T : Behaviour
    {
        T item = entity.GetComponent<T>(); return item != null && item.enabled;
    }
    static bool Registered(SteeringAgent agent)
    {
        foreach (SteeringAgent item in SteeringAgent.ActiveAgents) if (item == agent) return true;
        return false;
    }
    static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }

    static IEnumerator RunChecks()
    {
        Time.timeScale = 1f; UnityEngine.Random.InitState(74129);
        GameObject fixture = new GameObject("Course fixture (unsaved)");
        Cube(fixture.transform, "Floor", new Vector3(0,-0.5f,0), new Vector3(60,1,60));
        Cube(fixture.transform, "North", new Vector3(0,2,29), new Vector3(60,4,1));
        Cube(fixture.transform, "South", new Vector3(0,2,-29), new Vector3(60,4,1));
        Cube(fixture.transform, "East", new Vector3(29,2,0), new Vector3(1,4,60));
        Cube(fixture.transform, "West", new Vector3(-29,2,0), new Vector3(1,4,60));
        // Forces a two-corner detour on the opposite side of the fixture.
        Cube(fixture.transform, "Route detour wall", new Vector3(13.5f,2,9), new Vector3(1,4,12));
        NavMeshSurface surface = fixture.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        Physics.SyncTransforms(); surface.BuildNavMesh();
        Record("navigation.bake", NavMesh.SamplePosition(Vector3.zero, out NavMeshHit _, 2, NavMesh.AllAreas));

        GameObject player = new GameObject("Validation Player");
        player.tag = "Player"; player.layer = 2; player.transform.position = new Vector3(-4,0.6f,2);
        CharacterController character = player.AddComponent<CharacterController>();
        character.height = 1.8f; character.radius = 0.4f; character.center = Vector3.up * 0.3f;
        AnxietyManager anxiety = player.AddComponent<AnxietyManager>();
        anxiety.maxAnxiety = 100; anxiety.criticalThreshold = 60; anxiety.entitySpawnThreshold = 200;
        anxiety.baseRecoveryRate = 0; anxiety.timeToDie = 100;
        PlayerPowerState power = player.AddComponent<PlayerPowerState>();
        RoomData room = new GameObject("Validation room").AddComponent<RoomData>();
        room.ConfigureMovementBounds(new Bounds(new Vector3(-8,2,0), new Vector3(14,4,14)));
        room.areaSize = 100; room.entranceCount = 1; room.illuminationLevel = 1;
        EntityManager manager = new GameObject("Validation EntityManager").AddComponent<EntityManager>();
        GameObject template = new GameObject("Validation template"); template.SetActive(false);
        yield return null;
        anxiety.activePhobias.Clear(); anxiety.RecordRoomEntry(room);
        GameObject entity = manager.SpawnEntity(template, new Vector3(-11,0,0), Quaternion.identity, null, room, true);
        Record("entity.spawn", entity != null);
        if (entity == null) yield break;
        EnemyAIController enemy = entity.GetComponent<EnemyAIController>();
        enemy.fieldOfViewAngle = 360; enemy.detectionRadius = 50;
        SteeringAgent steering = entity.GetComponent<SteeringAgent>();
        yield return null;
        Record("entity.ready", enemy.IsReady && steering.IsOnNavigation);
        Record("entity.registered", manager.activeEntities.Contains(entity) && Registered(steering));
        Record("entity.homeRoom", enemy.HomeRoom == room && enemy.ConfinedToRoom);
        Type[] required = { typeof(PursuitBehavior), typeof(EvasionBehavior), typeof(ObstacleAvoidanceBehavior),
            typeof(WallFollowingBehavior), typeof(FlowFieldFollowingBehavior),
            typeof(UnalignedCollisionAvoidanceBehavior), typeof(SeparationBehavior) };
        foreach (Type type in required) Record("behavior.attached." + type.Name, entity.GetComponent(type) != null);
        Record("behavior.predictiveEvasion.attached", entity.GetComponent<PredictiveEvasionBehavior>() != null);
        Record("state.calm.patrol", enemy.currentState.ToString() == "Patrol", enemy.currentState.ToString());
        Record("behavior.patrol.noPursuit", !Enabled<PursuitBehavior>(entity) && !Enabled<EvasionBehavior>(entity));
        Record("behavior.patrol.wallFollowing", Enabled<WallFollowingBehavior>(entity));
        Record("behavior.avoidance.active", Enabled<ObstacleAvoidanceBehavior>(entity)
            && Enabled<UnalignedCollisionAvoidanceBehavior>(entity) && Enabled<SeparationBehavior>(entity));
        NavMeshFlowField field = entity.GetComponent<NavMeshFlowField>();
        field.Configure(steering, room.FloorCenter + Vector3.forward * 3);
        Vector3 flow = field.GetDirectionAtPosition(entity.transform.position);
        Record("flowField.spatialDirection", Finite(flow) && flow.sqrMagnitude > 0.01f, flow.ToString());

        Vector3 chaseStart = entity.transform.position; anxiety.AddAnxiety(80);
        float wait = Time.time + 0.8f; while (Time.time < wait) yield return null;
        Record("anxiety.critical", anxiety.IsCritical);
        Record("state.critical.chase", enemy.currentState.ToString() == "Chase", enemy.currentState.ToString());
        Record("behavior.chase.pursuit", Enabled<PursuitBehavior>(entity) && !Enabled<EvasionBehavior>(entity));
        Record("behavior.chase.noPatrolForces", !Enabled<WallFollowingBehavior>(entity) && !Enabled<FlowFieldFollowingBehavior>(entity));
        float moved = SteeringAgent.PlanarDistance(chaseStart, entity.transform.position);
        Record("navigation.actualDisplacement", moved > 0.08f, "distance=" + moved);
        Record("navigation.remainsBound", enemy.IsReady && room.ContainsMovementPoint(entity.transform.position));

        power.Activate(0.8f); wait = Time.time + 0.25f;
        while (Time.time < wait) yield return null;
        Record("power.active", power.IsEmpowered);
        Record("power.calms", anxiety.currentAnxiety < 0.01f && !anxiety.IsCritical);
        string afraid = enemy.currentState.ToString();
        Record("state.power.evadeOrHide", afraid == "Evade" || afraid == "Hide", afraid);
        Record("power.enemyVulnerable", enemy.IsVulnerable);
        Record("behavior.power.noPursuit", !Enabled<PursuitBehavior>(entity));
        Record("behavior.evade.selected", afraid != "Evade" || Enabled<EvasionBehavior>(entity));
        int beforeDefeat = power.DefeatedEntities, id = entity.GetInstanceID();
        enemy.ReceiveProjectileImpact();
        Record("defeat.deactivated", !entity.activeSelf && !manager.activeEntities.Contains(entity));
        Record("defeat.counter", power.DefeatedEntities == beforeDefeat + 1);
        Record("defeat.unregistersSteering", !Registered(steering));
        enemy.ReceiveProjectileImpact();
        Record("defeat.notCountedTwice", power.DefeatedEntities == beforeDefeat + 1);
        wait = Time.time + 0.9f; while (Time.time < wait) yield return null;
        Record("power.expires", !power.IsEmpowered);
        entity = manager.SpawnEntity(template, new Vector3(-2.3f,0,0), Quaternion.identity, null, room, true);
        Record("pool.reusesInstance", entity != null && entity.GetInstanceID() == id);
        if (entity == null) yield break;
        enemy = entity.GetComponent<EnemyAIController>(); steering = entity.GetComponent<SteeringAgent>();
        enemy.fieldOfViewAngle = 360; enemy.detectionRadius = 50; yield return null;
        Record("pool.restoresReadiness", entity.activeSelf && enemy.IsReady && Registered(steering));
        Record("pool.restoresState", !enemy.IsVulnerable && enemy.currentState.ToString() == "Patrol", enemy.currentState.ToString());
        Record("pool.noDuplicateRegistration", manager.activeEntities.FindAll(item => item == entity).Count == 1);

        player.transform.position = new Vector3(12,0.6f,0); Physics.SyncTransforms(); anxiety.AddAnxiety(80);
        bool inside = true; wait = Time.time + 1.5f;
        while (Time.time < wait) { inside &= room.ContainsMovementPoint(entity.transform.position, 0.15f); yield return null; }
        Record("room.confinesChaseEveryFrame", inside, entity.transform.position.ToString());
        Record("room.routeGoalInside", !steering.HasRoute || room.ContainsMovementPoint(steering.RouteGoal, 0.15f));
        anxiety.Calm(100);
        RoomData quickRoom = new GameObject("Quick exit probe").AddComponent<RoomData>();
        quickRoom.ConfigureMovementBounds(room.MovementBounds); quickRoom.areaSize = 100; quickRoom.illuminationLevel = 1;
        float beforeExit = anxiety.currentAnxiety; int exits = anxiety.QuickExitCount;
        anxiety.RecordRoomEntry(quickRoom); yield return null; anxiety.RecordRoomExit(quickRoom);
        Record("fear.quickExitRaisesAnxiety", anxiety.currentAnxiety > beforeExit && anxiety.QuickExitCount == exits + 1,
            "before=" + beforeExit + "; after=" + anxiety.currentAnxiety);
        Record("fear.exitClearsCurrentVisit", anxiety.CurrentRoom != quickRoom);

        // Keep the decision layer out of this test: the route follower and local
        // avoidance must negotiate a wall corner and actually reach the goal.
        GameObject detourEntity = manager.SpawnEntity(template, new Vector3(8,0,8),
            Quaternion.identity, null, null, false);
        if (detourEntity == null) Record("navigation.detourArrival", false, "Could not spawn detour agent.");
        else
        {
            EnemyAIController detourBrain = detourEntity.GetComponent<EnemyAIController>();
            detourBrain.enabled = false; // OnDisable resets motion; set the route afterwards.
            foreach (SteeringBehavior behavior in detourEntity.GetComponents<SteeringBehavior>()) behavior.enabled = false;
            detourEntity.GetComponent<ObstacleAvoidanceBehavior>().enabled = true;
            detourEntity.GetComponent<SeparationBehavior>().enabled = true;
            SteeringAgent detourSteering = detourEntity.GetComponent<SteeringAgent>();
            detourSteering.maxSpeed = 4f;
            Vector3 goal = new Vector3(19,0,8);
            bool routeAccepted = detourSteering.SetRouteGoal(goal, true);
            NavMeshPath detourPath = new NavMeshPath();
            bool hasDetour = NavMesh.CalculatePath(detourEntity.transform.position, goal,
                NavMesh.AllAreas, detourPath) && detourPath.status == NavMeshPathStatus.PathComplete
                && detourPath.corners.Length >= 3;
            float travelEnds = Time.time + 12f;
            while (Time.time < travelEnds && SteeringAgent.PlanarDistance(detourEntity.transform.position, goal) >= 1f)
                yield return null;
            float finalDistance = SteeringAgent.PlanarDistance(detourEntity.transform.position, goal);
            string coordinates = "";
            foreach (Vector3 corner in detourPath.corners) coordinates += corner.ToString("F2") + " ";
            Record("navigation.detourArrival", routeAccepted && hasDetour && finalDistance < 1f,
                "distance=" + finalDistance + "; position=" + detourEntity.transform.position.ToString("F2")
                + "; route=" + coordinates);
        }
        RunBallisticChecks(player.transform);
        yield return null;
        Record("projectile.cleansDangerRegistry", BallisticDangerZone.ActiveDangerZones.Count == 0);
    }

    static void RunBallisticChecks(Transform owner)
    {
        BallisticPredictor predictor = new GameObject("Validation predictor").AddComponent<BallisticPredictor>();
        predictor.ignoredRoot = owner; predictor.timeStep = 0.02f; predictor.simulationSteps = 400;
        predictor.dragCoefficient = 0; predictor.magnusStrength = 0;
        Vector3 origin = new Vector3(8,2,-12), velocity = new Vector3(4,8,2);
        BallisticModel model = predictor.CaptureModel(); float duration = 0.65f;
        Vector3 expected = origin + velocity * duration + model.gravity * (0.5f * duration * duration);
        float error = Vector3.Distance(BallisticPredictor.PositionAfter(model, origin, velocity, duration), expected);
        Record("ballistic.noDragMatchesParabola", error < 0.001f, "error=" + error);
        predictor.dragCoefficient = 0.015f; predictor.magnusStrength = 0.02f;
        predictor.spinVector = new Vector3(0,5,0); model = predictor.CaptureModel();
        Vector3 complex = BallisticPredictor.PositionAfter(model, origin, velocity, duration);
        Record("ballistic.dragMagnusChangePath", Finite(complex) && Vector3.Distance(complex, expected) > 0.01f);
        Physics.SyncTransforms();
        List<TrajectoryPoint> points = predictor.PredictTrajectory(origin, velocity, out RaycastHit hit, out bool hasHit);
        Record("ballistic.predictsFloorImpact", hasHit && hit.collider != null && hit.normal.y > 0.9f);
        if (hasHit)
        {
            GameObject go = new GameObject("Validation projectile"); go.layer = 2; go.transform.position = origin;
            BallisticProjectile projectile = go.AddComponent<BallisticProjectile>(); projectile.manualSimulation = true;
            projectile.Initialize(model, velocity, owner, null, 2, null, points, true, hit);
            Record("ballistic.registersDanger", projectile.DangerZone != null
                && BallisticDangerZone.ActiveDangerZones.Contains(projectile.DangerZone));
            for (int i = 0; i < 2000 && projectile.IsFlying; i++)
                projectile.AdvanceSimulation(i % 2 == 0 ? 0.013f : 0.041f);
            Record("ballistic.flightTerminatesOnImpact", !projectile.IsFlying && go.activeSelf);
            error = Vector3.Distance(projectile.transform.position, hit.point);
            Record("ballistic.previewMatchesActualImpact", error < 0.002f, "error=" + error);
            error = Mathf.Abs(projectile.FlightTime - points[points.Count - 1].time);
            Record("ballistic.previewMatchesActualTime", error < 0.0002f, "error=" + error);
            Record("ballistic.impactClearsDanger", projectile.DangerZone == null);
        }
        Vector3 target = origin + new Vector3(12,0,0), targetVelocity = new Vector3(0.7f,0,0.4f);
        bool solved = predictor.CalculateLeadLaunchVelocity(origin, target, targetVelocity, out Vector3 launch, out float flight);
        Record("ballistic.leadConverges", solved, "residual=" + predictor.LastLeadResidual);
        if (solved)
        {
            error = Vector3.Distance(BallisticPredictor.PositionAfter(model, origin, launch, flight), target + targetVelocity * flight);
            Record("ballistic.leadInterceptsMovingTarget", error <= predictor.leadTolerance + 0.001f, "residual=" + error);
            Record("ballistic.leadPreservesLaunchSpeed", Mathf.Abs(launch.magnitude - predictor.launchSpeed) < 0.001f);
        }
        bool unreachable = predictor.CalculateLeadLaunchVelocity(origin, origin + Vector3.right * 10000,
            Vector3.zero, out _, out _);
        Record("ballistic.rejectsUnreachableTarget", !unreachable);
    }
}

