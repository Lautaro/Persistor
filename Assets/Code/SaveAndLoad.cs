using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using PersistorEngine;

public class SaveAndLoad : MonoBehaviour
{
    public string SaveName = "";

    [Button]
    public void SaveAll()
    {
        if (string.IsNullOrEmpty(SaveName))
        {
            Debug.LogError("SaveName is not set. Please provide a valid SaveName.");
            return;
        }
        Persistor.SaveAll(SaveName);
        Debug.Log("Saved all EnemyUnits.");
    }

    [Button]
    public void Load()
    {
        if (string.IsNullOrEmpty(SaveName))
        {
            Debug.LogError("SaveName is not set. Please provide a valid SaveName.");
            return;
        }
        Persistor.LoadAll(SaveName);
    }
}
