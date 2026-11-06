using UnityEngine;

/// <summary>
/// 3. şahıs tepe açısı (Top-Down TPS) kamera.
/// Karakteri yukarıdan ve geriden, sarsıntısız (SmoothDamp) takip eder.
/// Kameranın açısı sabittir; sadece konumu yumuşakça karakteri izler.
/// </summary>
public class TopDownCamera : MonoBehaviour
{
    public Transform target;
    [Tooltip("Karaktere göre kamera konumu: yukarı ve geri")]
    public Vector3 offset = new Vector3(0f, 13f, -9f);
    [Tooltip("Kameranın bakacağı nokta karakterin ne kadar üstünde")]
    public float lookHeight = 1.0f;
    [Tooltip("Takip yumuşaklığı (küçük = daha hızlı)")]
    public float smoothTime = 0.2f;

    Vector3 velocity;

    void Start()
    {
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        transform.rotation = FixedRotation();
    }

    Quaternion FixedRotation()
    {
        // Kameradan karakterin biraz üstüne bakan sabit yön
        return Quaternion.LookRotation(Vector3.up * lookHeight - offset, Vector3.up);
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        transform.rotation = FixedRotation();
    }
}
