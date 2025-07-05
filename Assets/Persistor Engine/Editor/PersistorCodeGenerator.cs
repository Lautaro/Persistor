using PersistorEngine;
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
        // ALERT USER IF THERE ARE NON-PERSISTABLE
        // CLASSES WITH [Persist] MEMBERS
        CheckForNonPersistableClasses(); 

        _globalAdaptorMap = DiscoverGlobalAdaptors();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                // Check for [Persist] fields or properties
                var hasPersistMembers = GetAllPersistMembers(type).Any();
                if (hasPersistMembers)

                // Only generate for valid classes
                if (hasPersistMembers
                    && !type.IsInterface
                    && !type.IsAbstract
                    && !type.IsDefined(typeof(PersistorIgnoreAttribute), false))
                {
                    GeneratePersistenceClasses(type);
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

    /// This method is called by the code generator for each eligible type in the project.
    /// It determines the output folder, generates the data and preset classes, and writes them to disk.
    /// Generates all persistence-related code artifacts for a given type, including the data class
    /// (used for serialization/deserialization) and the preset ScriptableObject class (used for editor presets).
    private static void GeneratePersistenceClasses(Type unitType)
    {
        var settings = PersistorSettings.GetOrCreateSettings();
        var members = GetAllPersistMembers(unitType);

        // --- Validation for [Persist] reference types ---
        foreach (var member in members)
        {
            var memberType = member.MemberType;

            // 1. If it's a supported collection, skip further checks (handled by built-in logic)
            if (IsCollection(memberType))
                continue;

            // 2. Skip value types and string
            if (!memberType.IsClass || memberType == typeof(string))
                continue;

            // 3. Check for custom adaptor. Underscore in tuple is used to ignore the field name.
            var (adaptorType, _) = GetAdaptorInfo(member);

            // 4. If not IPersistorId and not handled by a custom adaptor, throw error
            if (!typeof(IPersistorId).IsAssignableFrom(memberType) && adaptorType == null)
            {
                throw new InvalidOperationException(
                    $"[Persist] member '{unitType.FullName}.{member.Name}' is a reference type that does not implement IPersistorId and is not handled by a custom adaptor. " +
                    "Reference persistence requires IPersistorId or a custom adaptor.");
            }
        }
        

        // Only generate for types that implement IPersistorId (directly or via base)
        if (!typeof(IPersistorId).IsAssignableFrom(unitType))
        {
            Debug.LogError($"Type '{unitType.FullName}': For Persistor Engine to persist this class it must implement IPersistorId (inherit from PersistorMonoBehaviour or PersistorObject).");
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
        var presetCode = GeneratePresetSO(unitName, members, settings.presetClassSuffix);
        File.WriteAllText(Path.Combine(folder, $"{unitName}{settings.presetClassSuffix}.cs"), presetCode);

        var dataCode = GenerateDataClass(unitName, members, settings.dataClassSuffix);
        File.WriteAllText(Path.Combine(folder, $"{unitName}{settings.dataClassSuffix}.cs"), dataCode);
    }

    private static string GeneratePresetSO(string unitName, List<PersistMember> members, string presetSuffix)
    {
        var namespaces = CollectNamespaces(members);
        namespaces.Add("UnityEngine"); // Always needed for ScriptableObject
        namespaces.Add("PersistorEngine"); // Always needed for Adaptor types

        var fieldsCode = string.Join("\n    ", members.Select(m => GenerateFieldWithAttributes(m, true)));
        var copyFromPreset = string.Join("\n        ", members.Select(m => $"unit.{m.Name} = this.{m.Name};"));

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
}}";
    }

    private static string GenerateDataClass(string unitName, List<PersistMember> members, string dataSuffix)
    {
        // Collect namespaces from member types
        var namespaces = CollectNamespaces(members);
        namespaces.Add("PersistorEngine"); // Always needed for Adaptor types
        namespaces.Add("PersistorEngine.Internal"); // Always needed for Adaptor types

        // Determine the unit type for hierarchy logic
        var unitType = Type.GetType(unitName) ?? members.FirstOrDefault()?.Field?.DeclaringType ?? members.FirstOrDefault()?.Property?.DeclaringType;
        bool isPersistorMonoBehaviour = unitType != null && typeof(PersistorMonoBehaviour).IsAssignableFrom(unitType);

        // Add UnityEngine only if needed for hierarchy code or member types
        bool needsUnityEngine = isPersistorMonoBehaviour ||
            members.Any(m => m.MemberType.Namespace != null && m.MemberType.Namespace.StartsWith("UnityEngine"));
        if (needsUnityEngine)
            namespaces.Add("UnityEngine");

        // Always include persistorId as the first field
        var fieldLines = new List<string> { "public string persistorId;" };

        // Add hierarchy fields for PersistorMonoBehaviour types
        if (isPersistorMonoBehaviour)
        {
            fieldLines.Add("public string anchorId;");
            fieldLines.Add("public string parentId;");
            fieldLines.Add("public int siblingIndex;");
            fieldLines.Add("public string gameObjectName;"); // Persist GameObject name
        }

        foreach (var m in members)
        {
            var (adaptorType, adaptorFieldName) = GetAdaptorInfo(m);
            if (adaptorType != null)
            {
                fieldLines.Add($"public {adaptorType.Name} {adaptorFieldName} = new();");
            }
            else if (IsCollection(m.MemberType))
            {
                var elementType = GetCollectionElementType(m.MemberType);
                if (typeof(IPersistorId).IsAssignableFrom(elementType))
                {
                    // Reference type: use Ids and registry
                    fieldLines.Add($"public List<string> {m.Name}Ids = new List<string>();");
                }
                else
                {
                    // Value type: serialize directly
                    string elemTypeName = GetCSharpTypeName(elementType);
                    fieldLines.Add($"public List<{elemTypeName}> {m.Name} = new List<{elemTypeName}>();");
                }
            }
            else if (typeof(IPersistorId).IsAssignableFrom(m.MemberType))
            {
                fieldLines.Add($"public string {m.Name}Id;");
            }
            else
            {
                string typeName = GetCSharpTypeName(m.MemberType);
                fieldLines.Add($"public {typeName} {m.Name};");
            }
        }

        // Add explicit adaptors (class/field)
        var adaptorTypes = GetAllAdaptorTypes(unitType);
        var adaptorFields = new List<string>(adaptorTypes.Select(t => $"public {t.Name} {ToCamelCase(t.Name)} = new();"));

        var allFields = fieldLines;
        if (adaptorFields.Count > 0)
            allFields.AddRange(adaptorFields);

        // Generate copy methods
        var copyToDataLines = new List<string>
        {
            "this.persistorId = unit.persistorId;"
        };

        // Save hierarchy info in CopyToData
        if (isPersistorMonoBehaviour)
        {
            copyToDataLines.Add(
    @"// Hierarchy persistence
    var parent = unit.transform.parent;
    if (parent != null)
    {
        var anchor = parent.GetComponent<PersistorAnchor>();
        if (anchor != null)
        {
            this.anchorId = anchor.AnchorID;
            this.parentId = null;
        }
        else
        {
            var parentPMB = parent.GetComponent<PersistorMonoBehaviour>();
            if (parentPMB != null)
            {
                this.parentId = parentPMB.persistorId;
                this.anchorId = null;
            }
            else
            {
                this.anchorId = null;
                this.parentId = null;
            }
        }
    }
    else
    {
        this.anchorId = null;
        this.parentId = null;
    }
    this.siblingIndex = unit.transform.GetSiblingIndex();
    this.gameObjectName = unit.gameObject.name;"
            );
        }

        // --- Add code to copy all [Persist] fields/properties ---
        foreach (var m in members)
        {
            var (adaptorType, adaptorField) = GetAdaptorInfo(m);
            if (adaptorType != null)
            {
                if (m.MemberType.IsValueType)
                    copyToDataLines.Add($"{adaptorField}.CopyToData(ref unit.{m.Name});");
                else
                    copyToDataLines.Add($"if (unit.{m.Name} != null) {adaptorField}.CopyToData(unit.{m.Name});");
            }
            else if (IsCollection(m.MemberType))
            {
                var elementType = GetCollectionElementType(m.MemberType);
                if (typeof(IPersistorId).IsAssignableFrom(elementType))
                {
                    copyToDataLines.Add(
                        $"this.{m.Name}Ids = unit.{m.Name} != null ? unit.{m.Name}.Select(x => x != null ? x.persistorId : null).Where(id => id != null).ToList() : new List<string>();"
                    );
                }
                else
                {
                    copyToDataLines.Add(
                        $"this.{m.Name} = unit.{m.Name} != null ? new List<{GetCSharpTypeName(elementType)}>(unit.{m.Name}) : new List<{GetCSharpTypeName(elementType)}>();"
                    );
                }
            }
            else if (typeof(IPersistorId).IsAssignableFrom(m.MemberType))
            {
                copyToDataLines.Add($"this.{m.Name}Id = unit.{m.Name} != null ? unit.{m.Name}.persistorId : null;");
            }
            else
            {
                copyToDataLines.Add($"this.{m.Name} = unit.{m.Name};");
            }
        }

        // --- CopyFromData logic (unchanged) ---
        var copyFromDataSignature = isPersistorMonoBehaviour
            ? $"public void CopyFromData({unitName} unit, Transform parent, int siblingIndex)"
            : $"public void CopyFromData({unitName} unit)";

        var copyFromDataLines = new List<string>
        {
            "unit.persistorId = this.persistorId;"
        };

        if (isPersistorMonoBehaviour)
        {
            copyFromDataLines.Add(
    @"// Hierarchy restoration (parameters provided by Persistor.LoadAll)
    unit.transform.SetParent(parent, true);
    unit.transform.SetSiblingIndex(siblingIndex);
    unit.gameObject.name = this.gameObjectName;"
            );
        }

        foreach (var m in members)
        {
            var (adaptorType, adaptorField) = GetAdaptorInfo(m);
            if (adaptorType != null)
            {
                if (m.MemberType.IsValueType)
                    copyFromDataLines.Add($"{adaptorField}.CopyFromData(ref unit.{m.Name});");
                else
                    copyFromDataLines.Add($"if (unit.{m.Name} != null) {adaptorField}.CopyFromData(unit.{m.Name});");
            }
            else if (IsCollection(m.MemberType))
            {
                var elementType = GetCollectionElementType(m.MemberType);
                if (typeof(IPersistorId).IsAssignableFrom(elementType))
                {
                    string elemTypeName = GetCSharpTypeName(elementType);
                    copyFromDataLines.Add(
                        $"unit.{m.Name} = this.{m.Name}Ids != null ? this.{m.Name}Ids.Select(id => PersistorRegistry.Resolve<{elemTypeName}>(id)).Where(x => x != null).ToList() : new List<{elemTypeName}>();"
                    );
                }
                else
                {
                    string elemTypeName = GetCSharpTypeName(elementType);
                    copyFromDataLines.Add($"unit.{m.Name} = this.{m.Name} != null ? new List<{elemTypeName}>(this.{m.Name}) : new List<{elemTypeName}>();");
                }
            }
            else if (typeof(IPersistorId).IsAssignableFrom(m.MemberType))
            {
                copyFromDataLines.Add($"unit.{m.Name} = PersistorRegistry.Resolve<{m.MemberType.Name}>(this.{m.Name}Id);");
            }
            else
            {
                copyFromDataLines.Add($"unit.{m.Name} = this.{m.Name};");
            }
        }

        // Add adaptor logic if needed (supporting IMonoBehaviourAdaptor)
        var adaptorCopyToData = string.Join("\n        ", adaptorTypes.Select(t => GenerateAdaptorCall("CopyToData", t, "unit")));
        var adaptorCopyFromData = string.Join("\n        ", adaptorTypes.Select(t => GenerateAdaptorCall("CopyFromData", t, "unit")));

        // Conditionally add System.Linq if any LINQ methods are used
        var needsLinq = copyToDataLines.Concat(copyFromDataLines).Any(line =>
            line.Contains(".Select(") || line.Contains(".Where(") || line.Contains(".ToList("));
        if (needsLinq)
            namespaces.Add("System.Linq");

        // Generate using statements from collected namespaces, sorted for consistency
        var usings = string.Join("\n", namespaces.OrderBy(ns => ns).Select(ns => $"using {ns};"));

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

        {copyFromDataSignature}
        {{
            {string.Join("\n        ", copyFromDataLines)}
            {adaptorCopyFromData}
        }}
    }}";
    }

    private static string GenerateDataField(PersistMember member)
    {
        var (adaptorType, adaptorFieldName) = GetAdaptorInfo(member);
        if (adaptorType != null)
            return $"public {adaptorType.Name} {adaptorFieldName} = new();";

        if (typeof(IPersistorId).IsAssignableFrom(member.MemberType))
            return $"public string {member.Name}Id;";
        string typeName = GetCSharpTypeName(member.MemberType);
        return $"public {typeName} {member.Name};";
    }

    private static string GenerateFieldWithAttributes(PersistMember member, bool forScriptableObject)
    {
        var attrs = (member.Field != null
            ? member.Field.GetCustomAttributes(false)
            : member.Property.GetCustomAttributes(false))
            .Select(attr => AttributeToSource(attr))
            .Where(s => !string.IsNullOrEmpty(s));
        string attributes = string.Join("\n    ", attrs);

        string typeName = GetCSharpTypeName(member.MemberType);
        string fieldLine = $"public {typeName} {member.Name};";
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

        // Handle generic types (e.g., List<T>, Dictionary<K,V>)
        if (type.IsGenericType)
        {
            var genericTypeName = type.GetGenericTypeDefinition().Name;
            var backtickIndex = genericTypeName.IndexOf('`');
            if (backtickIndex > 0)
                genericTypeName = genericTypeName.Substring(0, backtickIndex);

            var genericArgs = type.GetGenericArguments();
            var argNames = string.Join(", ", genericArgs.Select(GetCSharpTypeName));
            return $"{genericTypeName}<{argNames}>";
        }

        return type.Name;
    }

    private static HashSet<string> CollectNamespaces(List<PersistMember> members)
    {
        var namespaces = new HashSet<string>();
        foreach (var member in members)
        {
            CollectNamespacesFromType(member.MemberType, namespaces);
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

    private static (Type adaptorType, string adaptorFieldName) GetAdaptorInfo(PersistMember member)
    {
        var type = member.MemberType;
        // Check for global adaptor
        if (_globalAdaptorMap != null && _globalAdaptorMap.TryGetValue(type, out var globalType))
            return (globalType, $"{ToCamelCase(member.Name)}Adaptor");

        // Check for [Adaptor] attribute on the member's type
        var adaptorAttr = type.GetCustomAttribute<AdaptorAttribute>();
        if (adaptorAttr != null && adaptorAttr.AdaptorTypes != null && adaptorAttr.AdaptorTypes.Length > 0)
            return (adaptorAttr.AdaptorTypes[0], $"{ToCamelCase(member.Name)}Adaptor");

        return (null, null);
    }
    private static string GenerateAdaptorCall(string methodName, Type adaptorType, string unitVarName)
    {
        // If it's an IMonoBehaviourAdaptor, cast to MonoBehaviour
        if (typeof(IMonoBehaviourAdaptor).IsAssignableFrom(adaptorType))
        {
            return $"{ToCamelCase(adaptorType.Name)}.{methodName}(({nameof(MonoBehaviour)}){unitVarName});";
        }

        // If it's an IAdaptor<T>, call directly
        var adaptorInterface = adaptorType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.StartsWith("IAdaptor"));
        if (adaptorInterface != null)
        {
            return $"{ToCamelCase(adaptorType.Name)}.{methodName}({unitVarName});";
        }

        // Otherwise, throw a clear error
        return $"throw new System.InvalidOperationException(\"Adaptor {adaptorType.Name} must implement IAdaptor<T> or IMonoBehaviourAdaptor.\");";
    }
    private static bool IsCollection(Type type)
    {
        if (type.IsArray)
            return true;

        if (type.IsGenericType)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return true;
            }
        }
        return false;
    }
    // Gets all fields and properties with [Persist] attribute, including inherited ones.
    // Only accepts writable properties. Logs a warning for non-writable [Persist] properties.
    private static List<PersistMember> GetAllPersistMembers(Type type)
    {
        var members = new List<PersistMember>();
        while (type != null && type != typeof(object))
        {
            // Accept all fields with [Persist]
            members.AddRange(
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => f.IsDefined(typeof(PersistAttribute), false))
                    .Select(f => new PersistMember(f))
            );

            // Accept only writable properties with [Persist]
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(p => p.IsDefined(typeof(PersistAttribute), false)))
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    members.Add(new PersistMember(prop));
                }
                else
                {
                    Debug.LogError(
                        $"[Persist] property '{type.FullName}.{prop.Name}' is not writable and will be ignored. " +
                        "Properties marked with [Persist] must have both a getter and a setter (get; set;)."
                    );
                }
            }
            type = type.BaseType;
        }
        return members;
    }

    private static Type GetCollectionElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();

        if (type.IsGenericType)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
            }
        }
        return null;
    }
    private static void CheckForNonPersistableClasses()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                // Skip abstract, interface, or ignored types
                if (type.IsAbstract || type.IsInterface || type.IsDefined(typeof(PersistorIgnoreAttribute), false))
                    continue;

                // Find [Persist] fields/properties
                var persistFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => f.IsDefined(typeof(PersistAttribute), false));
                var persistProps = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(p => p.IsDefined(typeof(PersistAttribute), false) && p.CanRead && p.CanWrite);

                if (persistFields.Any() || persistProps.Any())
                {
                    // Check for IPersistorId
                    if (!typeof(IPersistorId).IsAssignableFrom(type))
                    {
                        Debug.LogWarning(
                            $"[Persistor] Class '{type.FullName}' has [Persist] fields or properties but does not implement IPersistorId (e.g., does not inherit from PersistorMonoBehaviour). " +
                            "It will NOT be persisted by Persistor.SaveAll/LoadAll. " +
                            "To persist this class, inherit from PersistorMonoBehaviour or implement IPersistorId."
                        );
                    }
                }
            }
        }
    }

    /// <summary>
    /// Represents a persistable member (either a field or a property) for the Persistor code generator.
    /// This abstraction allows the generator and runtime to treat fields and properties uniformly,
    /// enabling support for the [Persist] attribute on both fields and properties.
    /// 
    /// - If the member is a field, <see cref="Field"/> is non-null and <see cref="Property"/> is null.
    /// - If the member is a property, <see cref="Property"/> is non-null and <see cref="Field"/> is null.
    /// 
    /// Provides unified access to the member's name, type, value get/set, and attribute checks.
    /// </summary>
    public class PersistMember
    {
        public FieldInfo Field { get; }
        public PropertyInfo Property { get; }
        public string Name => Field != null ? Field.Name : Property.Name;
        public Type MemberType => Field != null ? Field.FieldType : Property.PropertyType;

        public PersistMember(FieldInfo field) { Field = field; }
        public PersistMember(PropertyInfo prop) { Property = prop; }

        public object GetValue(object obj) =>
            Field != null ? Field.GetValue(obj) : Property.GetValue(obj);

        public void SetValue(object obj, object value)
        {
            if (Field != null) Field.SetValue(obj, value);
            else if (Property.CanWrite) Property.SetValue(obj, value);
        }

        public bool IsDefined<T>() where T : Attribute =>
            Field != null ? Field.IsDefined(typeof(T), false) : Property.IsDefined(typeof(T), false);
    }

}
