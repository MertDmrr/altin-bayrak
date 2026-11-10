using TMPro;
using UnityEngine;

/// <summary>
/// Robot çim biçme makinesi (küp gövdeli) – geliştirilmiş kovalama yapay zekâsı.
/// Hocanın RandomCubeMovement koduna DOKUNMADAN çalışır:
///  1) Devriye  : RandomCubeMovement açıktır, robot sahada rastgele dolaşıp çim biçer (gözler mavi).
///  2) Fark etme: Oyuncu tehlike halkasına girince robot durur, oyuncuya döner, titrer, "!" çıkar,
///                gözleri kırmızıya döner (oyuncuya kaçması için kısa bir uyarı süresi).
///  3) Kovalama: Oyuncunun gideceği yeri tahmin ederek hızlanır, tepe lambası çakar, halka kırmızı yanıp söner.
///  4) Çarpma  : Oyuncuyu geri iter, kamera sarsılır, çim parçaları saçılır; robot sersemleyip döner (gözler sarı).
///  5) Kaybetme: Oyuncu kaçarsa "?" çıkar ve robot evine döner; sonra hocanın kodu tekrar devreye girer.
/// Serbest Dolaşma modunda robotlar kovalamaz, sadece devriye gezer.
/// </summary>
[RequireComponent(typeof(RandomCubeMovement))]
public class CubeChaser : MonoBehaviour
{
    public Transform player;
    [Tooltip("Oyuncu bu mesafeye girince robot fark eder")]
    public float detectRadius = 5f;
    [Tooltip("Oyuncu bu mesafeden uzaklaşınca kovalamayı bırakır")]
    public float giveUpRadius = 8.5f;
    [Tooltip("Kovalarken ulaşabildiği en yüksek hız (oyuncu 4 m/sn)")]
    public float chaseSpeed = 3f;
    [Tooltip("Kovalarken hızlanma (m/sn²)")]
    public float acceleration = 4f;
    public float returnSpeed = 3f;
    [Tooltip("Evinden en fazla bu kadar uzaklaşabilir")]
    public float leashRadius = 10f;
    [Tooltip("Fark ettikten sonra kovalamaya başlamadan önceki uyarı süresi")]
    public float alertTime = 0.55f;
    [Tooltip("Oyuncunun kaç saniye sonraki konumunu tahmin ederek kovalar")]
    public float leadTime = 0.45f;

    [Header("Çarpma")]
    public float hitForce = 4f;
    public float stunTime = 0.4f;
    public float cooldown = 3f;

    [Header("Görsel / Ses")]
    public Transform visual;          // gittiği yöne dönen ve titreyen görsel parça (fizik küpü dönmez)
    public GameObject alertIcon;      // başının üstündeki "!" / "?"
    public Renderer dangerRing;       // yerdeki tehlike halkası
    public Light beaconLight;         // kovalarken çakan tepe lambası
    public ParticleSystem hitBurst;   // çarpınca saçılan çim
    public AudioSource audioSource;
    public AudioClip alertClip;
    public AudioClip bonkClip;
    [Tooltip("Sürekli çalan motor sesi (kovalarken hızlanır)")]
    public AudioSource motorAudio;

    enum State { Wander, Alert, Chase, Return, Cooldown }
    State state = State.Wander;

    RandomCubeMovement wander;
    Vector3 home;
    float timer;
    float speed;
    float hopTime;
    Vector3 visualBasePos;
    Vector3 lastPos;
    Vector3 lastPlayerPos;
    Vector3 playerVel;
    Quaternion facing = Quaternion.identity;
    TMP_Text alertText;
    Material eyeMat, lampMat, ringMat;
    Color ringBase;
    float lostTimer;

    static readonly Color EyeWander = new Color(0.35f, 0.9f, 1f);
    static readonly Color EyeChase = new Color(1f, 0.15f, 0.1f);
    static readonly Color EyeStun = new Color(1f, 0.8f, 0.2f);

