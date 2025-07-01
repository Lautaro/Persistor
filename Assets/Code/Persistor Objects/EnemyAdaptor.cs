using PersistorEngine.Internal;
using System;
using UnityEngine;


[Serializable]
public class TransformAdaptor : IMonoBehaviourAdaptor
{
    public float[] position = new float[3];
    public float[] rotation = new float[3];
    public float[] scale = new float[3];
    public void CopyToData(MonoBehaviour mb)
    {   
        position[0] = mb.transform.position.x;
        position[1] = mb.transform.position.y;
        position[2] = mb.transform.position.z;
     
        rotation[0] = mb.transform.rotation.eulerAngles.x;
        rotation[1] = mb.transform.rotation.eulerAngles.y;
        rotation[2] = mb.transform.rotation.eulerAngles.z;
        
        scale[0] = mb.transform.localScale.x;
        scale[1] = mb.transform.localScale.y;
        scale[2] = mb.transform.localScale.z;
    }
    public void CopyFromData(MonoBehaviour mb)
    {   
        mb.transform.position = new Vector3(position[0], position[1], position[2]);
        mb.transform.rotation = Quaternion.Euler(rotation[0], rotation[1], rotation[2]);
        mb.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);
    }
}
