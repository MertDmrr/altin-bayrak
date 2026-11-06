using UnityEngine;

/// <summary>
/// Topun düşeceği yuva (socket / hole). Yuvanın dibindeki trigger topu algılar.
/// Her yuvaya bir top oturur; dolan yuva parlar ve kapanır.
/// </summary>
public class HoleSocket : MonoBehaviour
{
    [Tooltip("Top yuvaya oturunca merkezinin duracağı yükseklik (yuvanın merkezine göre)")]
    public float settleDepth = -0.62f;
    public ParticleSystem scoreEffect;
    public Renderer ringRenderer;
    public Color filledColor = new Color(1f, 0.82f, 0.25f);
    [Tooltip("Yuva dolunca açılan kapak; başka toplar üstünden geçer")]
    public Collider lidCollider;
    public Light glowLight;
    public GameObject flagBeacon;     // uzaktan görünen işaret, dolunca kapanır

    public bool IsFilled { get; private set; }

    void Start()
    {
        if (lidCollider) lidCollider.enabled = false;
        if (glowLight) glowLight.enabled = false;
    }

    // Bu fonksiyon yuvanın dibindeki trigger'dan çağrılır (HoleTrigger)
    public void OnBallEntered(Ball ball)
    {
        if (IsFilled || ball == null || ball.scored) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        IsFilled = true;
        ball.LockInto(transform.position + Vector3.up * settleDepth);

        if (scoreEffect) scoreEffect.Play();
        if (ringRenderer)
        {
            var mat = ringRenderer.material;
            mat.SetColor("_BaseColor", filledColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", filledColor * 1.5f);
            }
        }
        if (lidCollider) lidCollider.enabled = true;
        if (glowLight) glowLight.enabled = true;
        if (flagBeacon) flagBeacon.SetActive(false);

        if (GameManager.Instance != null) GameManager.Instance.OnBallScored(this);
    }
}
