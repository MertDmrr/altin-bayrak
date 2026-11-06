using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>ESC (veya P) ile duraklatma menüsü: Devam / Yeniden Başla / Ana Menü.</summary>
public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    public static bool IsPaused { get; private set; }

    void Start()
    {
        IsPaused = false;
        if (pausePanel) pausePanel.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
        {
            if (IsPaused) Resume();
            else if (GameManager.Instance != null && GameManager.Instance.IsPlaying) Pause();
        }
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;          // fizik ve sayaç durur
        AudioListener.pause = true;
        if (pausePanel) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (pausePanel) pausePanel.SetActive(false);
    }

    public void RestartGame()
    {
        Resume();
        GameManager.Instance.Restart();
    }

    public void BackToMenu()
    {
        Resume();
        GameManager.Instance.GoToMenu();
    }

    void OnDestroy()
    {
        AudioListener.pause = false;
    }
}
