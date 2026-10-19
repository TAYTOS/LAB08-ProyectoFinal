using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// -executeMethod GameplayUIValidation.Run -courseValidationReport <json>
// Optional: -courseScreenshotDir <directory>. No desktop capture is used.
[InitializeOnLoad]
public static class GameplayUIValidation
{
    const string Prefix = "GameplayUIValidation.";
    const string IntroKey = "Lab6.IntroductionSeen.v1";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    [Serializable] public class Check { public string name; public bool passed; public string detail; }
    [Serializable] public class Report
    {
        public string status, unityVersion, generatedUtc, reportPath;
        public List<Check> checks = new List<Check>();
        public List<string> errors = new List<string>();
        public List<string> screenshots = new List<string>();
        public string[] limitations = {
            "Programmatic button and slider events; does not replace visual or manual input QA.",
            "Resolution checks report actual dimensions and fail if the requested size was not applied.",
            "Interactive execution relies on Unity restoring the original edit-mode scene snapshot.",
            "Preferences, GameView selection and temporary custom resolutions are restored on completion or timeout."
        };
    }
    [Serializable] class SavedScenes { public string[] paths; public bool[] active, loaded; }
    static Report report;
    static IEnumerator sequence;
    static bool finished;
    static int lastFrame = -1;
    static EditorWindow gameView;
    static object sizeGroup;

    static GameplayUIValidation() { EditorApplication.update += Tick; }

    public static void Run() { Begin(false); }

    [MenuItem("Tools/Laboratorio6/Validate Gameplay UI")]
    public static void RunInteractive() { Begin(true); }

