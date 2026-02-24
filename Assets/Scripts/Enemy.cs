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

    [Tooltip("공격 가능 거리")]
    public float attackDistance = 2.0f;
    
    [Tooltip("공격 간격 (초 단위)")]
    public float attackInterval = 2.0f;
    
    private float attackTimer = 0.0f;
    private bool isAttacking = false;

    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        enemyModel = GetComponent<GrimReaper>();
        enemyModel.Build();

        GameObject weapon = new GameObject("Scythe");
        Scythe scythe = weapon.AddComponent<Scythe>();
        scythe.Build(enemyModel.RightHand);
    }

    // Update is called once per frame
    void Update()
    {
        var player = GameManager.Instance.player;
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        // 공격 타이머 업데이트
        if (true == isAttacking)
        {
            attackTimer += Time.deltaTime;
        }

        if (distanceToPlayer <= DetectionDistance)
        {
            LookAtPlayer();

            if(false == isAttacking)
            {
                // 타이머를 매 프레임 증가시킵니다.
                pathUpdateTimer += Time.deltaTime;
                // 타이머가 설정된 간격(pathUpdateInterval)을 넘었을 때만 목표 지점을 갱신합니다.
                if (pathUpdateTimer > pathUpdateInterval)
                {
                    // 목표 지점 설정
                    navMeshAgent.SetDestination(player.transform.position);

                    // 경로가 완전히 계획되었는지 확인
                    if (navMeshAgent.hasPath && navMeshAgent.pathPending == false)
                    {
                        // 경로의 상태 확인
                        if (navMeshAgent.remainingDistance == Mathf.Infinity)
                        {
                            // 도달 불가능한 경로
                            Debug.Log("Enemy: 플레이어에게 도달할 수 없는 경로입니다. 추격 중지!");
                            navMeshAgent.velocity = Vector3.zero;
                            navMeshAgent.ResetPath();
                        }
                        else if (navMeshAgent.hasPath)
                        {
                            // 도달 가능한 경로
                            navMeshAgent.velocity = navMeshAgent.desiredVelocity;
                        }
                    }

                    // 타이머 초기화
                    pathUpdateTimer = 0f;
                }
            }
        }

        // 공격 가능 거리에 플레이어가 들어왔는지 확인
        if (distanceToPlayer <= attackDistance)
        {
            // NavMeshAgent 속도 0으로 설정 (멈춤)
            navMeshAgent.velocity = Vector3.zero;
            navMeshAgent.ResetPath();

            // 공격 로직
            if (false == isAttacking)
            {
                isAttacking = true;
                attackTimer = 0.0f;
                enemyModel.PlayAnimation(GrimReaper.CharacterState.Attack);
                Debug.Log("Enemy: 공격 시작!");
            }
            else if (attackTimer >= attackInterval)
            {
                // 공격 간격이 지났으면 다시 공격
                attackTimer = 0.0f;
                enemyModel.PlayAnimation(GrimReaper.CharacterState.Attack);
                Debug.Log("Enemy: 반복 공격!");
            }
        }
        else
        {
            isAttacking = false;
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

        // 공격 거리 시각화
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackDistance);
    }
}
