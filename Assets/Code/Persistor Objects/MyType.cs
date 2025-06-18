using PersistorEngine.Internal;
using System;

[Adaptor(typeof(MyTypeAdaptor)), Serializable]
public class MyType : PersistorObject
{
    [Persist] public int someValue;
}
