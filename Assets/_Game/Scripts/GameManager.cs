using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Altın Bayrak oyun döngüsü (Game Loop): 60 saniyelik turnuva sayacı, skor (deliğe giren top x/10),
/// kazanma ("Altın Bayrak senin!") ve kaybetme ("Süre doldu!") panelleri.
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
    public TMP_Text timerLabel;       // "SÜRE" yazısı (serbest modda "SERBEST")
    public TMP_Text hintText;         // sağ üstteki kontrol ipucu
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
    public AudioClip startClip, scoreClip, tickClip, winClip, hornClip;

    public float TimeLeft { get; private set; }
    public int Scored { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsOver { get; private set; }
    public bool Won { get; private set; }
    /// <summary>Ana menüden "Serbest Dolaşma" seçildiyse süre yoktur.</summary>
    public bool FreeRoam => OyunModu.SerbestDolasma;

    int lastTickSecond = -1;
    Coroutine messageRoutine;
    Vector3 timerBaseScale = Vector3.one;

    /// <summary>Bir top deliğe girince haber verir (seyirciler zıplar, vb.).</summary>
    public static event System.Action<HoleSocket> BallScored;

    // Golf terimleriyle tebrik mesajları
    static readonly string[] praise = { "Birdie!", "Eagle!", "Tam isabet!", "Harika vuruş!", "Hole in one!", "Par!" };

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
        if (FreeRoam)
        {
            if (timerLabel) timerLabel.text = "MOD";
            if (hintText) hintText.text = "WASD: Yürü     Shift: Hızlı Yürü     ESC / P: Duraklat";
            ShowMessage("Serbest Dolaşma: süre yok, sahayı keşfet!", 3f);
        }
        else
        {
            ShowMessage("Korna çalmadan 10 topu deliklere sok!", 2.5f);
        }
        UpdateUI();
        Play(startClip);
    }

    void Update()
    {
        if (!IsPlaying) return;
        if (FreeRoam) return;                // serbest dolaşmada süre işlemez

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
        if (timerText && FreeRoam)
        {
            timerText.text = "SERBEST";
            timerText.fontSize = 44f;
            timerText.color = timerNormalColor;
        }
        else if (timerText)
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
        BallScored?.Invoke(socket);
        UpdateUI();
        if (Scored >= totalBalls && FreeRoam)
        {
            ShowMessage("10 topun hepsi delikte! Gezmeye devam edebilirsin.", 3.5f);
            Play(winClip);
        }
        else if (Scored >= totalBalls)
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
            // En iyi süre (rekor) kaydı
            float best = PlayerPrefs.GetFloat(OyunModu.EnIyiSureKey, 0f);
            bool record = best <= 0f || used < best;
            if (record) { PlayerPrefs.SetFloat(OyunModu.EnIyiSureKey, used); PlayerPrefs.Save(); best = used; }
            string bestLine = record ? "YENİ REKOR!" : "En iyi süren: " + best.ToString("0.0") + " sn";
            if (winDetailText) winDetailText.text = "10 topu " + used.ToString("0.0") + " saniyede deliklere soktun.\nAltın Bayrak Kupası senin!\n<color=#C98A12>" + bestLine + "</color>";
            if (winPanel) winPanel.SetActive(true);
            Play(winClip);
        }
        else
        {
            if (loseDetailText) loseDetailText.text = "Turnuva kornası çaldı!\nDelikteki top: " + Scored + " / " + totalBalls;
            if (losePanel) losePanel.SetActive(true);
            Play(hornClip);
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
