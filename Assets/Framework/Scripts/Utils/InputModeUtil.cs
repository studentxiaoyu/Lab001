using UnityEngine;

/// <summary>
/// Applies input-source component enablement without scene paths or experiment logic.
/// Callers supply their own components and retain ownership of event bindings.
/// </summary>
public static class InputModeUtil
{
    public static void Apply(bool virtualHands, Behaviour[] virtualHandInputs,
                             Behaviour[] hardwareInputs)
    {
        SetEnabled(virtualHandInputs, virtualHands);
        SetEnabled(hardwareInputs, !virtualHands);
    }

    private static void SetEnabled(Behaviour[] components, bool enabled)
    {
        if (components == null) return;
        foreach (Behaviour component in components) {
            if (component != null) component.enabled = enabled;
        }
    }
}
