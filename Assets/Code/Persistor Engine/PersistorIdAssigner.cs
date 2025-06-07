using System;
using System.Linq;
using System.Reflection;

public static class PersistorIdAssigner
{
    // This should be called when creating or validating a GameUnit instance
    public static void AssignIdIfNeeded(object unit)
    {
        var type = unit.GetType();
        var idField = type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(f => f.IsDefined(typeof(PersistorIdAttribute), true));
        if (idField == null) return;

        var currentId = idField.GetValue(unit) as string;
        if (!string.IsNullOrEmpty(currentId)) return;

        var attr = (PersistorIdAttribute)idField.GetCustomAttribute(typeof(PersistorIdAttribute), true);
        var prefix = attr?.Prefix ?? type.Name;

        // Assign a new unique ID using a GUID
        var newId = $"{prefix}__{Guid.NewGuid().ToString("N")}";
        idField.SetValue(unit, newId);
    }
}
