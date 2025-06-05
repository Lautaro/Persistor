using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class GameUnitPersistence
{
    public static void SaveAll<TUnit, TData>(IEnumerable<TUnit> units, System.Func<TUnit, TData> toData, string fileName)
    {
        var dataList = new List<TData>();
        foreach (var unit in units)
            dataList.Add(toData(unit));

        var json = JsonUtility.ToJson(new Wrapper<TData> { items = dataList }, true);
        var path = Path.Combine(Application.persistentDataPath, fileName);
        Debug.Log($"Saving units to {path}");
        File.WriteAllText(path, json);
        Debug.Log($"Saved {dataList.Count} units to {fileName}");
    }

    public static List<TData> LoadAll<TData>(string fileName)
    {
        var path = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(path))
            return new List<TData>();

        var json = File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<Wrapper<TData>>(json);
        return wrapper?.items ?? new List<TData>();
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public List<T> items;
    }
}
