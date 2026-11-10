using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Oyuncu hareketi (Mert Demir - 2506127004).
/// WASD / Yön tuşları ile kameraya göre hareket eder.
/// Rigidbody kullandığı için kürelere fiziksel olarak çarpar ve onları iter.
/// Animator'daki "Speed" parametresini besler: 0 = Idle, 1 = Walk.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    [Tooltip("Yürüme hızı (metre/saniye)")]
    public float walkSpeed = 3.5f;
    [Tooltip("Dönüş hızı (derece/saniye)")]
    public float turnSpeed = 600f;
    [Tooltip("Hareket yönü bu kameraya göre hesaplanır")]
    public Transform cameraTransform;

    [Header("Animasyon")]
    public Animator animator;
    [Tooltip("Mixamo Walking klibinin 1x hızdaki yaklaşık yürüme hızı (m/sn). Ayak kaymasını önlemek için kullanılır.")]
    public float walkClipSpeed = 1.5f;

    [HideInInspector] public bool canMove = true;   // GameManager oyun bitince kapatır

    Rigidbody rb;
    Vector3 moveDir;          // dünya uzayında hareket yönü (0..1 uzunluk)
    float stunTimer;          // küp çarpınca kısa süre kontrol kaybı

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int WalkMulHash = Animator.StringToHash("WalkSpeedMul");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;           // kamerada titreme olmasın
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;            // devrilmesin
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        Vector2 input = (canMove && stunTimer <= 0f) ? ReadInput() : Vector2.zero;
        // Kameranın baktığı yönü yere izdüşürüp ileri/sağ eksenlerini buluyoruz
        Vector3 forward = Vector3.forward;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
            forward.Normalize();
        }
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        moveDir = forward * input.y + right * input.x;

        if (animator != null)
        {
            // Speed > 0.1 olunca Idle -> Walk geçişi olur (Animator Controller'da)
            animator.SetFloat(SpeedHash, moveDir.magnitude, 0.05f, Time.deltaTime);
            animator.SetFloat(WalkMulHash, walkSpeed / walkClipSpeed);
        }
    }

    Vector2 ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        float x = 0f, y = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    void FixedUpdate()
    {
        if (stunTimer > 0f)
        {
            stunTimer -= Time.fixedDeltaTime;   // geri itilirken fizik serbest
            return;
        }

        Vector3 velocity = moveDir * walkSpeed;
        velocity.y = rb.linearVelocity.y;        // yerçekimi korunur
        rb.linearVelocity = velocity;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));
        }
    }

    /// <summary>Kovalayan küp çarpınca çağrılır: oyuncuyu geri iter.</summary>
    public void Knockback(Vector3 direction, float force, float stunTime)
    {
        direction.y = 0f;
        direction.Normalize();
        stunTimer = stunTime;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction * force + Vector3.up * force * 0.35f, ForceMode.VelocityChange);
    }

    public bool IsStunned => stunTimer > 0f;
}
