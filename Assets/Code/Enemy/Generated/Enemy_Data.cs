
[DataFor(typeof(Enemy))]
[System.Serializable]
public partial class Enemy_Data
{
    public string gameUnitId;
    public string unitName;
    public int health;
    public float speed;
    public TransformAdaptor transformAdaptor;
    public GameObjectNameAdaptor gameObjectNameAdaptor;
}