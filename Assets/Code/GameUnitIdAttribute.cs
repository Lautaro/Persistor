using System;

[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class GameUnitIdAttribute : Attribute
{
    public string Prefix { get; }
    public GameUnitIdAttribute() { }
    public GameUnitIdAttribute(string idPrefix) => Prefix = idPrefix;
}