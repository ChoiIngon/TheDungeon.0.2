using UnityEngine;

public class Actor : MonoBehaviour
{
    [Header("Gameplay Settings")]
    public float maxHp = 100f;
    private float currentHp;
    private bool isDead = false;

    // 모델 참조 (Player, Enemy에서 초기화)
    protected ActorModel actorModel;

    protected virtual void Start()
    {
        // 초기 HP 설정
        currentHp = maxHp;
        
        // 모델 가져오기 (하위 클래스에서 초기화하지 않으면 자동으로 찾기)
        if (actorModel == null)
        {
            actorModel = GetComponent<ActorModel>();
        }

        if (actorModel == null)
        {
            Debug.LogError($"Actor '{gameObject.name}'에 ActorModel 컴포넌트가 없습니다!");
        }
    }

    protected virtual void Update()
    {
        // 게임 플레이 기본 로직
    }

    /// <summary>
    /// 현재 HP를 반환합니다.
    /// </summary>
    public float GetCurrentHp()
    {
        return currentHp;
    }

    /// <summary>
    /// HP를 설정합니다.
    /// </summary>
    public void SetHp(float hp)
    {
        currentHp = Mathf.Clamp(hp, 0f, maxHp);
        
        // HP가 0이 되면 사망
        if (currentHp <= 0f && !isDead)
        {
            Die();
        }
    }

    /// <summary>
    /// 피해를 입습니다.
    /// </summary>
    /// <param name="damage">피해량</param>
    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        SetHp(currentHp - damage);
        
        // 사망하지 않았으면 피격 애니메이션 재생
        if (!isDead && actorModel != null)
        {
            actorModel.PlayAnimation(ActorModel.ActorState.Hurt);
        }

        Debug.Log($"{gameObject.name}이(가) {damage}의 피해를 입었습니다. 현재 HP: {currentHp}/{maxHp}");
    }

    /// <summary>
    /// HP를 회복합니다.
    /// </summary>
    /// <param name="amount">회복량</param>
    public void Heal(float amount)
    {
        if (isDead)
            return;

        SetHp(currentHp + amount);
        Debug.Log($"{gameObject.name}이(가) {amount}만큼 회복되었습니다. 현재 HP: {currentHp}/{maxHp}");
    }

    /// <summary>
    /// 사망 처리
    /// </summary>
    private void Die()
    {
        isDead = true;
        Debug.Log($"{gameObject.name}이(가) 사망했습니다!");

        if (actorModel != null)
        {
            actorModel.PlayAnimation(ActorModel.ActorState.Dead);
        }

        // 사망 후 추가 처리 (충돌 무시, 물리 비활성화 등)
        OnDeath();
    }

    /// <summary>
    /// 사망 시 호출되는 가상 메서드 (하위 클래스에서 재정의 가능)
    /// </summary>
    protected virtual void OnDeath()
    {
        // Rigidbody가 있으면 중력 활성화
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
        }

        // Collider를 Trigger로 설정하여 상호작용 불가능하게
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }
    }

    /// <summary>
    /// 사망 상태 확인
    /// </summary>
    public bool IsDead()
    {
        return isDead;
    }

    /// <summary>
    /// 현재 HP 비율 반환 (0~1)
    /// </summary>
    public float GetHpRatio()
    {
        return maxHp > 0 ? currentHp / maxHp : 0f;
    }
}
