using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Configuración de Vida")]
    public float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Barra de Vida Flotante")]
    public bool mostrarBarraDeVida = true;
    public float alturaBarra = 2.1f;
    public float distanciaVisibilidadBarra = 18f;

    [Header("Feedback Visual")]
    public Color colorFlashDanio = new Color(1f, 0.2f, 0.2f, 1f);
    public float duracionFlashDanio = 0.15f;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
    public bool IsDead => isDead;

    public System.Action<float, float> OnHealthChanged;
    public System.Action OnDeath;

    private bool isDead;
    private Renderer[] renderers;
    private List<Material> dynamicMaterials = new List<Material>();
    private List<Color> originalColors = new List<Color>();
    private Coroutine flashCoroutine;

    // Elementos de la UI de barra de vida
    private GameObject healthBarCanvasObj;
    private RectTransform healthBarCanvasRect;
    private Image healthBarFill;
    private Image healthBarBackground;
    private Text healthText;
    private Transform mainCameraTransform;

    void Awake()
    {
        currentHealth = maxHealth;
        isDead = false;
        CacheRenderersAndColors();
    }

    void Start()
    {
        if (mostrarBarraDeVida)
        {
            CrearBarraDeVidaMundo();
        }
        ActualizarBarraDeVida();
    }

    void OnEnable()
    {
        ResetHealth();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        ActualizarBarraDeVida();
    }

    void CacheRenderersAndColors()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        dynamicMaterials.Clear();
        originalColors.Clear();

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            // Ignorar renderers que pertenecen a la barra de vida flotante
            if (rend.GetComponentInParent<Canvas>() != null) continue;

            Material mat = rend.material;
            if (mat != null)
            {
                dynamicMaterials.Add(mat);
                Color orig = Color.white;
                if (mat.HasProperty("_BaseColor"))
                    orig = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color"))
                    orig = mat.color;

                originalColors.Add(orig);
            }
        }
    }

    public void TakeDamage(float amount, Vector3 hitPoint = default)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        ActualizarBarraDeVida();

        // Feedback de flash rojo
        if (gameObject.activeInHierarchy)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(RutinaFlashDanio());
        }

        // Crear número de daño flotante
        Vector3 posDanio = hitPoint != default ? hitPoint : transform.position + Vector3.up * 1.5f;
        CrearTextoDanioFlotante(amount, posDanio);

        // Notificar componentes de IA del impacto
        EnemyAIController ai = GetComponent<EnemyAIController>();
        if (ai != null) ai.ReceiveProjectileImpact();
        else SendMessage("ReceiveProjectileImpact", SendMessageOptions.DontRequireReceiver);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        ActualizarBarraDeVida();
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDeath?.Invoke();

        if (PlayerPowerState.Instance != null)
        {
            PlayerPowerState.Instance.RegisterDefeat();
        }

        // Apagar la barra de vida
        if (healthBarCanvasObj != null)
            healthBarCanvasObj.SetActive(false);

        // Notificar al controlador correspondiente para despawn / animación
        EnemyAIController ai = GetComponent<EnemyAIController>();
        if (ai != null)
        {
            ai.Defeat();
            return;
        }

        SendMessage("Defeat", SendMessageOptions.DontRequireReceiver);

        if (EntityManager.Instance != null)
        {
            EntityManager.Instance.DespawnEntity(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    IEnumerator RutinaFlashDanio()
    {
        // Poner color de flash
        for (int i = 0; i < dynamicMaterials.Count; i++)
        {
            Material mat = dynamicMaterials[i];
            if (mat == null) continue;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colorFlashDanio);
            if (mat.HasProperty("_Color")) mat.color = colorFlashDanio;
        }

        yield return new WaitForSeconds(duracionFlashDanio);

        // Restaurar color original
        for (int i = 0; i < dynamicMaterials.Count; i++)
        {
            Material mat = dynamicMaterials[i];
            if (mat == null) continue;
            Color orig = (i < originalColors.Count) ? originalColors[i] : Color.white;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", orig);
            if (mat.HasProperty("_Color")) mat.color = orig;
        }
    }

    void CrearBarraDeVidaMundo()
    {
        if (healthBarCanvasObj != null) return;

        // 1. Crear Canvas WorldSpace
        healthBarCanvasObj = new GameObject("HealthBar_Canvas");
        healthBarCanvasObj.transform.SetParent(transform, false);
        healthBarCanvasObj.transform.localPosition = Vector3.up * alturaBarra;

        Canvas canvas = healthBarCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;

        healthBarCanvasRect = healthBarCanvasObj.GetComponent<RectTransform>();
        healthBarCanvasRect.sizeDelta = new Vector2(1.4f, 0.22f);
        healthBarCanvasRect.localScale = Vector3.one * 0.8f;

        // 2. Fondo de la barra
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(healthBarCanvasObj.transform, false);
        healthBarBackground = bgObj.AddComponent<Image>();
        healthBarBackground.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. Relleno de la barra (Fill)
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(healthBarCanvasObj.transform, false);
        healthBarFill = fillObj.AddComponent<Image>();
        healthBarFill.color = Color.green;
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.02f, 0.1f);
        fillRect.anchorMax = new Vector2(0.98f, 0.9f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = Vector2.zero;

        // 4. Texto de HP
        GameObject textObj = new GameObject("Text_HP");
        textObj.transform.SetParent(healthBarCanvasObj.transform, false);
        healthText = textObj.AddComponent<Text>();
        healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        healthText.fontSize = 10;
        healthText.color = Color.white;
        healthText.alignment = TextAnchor.MiddleCenter;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }

    void ActualizarBarraDeVida()
    {
        if (healthBarFill == null) return;

        float pct = HealthPercentage;
        RectTransform fillRect = healthBarFill.rectTransform;
        fillRect.anchorMax = new Vector2(Mathf.Clamp(pct, 0.02f, 0.98f), fillRect.anchorMax.y);

        // Gradiente de color según porcentaje: Verde -> Amarillo -> Rojo
        if (pct > 0.5f)
            healthBarFill.color = Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f);
        else
            healthBarFill.color = Color.Lerp(Color.red, Color.yellow, pct * 2f);

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }
    }

    void LateUpdate()
    {
        if (!mostrarBarraDeVida || healthBarCanvasObj == null || isDead) return;

        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        if (mainCameraTransform != null)
        {
            float dist = Vector3.Distance(transform.position, mainCameraTransform.position);
            bool visible = dist <= distanciaVisibilidadBarra;
            if (healthBarCanvasObj.activeSelf != visible)
                healthBarCanvasObj.SetActive(visible);

            if (visible)
            {
                // Billboarding: que la barra siempre mire hacia la cámara
                healthBarCanvasObj.transform.rotation = Quaternion.LookRotation(healthBarCanvasObj.transform.position - mainCameraTransform.position);
            }
        }
    }

    void CrearTextoDanioFlotante(float amount, Vector3 position)
    {
        GameObject popupObj = new GameObject("DamagePopup");
        popupObj.transform.position = position + Vector3.up * 0.4f;

        Canvas canvas = popupObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;

        RectTransform rt = popupObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1f, 0.5f);
        rt.localScale = Vector3.one * 0.02f;

        Text txt = popupObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.text = $"-{Mathf.CeilToInt(amount)}";
        txt.fontSize = 28;
        txt.fontStyle = FontStyle.Bold;
        txt.color = new Color(1f, 0.35f, 0.1f, 1f); // Naranja rojizo brillante
        txt.alignment = TextAnchor.MiddleCenter;

        popupObj.AddComponent<DamagePopupAnimator>();
    }
}

/// <summary>
/// Anima el texto de daño flotante subiendo y desvaneciéndose.
/// </summary>
public class DamagePopupAnimator : MonoBehaviour
{
    private Text text;
    private float lifeTime = 0.85f;
    private float timer = 0f;
    private Vector3 moveDirection = Vector3.up * 1.2f;

    void Start()
    {
        text = GetComponent<Text>();
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        timer += Time.deltaTime;
        transform.position += moveDirection * Time.deltaTime;

        if (Camera.main != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }

        if (text != null)
        {
            float alpha = Mathf.Clamp01(1f - (timer / lifeTime));
            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }
    }
}
