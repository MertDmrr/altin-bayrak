using System.Collections;
using UnityEngine;

/// <summary>
/// İtilebilir oyuncak top (küre). Rigidbody + SphereCollider ile fizik tabanlı.
/// Yuvaya girince HoleSocket tarafından yerine kilitlenir.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    [HideInInspector] public bool scored;
    [Tooltip("Top haritadan düşerse bu yüksekliğin altında başlangıç yerine döner")]
    public float fallLimitY = -10f;

    Rigidbody rb;
    Vector3 spawnPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        spawnPos = transform.position;
    }

    /// <summary>BallSpawner rastgele yeri belirleyince çağırır.</summary>
    public void SetSpawn(Vector3 pos)
    {
        spawnPos = pos;
        transform.position = pos;
        rb.position = pos;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void FixedUpdate()
    {
        if (!scored && rb.position.y < fallLimitY)
            SetSpawn(spawnPos + Vector3.up * 1.5f);   // haritadan düştüyse geri gelsin
    }

    /// <summary>Top yuvaya girdi: fiziği kapat ve yuvanın içine yumuşakça otur.</summary>
    public void LockInto(Vector3 socketPos)
    {
        scored = true;
        StartCoroutine(SettleRoutine(socketPos));
    }

    IEnumerator SettleRoutine(Vector3 socketPos)
    {
        rb.isKinematic = true;
        Vector3 start = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            transform.position = Vector3.Lerp(start, socketPos, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.position = socketPos;
    }
}
