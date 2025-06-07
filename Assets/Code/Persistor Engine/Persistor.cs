using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

#region summary
/// <summary>
/// Persistor is a static utility for saving and loading collections of MonoBehaviour-derived units
/// to and from disk as JSON, using Unity's JsonUtility. It is designed for use with Unity GameObjects,
/// and supports flexible instantiation via prefab references, factory methods, or explicit prefab parameters.
///
/// <para>Usage Overview:</para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Save:</b> Call <c>Persistor.SaveAll&lt;TUnit&gt;(units, fileName)</c> to serialize a collection of units.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Load (with prefab):</b> Call <c>Persistor.LoadAll&lt;TUnit&gt;(fileName, prefab)</c> to instantiate from a prefab.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Load (with factory):</b> Call <c>Persistor.LoadAll&lt;TUnit&gt;(fileName, factory)</c> to use a custom instantiation method.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Load (with attribute):</b> Add <c>[PersistorPrefab("PrefabNameOrKey")]</c> to your unit or persistor class,
/// then call <c>Persistor.LoadAll&lt;TUnit&gt;(fileName)</c> to automatically use the referenced prefab.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Load (data only):</b> Call <c>Persistor.LoadAllData&lt;TUnit&gt;(fileName)</c> to get the raw data objects (not MonoBehaviours).
/// </description>
/// </item>
/// </list>
///
/// <para>Notes:</para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>MonoBehaviour constraint:</b> All generic methods require <c>TUnit : MonoBehaviour</c>.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>GameObject instantiation:</b> The user must provide a prefab, a factory, or a [PersistorPrefab] attribute.
/// No default GameObject is created automatically.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Prefab loading:</b> Prefabs can be referenced by Resources path, Addressable key, or a custom loader.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Extensibility:</b> To support non-MonoBehaviour types, refactor to remove the MonoBehaviour constraint and adapt instantiation logic.
/// </description>
/// </item>
/// </list>
/// </summary>
#endregion
public static class Persistor
{
    public static void SaveAll<TUnit>(IEnumerable<TUnit> units, string fileName)
        where TUnit : MonoBehaviour
    {
        var dataType = FindDataType<TUnit>();
        var persistorType = FindPersistorType<TUnit>();

        var copyToData = persistorType.GetMethod("CopyToData", BindingFlags.Public | BindingFlags.Static);
        if (copyToData == null)
            throw new InvalidOperationException($"CopyToData not found on {persistorType.Name}");

        var dataList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(dataType));
        foreach (var unit in units)
        {
            var data = Activator.CreateInstance(dataType);

            // Instantiate adaptors if needed
            foreach (var field in dataType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType.Name.EndsWith("Adaptor") && field.GetValue(data) == null)
                {
                    field.SetValue(data, Activator.CreateInstance(field.FieldType));
                }
            }

            copyToData.Invoke(null, new object[] { unit, data });
            dataList.Add(data);
        }

        var wrapperType = typeof(Wrapper<>).MakeGenericType(dataType);
        var wrapper = Activator.CreateInstance(wrapperType);
        wrapperType.GetField("items").SetValue(wrapper, dataList);

        var json = JsonUtility.ToJson(wrapper, true);
        var path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Loads all persisted data objects for the given unit type from the specified file.
    /// Returns an IList of the data objects (e.g., Enemy_Data), not instantiated MonoBehaviours.
    /// Use this if you want to manually handle instantiation and population.
    /// </summary>
    public static IList LoadAllData<TUnit>(string fileName)
        where TUnit : MonoBehaviour
    {
        return LoadDataList<TUnit>(fileName);
    }



    /// <summary>
    /// Loads all persisted data for the given unit type, instantiates new GameObjects,
    /// adds the TUnit component, and populates them using the persistor.
    /// Optionally, a factory can be provided to control instantiation.
    /// Returns a List of the instantiated and populated TUnit MonoBehaviours.
    /// </summary>
    public static List<TUnit> LoadAll<TUnit>(string fileName, Func<TUnit> factory = null)
    where TUnit : MonoBehaviour
    {
        var dataList = LoadDataList<TUnit>(fileName);
        var persistorType = FindPersistorType<TUnit>();
        var copyFromData = persistorType.GetMethod("CopyFromData", BindingFlags.Public | BindingFlags.Static);
        if (copyFromData == null)
            throw new InvalidOperationException($"CopyFromData not found on {persistorType.Name}");

        // Check for PersistorPrefabAttribute
        GameObject prefab = null;
        var attr = typeof(TUnit).GetCustomAttribute<PersistorPrefabAttribute>();
        if (attr != null)
        {
            prefab = PersistorPrefabLoader.LoadPrefab(attr.Reference);
            if (prefab == null)
                throw new InvalidOperationException($"Prefab '{attr.Reference}' could not be loaded for {typeof(TUnit).Name}");
        }

        var result = new List<TUnit>();
        foreach (var data in dataList)
        {
            TUnit unit;
            if (factory != null)
            {
                unit = factory();
            }
            else if (prefab != null)
            {
                var go = UnityEngine.Object.Instantiate(prefab);
                unit = go.GetComponent<TUnit>();
                if (unit == null)
                    throw new InvalidOperationException($"Prefab '{prefab.name}' does not have a component of type {typeof(TUnit).Name}");
            }
            else
            {
                throw new InvalidOperationException(
                    $"No factory or prefab provided for {typeof(TUnit).Name}. " +
                    "You must provide a factory method, a prefab parameter, or use a [PersistorPrefab] attribute.");
            }

            copyFromData.Invoke(null, new object[] { unit, data });
            result.Add(unit);
        }
        return result;
    }


    /// <summary>
    /// Loads all persisted data for the given unit type, instantiates the given prefab for each entry,
    /// and populates the TUnit component found on the root of the instantiated prefab.
    /// Returns a List of the instantiated and populated TUnit MonoBehaviours.
    /// </summary>
    public static List<TUnit> LoadAll<TUnit>(string fileName, GameObject prefab)
        where TUnit : UnityEngine.MonoBehaviour
    {
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab));

        // Pass a factory that instantiates the prefab and returns the TUnit component
        return LoadAll<TUnit>(fileName, () =>
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            var unit = go.GetComponent<TUnit>();
            if (unit == null)
                throw new InvalidOperationException($"Prefab root does not have a component of type {typeof(TUnit).Name}");
            return unit;
        });
    }

    // Private helper to load the data list from file
    private static IList LoadDataList<TUnit>(string fileName)
        where TUnit : MonoBehaviour
    {
        var dataType = FindDataType<TUnit>();
        var path = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(path))
            return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(dataType));

        var json = File.ReadAllText(path);
        var wrapperType = typeof(Wrapper<>).MakeGenericType(dataType);
        var wrapper = JsonUtility.FromJson(json, wrapperType);
        return (IList)wrapperType.GetField("items").GetValue(wrapper);
    }

    private static Type FindDataType<TUnit>()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t =>
                t.GetCustomAttributes(typeof(DataForAttribute), false)
                 .Cast<DataForAttribute>()
                 .Any(attr => attr.TargetType == typeof(TUnit)));
    }

    private static Type FindPersistorType<TUnit>()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t =>
                t.GetCustomAttributes(typeof(PersistorForAttribute), false)
                 .Cast<PersistorForAttribute>()
                 .Any(attr => attr.TargetType == typeof(TUnit)));
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public List<T> items;
    }
}
