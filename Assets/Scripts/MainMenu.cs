using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    void Awake()
    {
        GameSettings.Load();
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(GameSettings.VolumeKey, 1f));
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        OptionsMenuUI.EnsureEventSystem();
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) canvas = OptionsMenuUI.CreateCanvas("Menú principal", 0);
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Button options = null;
        foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
        {
            string name = button.name.ToLowerInvariant();
            if (name.Contains("option") || name.Contains("opcion")) { options = button; break; }
        }
        if (options == null)
            options = OptionsMenuUI.CreateButton(canvas.transform, "MainOptionsButton", "Opciones",
                new Vector2(0f, -56f), new Vector2(240f, 44f), ShowOptions);
        else
        {
            options.onClick = new Button.ButtonClickedEvent();
            options.onClick.AddListener(ShowOptions);
            SetLabel(options.gameObject, "Opciones");
        }
        Transform existingHelp = options.transform.parent.Find("HowToPlayButton");
        if (existingHelp == null)
        {
            RectTransform reference = options.GetComponent<RectTransform>();
            float spacing = Mathf.Max(52f, reference.sizeDelta.y + 22f);
            foreach (Button sibling in options.transform.parent.GetComponentsInChildren<Button>(false))
            {
                RectTransform rect = sibling.GetComponent<RectTransform>();
                if (sibling != options && rect.parent == reference.parent && rect.anchoredPosition.y < reference.anchoredPosition.y - 1f)
                    rect.anchoredPosition -= Vector2.up * spacing;
            }
            GameObject help = Instantiate(options.gameObject, options.transform.parent);
            help.name = "HowToPlayButton";
            help.GetComponent<RectTransform>().anchoredPosition = reference.anchoredPosition - Vector2.up * spacing;
            Button helpButton = help.GetComponent<Button>();
            helpButton.onClick = new Button.ButtonClickedEvent();
            helpButton.onClick.AddListener(ShowHelp);
            SetLabel(help, "Cómo jugar");
        }
        ArrangeMainButtons(canvas, options);
    }

    void ArrangeMainButtons(Canvas canvas, Button options)
    {
        Button start = null, exit = null;
        Button help = options.transform.parent.Find("HowToPlayButton").GetComponent<Button>();
        foreach (Button candidate in canvas.GetComponentsInChildren<Button>(true))
        {
            if (candidate == options || candidate == help || candidate.transform.parent != options.transform.parent) continue;
            if (candidate.name.ToLowerInvariant().Contains("start") || candidate.name.ToLowerInvariant().Contains("play")) start = candidate;
            else if (exit == null) exit = candidate;
        }
        if (start == null) start = OptionsMenuUI.CreateButton(options.transform.parent, "PlayButton", "Jugar", Vector2.zero, new Vector2(240f, 44f), LoadDemoScene);
        if (exit == null) exit = OptionsMenuUI.CreateButton(options.transform.parent, "QuitButton", "Salir", Vector2.zero, new Vector2(240f, 44f), QuitGame);
        start.onClick = new Button.ButtonClickedEvent(); start.onClick.AddListener(LoadDemoScene);
        exit.onClick = new Button.ButtonClickedEvent(); exit.onClick.AddListener(QuitGame);
        Button[] buttons = { start, options, help, exit };
        string[] labels = { "Jugar", "Opciones", "Cómo jugar", "Salir" };
        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform rect = buttons[i].GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -10f - 56f * i);
            rect.sizeDelta = new Vector2(240f, 44f);
            SetLabel(buttons[i].gameObject, labels[i]);
            TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.enableAutoSizing = true; label.fontSizeMin = 17f; label.fontSizeMax = 24f;
                label.margin = new Vector4(8f, 3f, 8f, 3f); label.raycastTarget = false;
            }
        }
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(start.gameObject);
        foreach (TMP_Text title in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (title.name.ToLowerInvariant() != "title") continue;
            RectTransform rect = title.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 108f); rect.sizeDelta = new Vector2(760f, 90f);
            title.margin = Vector4.zero; title.enableAutoSizing = true;
            title.fontSizeMin = 26f; title.fontSizeMax = 36f; title.alignment = TextAlignmentOptions.Center;
            title.raycastTarget = false;
        }
    }

    static void SetLabel(GameObject button, string value)
    {
        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = value;
        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null) text.text = value;
    }
    public void ShowOptions() { OptionsMenuUI.Show(false); }
    public void ShowHelp() { GameIntroductionUI.Show(false); }
    public void LoadScene(string sceneName)
    {
        OptionsMenuUI.Close();
        GameIntroductionUI.Close();
        GameSettings.Save();
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
    public void LoadDemoScene() { LoadScene("GameScene"); }
    public void QuitGame() { GameSettings.Save(); Application.Quit(); }
}
