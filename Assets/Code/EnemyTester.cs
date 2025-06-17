using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using PersistorEngine;

public class EnemyTester : MonoBehaviour
{
    public Enemy_Preset preset;
    public int createNewCount = 3;
    private List<Enemy> spawnedUnits = new List<Enemy>();

    [Button]
    public void CreateNew()
    {
        ClearUnits();
        for (int i = 0; i < createNewCount; i++)
        {
            var enemy = Persistor.Create<Enemy>(null, preset, "enemy_" + i);
            enemy.transform.position = Random.insideUnitSphere * 5f;
            spawnedUnits.Add(enemy);
        }
        Debug.Log($"Spawned {spawnedUnits.Count} EnemyUnits.");
    }

    [Button]
    public void SaveAll()
    {
        Persistor.SaveAll("Stuff");
        Debug.Log("Saved all EnemyUnits.");
    }

    [Button]
    public void Load()
    {
        ClearUnits();
        Persistor.LoadAll("Stuff");
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
