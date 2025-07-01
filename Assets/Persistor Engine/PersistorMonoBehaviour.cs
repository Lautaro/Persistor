using PersistorEngine.Internal;
using UnityEngine;
namespace PersistorEngine
{
    public class PersistorMonoBehaviour : MonoBehaviour, IPersistorId
    {
        [SerializeField]
        private string _persistorId;

        public string persistorId
        {
            get => _persistorId;
            set => _persistorId = value;
        }

        public virtual void Awake()
        {
            PersistorRegistry.Register(this);
        }

        public virtual void OnDestroy()
        {
            PersistorRegistry.Unregister(this);
        }
    }
}