using UnityEngine;

/// <summary>Yuvanın dibindeki görünmez alan. İçine top düşerse yuvaya haber verir.</summary>
[RequireComponent(typeof(Collider))]
public class HoleTrigger : MonoBehaviour
{
    public HoleSocket socket;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (socket == null) return;
        Ball ball = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<Ball>() : other.GetComponent<Ball>();
        if (ball != null) socket.OnBallEntered(ball);
    }
}
