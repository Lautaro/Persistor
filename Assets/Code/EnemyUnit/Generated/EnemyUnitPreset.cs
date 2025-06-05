using UnityEngine;
[CreateAssetMenu(menuName = "GameUnit/EnemyUnitPreset")]
public partial class EnemyUnitPreset : ScriptableObject
{
    public string gameUnitId;
    public string unitName;
    public int health;
    public float speed;
    public Vector3 spawnPosition;
}
