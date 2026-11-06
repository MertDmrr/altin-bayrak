
using UnityEngine;

public class RandomCubeMovement : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float moveRange = 3f;
    public float changeTime = 2f;

    private Vector3 startPosition;
    private float targetX;
    private float timer;

    void Start()
    {
        startPosition = transform.position;
        SetNewTarget();
    }

    void Update()
    {
        timer += Time.deltaTime;

        Vector3 targetPosition = new Vector3(
            targetX,
            transform.position.y,
            transform.position.z
        );

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        if (timer >= changeTime ||
            Mathf.Abs(transform.position.x - targetX) < 0.05f)
        {
            SetNewTarget();
        }
    }

    void SetNewTarget()
    {
        targetX = startPosition.x +
                  Random.Range(-moveRange, moveRange);

        timer = 0f;
    }
}
