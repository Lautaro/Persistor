using PersistorEngine.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace PersistorEngine
{
    /// <summary>
    /// The Persistor class handles the creation, saving, and loading of persistent game objects.
    /// It supports prefab instantiation, serialization to JSON, and reference resolution via a global registry.
    /// </summary>
    public static class Persistor
    {
        /// <summary>
        /// Creates a new instance of a MonoBehaviour, optionally from a prefab and/or preset.
        /// If a prefab is not provided, it tries to find one via a custom attribute.
        /// If a preset is provided, it copies preset data into the new instance.
        /// </summary>
        public static TUnit Create<TUnit>(
            GameObject prefab = null,
            ScriptableObject preset = null,
            string rootName = null)
            where TUnit : MonoBehaviour
        {
            var unit = (TUnit)CreateInstanceForType(typeof(TUnit), prefab, rootName);

            // If a preset is provided, copy its data into the new instance
            if (preset != null)
            {
                var copyFromPreset = preset.GetType().GetMethod("CopyFromPreset", BindingFlags.Public | BindingFlags.Instance);
                if (copyFromPreset != null)
                {
                    copyFromPreset.Invoke(preset, new object[] { unit });
                }
                else
                {
                    Debug.LogWarning($"Preset of type {preset.GetType().Name} does not have a CopyFromPreset method.");
                }
            }

            return unit;
        }

        /// <summary>
        /// Saves the entire scene graph (all registered IPersistorId objects) to a JSON file.
        /// </summary>
        public static void SaveAll(string fileName)
        {
            var allObjects = PersistorRegistry.GetAll();

            var duplicateIds = allObjects
            .GroupBy(o => o.persistorId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

            if (duplicateIds.Any())
            {
                Debug.LogWarning("Duplicate persistorId(s) found: " + string.Join(", ", duplicateIds));
            }

            // REMOVE
            if (allObjects.Any(o => o == null))
                Debug.LogError("PersistorRegistry contains null objects!");

            // Filter to only one object per unique persistorId
            var uniqueObjects = allObjects
                .GroupBy(o => o.persistorId)
                .Select(g => g.First())
                .ToList();

            var path = Path.Combine(Application.persistentDataPath, fileName + "_graph.json");
            var saveRoot = new PersistorSaveRoot();

            foreach (var group in uniqueObjects.GroupBy(o => o.GetType()))
            {
                var dataType = FindDataType(group.Key);
                if (dataType == null)
                {
                    Debug.LogWarning($"Persist Engine has not generated a data class for {group.Key.Name}. Did you forget to generate? Must inherit from PersistorObject, PersistorMonobehaviour and have at least one field marked [Persist]. Skipping save for this type.");
                    continue;
                }

                var section = new PersistorSaveRoot.TypeSection
                {
                    typeName = group.Key.FullName,
                    jsonObjects = new List<string>()
                };

                var copyToData = dataType.GetMethod("CopyToData", BindingFlags.Public | BindingFlags.Instance);

                foreach (var item in group)
                {
                    var data = Activator.CreateInstance(dataType);
                    copyToData.Invoke(data, new object[] { item });
                    section.jsonObjects.Add(JsonUtility.ToJson(data));
                }
                saveRoot.sections.Add(section);
            }

            var jsonOut = JsonUtility.ToJson(saveRoot, true);
            File.WriteAllText(path, jsonOut);
        }

        /// <summary>
        /// Loads the entire scene graph from a JSON file, recreating all objects and references.
        /// Destroys all existing PersistorMonoBehaviour objects before loading to avoid duplicates.
        /// </summary>
        public static void LoadAll(string fileName)
        {
            var path = Path.Combine(Application.persistentDataPath, fileName + "_graph.json");
            if (!File.Exists(path))
                return;

            // --- STEP 1: Destroy all existing persisted objects in the scene ---
            // This ensures we don't get duplicates when loading from file.
            // Only objects inheriting from PersistorMonoBehaviour are destroyed.
            var existingPersisted = UnityEngine.Object.FindObjectsByType<PersistorMonoBehaviour>(FindObjectsSortMode.None);
            foreach (var obj in existingPersisted)
            {
                // Destroy the entire GameObject to remove all components and children.
                UnityEngine.Object.Destroy(obj.gameObject);
            }

            // --- STEP 2: Clear the registry ---
            // This removes all references to previously registered objects.
            PersistorRegistry.Clear();

            // --- STEP 3: Read and parse the save file ---
            var json = File.ReadAllText(path);
            var saveRoot = JsonUtility.FromJson<PersistorSaveRoot>(json);

            // Dictionaries to map between loaded data objects and runtime objects.
            var dataToRuntime = new Dictionary<object, object>();
            var dataObjectsByType = new Dictionary<Type, List<object>>();
            var runtimeObjectsByType = new Dictionary<Type, List<object>>();

            // --- STEP 4: First pass - Instantiate and register all objects, but do NOT set up hierarchy yet ---
            foreach (var section in saveRoot.sections)
            {
                var runtimeType = GetRuntimeTypeByName(section.typeName);
                if (runtimeType == null)
                    continue;

                var dataType = FindDataType(runtimeType);
                if (dataType == null)
                    continue;

                var dataList = new List<object>();
                var runtimeList = new List<object>();

                foreach (var jsonObj in section.jsonObjects)
                {
                    // Deserialize the data object (fields from save file)
                    var data = JsonUtility.FromJson(jsonObj, dataType);
                    dataList.Add(data);

                    // Create the runtime object (MonoBehaviour or POCO)
                    object runtimeObj = CreateInstanceForType(runtimeType);

                    // Set the persistorId on the runtime object (so it can be found by registry)
                    var id = GetPersistorId(data, dataType);
                    SetPersistorId(runtimeObj, runtimeType, id);

                    // Register the object in the global registry
                    if (runtimeObj is IPersistorId idObj)
                    {
                        PersistorRegistry.UpdatePersistorId(idObj, id);
                    }

                    dataToRuntime[data] = runtimeObj;
                    runtimeList.Add(runtimeObj);
                }

                dataObjectsByType[runtimeType] = dataList;
                runtimeObjectsByType[runtimeType] = runtimeList;
            }

            // --- STEP 5: Second pass - Restore hierarchy and sibling order for all PersistorMonoBehaviour objects ---
            foreach (var kvp in dataObjectsByType)
            {
                var dataType = FindDataType(kvp.Key);
                var runtimeType = kvp.Key;
                var dataList = kvp.Value;
                var runtimeList = runtimeObjectsByType[runtimeType];

                // Use reflection to check if this type is a PersistorMonoBehaviour
                bool isPersistorMonoBehaviour = typeof(PersistorMonoBehaviour).IsAssignableFrom(runtimeType);

                if (!isPersistorMonoBehaviour)
                    continue; // Only do hierarchy for PersistorMonoBehaviour

                // Get the anchorId, parentId, and siblingIndex fields from the data class using reflection
                var anchorIdField = dataType.GetField("anchorId");
                var parentIdField = dataType.GetField("parentId");
                var siblingIndexField = dataType.GetField("siblingIndex");

                for (int i = 0; i < dataList.Count; i++)
                {
                    var data = dataList[i];
                    var runtimeObj = runtimeList[i] as PersistorMonoBehaviour;
                    if (runtimeObj == null)
                        continue;

                    // --- Find the correct parent transform ---
                    Transform newParent = null;

                    // 1. Try to parent to a PersistorAnchor if anchorId is set
                    string anchorId = anchorIdField?.GetValue(data) as string;
                    if (!string.IsNullOrEmpty(anchorId))
                    {
                        // Find all anchors in the scene and match by AnchorID
                        var anchors = UnityEngine.Object.FindObjectsByType<PersistorAnchor>(FindObjectsSortMode.None);
                        foreach (var anchor in anchors)
                        {
                            if (anchor.AnchorID == anchorId)
                            {
                                newParent = anchor.transform;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // 2. Try to parent to another PersistorMonoBehaviour if parentId is set
                        string parentId = parentIdField?.GetValue(data) as string;
                        if (!string.IsNullOrEmpty(parentId))
                        {
                            // Use the registry to resolve the parent by persistorId
                            var parentObj = PersistorRegistry.Resolve<PersistorMonoBehaviour>(parentId);
                            if (parentObj != null)
                                newParent = parentObj.transform;
                        }
                    }

                    // 3. If neither, parent will be null (scene root)
                    runtimeObj.transform.SetParent(newParent, true);

                    // --- Set sibling index ---
                    int siblingIndex = siblingIndexField != null ? (int)siblingIndexField.GetValue(data) : 0;
                    // Clamp to valid range
                    int maxSiblings = runtimeObj.transform.parent != null
                        ? runtimeObj.transform.parent.childCount
                        : UnityEngine.SceneManagement.SceneManager.GetActiveScene().rootCount;
                    siblingIndex = Mathf.Clamp(siblingIndex, 0, maxSiblings - 1);
                    runtimeObj.transform.SetSiblingIndex(siblingIndex);
                }
            }

            // --- STEP 6: Third pass - Copy all other data and run adaptors ---
            foreach (var kvp in dataObjectsByType)
            {
                var dataType = FindDataType(kvp.Key);
                var copyFromData = dataType.GetMethod("CopyFromData", BindingFlags.Public | BindingFlags.Instance);
                var dataList = kvp.Value;
                var runtimeList = runtimeObjectsByType[kvp.Key];
                for (int i = 0; i < dataList.Count; i++)
                {
                    // This will copy all fields and run adaptors, now that hierarchy is correct
                    copyFromData.Invoke(dataList[i], new object[] { runtimeList[i] });
                }
            }
        }

        /// <summary>
        /// Shared logic for creating a MonoBehaviour (with prefab lookup) or POCO.
        /// </summary>
        private static object CreateInstanceForType(Type type, GameObject explicitPrefab = null, string rootName = null)
        {
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                GameObject go = null;
                GameObject prefab = explicitPrefab;

                // If no explicit prefab, try attribute
                if (prefab == null)
                {
                    var prefabAttr = type.GetCustomAttribute<PersistorPrefabAttribute>();
                    if (prefabAttr != null)
                    {
                        prefab = PersistorPrefabLoader.LoadPrefab(prefabAttr.Reference);
                        if (prefab == null)
                            throw new InvalidOperationException($"Prefab '{prefabAttr.Reference}' could not be loaded for {type.Name}");
                    }
                }

                if (prefab != null)
                {
                    go = UnityEngine.Object.Instantiate(prefab);
                    go.name = string.IsNullOrEmpty(rootName) ? type.Name : rootName;
                }
                else
                {
                    go = new GameObject(string.IsNullOrEmpty(rootName) ? type.Name : rootName);
                }

                return go.GetComponent(type) ?? go.AddComponent(type);
            }
            else
            {
                return Activator.CreateInstance(type);
            }
        }// --- Helper and utility methods below ---

        /// <summary>
        /// Finds a runtime type by its full name across all loaded assemblies.
        /// </summary>
        private static Type GetRuntimeTypeByName(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == typeName);
        }

        /// <summary>
        /// Gets the persistorId value from an object, searching for a property or field.
        /// </summary>
        private static string GetPersistorId(object obj, Type type)
        {
            var idProp = type.GetProperty("persistorId");
            if (idProp != null)
                return (string)idProp.GetValue(obj);
            var idField = type.GetField("persistorId");
            if (idField != null)
                return (string)idField.GetValue(obj);
            return null;
        }

        /// <summary>
        /// Sets the persistorId value on an object, searching for a property or field.
        /// </summary>
        private static void SetPersistorId(object obj, Type type, string id)
        {
            var idProp = type.GetProperty("persistorId");
            if (idProp != null)
                idProp.SetValue(obj, id);
            else
            {
                var idField = type.GetField("persistorId");
                if (idField != null)
                    idField.SetValue(obj, id);
            }
        }

        /// <summary>
        /// Finds the data class type for a given unit type using the DataForAttribute.
        /// </summary>
        private static Type FindDataType<TUnit>()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t =>
                    t.GetCustomAttributes(typeof(DataForAttribute), false)
                     .Cast<DataForAttribute>()
                     .Any(attr => attr.TargetType == typeof(TUnit)));
        }

        /// <summary>
        /// Finds the data class type for a given unit type using the DataForAttribute.
        /// </summary>
        private static Type FindDataType(Type unitType)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t =>
                    t.GetCustomAttributes(typeof(DataForAttribute), false)
                     .Cast<DataForAttribute>()
                     .Any(attr => attr.TargetType == unitType));
        }

        /// <summary>
        /// Recursively collects all unique IPersistorId objects referenced by the given object.
        /// This is used to save the entire object graph for dependency-aware persistence.
        /// </summary>
        private static void CollectPersistorDependencies(object obj, HashSet<IPersistorId> collected)
        {
            if (obj == null) return;
            if (!(obj is IPersistorId idObj)) return;
            if (collected.Contains(idObj)) return;

            collected.Add(idObj);

            var type = obj.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (typeof(IPersistorId).IsAssignableFrom(field.FieldType))
                {
                    var dep = field.GetValue(obj);
                    if (dep != null)
                        CollectPersistorDependencies(dep, collected);
                }
                else if (typeof(IEnumerable).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(string))
                {
                    var enumerable = field.GetValue(obj) as IEnumerable;
                    if (enumerable != null)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item is IPersistorId)
                                CollectPersistorDependencies(item, collected);
                        }
                    }
                }
            }
        }
    }
}