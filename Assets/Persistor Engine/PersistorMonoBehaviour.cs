using UnityEngine;
namespace PersistorEngine.Internal
{
    public class PersistorMonoBehaviour : MonoBehaviour, IPersistorId
    {
        public string persistorId { get; set; }

        protected virtual void Awake()
        {
            PersistorRegistry.Register(this);
        }

        protected virtual void OnDestroy()
        {
            PersistorRegistry.Unregister(this);
        }
    }
}