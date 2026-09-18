using UnityEngine;
using UnityEngine.SceneManagement;

public class WinSceneController : MonoBehaviour
{
    private void Start()
    {
        // Ensure cursor is visible and free to click UI buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1.0f;
    }

    public void PlayAgain()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void MainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Debug.Log("[WinScene] Quitting game...");
        Application.Quit();
    }
}
