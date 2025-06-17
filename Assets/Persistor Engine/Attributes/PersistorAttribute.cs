using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class PersistorAttribute : Attribute
{
    public Type[] PersistorTypes { get; }

    public PersistorAttribute(params Type[] adaptorTypes)
    {
        PersistorTypes = adaptorTypes;
    }
}