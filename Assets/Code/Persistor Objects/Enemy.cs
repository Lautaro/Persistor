using Sirenix.OdinInspector;
using UnityEngine;

[Persistor(typeof(EnemyPersistor)), PersistorGeneratedFolder("Assets/Code/Persistor Objects/")]
public class Enemy : MonoBehaviour
{
    [PersistorId("GameUnit Enemy"), ReadOnly] public string gameUnitId;
    [Persist] public string unitName = "";
    [Persist] public int health = 100;
    [Persist] public float speed = 5.0f;
    [Persist] public Vector3 myVectorThree = new Vector3(0,0,0);
}
