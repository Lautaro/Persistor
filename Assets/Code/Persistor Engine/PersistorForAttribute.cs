using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PersistorForAttribute : Attribute
{
    public Type TargetType { get; }
    public PersistorForAttribute(Type targetType) => TargetType = targetType;
}
