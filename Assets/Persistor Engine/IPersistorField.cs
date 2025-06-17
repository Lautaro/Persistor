using UnityEngine;
namespace PersistorEngine.Internal
{
    public interface IPersistorField<T>
    {
        void CopyToData(T persistorTarget);
        void CopyFromData(ref T persistorTarget);
    }
}