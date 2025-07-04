using PersistorEngine;
using Sirenix.OdinInspector;
using UnityEngine;

[Adaptor(typeof(TransformAdaptor)),PersistorPrefab("CoolSphere Enemy")]
public class Enemy : PersistorMonoBehaviour
{
    [Persist] public string unitName = "";
    [Persist] public int health = 100;
    [Persist] public float speed = 5.0f;
    [Persist] public Vector3 myVectorThree;
    [Persist, HideLabel] public MyType myType = new ();
    [Persist, HideLabel] public MySharedData mySharedData= new ();
}
