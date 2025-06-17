using System;
using System.Collections.Generic;
namespace PersistorEngine.Internal
{
    public static class PersistorRegistry
    {
        private static readonly Dictionary<string, IPersistorId> _dict = new();
        private static readonly Dictionary<Type, string> _prefixCache = new();

        public static void Register(IPersistorId obj)
        {
            if (obj == null) return;

            // Assign persistorId if not set
            if (string.IsNullOrEmpty(obj.persistorId))
            {
                var type = obj.GetType();
                if (!_prefixCache.TryGetValue(type, out var prefix))
                {
                    // In the future, check for a custom attribute here
                    prefix = type.Name;
                    _prefixCache[type] = prefix;
                }
                obj.persistorId = $"{prefix}_{Guid.NewGuid()}";
            }

            _dict[obj.persistorId] = obj;
        }

        public static void Unregister(IPersistorId obj)
        {
            if (obj != null && !string.IsNullOrEmpty(obj.persistorId))
                _dict.Remove(obj.persistorId);
        }

        public static T Resolve<T>(string id) where T : class, IPersistorId
        {
            _dict.TryGetValue(id, out var obj);
            return obj as T;
        }
        public static void UpdatePersistorId(IPersistorId obj, string newId)
        {
            if (obj == null) return;
            // Remove old key if present
            var oldId = obj.persistorId;
            if (!string.IsNullOrEmpty(oldId) && _dict.ContainsKey(oldId))
                _dict.Remove(oldId);

            obj.persistorId = newId;
            if (!string.IsNullOrEmpty(newId))
                _dict[newId] = obj;
        }
        public static IEnumerable<IPersistorId> GetAll()
        {
            return _dict.Values;
        }

        public static void Clear()
        {
            _dict.Clear();
        }
    }
}