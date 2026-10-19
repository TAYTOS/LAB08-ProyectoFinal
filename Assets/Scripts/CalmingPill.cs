using UnityEngine;

public class CalmingPill : MonoBehaviour
{
    public float duration = 15f;
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
        player.Activate(duration);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    public static CalmingPill Create(Vector3 position, Transform parent = null)
    {
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "Pastilla calmante";
        capsule.layer = 10;
        capsule.transform.SetParent(parent);
        capsule.transform.position = position + Vector3.up * 0.7f;
        capsule.transform.localScale = new Vector3(0.38f, 0.26f, 0.38f);
        capsule.GetComponent<Collider>().isTrigger = true;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = new Color(0.15f, 1f, 0.55f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(0.08f, 0.8f, 0.3f));
        capsule.GetComponent<Renderer>().sharedMaterial = material;
        return capsule.AddComponent<CalmingPill>();
    }

    void OnDestroy()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null) Destroy(renderer.sharedMaterial);
    }
}
