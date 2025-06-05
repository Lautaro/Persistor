using System;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public class GameUnitAdaptorAttribute : Attribute
{
    public Type AdaptorType { get; }

    public GameUnitAdaptorAttribute(Type adaptorType)
    {
        AdaptorType = adaptorType;
    }
}
