using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using PersistorEngine;

public class PersistorListTest : PersistorMonoBehaviour
{
    [Persist, ShowInInspector]
    public List<int> Values { get; set; } = new();

    [Button("Save List (Persistor)")]
    public void Save()
    {
        // Save this instance using your Persistor system
        Persistor.SaveAll("LISTY");
    }

    [Button("Load List (Persistor)")]
    public void Load()
    {
        // Load into this instance using your Persistor system
        // If your API returns a new instance, you may need to copy values over
        Persistor.LoadAll("LISTY");
    }
}
