using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>Efecto temporal de la pastilla y eliminación de entidades vulnerables.</summary>
[RequireComponent(typeof(AnxietyManager))]
public class PlayerPowerState : MonoBehaviour
{
    public static PlayerPowerState Instance { get; private set; }
    public float defaultDuration = 15f;
    public float interactionDistance = 3f;
    public int DefeatedEntities { get; private set; }
    public float RemainingTime => Mathf.Max(0f, powerEndsAt - Time.time);
    public bool IsEmpowered => isActiveAndEnabled && RemainingTime > 0f && anxiety != null && !anxiety.isDead;
    public string LastMessage { get; private set; }
    private float powerEndsAt;
    private AnxietyManager anxiety;

    void Awake() { anxiety = GetComponent<AnxietyManager>(); }
    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    public void Activate(float duration)
    {
        if (anxiety == null) anxiety = GetComponent<AnxietyManager>();
        if (anxiety == null || anxiety.isDead) return;
        powerEndsAt = Time.time + Mathf.Max(0.1f, duration);
        anxiety.Calm(anxiety.maxAnxiety);
        LastMessage = "Pastilla activa: las entidades huyen. Acércate y pulsa E para neutralizarlas.";
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
            CalmingPill pill = hit.collider.GetComponentInParent<CalmingPill>();
            if (pill != null)
            {
                if (Vector3.Distance(transform.position, pill.transform.position) <= interactionDistance + 0.5f)
                    pill.TryConsume(this);
                break;
            }
            EnemyAIController enemy = hit.collider.GetComponentInParent<EnemyAIController>();
            if (enemy != null)
            {
                TryDefeat(enemy);
                break;
            }
            // Los volúmenes de habitaciones no bloquean la interacción; los muros sí.
            if (!hit.collider.isTrigger) break;
        }
    }

    public bool TryDefeat(EnemyAIController enemy)
    {
        if (!IsEmpowered || enemy == null || !enemy.IsVulnerable || !enemy.gameObject.activeInHierarchy) return false;
        if (Vector3.Distance(transform.position, enemy.transform.position) > interactionDistance + 0.8f) return false;
        enemy.Defeat();
        return true;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        TryDefeat(hit.collider.GetComponentInParent<EnemyAIController>());
    }
}
