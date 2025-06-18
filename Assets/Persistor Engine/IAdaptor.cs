using UnityEngine;
namespace PersistorEngine.Internal
{
    public interface IAdaptor<T>
    {
        void CopyToData(T adaptorTarget);
        void CopyFromData(T adaptorTarget);
    }
}