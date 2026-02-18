using UnityEngine;

public class Enemy : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private GrimReaper unit;
    private const float DetectionDistance = 10.0f;

    void Start()
    {
        unit = GetComponentInChildren<GrimReaper>();
        unit.Build();
    }

    // Update is called once per frame
    void Update()
    {
        var player = GameManager.Instance.player;
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        if (distanceToPlayer <= DetectionDistance)
        {
            LookAtPlayer();
        }
    }

    private void LookAtPlayer()
    {
        var player = GameManager.Instance.player;
        // 플레이어 방향 계산 (Y축 무시하여 수평 방향만 고려)
        Vector3 directionToPlayer = player.transform.position - transform.position;
        directionToPlayer.y = 0f;  // Y축 회전만 적용
        directionToPlayer = directionToPlayer.normalized;

        if (directionToPlayer.sqrMagnitude > 0.001f)
        {
            // 플레이어를 바라보는 회전 계산
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);

            // Smooth하게 회전
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
}
