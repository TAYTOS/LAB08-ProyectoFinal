using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Carga la escena por nombre
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // Carga la escena Demo_Scene específica
    public void LoadDemoScene()
    {
        // Cambiar este nombre si el nombre real de la escena es distinto
        SceneManager.LoadScene("GameScene");
    }

    // Cierra la aplicación
    public void QuitGame()
    {
        Debug.Log("Quit Game...");
        Application.Quit();
    }
}
