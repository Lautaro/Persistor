using UnityEngine;

[Persistor(typeof(TransformAdaptor), typeof(GameObjectNameAdaptor))]
public class Enemy : MonoBehaviour
{
    [PersistorId("GameUnit Enemy")] public string gameUnitId;
    [Persist] public string unitName = "";
    [Persist] public int health = 100;
    [Persist] public float speed = 5.0f;
}
