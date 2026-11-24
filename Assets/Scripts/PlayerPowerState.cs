using UnityEngine;
using UnityEngine.InputSystem;
using System;

public enum PillPowerMode
{
    Ligero,       // 25 de daño por impacto, mayor duración (25s) y velocidad
    Estandar,     // 50 de daño por impacto, duración equilibrada (15s)
    Sobrecarga    // 100 de daño por impacto (letal), duración corta (8s) y mayor rango
}

/// <summary>Efecto temporal de la pastilla, opciones de poder de daño y eliminación de entidades.</summary>
[RequireComponent(typeof(AnxietyManager))]
public class PlayerPowerState : MonoBehaviour
{
    public static PlayerPowerState Instance { get; private set; }

    [Header("Opciones de Poder de Pastillas")]
    public PillPowerMode currentPowerMode = PillPowerMode.Estandar;

    [Header("Configuración de Daño por Modo")]
    public float danioLigero = 25f;
    public float danioEstandar = 50f;
    public float danioSobrecarga = 100f;

    [Header("Configuración de Duración por Modo")]
    public float duracionLigero = 25f;
    public float duracionEstandar = 15f;
    public float duracionSobrecarga = 8f;

    [Header("Interacción")]
    public float defaultDuration = 15f;
    public float interactionDistance = 3f;

    public int DefeatedEntities { get; private set; }
    public float RemainingTime => Mathf.Max(0f, powerEndsAt - Time.time);
    public bool IsEmpowered => isActiveAndEnabled && RemainingTime > 0f && anxiety != null && !anxiety.isDead;
    public string LastMessage { get; private set; }

    public float CurrentDamage
    {
        get
        {
            switch (currentPowerMode)
            {
                case PillPowerMode.Ligero: return danioLigero;
                case PillPowerMode.Sobrecarga: return danioSobrecarga;
                case PillPowerMode.Estandar:
                default: return danioEstandar;
            }
        }
    }

    public float CurrentDuration
    {
        get
        {
            switch (currentPowerMode)
            {
                case PillPowerMode.Ligero: return duracionLigero;
                case PillPowerMode.Sobrecarga: return duracionSobrecarga;
                case PillPowerMode.Estandar:
                default: return duracionEstandar;
            }
        }
    }

    public Color CurrentModeColor
    {
        get
        {
            switch (currentPowerMode)
            {
                case PillPowerMode.Ligero: return new Color(0.2f, 0.9f, 1f); // Cian
                case PillPowerMode.Sobrecarga: return new Color(0.95f, 0.3f, 1f); // Magenta/Púrpura
                case PillPowerMode.Estandar:
                default: return new Color(0.15f, 1f, 0.55f); // Verde
            }
        }
    }

    public string CurrentModeName
    {
        get
        {
            switch (currentPowerMode)
            {
                case PillPowerMode.Ligero: return $"Dosis Ligera ({danioLigero} Daño · {duracionLigero}s)";
                case PillPowerMode.Sobrecarga: return $"Sobredosis ({danioSobrecarga} Daño · {duracionSobrecarga}s)";
                case PillPowerMode.Estandar:
                default: return $"Dosis Estándar ({danioEstandar} Daño · {duracionEstandar}s)";
            }
        }
    }

    private float powerEndsAt;
    private AnxietyManager anxiety;

    void Awake() { anxiety = GetComponent<AnxietyManager>(); }
    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    public void CyclePowerMode()
    {
        int next = ((int)currentPowerMode + 1) % 3;
        SetPowerMode((PillPowerMode)next);
    }

    public void SetPowerMode(PillPowerMode mode)
    {
        currentPowerMode = mode;
        LastMessage = $"Modo de poder cambiado a: {CurrentModeName}";
        Debug.Log($"<b>[PlayerPowerState]</b> {LastMessage}");
    }

