using UnityEditor;
using UnityEngine;
using System.Diagnostics;

public class GameUnitDashboardWindow : EditorWindow
{
    [MenuItem("Tools/GameUnit Dashboard")]
    public static void ShowWindow()
    {
        GetWindow<GameUnitDashboardWindow>("GameUnit Dashboard");
    }

    private void OnGUI()
    {
        GUILayout.Label("GameUnit Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Open Save Folder"))
        {
            OpenSaveFolder();
        }
    }

    private void OpenSaveFolder()
    {
        // Change this path to your actual save folder
        string saveFolder = Application.dataPath + "/../Saves";
        if (!System.IO.Directory.Exists(saveFolder))
        {
            EditorUtility.DisplayDialog("Folder Not Found", $"Save folder not found:\n{saveFolder}", "OK");
            return;
        }
        Process.Start(saveFolder);
    }
}