    static void Begin(bool interactive)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before starting Gameplay UI validation.");
        if (EditorApplication.isCompiling)
            throw new InvalidOperationException("Wait until Unity finishes compiling before UI validation.");
        RemoveLeftoverTemporaryResolutions();
        string path = Path.Combine(Path.GetTempPath(), "Laboratorio6", "GameplayUIValidation.json");
        string screenshotDirectory = "";
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-courseValidationReport") path = Path.GetFullPath(args[i + 1]);
            if (args[i] == "-courseScreenshotDir") screenshotDirectory = Path.GetFullPath(args[i + 1]);
        }
        if (interactive && string.IsNullOrEmpty(screenshotDirectory))
            screenshotDirectory = Path.Combine(Path.GetDirectoryName(path), "ui_validation_screens");
        SessionState.SetString(Prefix + "Report", path);
        SessionState.SetString(Prefix + "Screenshots", screenshotDirectory);
        SessionState.SetBool(Prefix + "Interactive", interactive);
        SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup);
        SaveFloatPreference(GameSettings.MouseSensitivityKey);
        SaveFloatPreference(GameSettings.VolumeKey);
        SessionState.SetBool(Prefix + "IntroHad", PlayerPrefs.HasKey(IntroKey));
        SessionState.SetInt(Prefix + "IntroValue", PlayerPrefs.GetInt(IntroKey, 0));
        SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
        var saved = new SavedScenes { paths = new string[scenes.Length], active = new bool[scenes.Length], loaded = new bool[scenes.Length] };
        for (int i = 0; i < scenes.Length; i++) { saved.paths[i] = scenes[i].path; saved.active[i] = scenes[i].isActive; saved.loaded[i] = scenes[i].isLoaded; }
        SessionState.SetString(Prefix + "Scenes", JsonUtility.ToJson(saved));
        SessionState.SetBool(Prefix + "PendingEditorRestore", false);
        SessionState.SetBool(Prefix + "ViewSaved", false);
        SessionState.SetBool(Prefix + "Active", true);
        PlayerPrefs.DeleteKey(IntroKey);
        report = null; sequence = null; finished = false; lastFrame = -1;
        // Interactive Play Mode preserves Unity's original scene/unsaved-edits snapshot.
        // The test only replaces scenes AFTER entering Play Mode.
        if (!interactive) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void SaveFloatPreference(string key)
    {
        SessionState.SetBool(Prefix + key + ".Had", PlayerPrefs.HasKey(key));
        SessionState.SetFloat(Prefix + key + ".Value", PlayerPrefs.GetFloat(key, 0));
    }
    static void RestorePreferences()
    {
        foreach (string key in new[] { GameSettings.MouseSensitivityKey, GameSettings.VolumeKey })
        {
            if (SessionState.GetBool(Prefix + key + ".Had", false))
                PlayerPrefs.SetFloat(key, SessionState.GetFloat(Prefix + key + ".Value", 0));
            else PlayerPrefs.DeleteKey(key);
        }
        if (SessionState.GetBool(Prefix + "IntroHad", false)) PlayerPrefs.SetInt(IntroKey, SessionState.GetInt(Prefix + "IntroValue", 0));
        else PlayerPrefs.DeleteKey(IntroKey);
        PlayerPrefs.Save();
        GameSettings.Load();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying && SessionState.GetBool(Prefix + "PendingEditorRestore", false))
        {
            SessionState.SetBool(Prefix + "PendingEditorRestore", false);
            FinishEditorRestore(); return;
        }
        if (!SessionState.GetBool(Prefix + "Active", false) || finished) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "Started", 0) > 100)
        {
            EnsureReport(); Record("harness.timeout", false, "Exceeded 100 seconds including scene loads."); Finish(); return;
        }
        if (!EditorApplication.isPlaying) return;
        if (sequence == null)
        {
            EnsureReport(); Application.logMessageReceived += CaptureLog; sequence = Validate();
        }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try { if (!sequence.MoveNext()) Finish(); }
        catch (Exception e) { Record("harness.exception", false, e.ToString()); Finish(); }
    }

    static void EnsureReport()
    {
        if (report == null) report = new Report { status = "running", unityVersion = Application.unityVersion,
            generatedUtc = DateTime.UtcNow.ToString("o"), reportPath = SessionState.GetString(Prefix + "Report", "") };
    }
    static void Record(string name, bool passed, string detail = "")
    {
        report.checks.Add(new Check { name = name, passed = passed, detail = detail });
    }
    static void CaptureLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + stack);
    }
    static bool Passed()
    {
        if (report.errors.Count != 0) return false;
        foreach (Check check in report.checks) if (!check.passed) return false;
        return true;
    }
    static void WriteReport()
    {
        report.status = Passed() ? "passed" : "failed";
        string directory = Path.GetDirectoryName(report.reportPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(report.reportPath, JsonUtility.ToJson(report, true));
    }
    static void Finish()
    {
        if (finished) return;
        finished = true;
        try { RestoreGameView(); RestorePreferences(); }
        catch (Exception e) { Record("harness.restore", false, e.ToString()); }
        bool restored = PlayerPrefs.HasKey(GameSettings.MouseSensitivityKey) == SessionState.GetBool(Prefix + GameSettings.MouseSensitivityKey + ".Had", false)
            && PlayerPrefs.HasKey(GameSettings.VolumeKey) == SessionState.GetBool(Prefix + GameSettings.VolumeKey + ".Had", false)
            && PlayerPrefs.HasKey(IntroKey) == SessionState.GetBool(Prefix + "IntroHad", false);
        foreach (string key in new[] { GameSettings.MouseSensitivityKey, GameSettings.VolumeKey })
            if (SessionState.GetBool(Prefix + key + ".Had", false))
                restored &= Mathf.Approximately(PlayerPrefs.GetFloat(key, float.NaN), SessionState.GetFloat(Prefix + key + ".Value", 0));
        if (SessionState.GetBool(Prefix + "IntroHad", false)) restored &= PlayerPrefs.GetInt(IntroKey, -1) == SessionState.GetInt(Prefix + "IntroValue", 0);
        Record("preferences.originalKeysRestored", restored);
        Application.logMessageReceived -= CaptureLog;
        WriteReport();
        SessionState.SetBool(Prefix + "Active", false);
        if (SessionState.GetBool(Prefix + "Interactive", false))
        {
            SessionState.SetBool(Prefix + "PendingEditorRestore", true);
            EditorApplication.ExitPlaymode();
        }
        else EditorApplication.Exit(Passed() ? 0 : 1);
    }
    static void FinishEditorRestore()
    {
        string path = SessionState.GetString(Prefix + "Report", "");
        if (!File.Exists(path)) return;
        report = JsonUtility.FromJson<Report>(File.ReadAllText(path));
        SavedScenes saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Prefix + "Scenes", "{}"));
        SceneSetup[] current = EditorSceneManager.GetSceneManagerSetup();
        bool same = saved.paths != null && saved.paths.Length == current.Length;
        for (int i = 0; same && i < current.Length; i++)
            same &= current[i].path == saved.paths[i] && current[i].isLoaded == saved.loaded[i] && current[i].isActive == saved.active[i];
        Record("editor.originalSceneSetupRestored", same);
        WriteReport();
        Debug.Log("Gameplay UI validation " + report.status + ". Report: " + path);
    }

    static Button FindButton(string first, string second = "")
    {
        foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            string label = "";
            foreach (Text text in button.GetComponentsInChildren<Text>(true)) label += text.text;
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true)) label += text.text;
            label = label.ToLowerInvariant();
            if (label.Contains(first) || (!string.IsNullOrEmpty(second) && label.Contains(second))) return button;
        }
        return null;
    }
    static Rect ScreenRect(RectTransform transform)
    {
        if (transform == null) return Rect.zero;
        Canvas canvas = transform.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector3[] corners = new Vector3[4]; transform.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        foreach (Vector3 corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    static bool WithinScreen(Rect rectangle)
    {
        return rectangle.width > 0 && rectangle.height > 0 && rectangle.xMin >= -1 && rectangle.yMin >= -1
            && rectangle.xMax <= Screen.width + 1 && rectangle.yMax <= Screen.height + 1;
    }
    static bool Capture(string name)
    {
        string directory = SessionState.GetString(Prefix + "Screenshots", "");
        if (string.IsNullOrEmpty(directory) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return false;
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".png");
        ScreenCapture.CaptureScreenshot(path); report.screenshots.Add(path); return true;
    }

    static IEnumerator Validate()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene("mainmenu", LoadSceneMode.Single);
        for (int i = 0; i < 3; i++) yield return null;
        Canvas.ForceUpdateCanvases();
        Button options = FindButton("opciones"), howTo = FindButton("cómo jugar", "como jugar");
        Record("mainMenu.optionsVisible", options != null && options.isActiveAndEnabled && options.interactable && WithinScreen(ScreenRect(options.transform as RectTransform)));
        Record("mainMenu.howToVisible", howTo != null && howTo.isActiveAndEnabled && howTo.interactable && WithinScreen(ScreenRect(howTo.transform as RectTransform)));
        if (Capture("main-menu")) for (int frame = 0; frame < 3; frame++) yield return null;
        CursorLockMode menuLock = Cursor.lockState; bool menuCursor = Cursor.visible;
        if (options != null) options.onClick.Invoke(); else OptionsMenuUI.Show(false);
        yield return null;
        Record("options.opensWithoutPausingMenu", OptionsMenuUI.IsOpen && Mathf.Approximately(Time.timeScale, 1)
            && Cursor.lockState == CursorLockMode.None && Cursor.visible);
        bool sliders = OptionsMenuUI.SensitivitySlider != null && OptionsMenuUI.VolumeSlider != null;
        Record("options.slidersAvailable", sliders);
        if (Capture("options-menu")) for (int frame = 0; frame < 3; frame++) yield return null;
        if (sliders)
        {
            OptionsMenuUI.SensitivitySlider.value = 0.08f;
            OptionsMenuUI.VolumeSlider.value = 0.45f;
            Record("options.liveSensitivityAndVolume", Mathf.Abs(GameSettings.MouseSensitivity - 0.08f) < 0.0001f
                && Mathf.Abs(AudioListener.volume - 0.45f) < 0.0001f);
        }
        GameSettings.SetMouseSensitivity(-5);
        bool low = Mathf.Approximately(GameSettings.MouseSensitivity, GameSettings.MinMouseSensitivity);
        GameSettings.SetMouseSensitivity(10);
        bool high = Mathf.Approximately(GameSettings.MouseSensitivity, GameSettings.MaxMouseSensitivity);
        OptionsMenuUI.ResetSensitivity();
        Record("options.clampAndResetDefault", low && high && Mathf.Approximately(GameSettings.MouseSensitivity, 0.035f));
        GameSettings.SetMouseSensitivity(0.08f);
        OptionsMenuUI.Close(); yield return null;
        Record("options.closeRestoresMenu", !OptionsMenuUI.IsOpen && Mathf.Approximately(Time.timeScale, 1)
            && Cursor.lockState == menuLock && Cursor.visible == menuCursor);
        if (howTo != null) howTo.onClick.Invoke(); else GameIntroductionUI.Show(false);
        yield return null;
        Record("mainMenu.howToOpens", GameIntroductionUI.IsOpen && Mathf.Approximately(Time.timeScale, 1));
        if (Capture("how-to-play")) for (int frame = 0; frame < 3; frame++) yield return null;
        GameIntroductionUI.Close(); yield return null;
        Record("mainMenu.howToCloses", !GameIntroductionUI.IsOpen && Mathf.Approximately(Time.timeScale, 1));

        ProceduralLevelGenerator.currentLevel = 1;
        SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        yield return null;
        double deadline = EditorApplication.timeSinceStartup + 35;
        SimplePlayerController player = null; GameplayStatusHUD hud = null;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            player = UnityEngine.Object.FindFirstObjectByType<SimplePlayerController>();
            hud = UnityEngine.Object.FindFirstObjectByType<GameplayStatusHUD>();
            if (player != null && hud != null && hud.KeynoteCounterRect != null && hud.ControlsPanelRect != null) break;
            yield return null;
        }
        Record("gameplay.hudAndPlayerInstalled", player != null && hud != null && hud.KeynoteCounterRect != null && hud.ControlsPanelRect != null);
        // The first-game introduction opens from a coroutine one frame after Start.
        for (int frame = 0; frame < 3; frame++) yield return null;
        Record("gameplay.introductionOpensFirstTime", GameIntroductionUI.IsOpen);
        if (Capture("first-game-introduction")) for (int frame = 0; frame < 3; frame++) yield return null;
        GameIntroductionUI.Close();
        for (int i = 0; i < 2; i++) yield return null;
        Record("gameplay.sensitivityLoaded", player != null && Mathf.Abs(player.mouseSensitivity - 0.08f) < 0.0001f);
        float beforeIntroTime = Time.timeScale; CursorLockMode beforeIntroLock = Cursor.lockState; bool beforeIntroCursor = Cursor.visible;
        GameIntroductionUI.Show(true); yield return null;
        Record("introduction.pausesAndUnlocks", GameIntroductionUI.IsOpen && Mathf.Approximately(Time.timeScale, 0)
            && Cursor.lockState == CursorLockMode.None && Cursor.visible);
        GameIntroductionUI.Close(); yield return null;
        Record("introduction.restoresGameplayState", !GameIntroductionUI.IsOpen && Mathf.Approximately(Time.timeScale, beforeIntroTime)
            && Mathf.Approximately(beforeIntroTime, 1) && beforeIntroLock == CursorLockMode.Locked && !beforeIntroCursor
            && Cursor.lockState == beforeIntroLock && Cursor.visible == beforeIntroCursor,
            "before=" + beforeIntroTime + "/" + beforeIntroLock + "/" + beforeIntroCursor
            + "; after=" + Time.timeScale + "/" + Cursor.lockState + "/" + Cursor.visible);

        PauseMenu pause = PauseMenu.EnsureController();
        pause.Pausar(); yield return null;
        if (Capture("pause-menu")) for (int frame = 0; frame < 3; frame++) yield return null;
        pause.ShowOptions(); yield return null;
        bool optionsWhilePaused = OptionsMenuUI.IsOpen && PauseMenu.IsPaused && Mathf.Approximately(Time.timeScale, 0);
        OptionsMenuUI.Close(); yield return null;
        Record("pause.optionsCloseKeepsPause", optionsWhilePaused && !OptionsMenuUI.IsOpen && PauseMenu.IsPaused
            && Mathf.Approximately(Time.timeScale, 0) && Cursor.lockState == CursorLockMode.None && Cursor.visible);
        pause.ShowHelp(); yield return null;
        bool helpWhilePaused = GameIntroductionUI.IsOpen && PauseMenu.IsPaused && Mathf.Approximately(Time.timeScale, 0);
        GameIntroductionUI.Close(); yield return null;
        Record("pause.helpCloseKeepsPause", helpWhilePaused && !GameIntroductionUI.IsOpen && PauseMenu.IsPaused
            && Mathf.Approximately(Time.timeScale, 0) && Cursor.lockState == CursorLockMode.None && Cursor.visible);
        pause.Reanudar(); yield return null;
        Record("pause.resumeRestoresInput", !PauseMenu.IsPaused && Mathf.Approximately(Time.timeScale, 1)
            && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible);

        // Inject representative UI data through public game APIs; hit accuracy is
        // covered separately by CourseIntegrationValidation.
        MissionManager missions = UnityEngine.Object.FindFirstObjectByType<MissionManager>();
        if (missions != null)
        {
            missions.isMissionUIActive = true;
            missions.currentMissionText = "MISIÓN: Recorre las habitaciones oscuras, recupera todos los keynotes y encuentra la salida segura. Usa una pastilla calmante para invertir la persecución y neutralizar a las entidades.";
        }
        if (player != null)
        {
            PlayerPowerState power = player.GetComponent<PlayerPowerState>();
            if (power != null) power.Activate(60);
            PredictiveThrower thrower = player.GetComponent<PredictiveThrower>();
            if (thrower != null)
            {
                GameObject impactProbe = new GameObject("UI impact metrics probe");
                BallisticProjectile projectile = impactProbe.AddComponent<BallisticProjectile>(); projectile.manualSimulation = true;
                var points = new List<TrajectoryPoint> { new TrajectoryPoint { time = 1 } };
                projectile.Initialize(thrower.predictor.CaptureModel(), Vector3.zero, player.transform, null, 0.1f, null, points, true, new RaycastHit());
                thrower.RecordImpact(projectile, Vector3.right * 0.12f, 1.1f);
                projectile.Cancel(); thrower.leadTargeting = true;
            }
        }
        yield return null;

        int[,] sizes = { {1600,900}, {1280,720}, {800,600}, {640,480} };
        for (int i = 0; i < sizes.GetLength(0); i++)
        {
            int width = sizes[i,0], height = sizes[i,1];
            string resizeDetail = SetResolution(width, height);
            for (int frame = 0; frame < 4; frame++) yield return null;
            Canvas.ForceUpdateCanvases();
            bool layout = false; string detail = "requested=" + width + "x" + height + "; actual=" + Screen.width + "x" + Screen.height + "; " + resizeDetail;
            if (hud != null && hud.KeynoteCounterRect != null && hud.ControlsPanelRect != null && hud.KeynoteCounterText != null)
            {
                Rect counter = ScreenRect(hud.KeynoteCounterRect), controls = ScreenRect(hud.ControlsPanelRect);
                Text text = hud.KeynoteCounterText;
                bool completeText = !string.IsNullOrEmpty(text.text) && text.rectTransform.rect.height + 1 >= text.preferredHeight;
                string controlTextDetail = "";
                foreach (Text controlText in hud.KeynoteCounterRect.parent.GetComponentsInChildren<Text>())
                {
                    completeText &= controlText.rectTransform.rect.height + 1 >= controlText.preferredHeight;
                    completeText &= WithinScreen(ScreenRect(controlText.rectTransform));
                    if (controlText.horizontalOverflow == HorizontalWrapMode.Overflow)
                        completeText &= controlText.rectTransform.rect.width + 1 >= controlText.preferredWidth;
                    controlTextDetail += "; " + controlText.name + "Height=" + controlText.preferredHeight + "/" + controlText.rectTransform.rect.height;
                }
                layout = Screen.width == width && Screen.height == height && WithinScreen(counter) && WithinScreen(controls)
                    && !counter.Overlaps(controls) && completeText;
                AnxietyUI anxietyUI = UnityEngine.Object.FindFirstObjectByType<AnxietyUI>();
                bool anxietyLayout = anxietyUI != null && anxietyUI.barraAnsiedad != null;
                var anxietyRects = new List<RectTransform>();
                if (anxietyUI != null)
                {
                    if (anxietyUI.barraAnsiedad != null) anxietyRects.Add(anxietyUI.barraAnsiedad.transform as RectTransform);
                    foreach (TMP_Text anxietyText in new TMP_Text[] { anxietyUI.textoPorcentaje, anxietyUI.textoEstadoMental, anxietyUI.textoFobias })
                    {
                        if (anxietyText == null) { anxietyLayout = false; continue; }
                        anxietyRects.Add(anxietyText.rectTransform);
                        anxietyLayout &= anxietyText.rectTransform.rect.height + 1 >= anxietyText.preferredHeight;
                    }
                }
                foreach (RectTransform anxietyRect in anxietyRects)
                {
                    Rect area = ScreenRect(anxietyRect);
                    anxietyLayout &= WithinScreen(area) && !area.Overlaps(counter) && !area.Overlaps(controls);
                }
                layout &= anxietyLayout;
                detail += "; anxietyLayout=" + anxietyLayout + "; case=long-mission,power,impact,lead";
                detail += "; counter=" + counter + "; controls=" + controls + "; textHeight=" + text.preferredHeight + "/" + text.rectTransform.rect.height + controlTextDetail;
            }
            Record("hud.layout." + width + "x" + height, layout, detail);
            if (Capture("hud-stress-" + width + "x" + height)) for (int frame = 0; frame < 3; frame++) yield return null;
        }
        if (player != null)
        {
            GameSettings.SetMouseSensitivity(0.06f); yield return null;
            bool live = Mathf.Abs(player.mouseSensitivity - 0.06f) < 0.0001f;
            SceneManager.LoadScene("GameScene", LoadSceneMode.Single); yield return null; yield return null;
            SimplePlayerController reloaded = UnityEngine.Object.FindFirstObjectByType<SimplePlayerController>();
            Record("settings.liveAndReloadedSensitivity", live && reloaded != null && Mathf.Abs(reloaded.mouseSensitivity - 0.06f) < 0.0001f
                && Mathf.Abs(PlayerPrefs.GetFloat(GameSettings.MouseSensitivityKey, -1) - 0.06f) < 0.0001f);
            GameIntroductionUI.Close();
        }
    }

    static string SetResolution(int width, int height)
    {
        Screen.SetResolution(width, height, false);
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return "Null graphics device: GameView resize unavailable.";
        try
        {
            Type viewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            Type sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            if (viewType == null || sizesType == null) return "GameView API unavailable.";
            if (gameView == null)
            {
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if (window.GetType() == viewType) { gameView = window; break; }
                bool created = gameView == null;
                if (created) gameView = EditorWindow.GetWindow(viewType);
                Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singletonType.GetProperty("instance", Flags).GetValue(null, null);
                object groupType = sizesType.GetProperty("currentGroupType", Flags).GetValue(sizes, null);
                sizeGroup = sizesType.GetMethod("GetGroup", Flags).Invoke(sizes, new[] { groupType });
                SessionState.SetInt(Prefix + "ViewIndex", (int)viewType.GetProperty("selectedSizeIndex", Flags).GetValue(gameView, null));
                SessionState.SetInt(Prefix + "CustomCount", (int)sizeGroup.GetType().GetMethod("GetCustomCount", Flags).Invoke(sizeGroup, null));
                SessionState.SetBool(Prefix + "ViewCreated", created);
                SessionState.SetBool(Prefix + "ViewSaved", true);
            }
            Type sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            Type modeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            object mode = Enum.Parse(modeType, "FixedResolution");
            object size = Activator.CreateInstance(sizeType, Flags, null, new object[] { mode, width, height, "Course UI temporary" }, null);
            sizeGroup.GetType().GetMethod("AddCustomSize", Flags).Invoke(sizeGroup, new[] { size });
            int builtin = (int)sizeGroup.GetType().GetMethod("GetBuiltinCount", Flags).Invoke(sizeGroup, null);
            int custom = (int)sizeGroup.GetType().GetMethod("GetCustomCount", Flags).Invoke(sizeGroup, null);
            viewType.GetProperty("selectedSizeIndex", Flags).SetValue(gameView, builtin + custom - 1, null);
            gameView.Repaint();
            return "Temporary fixed GameView resolution.";
        }
        catch (Exception e) { return "GameView resize failed: " + e.GetBaseException().Message; }
    }
    static void RestoreGameView()
    {
        if (!SessionState.GetBool(Prefix + "ViewSaved", false) || gameView == null || sizeGroup == null) return;
        gameView.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(gameView, SessionState.GetInt(Prefix + "ViewIndex", 0), null);
        int originalCount = SessionState.GetInt(Prefix + "CustomCount", 0);
        int count = (int)sizeGroup.GetType().GetMethod("GetCustomCount", Flags).Invoke(sizeGroup, null);
        int builtin = (int)sizeGroup.GetType().GetMethod("GetBuiltinCount", Flags).Invoke(sizeGroup, null);
        while (count > originalCount)
        {
            sizeGroup.GetType().GetMethod("RemoveCustomSize", Flags).Invoke(sizeGroup, new object[] { builtin + count - 1 }); count--;
        }
        if (SessionState.GetBool(Prefix + "ViewCreated", false)) gameView.Close(); else gameView.Repaint();
    }

    // Remove only this harness's named resolutions left behind by an interrupted
    // older run. Existing user-defined resolutions are preserved.
    static void RemoveLeftoverTemporaryResolutions()
    {
        Type sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        Type viewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (sizesType == null || viewType == null) return;
        object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", Flags).GetValue(null, null);
        object groupType = sizesType.GetProperty("currentGroupType", Flags).GetValue(sizes, null);
        object group = sizesType.GetMethod("GetGroup", Flags).Invoke(sizes, new[] { groupType });
        int builtin = (int)group.GetType().GetMethod("GetBuiltinCount", Flags).Invoke(group, null);
        int count = (int)group.GetType().GetMethod("GetCustomCount", Flags).Invoke(group, null);
        string[] labels = group.GetType().GetMethod("GetDisplayTexts", Flags).Invoke(group, null) as string[];
        if (labels == null) return;
        for (int i = count - 1; i >= 0; i--)
        {
            int globalIndex = builtin + i;
            if (globalIndex >= labels.Length || !labels[globalIndex].Contains("Course UI temporary")) continue;
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetType() != viewType) continue;
                PropertyInfo selection = viewType.GetProperty("selectedSizeIndex", Flags);
                int index = (int)selection.GetValue(window, null);
                if (index == globalIndex) selection.SetValue(window, 0, null);
                else if (index > globalIndex) selection.SetValue(window, index - 1, null);
            }
            group.GetType().GetMethod("RemoveCustomSize", Flags).Invoke(group, new object[] { globalIndex });
        }
    }
}

