using UnityEngine;

/// <summary>
/// Utility for validating required Unity object references (typically <see cref="SerializeField"/> fields).
/// Use <see cref="Check"/> at runtime (e.g., <c>Awake</c>) to hard-fail by disabling the component,
/// and <see cref="Warn"/> in <c>OnValidate</c> to surface missing references early in the editor.
/// </summary>
public static class RequireRef
{
    /// <summary>
    /// Validates that a required reference is assigned. If missing, logs an error and disables <paramref name="owner"/>.
    /// Intended for runtime safety checks (e.g., in <c>Awake</c> / <c>Start</c>).
    /// </summary>
    /// <param name="obj">The Unity object reference to validate (e.g., a <see cref="Transform"/>, <see cref="Component"/>, or <see cref="GameObject"/>).</param>
    /// <param name="owner">The component that owns the reference. This component will be disabled if validation fails.</param>
    /// <param name="name">The field/property name for the reference (typically passed as <c>nameof(myField)</c>).</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is assigned; otherwise <c>false</c>.</returns>
    public static bool Check(Object obj, MonoBehaviour owner, string name)
    {
        if (obj is not null) return true;

        Debug.LogError($"{owner.GetType().Name}: Missing '{name}' assignment", owner);
        owner.enabled = false;
        return false;
    }

    /// <summary>
    /// Emits an editor-friendly warning if a required reference is not assigned.
    /// Intended for <c>OnValidate</c> so missing references are visible before entering Play Mode.
    /// </summary>
    /// <param name="obj">The Unity object reference to validate.</param>
    /// <param name="owner">The component that owns the reference. Used as the log context for easy pinging in the editor.</param>
    /// <param name="name">The field/property name for the reference (typically passed as <c>nameof(myField)</c>).</param>
    public static void Warn(Object obj, MonoBehaviour owner, string name)
    {
        if (obj is not null) return;
        Debug.LogWarning($"{owner.GetType().Name}: Assign '{name}'", owner);
    }
}
