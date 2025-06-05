using System;
using UnityEngine;

[Serializable]
public class GameUnitTransformAdaptor : IGameUnitAdaptor<MonoBehaviour>
{
    public float[] position = new float[3];
    public float[] rotation = new float[3];
    public float[] scale = new float[3];
    public void CopyToData(MonoBehaviour gameUnit)
    {
        position[0] = gameUnit.transform.position.x;
        position[1] = gameUnit.transform.position.y;
        position[2] = gameUnit.transform.position.z;
     
        rotation[0] = gameUnit.transform.rotation.eulerAngles.x;
        rotation[1] = gameUnit.transform.rotation.eulerAngles.y;
        rotation[2] = gameUnit.transform.rotation.eulerAngles.z;
        
        scale[0] = gameUnit.transform.localScale.x;
        scale[1] = gameUnit.transform.localScale.y;
        scale[2] = gameUnit.transform.localScale.z;
    }
    public void CopyFromData(MonoBehaviour gameUnit)
    {
        gameUnit.transform.position = new Vector3(position[0], position[1], position[2]);
        gameUnit.transform.rotation = Quaternion.Euler(rotation[0], rotation[1], rotation[2]);
        gameUnit.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);
    }
}
