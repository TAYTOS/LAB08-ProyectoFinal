using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Objetivo y controles en un único layout que crece con el texto.</summary>
public class GameplayStatusHUD : MonoBehaviour
{
    public RectTransform KeynoteCounterRect => counter != null ? counter.rectTransform : null;
    public RectTransform CounterRect => KeynoteCounterRect;
    public RectTransform ControlsPanelRect { get; private set; }
    public Text KeynoteCounterText => counter;
    public Text CounterText => counter;

    GameObject canvasObject;
    RectTransform stack;
    Text counter, mission, status, feedback;
    MissionManager missions;
    PlayerPowerState power;
    AnxietyManager anxiety;
    PredictiveThrower thrower;
    AnxietyUI anxietyUI;
    bool anxietyLayoutBound;

    void Awake()
    {
        power = GetComponent<PlayerPowerState>(); anxiety = GetComponent<AnxietyManager>();
        thrower = GetComponent<PredictiveThrower>();
        canvasObject = new GameObject("Gameplay HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 25;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
        stack = new GameObject("Objective and controls", typeof(RectTransform)).GetComponent<RectTransform>();
        stack.SetParent(canvas.transform, false);
        stack.anchorMin = stack.anchorMax = stack.pivot = Vector2.zero;
        stack.anchoredPosition = new Vector2(18, 18); stack.sizeDelta = new Vector2(540, 0);
        AddLayout(stack.gameObject, 0, 8);
        counter = AddText("Keynote counter", stack, 21, FontStyle.Bold);
        counter.gameObject.AddComponent<LayoutElement>().minHeight = 28;
        counter.color = new Color(0.72f, 1f, 0.83f);
        mission = AddText("Mission notification", stack, 17, FontStyle.Bold);
        mission.gameObject.SetActive(false);
        ControlsPanelRect = new GameObject("Controls panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        ControlsPanelRect.SetParent(stack, false);
        ControlsPanelRect.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.045f, 0.88f);
        ControlsPanelRect.GetComponent<Image>().raycastTarget = false;
        AddLayout(ControlsPanelRect.gameObject, 13, 7);
        status = AddText("Current state", ControlsPanelRect, 18, FontStyle.Bold);
        Text controls = AddText("Controls", ControlsPanelRect, 15, FontStyle.Normal);
        controls.text = "E: recoger keynotes y pastillas / neutralizar con poder\n" +
            "Clic derecho: apuntar | Clic izquierdo / G: lanzar\n" +
            "T: anticipar blanco | Esc: pausa, ayuda y opciones";
        feedback = AddText("Gameplay feedback", ControlsPanelRect, 15, FontStyle.Normal);
        feedback.color = new Color(0.8f, 0.9f, 0.86f);
        Refresh();
    }

    static void AddLayout(GameObject target, int padding, float spacing)
    {
        var group = target.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(padding, padding, padding, padding); group.spacing = spacing;
        group.childAlignment = TextAnchor.LowerLeft;
        group.childControlWidth = group.childControlHeight = true;
        group.childForceExpandWidth = true; group.childForceExpandHeight = false;
        target.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static Text AddText(string name, Transform parent, int size, FontStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size; text.fontStyle = style; text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
        return text;
    }

    void Update() { Refresh(); }

    void LateUpdate()
    {
        // Start del HUD anterior configura sus anclajes; después se reserva una zona
        // superior que nunca invade el objetivo y los controles de abajo.
        if (anxietyLayoutBound) return;
        if (anxietyUI == null) anxietyUI = FindFirstObjectByType<AnxietyUI>();
        if (anxietyUI == null) return;
        PlaceAnxietyText(anxietyUI.textoPorcentaje, new Vector2(18, -18), new Vector2(300, 28), 20);
        PlaceAnxietyText(anxietyUI.textoEstadoMental, new Vector2(18, -72), new Vector2(400, 26), 16);
        PlaceAnxietyText(anxietyUI.textoFobias, new Vector2(18, -104), new Vector2(600, 58), 16);
        if (anxietyUI.barraAnsiedad != null)
        {
            RectTransform bar = anxietyUI.barraAnsiedad.GetComponent<RectTransform>();
            PlaceTopLeft(bar, new Vector2(18, -54), new Vector2(280, 12));
            anxietyUI.barraAnsiedad.interactable = false;
            foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        }
        anxietyLayoutBound = true;
    }

    void PlaceAnxietyText(TMP_Text text, Vector2 position, Vector2 size, float fontSize)
    {
        if (text == null) return;
        PlaceTopLeft(text.rectTransform, position, size);
        text.fontSize = fontSize; text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.margin = Vector4.zero; text.raycastTarget = false;
    }

    void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    void Refresh()
    {
        if (missions == null) missions = FindFirstObjectByType<MissionManager>();
        if (stack != null)
        {
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            stack.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(540, Mathf.Max(200, canvasRect.rect.width - 36)));
        }
        counter.text = missions != null ? "Keynotes: " + missions.collectedKeynotes + " / " + missions.totalKeynotes : "Keynotes: 0 / 0";
        bool showMission = missions != null && missions.isMissionUIActive;
        if (mission.gameObject.activeSelf != showMission) mission.gameObject.SetActive(showMission);
        if (showMission) { mission.text = missions.currentMissionText; mission.color = missions.currentMissionColor; }
        bool empowered = power != null && power.IsEmpowered;
        status.text = empowered ? "PASTILLA ACTIVA: " + power.RemainingTime.ToString("0") + " s · Las entidades huyen"
            : anxiety != null && anxiety.IsCritical ? "ANSIEDAD CRÍTICA · Busca una pastilla verde"
            : "Explora con cuidado · Reúne los keynotes";
        status.color = empowered ? new Color(0.6f, 1f, 0.7f) : Color.white;
        feedback.text = "Entidades neutralizadas: " + (power != null ? power.DefeatedEntities : 0);
        if (thrower != null && thrower.HasImpactComparison)
            feedback.text += "\nDiferencia del impacto previsto: " + thrower.LastPredictionError.ToString("0.00") + " m";
        if (thrower != null && thrower.leadTargeting)
            feedback.text += thrower.LeadSolutionValid ? "\nAnticipación: blanco en movimiento calculado" : "\nAnticipación activa · Apunta a una entidad";
    }

    void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }
}
