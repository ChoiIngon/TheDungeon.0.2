using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private GrimReaper enemyModel;
    private const float DetectionDistance = 10.0f;
    private NavMeshAgent navMeshAgent;

    [Header("AI Settings")]
    [Tooltip("목표 지점을 얼마나 자주 갱신할지 결정합니다 (초 단위).")]
    public float pathUpdateInterval = 1.0f;
    // 내부 타이머
    private float pathUpdateTimer;

    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        enemyModel = GetComponent<GrimReaper>();
        enemyModel.Build();
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

        // 타이머를 매 프레임 증가시킵니다.
        pathUpdateTimer += Time.deltaTime;
        // 타이머가 설정된 간격(pathUpdateInterval)을 넘었을 때만 목표 지점을 갱신합니다.
        if (pathUpdateTimer > pathUpdateInterval)
        {
            // 목표 지점 설정
            navMeshAgent.SetDestination(player.transform.position);

            // 타이머 초기화
            pathUpdateTimer = 0f;
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

    void OnDrawGizmos()
    {
        if (navMeshAgent != null && navMeshAgent.hasPath)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < navMeshAgent.path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(navMeshAgent.path.corners[i], navMeshAgent.path.corners[i + 1]);
            }
        }
    }
}
