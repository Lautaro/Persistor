using PersistorEngine.Internal;
using System;

[Persistor(typeof(MyTypePersistor)), Serializable]
public class MyType : PersistorObject
{
    [Persist] public int someValue;
}
