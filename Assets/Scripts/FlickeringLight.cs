using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Light))]
public class FlickeringLight : MonoBehaviour
{
    private Light[] myLights;
    private float[] baseIntensities;
    
    [Header("Configuración del Parpadeo")]
    public float minFlickerIntensity = 0.5f;
    public float maxFlickerIntensity = 1f;
    public float flickerSpeed = 0.2f;
    public bool isDefective = false; // Si está defectuosa, a veces se apaga por completo un segundo
    
    public bool isCorrupted = false; // Controlado por AnxietyManager

    void Start()
    {
        myLights = GetComponentsInChildren<Light>();
        baseIntensities = new float[myLights.Length];
        for (int i = 0; i < myLights.Length; i++)
        {
            baseIntensities[i] = myLights[i].intensity;
        }
        StartCoroutine(FlickerRoutine());
    }

    IEnumerator FlickerRoutine()
    {
        while (true)
        {
            if (isCorrupted)
            {
                // Parpadeo agresivo y rojizo
                float randomFactor = Random.Range(0f, 2f);
                for (int i = 0; i < myLights.Length; i++)
                {
                    myLights[i].color = Color.red;
                    myLights[i].intensity = baseIntensities[i] * randomFactor;
                }
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
            else
            {
                // Parpadeo normal / Luz defectuosa
                float normalFactor = Random.Range(minFlickerIntensity, maxFlickerIntensity);
                for (int i = 0; i < myLights.Length; i++)
                {
                    myLights[i].intensity = baseIntensities[i] * normalFactor;
                }
                
                if (isDefective && Random.value > 0.9f)
                {
                    for (int i = 0; i < myLights.Length; i++)
                    {
                        myLights[i].intensity = 0f;
                    }
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
        if (myLights != null)
        {
            foreach (var l in myLights)
            {
                l.intensity = 0f;
            }
        }
        this.enabled = false; // Desactivar el script
    }
}
