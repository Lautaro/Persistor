
using UnityEngine;

[CreateAssetMenu(menuName = "Persistor Presets/EnemyPreset")]
public partial class EnemyPreset : ScriptableObject
{
    public string gameUnitId;
    public string unitName;
    public int health;
    public float speed;
}
