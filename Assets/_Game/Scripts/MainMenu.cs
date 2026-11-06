using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Ana menü: Başla, Nasıl Oynanır, Çıkış.</summary>
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "Oyun";
    public GameObject howToPanel;
    public GameObject quitButton;

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (howToPanel) howToPanel.SetActive(false);
#if UNITY_WEBGL
        // Tarayıcıda oyundan çıkılamadığı için Çıkış butonu gizlenir
        if (quitButton) quitButton.SetActive(false);
#endif
    }

    public void PlayGame() { SceneManager.LoadScene(gameSceneName); }
    public void OpenHowTo() { if (howToPanel) howToPanel.SetActive(true); }
    public void CloseHowTo() { if (howToPanel) howToPanel.SetActive(false); }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
