using UnityEngine;

/// <summary>
/// Skor tabelasındaki turnuva saati (nirengi noktası).
/// Kırmızı kol oyundaki 60 saniyelik sayaçla birlikte bir tur döner; son 5 saniyede ve süre bitince saat titrer.
/// </summary>
public class TurnuvaSaati : MonoBehaviour
{
    public Transform hand;          // yerel Z ekseni etrafında döner
    public Transform body;          // titreyen parça
    public float shake = 4f;

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
            hand.localRotation = Quaternion.Euler(0f, 0f, -360f * elapsed01);   // önden bakınca saat yönünde
        }

        if (body)
        {
            bool alarm = (gm.IsOver && !gm.Won) || (gm.IsPlaying && gm.TimeLeft <= 5f);
            if (alarm)
            {
                float t = Time.time * 40f;
                body.localRotation = bodyBaseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t) * shake);
            }
            else
            {
                body.localRotation = bodyBaseRot;
                body.localPosition = bodyBasePos;
            }
        }
    }
}
