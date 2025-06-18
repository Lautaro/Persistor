using PersistorEngine.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class PersistorCodeGenerator
{
    private static Dictionary<Type, Type> _globalAdaptorMap;

    [MenuItem("Tools/Regenerate Adaptors")]
    public static void Generate()
    {
        Debug.Log("WEEEEE");
        _globalAdaptorMap = DiscoverGlobalAdaptors();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                // Check for [Persist] fields
                var hasPersistFields = GetAllPersistFields(type).Any();

                // Only generate for valid classes
                if (hasPersistFields
                    && !type.IsInterface
                    && !type.IsAbstract
                    && !type.IsDefined(typeof(PersistorIgnoreAttribute), false))
                {
                    GenerateForAdaptors(type);
                }
            }
        }
        AssetDatabase.Refresh();
        Debug.Log("Adaptor code generation complete.");
    }

    // Gets all fields with [Persist] attribute, including inherited ones
    private static FieldInfo[] GetAllPersistFields(Type type)
    {
        var fields = new List<FieldInfo>();
        while (type != null && type != typeof(object))
        {
            fields.AddRange(
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => f.IsDefined(typeof(PersistAttribute), false))
            );
            type = type.BaseType;
        }
        return fields.ToArray();
    }

    private static List<Type> GetAllAdaptorTypes(Type unitType)
    {
        var adaptorTypes = new HashSet<Type>();

        var classAttr = unitType.GetCustomAttribute<AdaptorAttribute>(inherit: true);
        if (classAttr != null && classAttr.AdaptorTypes != null)
        {
            foreach (var t in classAttr.AdaptorTypes)
                adaptorTypes.Add(t);
        }

        return adaptorTypes.ToList();
    }

    private static FieldInfo[] GetAllAdaptorIdFields(Type type)
    {
        var fields = new List<FieldInfo>();
        while (type != null && type != typeof(object))
        {
            fields.AddRange(
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => f.IsDefined(typeof(PersistorIdAttribute), false))
            );
            type = type.BaseType;
        }
        return fields.ToArray();
    }

    private static void GenerateForAdaptors(Type unitType)
    {
        var settings = PersistorSettings.GetOrCreateSettings();
        var fields = GetAllPersistFields(unitType);

        // --- Begin: Validation for [Persist] reference types ---
        foreach (var field in fields)
        {
            // Skip value types and string
            if (!field.FieldType.IsClass || field.FieldType == typeof(string))
                continue;

            // Check for custom adaptor
            var (adaptorType, _) = GetAdaptorInfo(field);

            // If not IPersistorId and not handled by a custom adaptor, throw error
            if (!typeof(IPersistorId).IsAssignableFrom(field.FieldType) && adaptorType == null)
            {
                throw new InvalidOperationException(
                    $"[Persist] field '{field.DeclaringType.FullName}.{field.Name}' is a reference type that does not implement IPersistorId and is not handled by a custom adaptor. " +
                    "Reference persistence requires IPersistorId or a custom adaptor.");
            }
        }
        // --- End: Validation for [Persist] reference types ---

        // Only generate for types that implement IPersistorId (directly or via base)
        if (!typeof(IPersistorId).IsAssignableFrom(unitType))
        {
            Debug.LogError($"Type '{unitType.FullName}' must implement IPersistorId (inherit from PersistorMonoBehaviour or PersistorObject).");
            return;
        }

        var unitName = unitType.Name;

        // 1. Check for custom folder attribute
        string folder = null;
        var folderAttr = unitType.GetCustomAttribute<PersistorGeneratedFolderAttribute>();
        if (folderAttr != null && !string.IsNullOrEmpty(folderAttr.FolderPath))
        {
            folder = folderAttr.FolderPath;
        }
        else
        {
            // 2. Use settings for default folder and subfolder logic
            folder = "Assets/" + settings.defaultGeneratedCodeFolder.Trim('/');

            if (settings.useTypeSubfolders)
            {
                folder = Path.Combine(folder, unitName + settings.typeSubfolderSuffix);
            }
        }

        Directory.CreateDirectory(folder);

        // 3. Use settings for suffixes
        var presetCode = GeneratePresetSO(unitName, fields, settings.presetClassSuffix);
        File.WriteAllText(Path.Combine(folder, $"{unitName}{settings.presetClassSuffix}.cs"), presetCode);

        var dataCode = GenerateDataClass(unitName, fields, settings.dataClassSuffix);
        File.WriteAllText(Path.Combine(folder, $"{unitName}{settings.dataClassSuffix}.cs"), dataCode);
    }

    private static string GeneratePresetSO(string unitName, FieldInfo[] fields, string presetSuffix)
    {
        var namespaces = CollectNamespaces(fields);
        namespaces.Add("UnityEngine"); // Always needed for ScriptableObject
        namespaces.Add("PersistorEngine"); // Always needed for Adaptor types

        var fieldsCode = string.Join("\n    ", fields.Select(f => GenerateFieldWithAttributes(f, true)));
        var copyFromPreset = string.Join("\n        ", fields.Select(f => $"unit.{f.Name} = this.{f.Name};"));

        var usings = string.Join("\n", namespaces.Select(ns => $"using {ns};"));

        // Remove leading underscores from suffix for menu/class name if present
        var menuSuffix = presetSuffix.StartsWith("_") ? presetSuffix.Substring(1) : presetSuffix;

        return
$@"// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by AdaptorCodeGenerator.
//     DO NOT EDIT. Any changes will be lost when the file is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------
{usings}
[CreateAssetMenu(menuName = ""Adaptor Presets/{unitName}{menuSuffix}"")]
public partial class {unitName}{presetSuffix} : ScriptableObject
{{
    {fieldsCode}

    public void CopyFromPreset({unitName} unit)
    {{
        {copyFromPreset}
    }}
}}
";
    }

    private static string GenerateDataClass(string unitName, FieldInfo[] fields, string dataSuffix)
    {
        var namespaces = CollectNamespaces(fields);
        namespaces.Add("PersistorEngine"); // Always needed for Adaptor types
        namespaces.Add("PersistorEngine.Internal"); // Always needed for Adaptor types

        var usings = string.Join("\n", namespaces.Select(ns => $"using {ns};"));

        // Always include persistorId as the first field
        var fieldLines = new List<string> { "public string persistorId;" };
        foreach (var f in fields)
        {
            var (adaptorType, adaptorFieldName) = GetAdaptorInfo(f);
            if (adaptorType != null)
            {
                fieldLines.Add($"public {adaptorType.Name} {adaptorFieldName} = new();");
            }
            else if (typeof(IPersistorId).IsAssignableFrom(f.FieldType))
            {
                fieldLines.Add($"public string {f.Name}Id;");
            }
            else
            {
                string typeName = GetCSharpTypeName(f.FieldType);
                fieldLines.Add($"public {typeName} {f.Name};");
            }
        }

        // Add explicit adaptors (class/field)
        var adaptorTypes = GetAllAdaptorTypes(Type.GetType(unitName) ?? fields.First().DeclaringType);
        var adaptorFields = new List<string>(adaptorTypes.Select(t => $"public {t.Name} {ToCamelCase(t.Name)} = new();"));

        var allFields = fieldLines;
        if (adaptorFields.Count > 0)
            allFields.AddRange(adaptorFields);

        // Generate copy methods
        var copyToDataLines = new List<string>
        {
            "this.persistorId = unit.persistorId;"
        };
        foreach (var f in fields)
        {
            var (adaptorType, adaptorField) = GetAdaptorInfo(f);
            if (adaptorType != null)
            {
                if (f.FieldType.IsValueType)
                    copyToDataLines.Add($"{adaptorField}.CopyToData(unit.{f.Name});");
                else
                    copyToDataLines.Add($"if (unit.{f.Name} != null) {adaptorField}.CopyToData(unit.{f.Name});");
            }
            else if (typeof(IPersistorId).IsAssignableFrom(f.FieldType))
            {
                copyToDataLines.Add($"this.{f.Name}Id = unit.{f.Name} != null ? unit.{f.Name}.persistorId : null;");
            }
            else
            {
                copyToDataLines.Add($"this.{f.Name} = unit.{f.Name};");
            }
        }

        var copyFromDataLines = new List<string>
        {
            "unit.persistorId = this.persistorId;"
        };
        foreach (var f in fields)
        {
            var (adaptorType, adaptorField) = GetAdaptorInfo(f);
            if (adaptorType != null)
            {
                if (f.FieldType.IsValueType)
                    copyFromDataLines.Add($"{adaptorField}.CopyFromData(ref unit.{f.Name});");
                else
                    copyFromDataLines.Add($"if (unit.{f.Name} != null) {adaptorField}.CopyFromData(unit.{f.Name});");
            }
            else if (typeof(IPersistorId).IsAssignableFrom(f.FieldType))
            {
                copyFromDataLines.Add($"unit.{f.Name} = PersistorRegistry.Resolve<{f.FieldType.Name}>(this.{f.Name}Id);");
            }
            else
            {
                copyFromDataLines.Add($"unit.{f.Name} = this.{f.Name};");
            }
        }

        // Add adaptor logic if needed
        var adaptorCopyToData = string.Join("\n        ", adaptorTypes.Select(t => $"{ToCamelCase(t.Name)}.CopyToData(unit);"));
        var adaptorCopyFromData = string.Join("\n        ", adaptorTypes.Select(t => $"{ToCamelCase(t.Name)}.CopyFromData(unit);"));

        return
$@"// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by AdaptorCodeGenerator.
//     DO NOT EDIT. Any changes will be lost when the file is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------
{usings}
[DataFor(typeof({unitName}))]
[System.Serializable]
public partial class {unitName}{dataSuffix}
{{
    {string.Join("\n    ", allFields)}

    public void CopyToData({unitName} unit)
    {{
        {string.Join("\n        ", copyToDataLines)}
        {adaptorCopyToData}
    }}

    public void CopyFromData({unitName} unit)
    {{
        {string.Join("\n        ", copyFromDataLines)}
        {adaptorCopyFromData}
    }}
}}";
    }

    private static string GenerateDataField(FieldInfo field)
    {
        var (adaptorType, adaptorFieldName) = GetAdaptorInfo(field);
        if (adaptorType != null)
            return $"public {adaptorType.Name} {adaptorFieldName} = new();";

        if (typeof(IPersistorId).IsAssignableFrom(field.FieldType))
            return $"public string {field.Name}Id;";
        string typeName = GetCSharpTypeName(field.FieldType);
        return $"public {typeName} {field.Name};";
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

    private static HashSet<string> CollectNamespaces(FieldInfo[] fields)
    {
        var namespaces = new HashSet<string>();
        foreach (var field in fields)
        {
            CollectNamespacesFromType(field.FieldType, namespaces);
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
                    .FirstOrDefault(i =>
                        i.IsGenericType &&
                        (i.GetGenericTypeDefinition() == typeof(IAdaptorField<>) ||
                         i.GetGenericTypeDefinition() == typeof(IAdaptor<>)));
                if (iface != null)
                {
                    var targetType = iface.GetGenericArguments()[0];
                    result[targetType] = type;
                }
            }
        }
        return result;
    }

    private static void CollectNamespacesFromType(Type type, HashSet<string> namespaces)
    {
        if (type == null || string.IsNullOrEmpty(type.Namespace) || type.Namespace == "System")
            return;

        namespaces.Add(type.Namespace);

        if (type.IsArray)
        {
            CollectNamespacesFromType(type.GetElementType(), namespaces);
        }
        else if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
                CollectNamespacesFromType(arg, namespaces);
        }
    }

    private static (Type adaptorType, string adaptorFieldName) GetAdaptorInfo(FieldInfo field)
    {
        // Check for global adaptor
        if (_globalAdaptorMap != null && _globalAdaptorMap.TryGetValue(field.FieldType, out var globalType))
            return (globalType, $"{ToCamelCase(field.Name)}Adaptor");

        // Check for [Adaptor] attribute on the field's type
        var adaptorAttr = field.FieldType.GetCustomAttribute<AdaptorAttribute>();
        if (adaptorAttr != null && adaptorAttr.AdaptorTypes != null && adaptorAttr.AdaptorTypes.Length > 0)
            return (adaptorAttr.AdaptorTypes[0], $"{ToCamelCase(field.Name)}Adaptor");

        return (null, null);
    }
}
