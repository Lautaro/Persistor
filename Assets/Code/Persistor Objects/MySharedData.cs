using PersistorEngine.Internal;
using System;

[Serializable]
public class MySharedData : PersistorObject
{
    [Persist] public string myEmbeddedDataString;
}