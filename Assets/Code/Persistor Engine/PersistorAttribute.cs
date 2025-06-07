using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class PersistorAttribute : Attribute
{
    public Type[] AdaptorTypes { get; }

    public PersistorAttribute(params Type[] adaptorTypes)
    {
        AdaptorTypes = adaptorTypes;
    }
}