using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
            Cleanup();
            return _dict.Values;
        }

        public static void Clear()
        {
            _dict.Clear();
        }

        public static void Cleanup()
        {
            var keysToRemove = _dict
                .Where(kvp => kvp.Value == null || (kvp.Value is UnityEngine.Object unityObj && unityObj == null))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
                _dict.Remove(key);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void RegisterAllPersistorObjectsToCatchDisabledOnes()
        {
            // THIS IS NEEDED AT STARTUP BECAUSE DISABLED MONOBEHAVIOURS WONT AUTO REGISTER UNTIL ENABLED
            // Register all PersistorMonoBehaviour objects, including disabled ones
            var allPMBs = UnityEngine.Object.FindObjectsByType<PersistorMonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pmb in allPMBs)
            {
                PersistorRegistry.Register(pmb);
            }

            //// If you have a PersistorAnchor type, register those as well
            //var allAnchors = UnityEngine.Object.FindObjectsByType<PersistorAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            //foreach (var anchor in allAnchors)
            //{
            //    PersistorRegistry.Register(anchor);
            //}
        }
    }
}