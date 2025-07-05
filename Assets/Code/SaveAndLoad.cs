using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using PersistorEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SaveAndLoad : MonoBehaviour
{
    public string SaveName = "";

    private static SaveAndLoad instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [Button]
    public void SaveAll()
    {
        if (string.IsNullOrEmpty(SaveName))
        {
            Debug.LogError("SaveName is not set. Please provide a valid SaveName.");
            return;
        }
        Persistor.SaveAll(SaveName);
        Debug.Log("Saved all EnemyUnits.");
    }

    [Button]
    public void Load()
    {
        if (string.IsNullOrEmpty(SaveName))
        {
            Debug.LogError("SaveName is not set. Please provide a valid SaveName.");
            return;
        }
        StartCoroutine(ReloadRoutine(SaveName));
    }

    private IEnumerator ReloadRoutine(string saveName)
    {
        Scene currentScene = SceneManager.GetActiveScene();
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(currentScene.name);

        // Optionally allow the scene activation to wait
        // loadOp.allowSceneActivation = false;

        while (!loadOp.isDone)
        {
            yield return null;
        }

        Persistor.LoadAll(saveName);
    }
}
