using UnityEngine;

/// <summary>
/// Kovalayan oyuncak küp. Hocanın RandomCubeMovement koduna DOKUNMADAN çalışır:
/// - Normalde küp, RandomCubeMovement ile rastgele dolaşır.
/// - Oyuncu yaklaşınca RandomCubeMovement geçici olarak kapatılır ve küp oyuncuyu kovalar.
/// - Oyuncuya çarparsa onu geri iter (can veya süre cezası yok).
/// - Oyuncu kaçınca küp evine döner ve RandomCubeMovement tekrar açılır.
/// </summary>
[RequireComponent(typeof(RandomCubeMovement))]
public class CubeChaser : MonoBehaviour
{
    public Transform player;
    [Tooltip("Oyuncu bu mesafeye girince kovalamaya başlar")]
    public float detectRadius = 7f;
    [Tooltip("Oyuncu bu mesafeden uzaklaşınca kovalamayı bırakır")]
    public float giveUpRadius = 11f;
    public float chaseSpeed = 2.6f;
    public float returnSpeed = 3f;
    [Tooltip("Evinden en fazla bu kadar uzaklaşabilir")]
    public float leashRadius = 14f;

    [Header("Çarpma")]
    public float hitForce = 7f;
    public float stunTime = 0.6f;
    public float cooldown = 2.5f;

    [Header("Görsel / Ses")]
    public Transform visual;          // zıplayan görsel parça
    public GameObject alertIcon;      // başının üstündeki "!"
    public AudioSource audioSource;
    public AudioClip alertClip;
    public AudioClip bonkClip;

    enum State { Wander, Chase, Return, Cooldown }
    State state = State.Wander;

    RandomCubeMovement wander;
    Vector3 home;
    float cooldownTimer;
    float hopTime;
    Vector3 visualBasePos;

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
        if (alertIcon) alertIcon.SetActive(false);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && !gm.IsPlaying)
        {
            wander.enabled = false;
            if (alertIcon) alertIcon.SetActive(false);
            return;
        }
        if (player == null) return;

        float dist = Flat(transform.position - player.position);

        switch (state)
        {
            case State.Wander:
                if (dist < detectRadius) StartChase();
                break;

            case State.Chase:
                MoveTo(player.position, chaseSpeed);
                if (dist > giveUpRadius || Flat(transform.position - home) > leashRadius) GoHome();
                break;

            case State.Return:
                MoveTo(home, returnSpeed);
                if (Flat(transform.position - home) < 0.05f)
                {
                    transform.position = home;
                    state = State.Wander;
                    wander.enabled = true;      // hocanın kodu tekrar devrede
                }
                else if (dist < detectRadius * 0.8f && Flat(player.position - home) < leashRadius)
                {
                    StartChase();
                }
                break;

            case State.Cooldown:
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f) GoHome();
                break;
        }

        AnimateVisual();
    }

    void StartChase()
    {
        state = State.Chase;
        wander.enabled = false;               // hocanın kodu geçici olarak durur
        if (alertIcon) alertIcon.SetActive(true);
        if (audioSource && alertClip) audioSource.PlayOneShot(alertClip);
    }

    void GoHome()
    {
        state = State.Return;
        if (alertIcon) alertIcon.SetActive(false);
    }

    void MoveTo(Vector3 target, float speed)
    {
        target.y = transform.position.y;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }

    void AnimateVisual()
    {
        if (visual == null) return;
        bool moving = state == State.Chase || state == State.Return;
        float hopSpeed = state == State.Chase ? 9f : 6f;
        hopTime += Time.deltaTime * (moving ? hopSpeed : 3f);
        float h = moving ? Mathf.Abs(Mathf.Sin(hopTime)) * 0.35f : Mathf.Abs(Mathf.Sin(hopTime)) * 0.05f;
        visual.localPosition = visualBasePos + Vector3.up * h;
        if (alertIcon && alertIcon.activeSelf) alertIcon.transform.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (state != State.Chase) return;
        var pc = collision.collider.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        Vector3 dir = pc.transform.position - transform.position;
        pc.Knockback(dir, hitForce, stunTime);
        if (audioSource && bonkClip) audioSource.PlayOneShot(bonkClip);

        state = State.Cooldown;
        cooldownTimer = cooldown;
        if (alertIcon) alertIcon.SetActive(false);
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
}
