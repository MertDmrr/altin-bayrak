using UnityEngine;

/// <summary>
/// Golf arabası gibi araçları bir yol noktaları döngüsünde yavaşça sürer.
/// Önünde oyuncu varsa durur ve bekler (oyuncuya çarpmaz).
/// </summary>
public class YolTakip : MonoBehaviour
{
    public Vector3[] points;           // dünya koordinatları (y yok sayılır, zemine oturtulur)
    public float speed = 3f;
    public float turnSpeed = 3f;
    public float stopDistance = 4f;
    public Transform player;
    public Terrain terrain;
    public int startIndex;
    public float yOffset;

    int target;

    void Start()
    {
        if (points == null || points.Length < 2) { enabled = false; return; }
        target = (startIndex + 1) % points.Length;
        Vector3 p = points[startIndex % points.Length];
        transform.position = Grounded(p);
        Vector3 d = points[target] - p; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc) player = pc.transform;
        }
    }

    Vector3 Grounded(Vector3 p)
    {
        if (terrain) p.y = terrain.SampleHeight(p) + terrain.transform.position.y + yOffset;
        return p;
    }

    void Update()
    {
        // Önünde oyuncu varsa bekle
        if (player)
        {
            Vector3 toP = player.position - transform.position; toP.y = 0f;
            if (toP.magnitude < stopDistance && Vector3.Dot(toP.normalized, transform.forward) > 0.3f) return;
        }

        Vector3 goal = Grounded(points[target]);
        Vector3 pos = transform.position;
        Vector3 flat = goal - pos; flat.y = 0f;
        if (flat.magnitude < 0.6f)
        {
            target = (target + 1) % points.Length;
            return;
        }
        Quaternion want = Quaternion.LookRotation(flat.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        Vector3 next = pos + transform.forward * speed * Time.deltaTime;
        transform.position = Grounded(next);
    }
}