    public void Activate(float customDuration = -1f)
    {
        if (anxiety == null) anxiety = GetComponent<AnxietyManager>();
        if (anxiety == null || anxiety.isDead) return;

        float duracionEfectiva = customDuration > 0f ? customDuration : CurrentDuration;
        powerEndsAt = Time.time + Mathf.Max(0.1f, duracionEfectiva);
        anxiety.Calm(anxiety.maxAnxiety);
        LastMessage = $"¡Pastilla activa ({currentPowerMode})! Daño: {CurrentDamage} HP. Acércate con E o dispara para atacar.";
    }

    public void RegisterDefeat()
    {
        DefeatedEntities++;
        LastMessage = "Entidad neutralizada";
    }

    void Update()
    {
        if (Time.timeScale <= 0f || OptionsMenuUI.IsOpen || GameIntroductionUI.IsOpen
            || OptionsMenuUI.EscapeConsumedThisFrame || anxiety == null || anxiety.isDead) return;

        // --- CONTROLES DE CAMBIO DE MODO DE PODER ---
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                CyclePowerMode();
            }
            else if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                SetPowerMode(PillPowerMode.Ligero);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                SetPowerMode(PillPowerMode.Estandar);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                SetPowerMode(PillPowerMode.Sobrecarga);
            }
        }

        // --- INTERACCIÓN CON EL ENTORNO (TECLA E) ---
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        Camera camera = Camera.main;
        if (camera == null) return;
        float cameraOffset = Vector3.Distance(camera.transform.position, transform.position);
        RaycastHit[] hits = Physics.SphereCastAll(camera.transform.position, 0.18f,
            camera.transform.forward, interactionDistance + cameraOffset, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;

            // 1. Recoger Pastilla
            CalmingPill pill = hit.collider.GetComponentInParent<CalmingPill>();
            if (pill != null)
            {
                if (Vector3.Distance(transform.position, pill.transform.position) <= interactionDistance + 0.5f)
                    pill.TryConsume(this);
                break;
            }

            // 2. Atacar / Neutralizar Enemigo si tiene vida
            if (IsEmpowered)
            {
                EnemyHealth health = hit.collider.GetComponentInParent<EnemyHealth>();
                if (health != null && !health.IsDead)
                {
                    if (Vector3.Distance(transform.position, health.transform.position) <= interactionDistance + 0.8f)
                    {
                        health.TakeDamage(CurrentDamage, hit.point);
                        LastMessage = $"¡Impacto de poder! Infligiste {CurrentDamage} de daño.";
                        break;
                    }
                }

                // Fallbacks tradicionales si no tuviera EnemyHealth
                EnemyAIController enemy = hit.collider.GetComponentInParent<EnemyAIController>();
                if (enemy != null)
                {
                    TryDefeat(enemy);
                    break;
                }

                if (hit.collider != null && Vector3.Distance(transform.position, hit.collider.transform.position) <= interactionDistance + 0.8f)
                {
                    hit.collider.SendMessageUpwards("Defeat", SendMessageOptions.DontRequireReceiver);
                }
            }

            // Los volúmenes de habitaciones no bloquean la interacción; los muros sí.
            if (!hit.collider.isTrigger) break;
        }
    }

    public bool TryDefeat(EnemyAIController enemy)
    {
        if (!IsEmpowered || enemy == null || !enemy.IsVulnerable || !enemy.gameObject.activeInHierarchy) return false;
        if (Vector3.Distance(transform.position, enemy.transform.position) > interactionDistance + 0.8f) return false;

        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null && !health.IsDead)
        {
            health.TakeDamage(CurrentDamage, enemy.transform.position);
            return health.IsDead;
        }

        enemy.Defeat();
        return true;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!IsEmpowered) return;

        EnemyHealth health = hit.collider.GetComponentInParent<EnemyHealth>();
        if (health != null && !health.IsDead)
        {
            health.TakeDamage(CurrentDamage, hit.point);
            return;
        }

        TryDefeat(hit.collider.GetComponentInParent<EnemyAIController>());
        hit.collider.SendMessageUpwards("Defeat", SendMessageOptions.DontRequireReceiver);
    }
}
