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

            // --- STEP 1: Read and parse the save file ---
            var json = File.ReadAllText(path);
            var saveRoot = JsonUtility.FromJson<PersistorSaveRoot>(json);

            // --- STEP 2: Collect all persistorIds from the save file ---
            var saveFileIds = new HashSet<string>();
            foreach (var section in saveRoot.sections)
            {
                var runtimeType = GetRuntimeTypeByName(section.typeName);
                if (runtimeType == null) continue;
                var dataType = FindDataType(runtimeType);
                if (dataType == null) continue;

                foreach (var jsonObj in section.jsonObjects)
                {
                    var data = JsonUtility.FromJson(jsonObj, dataType);
                    var id = GetPersistorId(data, dataType);
                    if (!string.IsNullOrEmpty(id))
                        saveFileIds.Add(id);
                }
            }

            // --- STEP 3: Cache parent and sibling index for all StaticID anchors ---
            var staticIdToAnchor = new Dictionary<string, PersistorMonoBehaviour>();
            var staticIdToParentInfo = new Dictionary<string, (Transform parent, int siblingIndex)>();

            foreach (var obj in PersistorRegistry.GetAll().OfType<PersistorMonoBehaviour>())
            {
                if (!string.IsNullOrEmpty(obj.persistorId))
                {
                    staticIdToAnchor[obj.persistorId] = obj;
                    staticIdToParentInfo[obj.persistorId] = (
                        obj.transform.parent,
                        obj.transform.GetSiblingIndex()
                    );
                    Debug.Log($"[Persistor] Cached anchor: {obj.persistorId} parent={obj.transform.parent?.name} siblingIndex={obj.transform.GetSiblingIndex()}");
                }
            }

            // --- STEP 4: Destroy scene anchors not present in the save file ---
            foreach (var kvp in staticIdToAnchor)
            {
                if (!saveFileIds.Contains(kvp.Key))
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(kvp.Value.gameObject);
#else
                    UnityEngine.Object.Destroy(kvp.Value.gameObject);
#endif
                }
            }
            PersistorRegistry.Clear();

            // --- STEP 5: Instantiate all objects from save file ---
            var dataObjectsByType = new Dictionary<Type, List<object>>();
            var runtimeObjectsByType = new Dictionary<Type, List<object>>();
            var dataToRuntime = new Dictionary<object, object>();

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
                    var data = JsonUtility.FromJson(jsonObj, dataType);
                    dataList.Add(data);

                    var id = GetPersistorId(data, dataType);

                    // Destroy anchor if present (should already be destroyed above, but double-check)
                    if (staticIdToAnchor.TryGetValue(id, out var anchorObj))
                    {
#if UNITY_EDITOR
                        UnityEngine.Object.DestroyImmediate(anchorObj.gameObject);
#else
                        UnityEngine.Object.Destroy(anchorObj.gameObject);
#endif
                    }

                    // Create the runtime object (MonoBehaviour or POCO)
                    object runtimeObj = CreateInstanceForType(runtimeType);

                    // Set the persistorId on the runtime object
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

            // --- STEP 6 & 7: Determine parent and sibling index for each object ---
            // We'll store the result for each object to use in STEP 8
            var parentingInfoByObject = new Dictionary<object, (Transform parent, int siblingIndex)>();

            foreach (var kvp in dataObjectsByType)
            {
                var dataType = FindDataType(kvp.Key);
                var runtimeType = kvp.Key;
                var dataList = kvp.Value;
                var runtimeList = runtimeObjectsByType[runtimeType];

                var anchorIdField = dataType.GetField("anchorId");
                var parentIdField = dataType.GetField("parentId");
                var siblingIndexField = dataType.GetField("siblingIndex");

                for (int i = 0; i < dataList.Count; i++)
                {
                    var data = dataList[i];
                    var runtimeObj = runtimeList[i] as PersistorMonoBehaviour;
                    Transform parent = null;
                    int siblingIndex = 0;

                    if (runtimeObj != null)
                    {
                        var id = GetPersistorId(data, dataType);
                        (Transform parent, int siblingIndex) parentInfo;
                        if (staticIdToParentInfo.TryGetValue(id, out parentInfo) && parentInfo.parent != null)
                        {
                            parent = parentInfo.Item1;
                            siblingIndex = parentInfo.Item2;
                            Debug.Log($"[Persistor] (Planned) Parenting {runtimeObj.name} ({id}) to anchor parent {parentInfo.Item1.name} at sibling {parentInfo.Item2}");
                        }
                        else
                        {
                            string anchorId = anchorIdField?.GetValue(data) as string;
                            if (!string.IsNullOrEmpty(anchorId))
                            {
                                var anchors = PersistorRegistry.GetAll().OfType<PersistorAnchor>();
                                foreach (var anchor in anchors)
                                {
                                    if (anchor.AnchorID == anchorId)
                                    {
                                        parent = anchor.transform;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                string parentId = parentIdField?.GetValue(data) as string;
                                if (!string.IsNullOrEmpty(parentId))
                                {
                                    var parentObj = PersistorRegistry.Resolve<PersistorMonoBehaviour>(parentId);
                                    if (parentObj != null)
                                        parent = parentObj.transform;
                                }
                            }
                            siblingIndex = siblingIndexField != null ? (int)siblingIndexField.GetValue(data) : 0;
                            Debug.Log($"[Persistor] (Planned) Parenting {runtimeObj?.name ?? "(null)"} ({id}) to {(parent != null ? parent.name : "root")} at sibling {siblingIndex}");
                        }
                    }

                    parentingInfoByObject[runtimeObj] = (parent, siblingIndex);
                }
            }

            // --- STEP 8: Copy all other data and run adaptors, passing parent/siblingIndex ---
            foreach (var kvp in dataObjectsByType)
            {
                var dataType = FindDataType(kvp.Key);
                var copyFromData = dataType.GetMethod("CopyFromData", BindingFlags.Public | BindingFlags.Instance);
                var dataList = kvp.Value;
                var runtimeList = runtimeObjectsByType[kvp.Key];

                for (int i = 0; i < dataList.Count; i++)
                {
                    var runtimeObj = runtimeList[i] as PersistorMonoBehaviour;
                    if (runtimeObj != null && parentingInfoByObject.TryGetValue(runtimeObj, out var info))
                    {
                        copyFromData.Invoke(dataList[i], new object[] { runtimeObj, info.parent, info.siblingIndex });
                    }
                    else
                    {
                        // For non-PersistorMonoBehaviour types, fallback to old signature
                        copyFromData.Invoke(dataList[i], new object[] { runtimeList[i] });
                    }
                }
            }

            // --- Print hierarchy for debug ---
            void PrintHierarchy(Transform t, string indent = "")
            {
                Debug.Log($"{indent}{t.name}");
                foreach (Transform child in t)
                    PrintHierarchy(child, indent + "  ");
            }
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                PrintHierarchy(root.transform);
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