using PersistorEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

public class Girl : PersistorMonoBehaviour
{
    [Persist] public string GirlName;
    [Persist] public string Mood;

    private static readonly List<string> RandomNames = new List<string>
   {
       "Alice", "Sophia", "Emma", "Olivia", "Ava",
       "Isabella", "Mia", "Charlotte", "Amelia", "Harper",
       "Stella", "Sofia", "Madeleine", "Matilda", "Maria"
   };

    [Button("Create Random Girl")]
    public static void CreateRandomGirl()
    {
        var parentObject = GameObject.Find("Orphanage");
        if (parentObject == null)
        {
            Debug.LogError("Parent object 'Orphanage' not found.");
            return;
        }

        var newGirl = new GameObject().AddComponent<Girl>();
        newGirl.GirlName = RandomNames[Random.Range(0, RandomNames.Count)];
        newGirl.name = $"Girl_{newGirl.GirlName}";
        newGirl.transform.SetParent(parentObject.transform);
    }
}
