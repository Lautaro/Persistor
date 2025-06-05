using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class GameUnitAdaptorsAttribute : Attribute
{
    public Type[] AdaptorTypes { get; }

    public GameUnitAdaptorsAttribute(params Type[] adaptorTypes)
    {
        AdaptorTypes = adaptorTypes;
    }
}