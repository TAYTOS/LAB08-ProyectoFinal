using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Conecta las funciones a las escenas existentes sin depender de asignaciones manuales.</summary>
public static class GameplayFeatureInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Subscribe()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallCurrent() { Install(); }
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }

    public static void Install()
    {
        if (Object.FindFirstObjectByType<ProceduralLevelGenerator>() == null) return;
        if (EntityManager.Instance == null && Object.FindFirstObjectByType<EntityManager>() == null)
            new GameObject("EntityManager").AddComponent<EntityManager>();
        SimplePlayerController player = Object.FindFirstObjectByType<SimplePlayerController>();
        if (player == null) return;
        if (player.GetComponent<PlayerPowerState>() == null) player.gameObject.AddComponent<PlayerPowerState>();
        PredictiveThrower thrower = player.GetComponent<PredictiveThrower>();
        if (thrower == null) thrower = player.gameObject.AddComponent<PredictiveThrower>();
        thrower.showControls = false;
        if (player.GetComponent<GameplayStatusHUD>() == null) player.gameObject.AddComponent<GameplayStatusHUD>();
        PauseMenu.EnsureController();
        if (PlayerPrefs.GetInt(GameIntroductionUI.SeenKey, 0) == 0 && player.GetComponent<FirstGameIntroduction>() == null)
            player.gameObject.AddComponent<FirstGameIntroduction>();
    }
}
