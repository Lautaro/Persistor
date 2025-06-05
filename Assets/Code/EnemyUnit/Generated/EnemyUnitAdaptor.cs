using UnityEngine;
public static partial class EnemyUnitPersistor
{
    public static void CopyToData(EnemyUnit unit, EnemyUnit_Data data)
    {
        GameUnitIdAssigner.AssignIdIfNeeded(unit);
        data.gameUnitId = unit.gameUnitId;
        data.unitName = unit.unitName;
        data.health = unit.health;
        data.speed = unit.speed;
        data.spawnPosition = unit.spawnPosition;
        if (data.gameUnitTransformAdaptor != null) data.gameUnitTransformAdaptor.CopyToData(unit);
        if (data.spawnPositionAdaptor != null) data.spawnPositionAdaptor.CopyToData(unit.spawnPosition);
    }

    public static void CopyFromData(EnemyUnit unit, EnemyUnit_Data data)
    {
        unit.gameUnitId = data.gameUnitId;
        unit.unitName = data.unitName;
        unit.health = data.health;
        unit.speed = data.speed;
        unit.spawnPosition = data.spawnPosition;
        if (data.gameUnitTransformAdaptor != null) data.gameUnitTransformAdaptor.CopyFromData(unit);
        if (data.spawnPositionAdaptor != null) data.spawnPositionAdaptor.CopyFromData(unit.spawnPosition);
    }

    public static void CopyFromPreset(EnemyUnit unit, EnemyUnitPreset preset)
    {
        unit.gameUnitId = preset.gameUnitId;
        unit.unitName = preset.unitName;
        unit.health = preset.health;
        unit.speed = preset.speed;
        unit.spawnPosition = preset.spawnPosition;
    }
}