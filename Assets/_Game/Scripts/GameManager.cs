using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Oyun döngüsü (Game Loop): 60 saniyelik geri sayım, skor (x/10),
/// kazanma ("Kazandınız") ve kaybetme ("Süre Bitti") panelleri.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Kurallar (ödev yönergesi)")]
    public float gameDuration = 60f;   // 1 dakika
    public int totalBalls = 10;        // 10 küre
    public string menuSceneName = "AnaMenu";

    [Header("Referanslar")]
    public PlayerController player;

    [Header("Arayüz")]
    public TMP_Text timerText;
    public TMP_Text scoreText;
    public TMP_Text messageText;
    public GameObject winPanel;
    public TMP_Text winDetailText;
    public GameObject losePanel;
    public TMP_Text loseDetailText;
    public Color timerNormalColor = Color.white;
    public Color timerDangerColor = new Color(1f, 0.32f, 0.32f);

    [Header("Ses")]
    public AudioSource sfx;
    public AudioSource music;
    public AudioClip startClip, scoreClip, tickClip, winClip, alarmClip;

    public float TimeLeft { get; private set; }
    public int Scored { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsOver { get; private set; }
    public bool Won { get; private set; }

    int lastTickSecond = -1;
    Coroutine messageRoutine;
    Vector3 timerBaseScale = Vector3.one;

    static readonly string[] praise = { "Süper!", "Harika!", "Bir top daha!", "Yuvaya oturdu!", "Mükemmel!" };

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        // Oyuncu "Başla"ya bastığı an bu sahne açılır ve sayaç başlar
        TimeLeft = gameDuration;
        Scored = 0;
        IsPlaying = true;
        if (winPanel) winPanel.SetActive(false);
        if (losePanel) losePanel.SetActive(false);
        if (timerText) timerBaseScale = timerText.transform.localScale;
        UpdateUI();
        Play(startClip);
        ShowMessage("Uyanmadan önce 10 topu yuvalarına yuvarla!", 2.5f);
    }

    void Update()
    {
        if (!IsPlaying) return;

        TimeLeft -= Time.deltaTime;   // Duraklatınca Time.timeScale = 0 olduğu için sayaç da durur
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            UpdateUI();
            EndGame(false);
            return;
        }

        int sec = Mathf.CeilToInt(TimeLeft);
        if (sec <= 10 && sec != lastTickSecond)
        {
            lastTickSecond = sec;
            Play(tickClip);
        }
        UpdateUI();
    }

    void UpdateUI()
    {
        int sec = Mathf.CeilToInt(TimeLeft);
        if (timerText)
        {
            timerText.text = sec.ToString();
            bool danger = sec <= 10 && IsPlaying;
            timerText.color = danger ? timerDangerColor : timerNormalColor;
            float pulse = danger ? 1f + 0.15f * Mathf.Abs(Mathf.Sin(TimeLeft * Mathf.PI)) : 1f;
            timerText.transform.localScale = timerBaseScale * pulse;
        }
        if (scoreText) scoreText.text = Scored + " / " + totalBalls;
    }

    /// <summary>HoleSocket, içine top girince çağırır.</summary>
    public void OnBallScored(HoleSocket socket)
    {
        if (!IsPlaying) return;
        Scored++;
        Play(scoreClip);
        UpdateUI();
        if (Scored >= totalBalls)
        {
            EndGame(true);
        }
        else
        {
            int left = totalBalls - Scored;
            ShowMessage(praise[Random.Range(0, praise.Length)] + "  " + left + " top kaldı", 1.4f);
        }
    }

    void EndGame(bool win)
    {
        IsPlaying = false;
        IsOver = true;
        Won = win;
        if (player) player.canMove = false;
        if (music) music.volume *= 0.35f;
        if (messageText) messageText.gameObject.SetActive(false);

        if (win)
        {
            float used = gameDuration - TimeLeft;
            if (winDetailText) winDetailText.text = "10 topun hepsini " + used.ToString("0.0") + " saniyede yuvalarına yerleştirdin.\nRüyadan mutlu uyandın!";
            if (winPanel) winPanel.SetActive(true);
            Play(winClip);
        }
        else
        {
            if (loseDetailText) loseDetailText.text = "Alarm çaldı, uyandın!\nYerleştirilen top: " + Scored + " / " + totalBalls;
            if (losePanel) losePanel.SetActive(true);
            Play(alarmClip);
        }
    }

    public void ShowMessage(string text, float duration)
    {
        if (messageText == null) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(text, duration));
    }

    IEnumerator MessageRoutine(string text, float duration)
    {
        messageText.gameObject.SetActive(true);
        messageText.text = text;
        yield return new WaitForSeconds(duration);
        messageText.gameObject.SetActive(false);
    }

    void Play(AudioClip clip)
    {
        if (sfx != null && clip != null) sfx.PlayOneShot(clip);
    }

    // --- Buton fonksiyonları ---
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }
}
