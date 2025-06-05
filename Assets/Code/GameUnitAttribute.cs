using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GameUnitAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class GameUnitDataAttribute : Attribute
{
}
