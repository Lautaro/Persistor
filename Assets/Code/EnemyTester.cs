using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public class EnemyTester : MonoBehaviour
{
    public Enemy_Preset preset;

    public GameObject enemyPrefab;

    public int createNewCount = 3;
    private List<Enemy> spawnedUnits = new List<Enemy>();

    [Button]
    public void CreateNew()
    {
        ClearUnits();
        for (int i = 0; i < createNewCount; i++)
        {
            var enemy = Persistor.Create<Enemy>(enemyPrefab, preset, "enemy_" + i);
            enemy.transform.position = Random.insideUnitSphere * 5f;
            spawnedUnits.Add(enemy);
        }
        Debug.Log($"Spawned {spawnedUnits.Count} EnemyUnits.");
    }

    private Enemy Spawn(string name = "")
    {
        var go = new GameObject($"Enemy_{name}");

        go.transform.position = Random.insideUnitSphere * 5f;
        var unit = go.GetComponent<Enemy>() ?? go.AddComponent<Enemy>();

        unit.unitName = name;
        AddSphere(go);
        return unit;
    }

    [Button]
    public void SaveAll()
    {
        Persistor.SaveAll(spawnedUnits, "EnemyUnits.json");
        Debug.Log("Saved all EnemyUnits.");
    }

    [Button]
    public void LoadWithPrefab()
    {
        ClearUnits();
        spawnedUnits = Persistor.LoadAll<Enemy>("EnemyUnits.json", enemyPrefab);
    }

    [Button]
    public void LoadWithPrefabParameter()
    {
        ClearUnits();
        spawnedUnits = Persistor.LoadAll<Enemy>("EnemyUnits.json");
    }

    [Button]
    public void LoadWithCallback()
    {
        ClearUnits();
        spawnedUnits = Persistor.LoadAll<Enemy>("EnemyUnits.json",
        () =>
        {
            return Spawn();
        });
    }

    [Button]
    public void Load()
    {
        ClearUnits();
        spawnedUnits = Persistor.LoadAll<Enemy>("EnemyUnits.json");

        foreach (var unit in spawnedUnits)
        {
            AddSphere(unit.gameObject);
        }

        Debug.Log($"Loaded {spawnedUnits.Count} EnemyUnits.");
    }

    void AddSphere(GameObject parent)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(parent.transform);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * 0.5f;
    }

    private void ClearUnits()
    {
        foreach (var unit in spawnedUnits)
        {
            if (unit != null)
                Destroy(unit.gameObject);
        }
        spawnedUnits.Clear();
    }
}
