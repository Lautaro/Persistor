using PersistorEngine.Internal;
using System;
using UnityEngine;


[Serializable]
public class EnemyPersistor : IPersistor<Enemy>
{
    public float[] position = new float[3];
    public float[] rotation = new float[3];
    public float[] scale = new float[3];
    public void CopyToData(Enemy enemy)
    {   
        position[0] = enemy.transform.position.x;
        position[1] = enemy.transform.position.y;
        position[2] = enemy.transform.position.z;
     
        rotation[0] = enemy.transform.rotation.eulerAngles.x;
        rotation[1] = enemy.transform.rotation.eulerAngles.y;
        rotation[2] = enemy.transform.rotation.eulerAngles.z;
        
        scale[0] = enemy.transform.localScale.x;
        scale[1] = enemy.transform.localScale.y;
        scale[2] = enemy.transform.localScale.z;
    }
    public void CopyFromData(Enemy enemy)
    {   
        enemy.transform.position = new Vector3(position[0], position[1], position[2]);
        enemy.transform.rotation = Quaternion.Euler(rotation[0], rotation[1], rotation[2]);
        enemy.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);
    }
}
