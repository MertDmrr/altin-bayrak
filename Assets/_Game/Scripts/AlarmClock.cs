using UnityEngine;

/// <summary>
/// Haritanın ortasındaki dev çalar saat (nirengi noktası).
/// Saniye kolu oyundaki 60 saniyelik sayaçla birlikte döner; süre bitince saat zıplayarak çalar.
/// </summary>
public class AlarmClock : MonoBehaviour
{
    public Transform hand;          // yerel Z ekseni etrafında döner
    public Transform body;          // çalarken sallanan parça
    public float ringShake = 6f;

    Vector3 bodyBasePos;
    Quaternion bodyBaseRot;

    void Start()
    {
        if (body) { bodyBasePos = body.localPosition; bodyBaseRot = body.localRotation; }
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (hand)
        {
            float elapsed01 = 1f - gm.TimeLeft / gm.gameDuration;
            hand.localRotation = Quaternion.Euler(0f, 0f, 360f * elapsed01);   // önden bakınca saat yönünde
        }

        if (body)
        {
            bool ringing = (gm.IsOver && !gm.Won) || (gm.IsPlaying && gm.TimeLeft <= 5f);
            if (ringing)
            {
                float t = Time.time * 40f;
                body.localRotation = bodyBaseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t) * ringShake);
                body.localPosition = bodyBasePos + Vector3.up * Mathf.Abs(Mathf.Sin(t * 0.5f)) * 0.3f;
            }
            else
            {
                body.localRotation = bodyBaseRot;
                body.localPosition = bodyBasePos;
            }
        }
    }
}
