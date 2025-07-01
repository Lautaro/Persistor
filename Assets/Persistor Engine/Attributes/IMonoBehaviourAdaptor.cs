using UnityEngine;

/// <summary>
/// Implement this interface for adaptors that operate on MonoBehaviour (or subclasses).
/// 
/// Usage:
/// - Implement on adaptor classes that should be reusable for any PersistorMonoBehaviour subclass.
/// - The code generator will cast the target to MonoBehaviour when calling these methods.
/// - Use [Adaptor(typeof(YourAdaptor))] on your persistable class to apply.
/// </summary>
public interface IMonoBehaviourAdaptor
{
    /// <summary>
    /// Copies data from the MonoBehaviour target into the adaptor.
    /// </summary>
    void CopyToData(MonoBehaviour target);

    /// <summary>
    /// Applies data from the adaptor back to the MonoBehaviour target.
    /// </summary>
    void CopyFromData(MonoBehaviour target);
}
