using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Ana menü: Turnuva (60 sn), Serbest Dolaşma, Blender Modellerim, Nasıl Oynanır, Çıkış ve en iyi süre.</summary>
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "Oyun";
    public GameObject howToPanel;
    public GameObject quitButton;
    [Tooltip("Tarayıcı (WebGL) sürümünde Çıkış'a basınca açılan bilgi paneli")]
    public GameObject quitPanel;
    public TMP_Text bestTimeText;

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (howToPanel) howToPanel.SetActive(false);
        if (quitPanel) quitPanel.SetActive(false);
        if (quitButton) quitButton.SetActive(true);   // Çıkış her sürümde görünür
        if (bestTimeText)
        {
            float best = PlayerPrefs.GetFloat(OyunModu.EnIyiSureKey, 0f);
            bestTimeText.text = best > 0f ? "En iyi süre: " + best.ToString("0.0") + " sn" : "En iyi süre: henüz yok";
        }
    }

    /// <summary>Turnuva modu: 60 saniyede 10 top (ödevin asıl modu).</summary>
    public void PlayGame()
    {
        OyunModu.SerbestDolasma = false;
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>Serbest dolaşma: süre yok, saha rahatça gezilir.</summary>
    public void PlayFreeRoam()
    {
        OyunModu.SerbestDolasma = true;
        SceneManager.LoadScene(gameSceneName);
    }
    public void OpenHowTo() { if (howToPanel) howToPanel.SetActive(true); }
    public void CloseHowTo() { if (howToPanel) howToPanel.SetActive(false); }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        // Tarayıcıda oyun kapatılamaz: veda paneli gösterilir, sekme kapatılarak çıkılır
        if (quitPanel) quitPanel.SetActive(true);
#else
        Application.Quit();
#endif
    }

    public void CloseQuitPanel() { if (quitPanel) quitPanel.SetActive(false); }
}
