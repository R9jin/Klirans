using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public static class InspectRooms
{
    static InspectRooms()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        string outPath = Path.Combine(Application.dataPath, "EditorOutput.txt");
        File.WriteAllText(outPath, "Hello from Unity Editor! Scene: " + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name);
    }
}
