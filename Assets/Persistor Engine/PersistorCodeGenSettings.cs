using UnityEngine;
using UnityEditor;
namespace PersistorEngine.Internal
{
    [CreateAssetMenu(fileName = "PersistorSettings", menuName = "Persistor/Settings", order = 0)]
    public class PersistorSettings : ScriptableObject
    {
        public string defaultGeneratedCodeFolder = "Code/Persistors";
        public bool useTypeSubfolders = true;
        public string typeSubfolderSuffix = " Generated Code";
        public string defaultPrefabFolder = "Persistor Prefabs";
        public string dataClassSuffix = "__Data";
        public string presetClassSuffix = "_Preset";

        private const string assetPath = "Assets/Persistor Engine/Editor/PersistorSettings.asset";

        public static PersistorSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PersistorSettings>(assetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PersistorSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }
    }
}