    void Start()
    {
        wander = GetComponent<RandomCubeMovement>();
        home = transform.position;
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc) player = pc.transform;
        }
        if (visual) visualBasePos = visual.localPosition;
        lastPos = transform.position;
        if (player) lastPlayerPos = player.position;
        if (alertIcon) { alertIcon.SetActive(false); alertText = alertIcon.GetComponent<TMP_Text>(); }

        // Göz ve lamba materyallerini bu robota özel kopyala (renkleri ayrı ayrı değişsin)
        if (visual)
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                foreach (var m in mats)
                {
                    if (m.name.StartsWith("Robot_Goz")) eyeMat = m;
                    else if (m.name.StartsWith("Robot_Lamba")) lampMat = m;
                }
            }
        if (dangerRing)
        {
            ringMat = dangerRing.material;
            ringBase = ringMat.GetColor("_BaseColor");
            dangerRing.transform.localScale = Vector3.one * detectRadius * 2f;
            dangerRing.enabled = !OyunModu.SerbestDolasma;
        }
        if (beaconLight) beaconLight.enabled = false;
        SetEyes(EyeWander, 4f);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && !gm.IsPlaying)
        {
            wander.enabled = false;
            if (alertIcon) alertIcon.SetActive(false);
            if (beaconLight) beaconLight.enabled = false;
            if (motorAudio) motorAudio.volume = Mathf.MoveTowards(motorAudio.volume, 0f, Time.unscaledDeltaTime);
            return;
        }
        if (player == null) return;

        // Oyuncunun hızını tahmin et (kovalarken önünü kesmek için)
        if (Time.deltaTime > 0f)
        {
            Vector3 pv = (player.position - lastPlayerPos) / Time.deltaTime; pv.y = 0f;
            playerVel = Vector3.Lerp(playerVel, pv, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }
        lastPlayerPos = player.position;

        float dist = Flat(transform.position - player.position);
        bool canChase = !OyunModu.SerbestDolasma;

        switch (state)
        {
            case State.Wander:
                if (canChase && dist < detectRadius) StartAlert();
                break;

            case State.Alert:
                timer -= Time.deltaTime;
                if (timer <= 0f) { state = State.Chase; speed = 0.5f; }
                break;

            case State.Chase:
            {
                speed = Mathf.MoveTowards(speed, chaseSpeed, acceleration * Time.deltaTime);
                Vector3 predicted = player.position + playerVel * leadTime;
                // tahmin noktası oyuncudan çok sapmasın
                Vector3 off = predicted - player.position;
                if (off.magnitude > 2.5f) predicted = player.position + off.normalized * 2.5f;
                MoveTo(predicted, speed);
                if (dist > giveUpRadius || Flat(transform.position - home) > leashRadius) LosePlayer();
                break;
            }

            case State.Return:
                MoveTo(home, returnSpeed);
                if (lostTimer > 0f)
                {
                    lostTimer -= Time.deltaTime;
                    if (lostTimer <= 0f && alertIcon) alertIcon.SetActive(false);
                }
                if (Flat(transform.position - home) < 0.05f)
                {
                    transform.position = home;
                    state = State.Wander;
                    wander.enabled = true;      // hocanın kodu tekrar devrede
                    SetEyes(EyeWander, 4f);
                    if (alertIcon) alertIcon.SetActive(false);
                }
                else if (canChase && dist < detectRadius * 0.8f && Flat(player.position - home) < leashRadius)
                {
                    StartAlert();
                }
                break;

            case State.Cooldown:
                timer -= Time.deltaTime;
                if (timer <= 0f) { state = State.Return; SetEyes(EyeWander, 3f); }
                break;
        }

        AnimateVisual();
    }

    void StartAlert()
    {
        state = State.Alert;
        timer = alertTime;
        wander.enabled = false;               // hocanın kodu geçici olarak durur
        ShowIcon("!", new Color(1f, 0.35f, 0.15f));
        SetEyes(EyeChase, 7f);
        if (audioSource && alertClip) audioSource.PlayOneShot(alertClip);
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) facing = Quaternion.LookRotation(d.normalized);
    }

    void LosePlayer()
    {
        state = State.Return;
        lostTimer = 1.2f;
        ShowIcon("?", new Color(1f, 0.85f, 0.3f));
        SetEyes(EyeStun, 3f);
    }

    void ShowIcon(string s, Color c)
    {
        if (!alertIcon) return;
        alertIcon.SetActive(true);
        alertIcon.transform.localScale = Vector3.one * 0.3f;
        if (alertText) { alertText.text = s; alertText.color = c; }
    }

    void SetEyes(Color c, float intensity)
    {
        if (eyeMat == null) return;
        eyeMat.SetColor("_BaseColor", c);
        eyeMat.SetColor("_EmissionColor", c * intensity);
    }

    void MoveTo(Vector3 target, float spd)
    {
        target.y = transform.position.y;
        transform.position = Vector3.MoveTowards(transform.position, target, spd * Time.deltaTime);
    }

    void AnimateVisual()
    {
        if (visual == null) return;
        // Görsel, gittiği yöne döner (fizik küpü ve hocanın kodu etkilenmez)
        Vector3 delta = transform.position - lastPos; delta.y = 0f;
        lastPos = transform.position;
        if (state != State.Alert && delta.sqrMagnitude > 0.000004f) facing = Quaternion.LookRotation(delta.normalized, Vector3.up);
        Quaternion rot = facing;
        if (state == State.Alert) rot *= Quaternion.Euler(0f, Mathf.Sin(Time.time * 60f) * 6f, 0f);              // öfkeyle titrer
        if (state == State.Cooldown) rot *= Quaternion.Euler(0f, Time.time * 540f % 360f, 0f);                  // sersemleyip döner
        visual.rotation = Quaternion.Slerp(visual.rotation, rot, 1f - Mathf.Exp(-(state == State.Cooldown ? 30f : 8f) * Time.deltaTime));

        bool chasing = state == State.Chase;
        if (motorAudio)
        {
            float targetPitch = chasing ? 1.45f : (state == State.Alert ? 1.2f : 1f);
            motorAudio.pitch = Mathf.MoveTowards(motorAudio.pitch, targetPitch, Time.deltaTime * 2f);
        }
        bool moving = chasing || state == State.Return;
        hopTime += Time.deltaTime * (moving ? (chasing ? 11f : 6f) : 3f);
        float h = moving ? Mathf.Abs(Mathf.Sin(hopTime)) * (chasing ? 0.14f : 0.08f) : Mathf.Abs(Mathf.Sin(hopTime)) * 0.02f;
        if (state == State.Alert) h = 0.12f + Mathf.Abs(Mathf.Sin(Time.time * 30f)) * 0.06f;               // zıplayarak uyarır
        visual.localPosition = visualBasePos + Vector3.up * h;

        // Tepe lambası ve ışık: kovalarken çakar
        float flash = (chasing || state == State.Alert) ? (Mathf.Sin(Time.time * 18f) > 0f ? 1f : 0.15f) : 0.3f;
        if (lampMat) lampMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.08f) * (1f + flash * 3f));
        if (beaconLight)
        {
            beaconLight.enabled = chasing || state == State.Alert;
            beaconLight.intensity = 0.8f + flash * 2.2f;
        }

        // Tehlike halkası: devriyede sakin turuncu, kovalarken kırmızı nabız
        if (ringMat)
        {
            Color c = ringBase;
            if (chasing || state == State.Alert) c = Color.Lerp(new Color(1f, 0.2f, 0.12f, 0.55f), new Color(1f, 0.35f, 0.2f, 0.9f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
            else if (state == State.Cooldown) c = new Color(1f, 0.85f, 0.3f, 0.35f);
            ringMat.SetColor("_BaseColor", c);
        }

        if (alertIcon && alertIcon.activeSelf)
        {
            alertIcon.transform.localScale = Vector3.Lerp(alertIcon.transform.localScale, Vector3.one, 1f - Mathf.Exp(-12f * Time.deltaTime));
            alertIcon.transform.rotation = Quaternion.LookRotation(alertIcon.transform.position - Camera.main.transform.position);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (state != State.Chase) return;
        var pc = collision.collider.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        Vector3 dir = pc.transform.position - transform.position;
        pc.Knockback(dir, hitForce, stunTime);
        if (audioSource && bonkClip) audioSource.PlayOneShot(bonkClip);
        if (hitBurst) hitBurst.Play();
        GameManager.Instance?.ShowMessage("Robot çarptı!", 1f);

        state = State.Cooldown;
        timer = cooldown;
        speed = 0f;
        SetEyes(EyeStun, 4f);
        ShowIcon("!!", new Color(1f, 0.85f, 0.3f));
        lostTimer = 0f;
        Invoke(nameof(HideIcon), 1.2f);
    }

    void HideIcon() { if (alertIcon && state != State.Alert && state != State.Chase) alertIcon.SetActive(false); }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
}
