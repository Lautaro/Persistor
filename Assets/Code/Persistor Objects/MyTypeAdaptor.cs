using PersistorEngine.Internal;
using System;

[Serializable]
public class MyTypeAdaptor : IAdaptor<MyType>
{
    public int someValue;

    public void CopyToData(MyType obj)
    {
        someValue = obj.someValue;
    }

    public void CopyFromData(MyType obj)
    {
        obj.someValue = someValue;
    }
}