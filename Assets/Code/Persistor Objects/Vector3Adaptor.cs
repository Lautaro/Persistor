using System;
using UnityEngine;

[Serializable]
public class Vector3Adaptor : IPersistor_Global_Field<Vector3>
{
    public float x, y, z;

    public void CopyToData(Vector3 v3)
    {
        x = v3.x;
        y = v3.y;
        z = v3.z;
    }
    public void CopyFromData(Vector3 v3)
    {
        v3.x = x;
        v3.y = y;
        v3.z = z;
    }
}