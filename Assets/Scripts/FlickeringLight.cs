using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Light))]
public class FlickeringLight : MonoBehaviour
{
    private Light myLight;
    private float baseIntensity;
    
    [Header("Configuración del Parpadeo")]
    public float minFlickerIntensity = 0.5f;
    public float maxFlickerIntensity = 1f;
    public float flickerSpeed = 0.2f;
    public bool isDefective = false; // Si está defectuosa, a veces se apaga por completo un segundo
    
    public bool isCorrupted = false; // Controlado por AnxietyManager

    void Start()
    {
        myLight = GetComponent<Light>();
        baseIntensity = myLight.intensity;
        StartCoroutine(FlickerRoutine());
    }

    IEnumerator FlickerRoutine()
    {
        while (true)
        {
            if (isCorrupted)
            {
                // Parpadeo agresivo y rojizo
                myLight.color = Color.red;
                myLight.intensity = Random.Range(0f, baseIntensity * 2f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
            else
            {
                // Parpadeo normal / Luz defectuosa
                myLight.intensity = Random.Range(minFlickerIntensity, maxFlickerIntensity) * baseIntensity;
                
                if (isDefective && Random.value > 0.9f)
                {
                    myLight.intensity = 0f;
                    yield return new WaitForSeconds(Random.Range(0.2f, 1.0f)); // Se apaga un rato
                }
                else
                {
                    yield return new WaitForSeconds(Random.Range(0.05f, flickerSpeed));
                }
            }
        }
    }

    public void ForceOff()
    {
        StopAllCoroutines();
        myLight.intensity = 0f;
        this.enabled = false; // Desactivar el script
    }
}
