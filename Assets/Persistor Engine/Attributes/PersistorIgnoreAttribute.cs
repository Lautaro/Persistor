using System;

/// <summary>
/// Prevents the code generator from generating persistence code for the decorated class.
/// 
/// Usage:
/// - Place on any class you want the code generator to skip.
/// - Useful for base classes, abstract classes, or types you do not want persisted.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PersistorIgnoreAttribute : Attribute { }
