using UnityEngine;
namespace PersistorEngine.Internal
{
    public interface IAdaptorField<T>
    {
        void CopyToData(T persistorTarget);
        void CopyFromData(ref T persistorTarget);
    }
}