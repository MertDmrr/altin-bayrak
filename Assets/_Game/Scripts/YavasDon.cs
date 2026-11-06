using UnityEngine;

/// <summary>Dekor için yavaş dönme ve süzülme (menüdeki oyuncaklar, bulutlar).</summary>
public class YavasDon : MonoBehaviour
{
    public Vector3 rotationSpeed = new Vector3(0f, 10f, 0f);
    public float bobAmount = 0f;
    public float bobSpeed = 1f;

    Vector3 basePos;
    float phase;

    void Start()
    {
        basePos = transform.localPosition;
        phase = Random.value * 10f;
    }

    void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        if (bobAmount > 0f)
            transform.localPosition = basePos + Vector3.up * Mathf.Sin((Time.time + phase) * bobSpeed) * bobAmount;
    }
}
