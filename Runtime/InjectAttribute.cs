using System;

/// <summary>
/// Marks a field for lazy dependency injection via Source Generator.
/// The class must be partial. Access via generated property, not the field.
/// Recommended: Use 'd' prefix (e.g., dPlayerState) to avoid type name confusion.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class InjectAttribute : Attribute
{
    /// <summary>
    /// Optional key to distinguish multiple registrations of the same type.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Injects dependency with default key.
    /// </summary>
    public InjectAttribute()
    {
        Key = "";
    }

    /// <summary>
    /// Injects dependency with specified key.
    /// </summary>
    /// <param name="key">Key to identify the dependency.</param>
    public InjectAttribute(string key)
    {
        Key = key;
    }
}
