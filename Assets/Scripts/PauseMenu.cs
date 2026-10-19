using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

// Run after modal windows so one Escape press has exactly one owner.
[DefaultExecutionOrder(11000)]
public class PauseMenu : MonoBehaviour
{
    public GameObject panelPausa;
    public Slider sliderVolumen;
    public string nombreEscenaMenu = "mainmenu";
    public bool juegoPausado { get; private set; }
    public static PauseMenu Instance { get; private set; }
    public static bool IsPaused => Instance != null && Instance.juegoPausado;
    private float resumeTimeScale = 1f;
    private CursorLockMode resumeCursorLock;
    private bool resumeCursorVisible;
    private bool introductionWasOpen;
    private bool configured;
    private GameObject generatedCanvas;

    public static PauseMenu EnsureController()
    {
        if (Instance != null && Instance.isActiveAndEnabled) return Instance;
        PauseMenu[] existing = Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PauseMenu candidate in existing)
        {
            if (candidate.isActiveAndEnabled) return candidate;
        }
        // Never put the input listener on the panel that it has to hide.
        GameObject host = new GameObject("Pause input controller");
        PauseMenu controller = host.AddComponent<PauseMenu>();
        foreach (PauseMenu candidate in existing) candidate.enabled = false;
        return controller;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        if (panelPausa != null && (panelPausa == gameObject || transform.IsChildOf(panelPausa.transform)))
        {
            // Legacy scene: retain public button targets as delegates, move input
            // to a separate active host before hiding this hierarchy.
            GameObject oldPanel = panelPausa;
            enabled = false;
            PauseMenu replacement = new GameObject("Pause input controller").AddComponent<PauseMenu>();
            replacement.nombreEscenaMenu = "mainmenu";
            oldPanel.SetActive(false);
            return;
        }
        Instance = this;
        if (panelPausa != null) panelPausa.SetActive(false);
        nombreEscenaMenu = "mainmenu"; // Fix the differently-cased serialized scene name.
        BuildPausePanel();
        configured = true;
        juegoPausado = false;
        panelPausa.SetActive(false);
        CambiarVolumenGeneral(PlayerPrefs.GetFloat(GameSettings.VolumeKey, 1f));
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (juegoPausado)
        {
            Time.timeScale = resumeTimeScale;
            Cursor.lockState = resumeCursorLock;
            Cursor.visible = resumeCursorVisible;
        }
        if (generatedCanvas != null) Destroy(generatedCanvas);
        Instance = null;
    }

    void Update()
    {
        if (!configured || Instance != this) return;
        if (sliderVolumen != null && !Mathf.Approximately(sliderVolumen.value, AudioListener.volume))
            sliderVolumen.SetValueWithoutNotify(AudioListener.volume);
        if (GameIntroductionUI.IsOpen) { introductionWasOpen = true; return; }
        if (introductionWasOpen) { introductionWasOpen = false; return; }
        if (OptionsMenuUI.IsOpen || OptionsMenuUI.EscapeConsumedThisFrame) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            OptionsMenuUI.ConsumeEscapeForCurrentFrame();
            if (juegoPausado) Reanudar(); else Pausar();
        }
    }

    public void Pausar()
    {
        if (Instance != null && Instance != this) { Instance.Pausar(); return; }
        if (juegoPausado || OptionsMenuUI.IsOpen || GameIntroductionUI.IsOpen) return;
        resumeTimeScale = Time.timeScale;
        resumeCursorLock = Cursor.lockState;
        resumeCursorVisible = Cursor.visible;
        juegoPausado = true;
        OptionsMenuUI.ConsumeEscapeForCurrentFrame();
        panelPausa.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Button first = panelPausa.GetComponentInChildren<Button>();
        if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    public void Reanudar()
    {
        if (Instance != null && Instance != this) { Instance.Reanudar(); return; }
        if (GameIntroductionUI.IsOpen) return;
        OptionsMenuUI.Close();
        if (!juegoPausado) return;
        juegoPausado = false;
        if (panelPausa != null) panelPausa.SetActive(false);
        Time.timeScale = resumeTimeScale;
        Cursor.lockState = resumeCursorLock;
        Cursor.visible = resumeCursorVisible;
        OptionsMenuUI.ConsumeEscapeForCurrentFrame();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void ShowOptions()
    {
        if (Instance != null && Instance != this) { Instance.ShowOptions(); return; }
        if (!juegoPausado) Pausar();
        OptionsMenuUI.Show(true);
    }
    public void ShowHelp()
    {
        if (Instance != null && Instance != this) { Instance.ShowHelp(); return; }
        if (!juegoPausado) Pausar();
        GameIntroductionUI.Show(true);
    }

    public void CambiarVolumenGeneral(float volumen)
    {
        if (Instance != null && Instance != this) { Instance.CambiarVolumenGeneral(volumen); return; }
        AudioListener.volume = Mathf.Clamp01(volumen);
        PlayerPrefs.SetFloat(GameSettings.VolumeKey, AudioListener.volume);
        PlayerPrefs.Save();
        if (sliderVolumen != null) sliderVolumen.SetValueWithoutNotify(AudioListener.volume);
    }
    public void AumentarSonido() { CambiarVolumenGeneral(AudioListener.volume + 0.1f); }
    public void BajarSonido() { CambiarVolumenGeneral(AudioListener.volume - 0.1f); }

    public void VolverAlMenuPrincipal()
    {
        if (Instance != null && Instance != this) { Instance.VolverAlMenuPrincipal(); return; }
        OptionsMenuUI.Close();
        GameIntroductionUI.Close();
        juegoPausado = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("mainmenu");
    }

    void BuildPausePanel()
    {
        Canvas canvas = OptionsMenuUI.CreateCanvas("Menú de pausa", 2000);
        generatedCanvas = canvas.gameObject;
        panelPausa = generatedCanvas;
        RectTransform backdrop = OptionsMenuUI.CreateRect("Fondo", canvas.transform, Vector2.zero, Vector2.zero);
        OptionsMenuUI.Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.02f, 0.025f, 0.9f);
        RectTransform card = OptionsMenuUI.CreateRect("Panel", backdrop, Vector2.zero, new Vector2(500f, 480f));
        card.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.085f, 0.105f);
        OptionsMenuUI.CreateText(card, "Título", "PAUSA", new Vector2(0f, 188f), new Vector2(430f, 44f), 32, TextAnchor.MiddleCenter);
        OptionsMenuUI.CreateText(card, "Subtítulo", "Respira. Continúa cuando estés listo.", new Vector2(0f, 148f), new Vector2(440f, 30f), 17, TextAnchor.MiddleCenter);
        OptionsMenuUI.CreateButton(card, "ResumeButton", "Continuar", new Vector2(0f, 96f), new Vector2(360f, 44f), Reanudar);
        OptionsMenuUI.CreateButton(card, "PauseOptionsButton", "Opciones", new Vector2(0f, 42f), new Vector2(360f, 44f), ShowOptions);
        OptionsMenuUI.CreateButton(card, "PauseHelpButton", "Ayuda: cómo jugar", new Vector2(0f, -12f), new Vector2(360f, 44f), ShowHelp);
        OptionsMenuUI.CreateButton(card, "MainMenuButton", "Volver al menú principal", new Vector2(0f, -66f), new Vector2(360f, 44f), VolverAlMenuPrincipal);
        OptionsMenuUI.CreateText(card, "Volumen", "Volumen general", new Vector2(0f, -122f), new Vector2(360f, 30f), 18, TextAnchor.MiddleLeft);
        sliderVolumen = OptionsMenuUI.CreateSlider(card, "PauseVolumeSlider", new Vector2(0f, -160f), 360f, 0f, 1f);
        sliderVolumen.onValueChanged.AddListener(CambiarVolumenGeneral);
        OptionsMenuUI.CreateText(card, "Escape", "ESC para continuar", new Vector2(0f, -208f), new Vector2(360f, 24f), 14, TextAnchor.MiddleCenter);
    }
}
