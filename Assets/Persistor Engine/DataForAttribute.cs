[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DataForAttribute : System.Attribute
{
    public System.Type TargetType { get; }
    public DataForAttribute(System.Type targetType) => TargetType = targetType;
}