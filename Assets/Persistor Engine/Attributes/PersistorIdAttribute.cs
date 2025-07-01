using System;

/// <summary>
/// Marks a field as the unique identifier for a persistable object.
/// 
/// Usage:
/// - Place on a field in a class that implements IPersistorId to indicate which field is used as the object's unique ID.
/// - Optionally, provide a prefix for the generated ID.
/// - Used by the persistence system to identify and resolve object references.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class PersistorIdAttribute : Attribute
{
    /// <summary>
    /// Optional prefix for the generated ID.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Marks a field as the unique identifier for persistence.
    /// </summary>
    public PersistorIdAttribute() { }

    /// <summary>
    /// Marks a field as the unique identifier for persistence, with a custom prefix.
    /// </summary>
    /// <param name="idPrefix">The prefix to use for the generated ID.</param>
    public PersistorIdAttribute(string idPrefix) => Prefix = idPrefix;
}
