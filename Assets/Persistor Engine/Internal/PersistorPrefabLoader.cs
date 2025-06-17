using System;
using UnityEngine;
#if ENABLE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace PersistorEngine.Internal
{
    public static class PersistorPrefabLoader
    {
        public static GameObject LoadPrefab(string reference, Action<GameObject> onComplete = null)
        {
            // 1. Try "Persistor Prefabs" Resources folder
            var prefab = Resources.Load<GameObject>($"Persistor Prefabs/{reference}");
            if (prefab != null)
            {
                onComplete?.Invoke(prefab);
                return prefab;
            }

#if ENABLE_ADDRESSABLES
        // 2. Try Addressables
        if (onComplete != null)
        {
            // Async loading
            var handle = Addressables.LoadAssetAsync<GameObject>(reference);
            handle.Completed += op =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                    onComplete(op.Result);
                else
                    throw new InvalidOperationException($"Addressable prefab not found: {reference}");
            };
            return null; // Indicate async, result will be delivered via callback
        }
        else
        {
            // Sync loading (not recommended for large assets, but possible)
            var handle = Addressables.LoadAssetAsync<GameObject>(reference);
            handle.WaitForCompletion();
            if (handle.Status == AsyncOperationStatus.Succeeded)
                return handle.Result;
            throw new InvalidOperationException($"Addressable prefab not found: {reference}");
        }
#endif

            // 3. Try direct Resources path
            prefab = Resources.Load<GameObject>(reference);
            if (prefab != null)
            {
                onComplete?.Invoke(prefab);
                return prefab;
            }

            throw new InvalidOperationException($"Could not find prefab '{reference}' in 'Persistor Prefabs' Resources folder, as an Addressable, or as a Resources path.");
        }
    }
}
