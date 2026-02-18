using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 20f;
    
    [Header("Explosion Settings")]
    public GameObject explosionPrefab;
    public float explosionRadius = 2f;
    
    private Vector3 direction = Vector3.forward;
    private bool isDestroyed = false;

    /// <summary>
    /// Projectile을 초기화하고 방향을 설정합니다.
    /// </summary>
    public void Initialize(Vector3 startPosition, Vector3 shootDirection)
    {
        transform.position = startPosition;
        direction = shootDirection.normalized;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    void Update()
    {
        if (isDestroyed)
            return;

        // Projectile 이동
        transform.position += direction * speed * Time.deltaTime;

        // 월드 범위를 벗어나면 제거 (무한 이동 방지)
        if (transform.position.magnitude > 1000f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 충돌 감지
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (isDestroyed)
            return;

        // Wall과 충돌했을 때
        if (other.CompareTag("Wall") || other.name.Contains("Wall"))
        {
            OnHitWall(transform.position);
            return;
        }

        // Floor와는 충돌 안함
        if (other.CompareTag("Floor") || other.name.Contains("Floor"))
        {
            return;
        }

        // Column과 충돌
        if (other.CompareTag("Column") || other.name.Contains("Column"))
        {
            OnHitWall(transform.position);
            return;
        }
    }

    /// <summary>
    /// 벽에 충돌했을 때 폭발 처리
    /// </summary>
    private void OnHitWall(Vector3 hitPosition)
    {
        isDestroyed = true;

        // 폭발 이펙트 생성
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, hitPosition, Quaternion.identity);
        }
        else
        {
            // 기본 폭발 이펙트 (프리팹이 없을 경우)
            CreateDefaultExplosion(hitPosition);
        }

        // Projectile 제거
        Destroy(gameObject);
    }

    /// <summary>
    /// 기본 폭발 이펙트 생성 (간단한 파티클 효과)
    /// </summary>
    private void CreateDefaultExplosion(Vector3 position)
    {
        // 간단한 구 모양의 파티클 이펙트
        GameObject explosion = new GameObject("Explosion");
        explosion.transform.position = position;

        // 파티클 시스템 추가
        ParticleSystem ps = explosion.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startSize = 0.5f;
        main.startLifetime = 0.5f;
        main.startSpeed = 10f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 30f;

        // 3초 후 폭발 이펙트 제거
        Destroy(explosion, 1f);
    }

    /// <summary>
    /// Projectile의 속도를 변경합니다.
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
}
