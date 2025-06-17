using UnityEngine;
namespace PersistorEngine.Internal
{
    public interface IPersistor<T>
    {
        void CopyToData(T persistorTarget);
        void CopyFromData(T persistorTarget);
    }
}