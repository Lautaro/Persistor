using System;

/// <summary>
/// Specifies the prefab to use when instantiating a persistable MonoBehaviour.
/// 
/// Usage:
/// - Place on a class that inherits from MonoBehaviour or PersistorMonoBehaviour.
/// - The reference should be the name or path of the prefab to instantiate for this type.
/// - Used by the persistence system to instantiate the correct prefab when loading.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class PersistorPrefabAttribute : Attribute
{
    /// <summary>
    /// The prefab reference (name or path).
    /// </summary>
    public string Reference { get; }

    /// <summary>
    /// Specifies the prefab to use for this persistable type.
    /// </summary>
    /// <param name="reference">The prefab name or path.</param>
    public PersistorPrefabAttribute(string reference) => Reference = reference;
}
