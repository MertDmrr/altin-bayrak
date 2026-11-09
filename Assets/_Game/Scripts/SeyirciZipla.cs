using UnityEngine;

/// <summary>
/// Karton seyirciler: hafifçe sallanır, bir top deliğe girince sevinçle zıplar.
/// </summary>
public class SeyirciZipla : MonoBehaviour
{
    public float idleSway = 2.5f;
    public float jumpHeight = 0.45f;

    Vector3 basePos;
    Quaternion baseRot;
    float phase;
    float cheer;

    void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        phase = Random.value * 10f;
    }

    void OnEnable() { GameManager.BallScored += OnScored; }
    void OnDisable() { GameManager.BallScored -= OnScored; }

    void OnScored(HoleSocket s) { cheer = 1.2f + Random.value * 0.4f; }

    void Update()
    {
        phase += Time.deltaTime;
        float y = 0f;
        if (cheer > 0f)
        {
            cheer -= Time.deltaTime;
            y = Mathf.Abs(Mathf.Sin(phase * 9f)) * jumpHeight;
        }
        transform.localPosition = basePos + Vector3.up * y;
        transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 1.3f) * idleSway);
    }
}
