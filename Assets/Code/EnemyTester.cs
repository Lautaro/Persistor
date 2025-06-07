using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Unity.VisualScripting;

public class EnemyTester : MonoBehaviour
{
    public EnemyPreset preset;

    public GameObject enemyPrefab;

    public int spawnCount = 3;
    private List<Enemy> spawnedUnits = new List<Enemy>();

    [Button]
    public void SpawnUnits()
    {
        ClearUnits();
        for (int i = 0; i < spawnCount; i++)
        {
            Enemy unit = Spawn("enemy_" + i);
            spawnedUnits.Add(unit);
        }
        Debug.Log($"Spawned {spawnedUnits.Count} EnemyUnits.");
    }

    private Enemy Spawn(string name = "")
    {
        var go = enemyPrefab != null
            ? Instantiate(enemyPrefab)
            : new GameObject($"Enemy_{name}");

        go.transform.position = Random.insideUnitSphere * 5f;
        var unit = go.GetComponent<Enemy>() ?? go.AddComponent<Enemy>();

        if (preset != null)
        {
            EnemyPersistor.CopyFromPreset(unit, preset);
        }
        else
        {
            unit.unitName = $"Enemy_{name}";
            unit.health = Random.Range(50, 150);
            unit.speed = Random.Range(2f, 8f);
        }
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

        // Optionally, add spheres to loaded units
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
