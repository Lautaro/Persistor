using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class PersistorPrefabAttribute : Attribute
{
    public string Reference { get; }
    public PersistorPrefabAttribute(string reference) => Reference = reference;
}
