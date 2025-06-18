using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AdaptorAttribute : Attribute
{
    public Type[] AdaptorTypes { get; }

    public AdaptorAttribute(params Type[] adaptorTypes)
    {
        AdaptorTypes = adaptorTypes;
    }
}