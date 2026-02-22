using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ActorModel : MonoBehaviour
{
    protected List<GameObject> parts = new List<GameObject>();
    protected Transform headPivot;
    protected Transform armLeftPivot, armRightPivot;
    protected Transform handLeftPivot, handRightPivot;
    protected Transform legLeftPivot, legRightPivot;

    public enum ActorState
    {
        Idle,    // 대기
        Walk,    // 걷기
        Attack,  // 공격
        Hurt,     // 피격
        Dead     // 사망
    }

    private ActorState currentState = ActorState.Idle;
    protected float animationElapsedTime { get; private set; } = 0.0f;
    private float smoothTime = 10.0f;

    public Transform Head { get { return headPivot; } }
    public Transform RightHand { get { return handRightPivot; } }
    public Transform LeftHand { get { return handLeftPivot; } }

    public void Update()
    {
        switch (currentState)
        {
            case ActorState.Idle: AnimateIdle(); break;
            case ActorState.Walk: AnimateWalk(); break;
            case ActorState.Attack: AnimateAttack(); break;
            case ActorState.Hurt: AnimateHurt(); break;
            case ActorState.Dead: AnimateDead(); break;
        }
    }

    public void PlayAnimation(ActorState state)
    {
        if (currentState == state)
        {
            return;
        }

        currentState = state;
        animationElapsedTime = 0.0f;
    }

    virtual protected void AnimateIdle() { }
    virtual protected void AnimateWalk() { }
    virtual protected void AnimateAttack() { }
    virtual protected void AnimateHurt() { }
    virtual protected void AnimateDead() { }

    protected void ApplyRotations(Quaternion lArm, Quaternion rArm, Quaternion lLeg, Quaternion rLeg)
    {
        float dt = Time.deltaTime * smoothTime;
        if (armLeftPivot != null && lArm != null)
        {
            armLeftPivot.localRotation = Quaternion.Slerp(armLeftPivot.localRotation, lArm, dt);
        }
        if (armRightPivot != null && rArm != null)
        {
            armRightPivot.localRotation = Quaternion.Slerp(armRightPivot.localRotation, rArm, dt);
        }
        if (legLeftPivot != null && lLeg != null)
        {
            legLeftPivot.localRotation = Quaternion.Slerp(legLeftPivot.localRotation, lLeg, dt);
        }
        if (legRightPivot != null && rLeg != null)
        {
            legRightPivot.localRotation = Quaternion.Slerp(legRightPivot.localRotation, rLeg, dt);
        }

        animationElapsedTime += Time.deltaTime;
    }

    protected Bounds GetHierarchyBounds()
    {
        // headPivot, arm, hand, leg Pivot들의 자식 컴포넌트들에서 Renderer를 추출
        Renderer[] renderers = new List<Transform>
        {
            headPivot,
            armLeftPivot, armRightPivot,
            handLeftPivot, handRightPivot,
            legLeftPivot, legRightPivot
        }
        .Where(transform => transform != null)  // null이 아닌 transform만 필터링
        .SelectMany(transform => transform.GetComponentsInChildren<Renderer>())  // 각 transform의 자식에서 Renderer 추출
        .ToArray();

        // 렌더러가 하나도 없으면, 계산할 수 없으므로 크기가 0인 Bounds를 반환합니다.
        if (renderers.Length == 0)
        {
            Debug.LogWarning("GetHierarchyBounds: 지정된 오브젝트와 그 자식들 내에 Renderer 컴포넌트가 없습니다.");
            return new Bounds(transform.position, Vector3.zero);
        }

        // 첫 번째 렌더러의 경계를 초기 전체 경계(Total Bounds)로 설정합니다.
        Bounds totalBounds = renderers[0].bounds;

        // 나머지 모든 렌더러들의 경계를 순회하며 전체 경계에 포함시킵니다(Encapsulate).
        for (int i = 1; i < renderers.Length; i++)
        {
            totalBounds.Encapsulate(renderers[i].bounds);
        }

        // 최종적으로 계산된 전체 경계를 반환합니다.
        return totalBounds;
    }
}
