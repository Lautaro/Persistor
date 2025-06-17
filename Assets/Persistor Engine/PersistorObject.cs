using System;
namespace PersistorEngine.Internal
{
    /// <summary>
    /// Base class for all persistable objects that needs an id to be save and loaded by reference. TO DO: when creating PersistorObjects the persistorId should be automatically set to a unique value.
    /// </summary>
    [Serializable]
    public class PersistorObject : IPersistorId
    {
        public string persistorId { get; set; }

        public PersistorObject()
        {
            persistorId = Guid.NewGuid().ToString();
            PersistorRegistry.Register(this);
        }
    }
}