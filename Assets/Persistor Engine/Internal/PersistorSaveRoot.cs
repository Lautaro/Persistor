using System.Collections.Generic;
namespace PersistorEngine.Internal
{
    [System.Serializable]
    public class PersistorSaveRoot
    {
        public List<TypeSection> sections = new();

        [System.Serializable]
        public class TypeSection
        {
            public string typeName;
            public List<string> jsonObjects = new(); // Each entry is a JSON string for a single object
        }
    }
}