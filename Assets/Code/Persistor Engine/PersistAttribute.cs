using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class PersistAttribute : Attribute
{
}
