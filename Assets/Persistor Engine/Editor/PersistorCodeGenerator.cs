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

    [MenuItem("Tools/Regenerate Persistors")]
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
                    GenerateForPersistors(type);
                }
            }
        }
        AssetDatabase.Refresh();
        Debug.Log("Persistor code generation complete.");
    }

    // Gets all fields with [Persist] attribute, including inherited ones
    private static FieldInfo[] GetAllPersistFields(Type type)
    {
        var fields = new List<FieldInfo>();
        while (type != null && type != typeof(object))
        {
            fields.AddRange(
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f =>
                        f.IsDefined(typeof(PersistAttribute), false) ||
                        f.IsDefined(typeof(PersistorIdAttribute), false))
            );
            type = type.BaseType;
        }
        return fields.ToArray();
    }
    private static List<Type> GetAllAdaptorTypes(Type unitType)
    {
        var adaptorTypes = new HashSet<Type>();

        var classAttr = unitType.GetCustomAttribute<PersistorAttribute>(inherit: true);
        if (classAttr != null && classAttr.AdaptorTypes != null)
        {
            foreach (var t in classAttr.AdaptorTypes)
                adaptorTypes.Add(t);
        }

        return adaptorTypes.ToList();
    }
    private static FieldInfo[] GetAllPersistorIdFields(Type type)
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
    private static void GenerateForPersistors(Type unitType)
    {
        var fields = GetAllPersistFields(unitType);

        // Enforce exactly one [PersistorId] field of type string (including inherited)
        var idFields = GetAllPersistorIdFields(unitType);
        if (idFields.Length != 1)
        {
            Debug.LogError($"Type '{unitType.FullName}' must have exactly one field marked with [PersistorId] (including inherited fields). Found: {idFields.Length}");
            return;
        }
        if (idFields[0].FieldType != typeof(string))
        {
            Debug.LogError($"Field '{idFields[0].Name}' in '{unitType.FullName}' marked with [PersistorId] must be of type string.");
            return;
        }

        var unitName = unitType.Name;

        // Check for custom folder attribute
        string folder = null;
        var folderAttr = unitType.GetCustomAttribute<PersistorGeneratedFolderAttribute>();
        if (folderAttr != null && !string.IsNullOrEmpty(folderAttr.FolderPath))
        {
            folder = folderAttr.FolderPath;
        }
        else
        {
            // Fallback to default
            folder = $"Assets/Code/{unitName} Persistor";
        }
        Directory.CreateDirectory(folder);

        // Preset ScriptableObject
        var presetCode = GeneratePresetSO(unitName, fields);
        File.WriteAllText(Path.Combine(folder, $"{unitName}_Preset.cs"), presetCode);

        // Data 
        var dataCode = GenerateDataClass(unitName, fields);
        File.WriteAllText(Path.Combine(folder, $"{unitName}__Data.cs"), dataCode);
    }
    private static string GeneratePresetSO(string unitName, FieldInfo[] fields)
    {
        var namespaces = CollectNamespaces(fields);
        namespaces.Add("UnityEngine"); // Always needed for ScriptableObject

        // Exclude fields with [PersistorId] from the preset
        var presetFields = fields.Where(f => !f.IsDefined(typeof(PersistorIdAttribute), false)).ToArray();
        var fieldsCode = string.Join("\n    ", presetFields.Select(f => GenerateFieldWithAttributes(f, true)));
        var copyFromPreset = string.Join("\n        ", presetFields.Select(f => $"unit.{f.Name} = this.{f.Name};"));

        var usings = string.Join("\n", namespaces.Select(ns => $"using {ns};"));

        return
    $@"// ------------------------------------------------------------------------------
    // <auto-generated>
    //     This code was generated by PersistorCodeGenerator.
    //     DO NOT EDIT. Any changes will be lost when the file is regenerated.
    // </auto-generated>
    // ------------------------------------------------------------------------------
    {usings}
    [CreateAssetMenu(menuName = ""Persistor Presets/{unitName}Preset"")]
    public partial class {unitName}_Preset : ScriptableObject
    {{
        {fieldsCode}

        public void CopyFromPreset({unitName} unit)
        {{
            {copyFromPreset}
        }}
    }}
    ";
    }
    private static string GenerateDataClass(string unitName, FieldInfo[] fields)
    {
        var namespaces = CollectNamespaces(fields);
        var usings = string.Join("\n", namespaces.Select(ns => $"using {ns};"));
        var fieldsCode = string.Join("\n    ", fields.Select(f => GenerateDataField(f)));

        // Add explicit adaptors (class/field)
        var adaptorTypes = GetAllAdaptorTypes(Type.GetType(unitName) ?? fields.First().DeclaringType);
        var adaptorFields = new List<string>(adaptorTypes.Select(t => $"public {t.Name} {ToCamelCase(t.Name)};"));

        // Add global adaptors for [Persist] fields (if not already present)
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

        // Generate copy methods
        var copyToData = string.Join("\n        ", fields.Select(f => $"this.{f.Name} = unit.{f.Name};"));
        var copyFromData = string.Join("\n        ", fields.Select(f => $"unit.{f.Name} = this.{f.Name};"));

        // Add adaptor logic if needed
        var adaptorCopyToData = string.Join("\n        ", adaptorTypes.Select(t => $"if ({ToCamelCase(t.Name)} != null) {ToCamelCase(t.Name)}.CopyToData(unit);"));
        var adaptorCopyFromData = string.Join("\n        ", adaptorTypes.Select(t => $"if ({ToCamelCase(t.Name)} != null) {ToCamelCase(t.Name)}.CopyFromData(unit);"));

        var globalAdaptorCopyToData = string.Join("\n        ",
            fields.Where(f => _globalAdaptorMap != null && _globalAdaptorMap.TryGetValue(f.FieldType, out var _))
                  .Select(f => $"if ({ToCamelCase(f.Name)}Adaptor != null) {ToCamelCase(f.Name)}Adaptor.CopyToData(unit.{f.Name});"));

        var globalAdaptorCopyFromData = string.Join("\n        ",
            fields.Where(f => _globalAdaptorMap != null && _globalAdaptorMap.TryGetValue(f.FieldType, out var _))
                  .Select(f => $"if ({ToCamelCase(f.Name)}Adaptor != null) {ToCamelCase(f.Name)}Adaptor.CopyFromData(unit.{f.Name});"));

        return
    $@"// ------------------------------------------------------------------------------
    // <auto-generated>
    //     This code was generated by PersistorCodeGenerator.
    //     DO NOT EDIT. Any changes will be lost when the file is regenerated.
    // </auto-generated>
    // ------------------------------------------------------------------------------
    {usings}
    [DataFor(typeof({unitName}))]
    [System.Serializable]
    public partial class {unitName}__data
    {{
        {allFields}

        public void CopyToData({unitName} unit)
        {{
            PersistorIdAssigner.AssignIdIfNeeded(unit);
            {copyToData}
            {adaptorCopyToData}
            {globalAdaptorCopyToData}
        }}

        public void CopyFromData({unitName} unit)
        {{
            {copyFromData}
            {adaptorCopyFromData}
            {globalAdaptorCopyFromData}
        }}
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

        // Global adaptors for [Persist] fields
        var globalAdaptorCopyToData = string.Join("\n        ",
            fields.Where(f => _globalAdaptorMap != null && _globalAdaptorMap.TryGetValue(f.FieldType, out var _))
                  .Select(f => $"if (data.{ToCamelCase(f.Name)}Adaptor != null) data.{ToCamelCase(f.Name)}Adaptor.CopyToData(unit.{f.Name});"));

        var globalAdaptorCopyFromData = string.Join("\n        ",
            fields.Where(f => _globalAdaptorMap != null && _globalAdaptorMap.TryGetValue(f.FieldType, out var _))
                  .Select(f => $"if (data.{ToCamelCase(f.Name)}Adaptor != null) data.{ToCamelCase(f.Name)}Adaptor.CopyFromData(unit.{f.Name});"));

        return
    $@"// ------------------------------------------------------------------------------
    // <auto-generated>
    //     This code was generated by PersistorCodeGenerator.
    //     DO NOT EDIT. Any changes will be lost when the file is regenerated.
    // </auto-generated>
    // ------------------------------------------------------------------------------
    {usings}
    [PersistorFor(typeof({unitName}))]
    public static partial class {unitName}__Adaptor
    {{
        public static void CopyToData({unitName} unit, {unitName}__data data)
        {{
            PersistorIdAssigner.AssignIdIfNeeded(unit);
            {copyToData}
            {adaptorCopyToData}
            {globalAdaptorCopyToData}
        }}

        public static void CopyFromData({unitName} unit, {unitName}__data data)
        {{
            {copyFromData}
            {adaptorCopyFromData}
            {globalAdaptorCopyFromData}
        }}

        public static void CopyFromPreset({unitName} unit, {unitName}_Preset preset)
        {{
            {copyFromPreset}
        }}
    }}";
    }
    private static string GenerateDataField(FieldInfo field)
    {
        string typeName = GetCSharpTypeName(field.FieldType);

        // Inline instantiate if field type implements IPersistor
        bool isPersistor = typeof(IPersistor).IsAssignableFrom(field.FieldType);
        string initializer = isPersistor ? $" = new ();" : "";

        return $"public {typeName} {field.Name}{initializer};";
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
        return $"data.{field.Name} = unit.{field.Name};";
    }
    private static string GenerateCopyFromData(FieldInfo field)
    {
        return $"unit.{field.Name} = data.{field.Name};";
    }
    private static string GenerateCopyFromPreset(FieldInfo field, string unitName)
    {
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
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.StartsWith("IPersistor_Global_Adaptor"));
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
}
