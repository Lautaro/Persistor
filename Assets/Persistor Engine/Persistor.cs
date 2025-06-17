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
            var allObjects = PersistorRegistry.GetAll().ToList();
            var path = Path.Combine(Application.persistentDataPath, fileName + "_graph.json");
            var saveRoot = new PersistorSaveRoot();

            foreach (var group in allObjects.GroupBy(o => o.GetType()))
            {
                var dataType = FindDataType(group.Key);
                if (dataType == null)
                {
                    Debug.LogWarning($"No data class found for {group.Key.Name}. Skipping save for this type.");
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
        /// </summary>
        public static void LoadAll(string fileName)
        {
            var path = Path.Combine(Application.persistentDataPath, fileName + "_graph.json");
            if (!File.Exists(path))
                return;

            PersistorRegistry.Clear();

            var json = File.ReadAllText(path);
            var saveRoot = JsonUtility.FromJson<PersistorSaveRoot>(json);

            var dataToRuntime = new Dictionary<object, object>();
            var dataObjectsByType = new Dictionary<Type, List<object>>();
            var runtimeObjectsByType = new Dictionary<Type, List<object>>();

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

                    object runtimeObj = CreateInstanceForType(runtimeType);

                    var id = GetPersistorId(data, dataType);
                    SetPersistorId(runtimeObj, runtimeType, id);

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

            foreach (var kvp in dataObjectsByType)
            {
                var dataType = FindDataType(kvp.Key);
                var copyFromData = dataType.GetMethod("CopyFromData", BindingFlags.Public | BindingFlags.Instance);
                var dataList = kvp.Value;
                var runtimeList = runtimeObjectsByType[kvp.Key];
                for (int i = 0; i < dataList.Count; i++)
                {
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