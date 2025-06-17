using PersistorEngine.Internal;
using UnityEditor;
using UnityEngine;

public class PersistorDashboard : EditorWindow
{
    [MenuItem("Tools/Persistor Dashboard")]
    public static void ShowWindow()
    {
        GetWindow<PersistorDashboard>("Persistor Dashboard");
    }

    private void OnGUI()
    {
        GUILayout.Label("Persistor Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Open Save Folder"))
        {
            OpenSaveFolder();
        }

        if (GUILayout.Button("Open Code Generation Settings"))
        {
            Selection.activeObject = PersistorSettings.GetOrCreateSettings();
        }
    }

    private void OpenSaveFolder()
    {
        string saveFolder = Application.dataPath + "/../Saves";
        if (!System.IO.Directory.Exists(saveFolder))
        {
            EditorUtility.DisplayDialog("Folder Not Found", $"Save folder not found:\n{saveFolder}", "OK");
            return;
        }
        System.Diagnostics.Process.Start(saveFolder);
    }
}
