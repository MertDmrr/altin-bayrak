using UnityEngine;

/// <summary>Golf bayrağı rüzgârda hafifçe dalgalanır (döndürme + ölçek salınımı).</summary>
public class BayrakDalga : MonoBehaviour
{
    public float angle = 10f;
    public float speed = 2.2f;

    Quaternion baseRot;
    Vector3 baseScale;
    float phase;

    void Awake()
    {
        baseRot = transform.localRotation;
        baseScale = transform.localScale;
        phase = Random.value * 6f;
    }

    void Update()
    {
        float t = Time.time * speed + phase;
        transform.localRotation = baseRot * Quaternion.Euler(0f, Mathf.Sin(t) * angle, Mathf.Sin(t * 1.7f) * 2f);
        transform.localScale = new Vector3(baseScale.x * (1f + 0.04f * Mathf.Sin(t * 2.3f)), baseScale.y, baseScale.z);
    }
}
