using System;
using UnityEngine;

public static class GameSettings
{
    // New key deliberately ignores the old serialized 0.1 sensitivity.
    public const string MouseSensitivityKey = "Lab6.MouseSensitivity.v1";
    public const string VolumeKey = "VolumenGeneral";
    public const float DefaultMouseSensitivity = 0.035f;
    public const float MinMouseSensitivity = 0.005f;
    public const float MaxMouseSensitivity = 0.2f;
    public const float Default = DefaultMouseSensitivity;
    public const float Min = MinMouseSensitivity;
    public const float Max = MaxMouseSensitivity;
    public static event Action<float> SensitivityChanged;

    private static bool loaded;
    private static float sensitivity = DefaultMouseSensitivity;
    public static float MouseSensitivity
    {
        get { if (!loaded) Load(); return sensitivity; }
    }

    public static void Load()
    {
        float value = Sanitize(PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity));
        bool changed = !loaded || !Mathf.Approximately(sensitivity, value);
        sensitivity = value;
        loaded = true;
        if (changed) SensitivityChanged?.Invoke(sensitivity);
    }

    public static void SetMouseSensitivity(float value)
    {
        value = Sanitize(value);
        bool changed = !loaded || !Mathf.Approximately(sensitivity, value);
        sensitivity = value;
        loaded = true;
        PlayerPrefs.SetFloat(MouseSensitivityKey, sensitivity);
        if (changed) SensitivityChanged?.Invoke(sensitivity);
    }

    public static void ResetMouseSensitivity() { SetMouseSensitivity(DefaultMouseSensitivity); }
    public static void Save() { PlayerPrefs.Save(); }

    private static float Sanitize(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? DefaultMouseSensitivity
            : Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
    }
}
