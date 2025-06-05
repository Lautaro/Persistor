using System;
using System.Linq;
using System.Reflection;

public static class GameUnitIdAssigner
{
    // This should be called when creating or validating a GameUnit instance
    public static void AssignIdIfNeeded(object unit)
    {
        var type = unit.GetType();
        var idField = type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(f => f.IsDefined(typeof(GameUnitIdAttribute), true));
        if (idField == null) return;

        var currentId = idField.GetValue(unit) as string;
        if (!string.IsNullOrEmpty(currentId)) return;

        var attr = (GameUnitIdAttribute)idField.GetCustomAttribute(typeof(GameUnitIdAttribute), true);
        var prefix = attr?.Prefix ?? type.Name;

        // Assign a new unique ID using a GUID
        var newId = $"{prefix}__{Guid.NewGuid().ToString("N")}";
        idField.SetValue(unit, newId);
    }
}
