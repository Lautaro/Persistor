
[PersistorFor(typeof(Enemy))]
public static partial class EnemyPersistor
{
    public static void CopyToData(Enemy unit, Enemy_Data data)
    {
        PersistorIdAssigner.AssignIdIfNeeded(unit);
        data.gameUnitId = unit.gameUnitId;
        data.unitName = unit.unitName;
        data.health = unit.health;
        data.speed = unit.speed;
        if (data.transformAdaptor != null) data.transformAdaptor.CopyToData(unit);
        if (data.gameObjectNameAdaptor != null) data.gameObjectNameAdaptor.CopyToData(unit);
        
    }

    public static void CopyFromData(Enemy unit, Enemy_Data data)
    {
        unit.gameUnitId = data.gameUnitId;
        unit.unitName = data.unitName;
        unit.health = data.health;
        unit.speed = data.speed;
        if (data.transformAdaptor != null) data.transformAdaptor.CopyFromData(unit);
        if (data.gameObjectNameAdaptor != null) data.gameObjectNameAdaptor.CopyFromData(unit);
        
    }

    public static void CopyFromPreset(Enemy unit, EnemyPreset preset)
    {
        unit.gameUnitId = preset.gameUnitId;
        unit.unitName = preset.unitName;
        unit.health = preset.health;
        unit.speed = preset.speed;
    }
}