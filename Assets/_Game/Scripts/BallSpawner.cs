using UnityEngine;

/// <summary>
/// 10 topu her oyunda haritaya RASTGELE yerleştirir.
/// Her top bir yuvanın çevresinde (minDistance - maxDistance arası) rastgele bir noktaya konur,
/// böylece 60 saniyede bitirilebilir kalır.
/// </summary>
public class BallSpawner : MonoBehaviour
{
    public Ball[] balls;
    public HoleSocket[] holes;
    public Terrain terrain;
    public float ballRadius = 0.7f;
    public float minDistance = 3.2f;
    public float maxDistance = 5.5f;
    [Tooltip("Top, hiçbir yuvanın bu mesafeden yakınına konmaz (huninin içine düşmesin)")]
    public float keepAwayFromHoles = 3.0f;
    public Vector2 areaMin = new Vector2(-30f, -30f);
    public Vector2 areaMax = new Vector2(30f, 30f);
    public LayerMask obstacleMask = ~0;
    [Tooltip("Toplar bu noktalara (kovalayan küpler) yakın doğmaz; küp topu kendi kendine yuvaya itmesin")]
    public Transform[] avoidPoints;
    public float avoidRadius = 5f;

    void Start()
    {
        int count = Mathf.Min(balls.Length, holes.Length);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = FindSpot(holes[i].transform.position, i);
            balls[i].SetSpawn(pos);
        }
    }

    Vector3 FindSpot(Vector3 center, int index)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(minDistance, maxDistance);
            Vector3 p = center + new Vector3(dir.x, 0f, dir.y) * dist;
            if (p.x < areaMin.x || p.x > areaMax.x || p.z < areaMin.y || p.z > areaMax.y) continue;
            if (TooCloseToHoles(p)) continue;
            if (TooCloseToBalls(p, index)) continue;
            if (TooCloseToAvoid(p)) continue;
            p.y = GroundY(p) + ballRadius + 0.05f;
            if (Physics.CheckSphere(p, ballRadius * 0.95f, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            return p;
        }
        // Yer bulunamazsa yuvanın biraz yanına koy
        Vector3 fallback = center + new Vector3(minDistance, 0f, 0f);
        fallback.y = GroundY(fallback) + ballRadius + 0.05f;
        return fallback;
    }

    bool TooCloseToHoles(Vector3 p)
    {
        foreach (var h in holes)
        {
            Vector3 d = h.transform.position - p; d.y = 0f;
            if (d.magnitude < keepAwayFromHoles) return true;
        }
        return false;
    }

    bool TooCloseToAvoid(Vector3 p)
    {
        if (avoidPoints == null) return false;
        foreach (var a in avoidPoints)
        {
            if (a == null) continue;
            Vector3 d = a.position - p; d.y = 0f;
            if (d.magnitude < avoidRadius) return true;
        }
        return false;
    }

    bool TooCloseToBalls(Vector3 p, int upTo)
    {
        for (int i = 0; i < upTo; i++)
        {
            Vector3 d = balls[i].transform.position - p; d.y = 0f;
            if (d.magnitude < ballRadius * 2f + 0.6f) return true;
        }
        return false;
    }

    float GroundY(Vector3 p)
    {
        if (terrain != null) return terrain.SampleHeight(p) + terrain.transform.position.y;
        return 0f;
    }
}
