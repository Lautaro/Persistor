using System;
using UnityEngine;

[Serializable]
public class GameObjectNameAdaptor : IPersistor
{
    public string gameObjectName;
    public void CopyToData(MonoBehaviour adaptorTarget)
    {
        gameObjectName = adaptorTarget.name;  

    }
    public void CopyFromData(MonoBehaviour adaptorTarget)
    {
        adaptorTarget.name = gameObjectName;
    }
}
