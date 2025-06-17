using PersistorEngine.Internal;
using System;
using UnityEngine;

[Serializable]
public class Vector3Persistor : IPersistorField<Vector3>
{
    public float x, y, z;

    public void CopyToData(Vector3 v3)
    {
        x = v3.x;
        y = v3.y;
        z = v3.z;
    }
    public void CopyFromData(ref Vector3 v3)
    {
        v3.x = x;
        v3.y = y;
        v3.z = z;
    }
}