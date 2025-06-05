using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GameUnitCodeGenerator
{
    private static Dictionary<Type, Type> _converterMap;
    private static Dictionary<Type, Type> _globalAdaptorMap;

    [MenuItem("Tools/Regenerate GameUnits")]
    public static void Generate()
    {
        _globalAdaptorMap = DiscoverGlobalAdaptors();
        _converterMap = DiscoverConverters();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                // Check for [GameUnitData] fields
                var hasGameUnitDataFields = GetAllGameUnitDataFields(type).Any();

                // Only generate for valid GameUnits
                if (hasGameUnitDataFields
                    && !type.IsInterface
                    && !type.IsAbstract
                    && !type.IsDefined(typeof(GameUnitDontGenerateAttribute), false))
                {
                    GenerateForGameUnit(type);
                }
            }
        }
        AssetDatabase.Refresh();
        Debug.Log("GameUnit code generation complete.");
    }

    // Gets all fields with [GameUnitData] attribute, including inherited ones
    private static FieldInfo[] GetAllGameUnitDataFields(Type type)
    {
        var fields = new List<FieldInfo>();
        while (type != null && type != typeof(object))
        {
            fields.AddRange(
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f =>
                        f.IsDefined(typeof(GameUnitDataAttribute), false) ||
                        f.IsDefined(typeof(GameUnitIdAttribute), false))
            );
            type = type.BaseType;
        }
        return fields.ToArray();
    }


    private static List<Type> GetAllAdaptorTypes(Type unitType)
    {
        var adaptorTypes = new HashSet<Type>();

        // 1. Class-level [GameUnitAdaptors]
        var classAttr = unitType.GetCustomAttribute<GameUnitAdaptorsAttribute>(inherit: true);
        if (classAttr != null && classAttr.AdaptorTypes != null)
        {
            foreach (var t in classAttr.AdaptorTypes)
                adaptorTypes.Add(t);
        }

        // 2. Field-level [GameUnitAdaptor]
        foreach (var field in unitType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            var fieldAttrs = field.GetCustomAttributes<GameUnitAdaptorAttribute>(inherit: true);
            foreach (var attr in fieldAttrs)
            {
                if (attr.AdaptorType != null)
                    adaptorTypes.Add(attr.AdaptorType);
            }
        }

        return adaptorTypes.ToList();
    }


    private static FieldInfo[] GetAllGameUnitIdFields(Type type)
    {
        var fields = new List<FieldInfo>();
        while (type != null && type != typeof(object))
        {
            fields.AddRange(
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => f.IsDefined(typeof(GameUnitIdAttribute), false))
            );
            type = type.BaseType;
        }
        return fields.ToArray();
    }

    private static void GenerateForGameUnit(Type unitType)
    {
        var fields = GetAllGameUnitDataFields(unitType);

        // Enforce exactly one [GameUnitId] field of type string (including inherited)
        var idFields = GetAllGameUnitIdFields(unitType);
        if (idFields.Length != 1)
        {
            Debug.LogError($"Type '{unitType.FullName}' must have exactly one field marked with [GameUnitId] (including inherited fields). Found: {idFields.Length}");
            return;
        }
        if (idFields[0].FieldType != typeof(string))
        {
            Debug.LogError($"Field '{idFields[0].Name}' in '{unitType.FullName}' marked with [GameUnitId] must be of type string.");
            return;
        }

        var unitName = unitType.Name;
        var folder = $"Assets/Code/{unitName}/Generated";
        Directory.CreateDirectory(folder);

        // Preset ScriptableObject
        var presetCode = GeneratePresetSO(unitName, fields);
        File.WriteAllText($"{folder}/{unitName}Preset.cs", presetCode);

        // Data POCO
        var dataCode = GenerateDataPOCO(unitName, fields);
        File.WriteAllText($"{folder}/{unitName}_Data.cs", dataCode);

        // Adaptor
        var adaptorCode = GeneratePersistor(unitName, fields);
        File.WriteAllText($"{folder}/{unitName}Adaptor.cs", adaptorCode);
    }

    private static string GeneratePresetSO(string unitName, FieldInfo[] fields)
    {
        var namespaces = CollectNamespaces(fields);
        var usings = string.Join("\n", namespaces.Select(ns => $"using {ns};"));
        var fieldsCode = string.Join("\n    ", fields.Select(f => GenerateFieldWithAttributes(f, true)));
        return
$@"{usings}
[CreateAssetMenu(menuName = ""GameUnit/{unitName}Preset"")]
public partial class {unitName}Preset : ScriptableObject
{{
    {fieldsCode}
}}
";
    }

    private static string GenerateDataPOCO(string unitName, FieldInfo[] fields)
    {
        var namespaces = CollectNamespaces(fields);
        var usings = string.Join("\n", namespaces.Select(ns => $"using {ns};"));
        var fieldsCode = string.Join("\n    ", fields.Select(f => GenerateDataField(f)));

        // Add explicit adaptors (class/field)
        var adaptorTypes = GetAllAdaptorTypes(Type.GetType(unitName) ?? fields.First().DeclaringType);
        var adaptorFields = new List<string>(adaptorTypes.Select(t => $"public {t.Name} {ToCamelCase(t.Name)};"));

        // Add global adaptors for [GameUnitData] fields (if not already present)
        foreach (var field in fields)
        {
            if (_globalAdaptorMap != null && _globalAdaptorMap.TryGetValue(field.FieldType, out var globalAdaptorType))
            {
                if (!adaptorTypes.Contains(globalAdaptorType))
                    adaptorFields.Add($"public {globalAdaptorType.Name} {ToCamelCase(field.Name)}Adaptor;");
            }
        }

        var allFields = string.IsNullOrWhiteSpace(fieldsCode)
            ? string.Join("\n    ", adaptorFields)
            : adaptorFields.Count == 0
                ? fieldsCode
                : fieldsCode + "\n    " + string.Join("\n    ", adaptorFields);

        return
$@"{usings}
[System.Serializable]
public partial class {unitName}_Data
{{
    {allFields}
}}";
    }

    private static string GeneratePersistor(string unitName, FieldInfo[] fields)
    {
        var namespaces = CollectNamespaces(fields);
        var usings = string.Join("\n", namespaces.Select(ns => $"using {ns};"));

        var copyToData = string.Join("\n        ", fields.Select(f => GenerateCopyToData(f)));
        var copyFromData = string.Join("\n        ", fields.Select(f => GenerateCopyFromData(f)));
        var copyFromPreset = string.Join("\n        ", fields.Select(f => GenerateCopyFromPreset(f, unitName)));

        // Explicit adaptors (class/field)
        var adaptorTypes = GetAllAdaptorTypes(Type.GetType(unitName) ?? fields.First().DeclaringType);
        var adaptorCopyToData = string.Join("\n        ", adaptorTypes.Select(t => $"if (data.{ToCamelCase(t.Name)} != null) data.{ToCamelCase(t.Name)}.CopyToData(unit);"));
        var adaptorCopyFromData = string.Join("\n        ", adaptorTypes.Select(t => $"if (data.{ToCamelCase(t.Name)} != null) data.{ToCamelCase(t.Name)}.CopyFromData(unit);"));

        // Global adaptors for [GameUnitData] fields
        var globalAdaptorCopyToData = string.Join("\n        ",
            fields.Where(f => _globalAdaptorMap != null && _globalAdaptorMap.TryGetValue(f.FieldType, out var _))
                  .Select(f => $"if (data.{ToCamelCase(f.Name)}Adaptor != null) data.{ToCamelCase(f.Name)}Adaptor.CopyToData(unit.{f.Name});"));

        var globalAdaptorCopyFromData = string.Join("\n        ",
            fields.Where(f => _globalAdaptorMap != null && _globalAdaptorMap.TryGetValue(f.FieldType, out var _))
                  .Select(f => $"if (data.{ToCamelCase(f.Name)}Adaptor != null) data.{ToCamelCase(f.Name)}Adaptor.CopyFromData(unit.{f.Name});"));

        return
$@"{usings}
public static partial class {unitName}Persistor
{{
    public static void CopyToData({unitName} unit, {unitName}_Data data)
    {{
        GameUnitIdAssigner.AssignIdIfNeeded(unit);
        {copyToData}
        {adaptorCopyToData}
        {globalAdaptorCopyToData}
    }}

    public static void CopyFromData({unitName} unit, {unitName}_Data data)
    {{
        {copyFromData}
        {adaptorCopyFromData}
        {globalAdaptorCopyFromData}
    }}

    public static void CopyFromPreset({unitName} unit, {unitName}Preset preset)
    {{
        {copyFromPreset}
    }}
}}";
    }

    private static string GenerateDataField(FieldInfo field)
    {
        // Use converter data type if available, else C# type name
        if (_converterMap != null && _converterMap.TryGetValue(field.FieldType, out var dataType))
            return $"public {dataType.Name} {field.Name};";
        return GenerateFieldWithAttributes(field, false);
    }

    private static string GenerateFieldWithAttributes(FieldInfo field, bool forScriptableObject)
    {
        var attrs = field.GetCustomAttributes(false)
            .Select(attr => AttributeToSource(attr))
            .Where(s => !string.IsNullOrEmpty(s));
        string attributes = string.Join("\n    ", attrs);

        // Always public for generated fields
        string typeName = GetCSharpTypeName(field.FieldType);
        string fieldLine = $"public {typeName} {field.Name};";
        return string.IsNullOrEmpty(attributes) ? fieldLine : $"{attributes}\n    {fieldLine}";
    }

    private static string AttributeToSource(object attr)
    {
        var type = attr.GetType();
        // Handle common Unity attributes
        if (type == typeof(SerializeField)) return "[SerializeField]";
        if (type == typeof(TooltipAttribute)) return $"[Tooltip(\"{((TooltipAttribute)attr).tooltip}\")]";
        if (type == typeof(RangeAttribute))
        {
            var range = (RangeAttribute)attr;
            return $"[Range({range.min}f, {range.max}f)]";
        }
        if (type == typeof(HeaderAttribute))
        {
            var header = (HeaderAttribute)attr;
            return $"[Header(\"{header.header}\")]";
        }
        // Add more as needed, or fallback to attribute name
        if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
            return $"[{type.Name.Replace("Attribute", "")}]";
        return null; // Skip unknown attributes for now
    }

    private static string GenerateCopyToData(FieldInfo field)
    {
        if (_converterMap != null && _converterMap.TryGetValue(field.FieldType, out var dataType))
        {
            return $"data.{field.Name} = GameUnitConverterRegistry.ToData<{field.FieldType.Name}, {dataType.Name}>(unit.{field.Name});";
        }
        return $"data.{field.Name} = unit.{field.Name};";
    }

    private static string GenerateCopyFromData(FieldInfo field)
    {
        if (_converterMap != null && _converterMap.TryGetValue(field.FieldType, out var dataType))
        {
            return $"unit.{field.Name} = GameUnitConverterRegistry.FromData<{field.FieldType.Name}, {dataType.Name}>(data.{field.Name});";
        }
        return $"unit.{field.Name} = data.{field.Name};";
    }

    private static string GenerateCopyFromPreset(FieldInfo field, string unitName)
    {
        var presetFieldType = field.FieldType;
        if (_converterMap != null && _converterMap.TryGetValue(field.FieldType, out var dataType))
        {
            if (presetFieldType == field.FieldType)
                return $"unit.{field.Name} = preset.{field.Name};";
            return $"unit.{field.Name} = ((IGameUnitConverter<{field.FieldType.Name}, {dataType.Name}>)GameUnitConverterRegistry.Get(typeof({field.FieldType.Name}))).FromData(preset.{field.Name});";
        }
        return $"unit.{field.Name} = preset.{field.Name};";
    }

    private static string GetCSharpTypeName(Type type)
    {
        if (type == typeof(int)) return "int";
        if (type == typeof(float)) return "float";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(double)) return "double";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(char)) return "char";
        if (type == typeof(object)) return "object";
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return GetCSharpTypeName(type.GetGenericArguments()[0]) + "?";
        return type.Name;
    }

    private static Dictionary<Type, Type> DiscoverConverters()
    {
        var result = new Dictionary<Type, Type>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsAbstract && typeof(IGameUnitConverter).IsAssignableFrom(type))
                {
                    var instance = Activator.CreateInstance(type) as IGameUnitConverter;
                    if (instance != null)
                        result[instance.SourceType] = instance.DataType;
                }
            }
        }
        return result;
    }

    private static HashSet<string> CollectNamespaces(FieldInfo[] fields)
    {
        var namespaces = new HashSet<string>();
        foreach (var field in fields)
        {
            var type = field.FieldType;
            if (!string.IsNullOrEmpty(type.Namespace) && type.Namespace != "System")
                namespaces.Add(type.Namespace);

            if (_converterMap != null && _converterMap.TryGetValue(type, out var dataType))
            {
                if (!string.IsNullOrEmpty(dataType.Namespace) && dataType.Namespace != "System")
                    namespaces.Add(dataType.Namespace);
            }
        }
        return namespaces;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
    private static Dictionary<Type, Type> DiscoverGlobalAdaptors()
    {
        var result = new Dictionary<Type, Type>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var iface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.StartsWith("IGameUnit_Global_Adaptor"));
                if (iface != null)
                {
                    var targetType = iface.GetGenericArguments()[0];
                    result[targetType] = type;
                }
            }
        }
        return result;
    }
}
