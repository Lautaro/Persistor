using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Unity.VisualScripting;

public class EnemyUnitTester : MonoBehaviour
{
    [Tooltip("Optional: If set, new units will use this preset.")]
    public EnemyUnitPreset preset;

    public int spawnCount = 3;
    private List<EnemyUnit> spawnedUnits = new List<EnemyUnit>();

    [Button]
    public void SpawnUnits()
    {
        ClearUnits();
        for (int i = 0; i < spawnCount; i++)
        {
            var go = new GameObject($"Enemy_{i}");
            go.transform.position = Random.insideUnitSphere * 5f;
            var unit = go.AddComponent<EnemyUnit>();

            if (preset != null)
            {
                EnemyUnitPersistor.CopyFromPreset(unit, preset);
            }
            else
            {
                unit.unitName = $"Enemy_{i}";
                unit.health = Random.Range(50, 150);
                unit.speed = Random.Range(2f, 8f);
            }
            AddSphere(go);
            spawnedUnits.Add(unit);
        }
        Debug.Log($"Spawned {spawnedUnits.Count} EnemyUnits.");
    }

    [Button]
    public void RandomizeHealth()
    {
        foreach (var unit in spawnedUnits)
        {
            unit.health = Random.Range(10, 200);
        }
        Debug.Log("Randomized health for all spawned units.");
    }

    [Button]
    public void SaveAll()
    {
        GameUnitPersistence.SaveAll<EnemyUnit, EnemyUnit_Data>(
            spawnedUnits,
            unit =>
            {
                var data = new EnemyUnit_Data();
                // Ensure the adaptor is present
                data.gameUnitTransformAdaptor = new GameUnitTransformAdaptor();
                EnemyUnitPersistor.CopyToData(unit, data);
                return data;
            },
            "EnemyUnits.json"
        );
    }

    [Button]
    public void LoadAll()
    {
        var loadedData = GameUnitPersistence.LoadAll<EnemyUnit_Data>("EnemyUnits.json");
        ClearUnits();
        for (int i = 0; i < loadedData.Count; i++)
        {
            EnemyUnit_Data data = loadedData[i];
            var go = new GameObject($"Loaded_{i}");
            AddSphere(go);
            // The transform will be set by the adaptor during CopyFromData
            var unit = go.AddComponent<EnemyUnit>();
            EnemyUnitPersistor.CopyFromData(unit, data);

            spawnedUnits.Add(unit);
        }
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

    [Button]
    public void LogUnitPositions()
    {
        foreach (var unit in spawnedUnits)
        {
            if (unit != null)
            {
                Vector3 position = unit.transform.position;
                Debug.Log($"Unit {unit.gameObject.name} Position - X: {position.x}, Y: {position.y}");
            }
        }
    }
}
