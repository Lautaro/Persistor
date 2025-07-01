namespace PersistorEngine
{
  /// <summary>
    /// Used by the code generator that will mark a data class as the data container 
    /// for a specific runtime type.
    /// 
    /// Usage:
    /// - Codegenerator places this attribute on the generated data classes (e.g., Enemy__Data).
    /// - The TargetType should be the runtime type this data class is for (e.g., Enemy).
    /// - Used by the persistence system to map runtime objects to their data containers.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DataForAttribute : System.Attribute
    {
        public System.Type TargetType { get; }

        public DataForAttribute(System.Type targetType) => TargetType = targetType;
    }
}