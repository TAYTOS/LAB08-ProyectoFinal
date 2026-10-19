using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class OptionsMenuUI : MonoBehaviour
{
    private static OptionsMenuUI instance;
    private static int consumedEscapeFrame = -1;
    public static bool IsOpen => instance != null && instance.open;
    public static int LastClosedFrame { get; private set; } = -1;
    public static bool EscapeConsumedThisFrame => consumedEscapeFrame == Time.frameCount;
    public static Slider SensitivitySlider => instance != null ? instance.sensitivitySlider : null;
    public static Slider VolumeSlider => instance != null ? instance.volumeSlider : null;

    private bool open;
    private bool pausedGameplay;
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private GameObject previousSelection;
    private Slider sensitivitySlider;
    private Slider volumeSlider;
    private Text sensitivityValue;
    private Text volumeValue;
    private static readonly Color PanelColor = new Color(0.075f, 0.085f, 0.105f, 1f);
    private static readonly Color AccentColor = new Color(0.72f, 0.88f, 0.83f, 1f);

    public static void ConsumeEscapeForCurrentFrame() { consumedEscapeFrame = Time.frameCount; }

    public static void Show(bool pauseGameplay)
    {
        if (IsOpen || GameIntroductionUI.IsOpen) return;
        if (instance == null)
        {
            Canvas canvas = CreateCanvas("Opciones", 3000);
            instance = canvas.gameObject.AddComponent<OptionsMenuUI>();
            instance.Build();
        }
        instance.previousTimeScale = Time.timeScale;
        instance.previousCursorLock = Cursor.lockState;
        instance.previousCursorVisible = Cursor.visible;
        instance.previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        instance.pausedGameplay = pauseGameplay;
        instance.open = true;
        ConsumeEscapeForCurrentFrame();
        instance.gameObject.SetActive(true);
        if (pauseGameplay) Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        instance.RefreshSensitivity(GameSettings.MouseSensitivity);
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(GameSettings.VolumeKey, AudioListener.volume));
        AudioListener.volume = volume;
        instance.volumeSlider.SetValueWithoutNotify(volume);
        instance.RefreshVolume(volume);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(instance.sensitivitySlider.gameObject);
    }

    public static void Close()
    {
        if (!IsOpen) return;
        OptionsMenuUI menu = instance;
        menu.open = false;
        GameSettings.Save();
        LastClosedFrame = Time.frameCount;
        ConsumeEscapeForCurrentFrame();
        if (menu.pausedGameplay) Time.timeScale = menu.previousTimeScale;
        Cursor.lockState = menu.previousCursorLock;
        Cursor.visible = menu.previousCursorVisible;
        menu.gameObject.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(menu.previousSelection != null && menu.previousSelection.activeInHierarchy
                ? menu.previousSelection : null);
    }

    public static void ResetSensitivity() { GameSettings.ResetMouseSensitivity(); }
    void OnEnable() { GameSettings.SensitivityChanged += RefreshSensitivity; }
    void OnDisable() { GameSettings.SensitivityChanged -= RefreshSensitivity; }
    void OnDestroy()
    {
        if (instance != this) return;
        if (open)
        {
            GameSettings.Save();
            if (pausedGameplay) Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
        instance = null;
    }
    void Update()
    {
        if (open && !EscapeConsumedThisFrame && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }
    void RefreshSensitivity(float value)
    {
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(value);
        if (sensitivityValue != null) sensitivityValue.text = value.ToString("0.000", CultureInfo.InvariantCulture);
    }
    void RefreshVolume(float value)
    {
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    void Build()
    {
        RectTransform backdrop = CreateRect("Fondo modal", transform, Vector2.zero, Vector2.zero);
        Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.02f, 0.025f, 0.94f);
        RectTransform card = CreateRect("Panel de opciones", backdrop, Vector2.zero, new Vector2(640f, 440f));
        card.gameObject.AddComponent<Image>().color = PanelColor;
        CreateText(card, "Título", "OPCIONES", new Vector2(0f, 170f), new Vector2(550f, 48f), 30, TextAnchor.MiddleCenter);
        CreateText(card, "Descripción", "Ajusta la cámara y el sonido a tu ritmo.", new Vector2(0f, 126f), new Vector2(550f, 32f), 18, TextAnchor.MiddleCenter);
        CreateText(card, "Etiqueta sensibilidad", "Sensibilidad del mouse", new Vector2(-60f, 70f), new Vector2(420f, 30f), 20, TextAnchor.MiddleLeft);
        sensitivityValue = CreateText(card, "Valor sensibilidad", "", new Vector2(235f, 70f), new Vector2(80f, 30f), 20, TextAnchor.MiddleRight);
        sensitivityValue.color = AccentColor;
        sensitivitySlider = CreateSlider(card, "SensitivitySlider", new Vector2(0f, 30f), 540f,
            GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity);
        sensitivitySlider.onValueChanged.AddListener(GameSettings.SetMouseSensitivity);
        CreateText(card, "Baja", "Baja", new Vector2(-235f, 0f), new Vector2(70f, 26f), 15, TextAnchor.MiddleLeft);
        CreateText(card, "Alta", "Alta", new Vector2(235f, 0f), new Vector2(70f, 26f), 15, TextAnchor.MiddleRight);
        CreateText(card, "Etiqueta volumen", "Volumen general", new Vector2(-60f, -48f), new Vector2(420f, 30f), 20, TextAnchor.MiddleLeft);
        volumeValue = CreateText(card, "Valor volumen", "", new Vector2(235f, -48f), new Vector2(80f, 30f), 20, TextAnchor.MiddleRight);
        volumeSlider = CreateSlider(card, "VolumeSlider", new Vector2(0f, -88f), 540f, 0f, 1f);
        volumeSlider.onValueChanged.AddListener(value =>
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(GameSettings.VolumeKey, AudioListener.volume);
            RefreshVolume(AudioListener.volume);
        });
        CreateText(card, "Aplicación en vivo", "Los cambios se aplican al mover el control y se guardan al cerrar.",
            new Vector2(0f, -125f), new Vector2(560f, 30f), 14, TextAnchor.MiddleCenter);
        CreateButton(card, "ResetSensitivityButton", "Restablecer sensibilidad", new Vector2(-135f, -178f), new Vector2(265f, 44f), ResetSensitivity);
        CreateButton(card, "CloseOptionsButton", "Cerrar", new Vector2(145f, -178f), new Vector2(230f, 44f), Close);
    }

    public static Canvas CreateCanvas(string objectName, int sortingOrder)
    {
        EnsureEventSystem();
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.layer = 5;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        EventSystem current = EventSystem.current != null ? EventSystem.current : Object.FindFirstObjectByType<EventSystem>();
        if (current == null)
        {
            GameObject go = new GameObject("UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        else if (current.GetComponent<BaseInputModule>() == null)
            current.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    public static RectTransform CreateRect(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = 5;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    public static Text CreateText(Transform parent, string objectName, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        Text text = CreateRect(objectName, parent, position, size).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.fontSize = fontSize; text.alignment = alignment;
        text.color = new Color(0.93f, 0.95f, 0.94f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return text;
    }
    public static Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityAction action)
    {
        RectTransform rect = CreateRect(objectName, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.2f, 0.21f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.76f, 0.94f, 0.88f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        Text text = CreateText(rect, "Texto", label, Vector2.zero, size - new Vector2(14f, 4f), 18, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(7f, 2f); text.rectTransform.offsetMax = new Vector2(-7f, -2f);
        button.onClick.AddListener(action);
        return button;
    }
    public static Slider CreateSlider(Transform parent, string objectName, Vector2 position, float width, float min, float max)
    {
        RectTransform root = CreateRect(objectName, parent, position, new Vector2(width, 28f));
        Slider slider = root.gameObject.AddComponent<Slider>();
        RectTransform track = CreateRect("Pista", root, Vector2.zero, new Vector2(width, 7f));
        track.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.25f, 0.27f);
        RectTransform fillArea = CreateRect("Área relleno", root, Vector2.zero, Vector2.zero);
        Stretch(fillArea); fillArea.offsetMin = new Vector2(5f, 10f); fillArea.offsetMax = new Vector2(-5f, -10f);
        RectTransform fill = CreateRect("Relleno", fillArea, Vector2.zero, Vector2.zero);
        Stretch(fill); fill.gameObject.AddComponent<Image>().color = AccentColor;
        RectTransform handleArea = CreateRect("Área control", root, Vector2.zero, Vector2.zero);
        Stretch(handleArea); handleArea.offsetMin = new Vector2(8f, 0f); handleArea.offsetMax = new Vector2(-8f, 0f);
        RectTransform handle = CreateRect("Control", handleArea, Vector2.zero, new Vector2(18f, 26f));
        Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Color.white;
        slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
        slider.minValue = min; slider.maxValue = max; slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }
}
