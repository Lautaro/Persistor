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
#if ENABLE_ADDRESSABLES
            // 1. Try Addressables (if enabled)
            if (Addressables.ResourceLocators.Any(l => l.Locate(reference, typeof(GameObject), out var _)))
            {
                if (onComplete != null)
                {
                    var handle = Addressables.LoadAssetAsync<GameObject>(reference);
                    handle.Completed += op =>
                    {
                        if (op.Status == AsyncOperationStatus.Succeeded)
                            onComplete(op.Result);
                        else
                            throw new InvalidOperationException($"Addressable prefab not found: {reference}");
                    };
                    return null;
                }
                else
                {
                    var handle = Addressables.LoadAssetAsync<GameObject>(reference);
                    handle.WaitForCompletion();
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                        return handle.Result;
                    throw new InvalidOperationException($"Addressable prefab not found: {reference}");
                }
            }
#endif

            // 2. If it looks like a path (contains '/'), treat as Resources path
            if (reference.Contains("/"))
            {
                var prefab = Resources.Load<GameObject>(reference);
                if (prefab != null)
                {
                    onComplete?.Invoke(prefab);
                    return prefab;
                }
            }
            else
            {
                // 3. Otherwise, treat as prefab name in "Persistor Prefabs"
                var prefab = Resources.Load<GameObject>($"Persistor Prefabs/{reference}");
                if (prefab != null)
                {
                    onComplete?.Invoke(prefab);
                    return prefab;
                }
            }

            throw new InvalidOperationException($"Could not find prefab '{reference}' as an Addressable, Resources path, or in 'Persistor Prefabs'.");
        }
    }
}
