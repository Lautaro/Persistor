using UnityEngine;
[System.Serializable]
public partial class EnemyUnit_Data
{
    public string gameUnitId;
    public string unitName;
    public int health;
    public float speed;
    public Vector3 spawnPosition;
    public GameUnitTransformAdaptor gameUnitTransformAdaptor;
    public GameUnitVector3Adaptor spawnPositionAdaptor;
}