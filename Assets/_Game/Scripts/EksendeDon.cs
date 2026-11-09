using UnityEngine;

/// <summary>Bir nesneyi, referans nesnenin bir ekseni etrafında döndürür (oyuncak arabanın kurma anahtarı).</summary>
public class EksendeDon : MonoBehaviour
{
    public Transform reference;
    public Vector3 localAxis = Vector3.forward;
    public float speed = 120f;

    void Update()
    {
        Vector3 axis = reference != null ? reference.TransformDirection(localAxis) : localAxis;
        transform.Rotate(axis, speed * Time.deltaTime, Space.World);
    }
}
