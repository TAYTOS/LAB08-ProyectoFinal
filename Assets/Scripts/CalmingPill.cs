using UnityEngine;

public class CalmingPill : MonoBehaviour
{
    public float duration = 15f;
    public PillPowerMode pillPowerType = PillPowerMode.Estandar;
    public bool setPlayerPowerModeOnPickup = false;
    public bool IsConsumed { get; private set; }
    private Vector3 restingPosition;

    void Start() { restingPosition = transform.position; }

    void Update()
    {
        transform.position = restingPosition + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.08f);
        transform.Rotate(Vector3.up, 35f * Time.deltaTime, Space.World);
    }

    public bool TryConsume(PlayerPowerState player)
    {
        if (IsConsumed || player == null) return false;
        IsConsumed = true;
        if (setPlayerPowerModeOnPickup)
        {
            player.SetPowerMode(pillPowerType);
        }
        player.Activate(duration > 0f ? duration : player.CurrentDuration);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    public static CalmingPill Create(Vector3 position, Transform parent = null, PillPowerMode mode = PillPowerMode.Estandar)
    {
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = $"Pastilla calmante ({mode})";
        capsule.layer = 10;
        capsule.transform.SetParent(parent);
        capsule.transform.position = position + Vector3.up * 0.7f;
        capsule.transform.localScale = new Vector3(0.38f, 0.26f, 0.38f);
        capsule.GetComponent<Collider>().isTrigger = true;

        Color mainColor;
        Color emitColor;
        switch (mode)
        {
            case PillPowerMode.Ligero:
                mainColor = new Color(0.2f, 0.9f, 1f);
                emitColor = new Color(0.1f, 0.7f, 0.9f);
                break;
            case PillPowerMode.Sobrecarga:
                mainColor = new Color(0.95f, 0.3f, 1f);
                emitColor = new Color(0.8f, 0.1f, 0.9f);
                break;
            case PillPowerMode.Estandar:
            default:
                mainColor = new Color(0.15f, 1f, 0.55f);
                emitColor = new Color(0.08f, 0.8f, 0.3f);
                break;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = mainColor;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emitColor);
        capsule.GetComponent<Renderer>().sharedMaterial = material;

        CalmingPill pill = capsule.AddComponent<CalmingPill>();
        pill.pillPowerType = mode;
        return pill;
    }

    void OnDestroy()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null) Destroy(renderer.sharedMaterial);
    }
}
