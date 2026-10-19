using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Ayuda consultable y bienvenida de la primera partida; conserva la pausa previa.</summary>
public class GameIntroductionUI : MonoBehaviour
{
    public const string SeenKey = "Lab6.IntroductionSeen.v1";
    static GameIntroductionUI instance;
    public static bool IsOpen => instance != null && instance.open;
    bool open, pausesGameplay, firstGame;
    float previousTimeScale;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;
    GameObject previousSelection;
    Text closeLabel;

    public static void Show(bool pauseGameplay)
    {
        if (IsOpen || OptionsMenuUI.IsOpen) return;
        if (instance == null)
        {
            Canvas canvas = OptionsMenuUI.CreateCanvas("Cómo jugar", 2900);
            instance = canvas.gameObject.AddComponent<GameIntroductionUI>();
            instance.Build();
        }
        instance.previousTimeScale = Time.timeScale;
        instance.previousCursorLock = Cursor.lockState;
        instance.previousCursorVisible = Cursor.visible;
        instance.previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        instance.pausesGameplay = pauseGameplay;
        instance.firstGame = false;
        instance.closeLabel.text = pauseGameplay ? "Continuar" : "Volver";
        instance.open = true;
        OptionsMenuUI.ConsumeEscapeForCurrentFrame();
        instance.gameObject.SetActive(true);
        if (pauseGameplay) Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public static void ShowForFirstGame()
    {
        if (PlayerPrefs.GetInt(SeenKey, 0) != 0) return;
        Show(true);
        if (IsOpen) { instance.firstGame = true; instance.closeLabel.text = "Comenzar"; }
    }

    public static void Close()
    {
        if (!IsOpen) return;
        if (instance.firstGame) { PlayerPrefs.SetInt(SeenKey, 1); PlayerPrefs.Save(); }
        OptionsMenuUI.ConsumeEscapeForCurrentFrame();
        instance.Restore();
        instance.open = false;
        instance.gameObject.SetActive(false);
    }

    void Restore()
    {
        if (pausesGameplay) Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
    }

    void Update()
    {
        if (!open || Keyboard.current == null || OptionsMenuUI.EscapeConsumedThisFrame) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) Close();
    }

    void OnDestroy()
    {
        if (instance != this) return;
        if (open) Restore();
        instance = null;
    }

    void Build()
    {
        RectTransform backdrop = OptionsMenuUI.CreateRect("Fondo", transform, Vector2.zero, Vector2.zero);
        OptionsMenuUI.Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.02f, 0.025f, 0.96f);
        RectTransform card = OptionsMenuUI.CreateRect("Guía de juego", backdrop, Vector2.zero, new Vector2(810, 490));
        card.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.085f, 0.105f);
        OptionsMenuUI.CreateText(card, "Título", "ANTES DE ENTRAR AL LABERINTO", new Vector2(0, 205), new Vector2(750, 38), 27, TextAnchor.MiddleCenter);
        OptionsMenuUI.CreateText(card, "Objetivo", "Reúne todos los keynotes de cada nivel para avanzar y escapar.",
            new Vector2(0, 162), new Vector2(750, 36), 19, TextAnchor.MiddleCenter);
        AddRow(card, "01  EXPLORA", "WASD: moverte · Shift: correr · F: linterna · M: mapa\nAcércate a un keynote, míralo y pulsa E para recogerlo.", 104);
        AddRow(card, "02  CONTROLA LA ANSIEDAD", "Las entidades aparecen desde el nivel 1. Mirarlas mucho tiempo o salir\nrápido de su habitación aumenta la ansiedad. En estado crítico te persiguen.", 24);
        AddRow(card, "03  INVIERTE LA PERSECUCIÓN", "Busca una pastilla verde y pulsa E: te calma y te da 15 s de poder.\nLas entidades huyen; acércate y pulsa E para neutralizarlas.", -56);
        AddRow(card, "04  PRUEBA LOS LANZAMIENTOS", "Clic derecho: ver la trayectoria · Clic izquierdo / G: lanzar\nT: anticipar un blanco móvil · Esc: pausa, ayuda y sensibilidad.", -136);
        Button close = OptionsMenuUI.CreateButton(card, "ContinueIntroductionButton", "Continuar", new Vector2(0, -210), new Vector2(260, 42), Close);
        closeLabel = close.GetComponentInChildren<Text>();
    }

    static void AddRow(Transform parent, string heading, string content, float y)
    {
        var title = OptionsMenuUI.CreateText(parent, heading, heading, new Vector2(0, y + 17), new Vector2(730, 24), 17, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold; title.color = new Color(0.72f, 0.93f, 0.83f);
        OptionsMenuUI.CreateText(parent, heading + " detalles", content, new Vector2(0, y - 17), new Vector2(730, 46), 17, TextAnchor.MiddleLeft);
    }
}

/// <summary>Espera al Start de todos los componentes antes de pausar la bienvenida.</summary>
public class FirstGameIntroduction : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return null;
        GameIntroductionUI.ShowForFirstGame();
        Destroy(this);
    }
}
