using UnityEngine;


[GameUnitAdaptors(typeof(GameUnitTransformAdaptor))]
public class EnemyUnit : MonoBehaviour
{
    [GameUnitId("GameUnit Enemy")] public string gameUnitId;
    [GameUnitData] public string unitName = "";
    [GameUnitData] public int health = 100;
    [GameUnitData] public float speed = 5.0f;
    [GameUnitData] public Vector3 spawnPosition;

    // Game logic and methods can be added here
    void Start()
    {
   
    }
}
