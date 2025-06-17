using System;

[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class PersistorIdAttribute : Attribute
{
    public string Prefix { get; }
    public PersistorIdAttribute() { }
    public PersistorIdAttribute(string idPrefix) => Prefix = idPrefix;
}