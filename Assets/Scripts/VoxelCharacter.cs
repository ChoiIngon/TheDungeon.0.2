using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.UIElements;

public class VoxelCharacter : MonoBehaviour
{
    private List<GameObject> parts = new List<GameObject>();
    private Transform armLeftPivotTransform, armRightPivotTransform, legLeftPivotTransform, legRightPivotTransform, handLeftPivotTransform, handRightPivotTransform, headPivotTransform;
   
    public enum CharacterState
    {
        Idle,    // 대기
        Walk,    // 걷기
        Attack,  // 공격
        Hurt,     // 피격
        Dead     // 사망
    }

    private CharacterState currentState = CharacterState.Idle;

    public Color hairColor = new Color32(143, 87, 48, 255);
    public Color skinColor = new Color32(255, 204, 184, 255);
    public Color bodyColor = new Color32(143, 143, 143, 255);
    public Color pantsColor = new Color32(87, 48, 87, 255);
    public Color shoeColor = Color.black;
    public Color eyeColor = Color.black;

    public Transform Head { get { return headPivotTransform; } }
    public Transform RightHand { get { return handRightPivotTransform; } }
    public Transform LeftHand { get { return handLeftPivotTransform; } }

    [Header("Idle Animation Settings")]
    public float idleSpeed = 2.0f;
    public Vector2 idleArmAngleRange = new Vector2(5f, 10f);

    [Header("Walk Animation Settings")]
    public float walkSpeed = 10.0f;
    public float walkAngle = 35.0f;

    [Header("Attack Animation Settings")]
    public float attackDuration = 0.4f;

    [Header("Animation Duration Settings")]
    public float hurtDuration = 0.3f;
    public float deathDuration = 1.5f;

    private float animationElapsedTime = 0.0f;
    private float smoothTime = 10f;
    //private float savedYawAngle = 0f;
    
    public void Update()
    {
        switch (currentState)
        {
            case CharacterState.Idle: AnimateIdle(); break;
            case CharacterState.Attack: AnimateAttack(); break;
            case CharacterState.Walk: AnimateWalk(); break;
            case CharacterState.Hurt: AnimateHurt(); break;
            case CharacterState.Dead: AnimateDead(); break;
        }
    }
    public void Build()
    {
        foreach (var part in parts)
        {
            if (null != part)
            {
                GameObject.Destroy(part);
            }
        }
        parts.Clear();

        GameObject body = Primitive.CreateCube( "Body", new Vector3(0, 0.2f, 0), new Vector3(0.8f, 0.4f, 0.5f), bodyColor, this.transform);
        GameObject pants = Primitive.CreateCube( "Pants", new Vector3(0, -0.15f, 0), new Vector3(0.8f, 0.3f, 0.5f), pantsColor, this.transform);

        GameObject headPivot = new GameObject("HeadPivot");
        headPivot.transform.SetParent(this.transform, false);
        headPivot.transform.localPosition = new Vector3(0, 0.9f, 0);
        headPivotTransform = headPivot.transform;
        parts.Add(headPivot);

        GameObject face = Primitive.CreateCube( "Face", new Vector3(0, 0, 0), new Vector3(1.2f, 1.0f, 0.8f), skinColor, headPivotTransform);
        Primitive.CreateCube( "EyeLeft", new Vector3(-0.3f, 0.1f, 0.53f), new Vector3(0.15f, 0.25f, 0.1f), eyeColor, face.transform);
        Primitive.CreateCube( "EyeRight", new Vector3(0.3f, 0.1f, 0.53f), new Vector3(0.15f, 0.25f, 0.1f), eyeColor, face.transform);

        BuildHair(headPivotTransform);

        GameObject armLeftPivot = new GameObject("ArmLeftPivot");
        armLeftPivot.transform.SetParent(this.transform, false);
        armLeftPivot.transform.localPosition = new Vector3(-0.5f, 0.3f, 0);
        armLeftPivotTransform = armLeftPivot.transform;
        Primitive.CreateCylinder( "ArmLeft", new Vector3(0, -0.15f, 0), new Vector3(0.2f, 0.2f, 0.2f), bodyColor, armLeftPivotTransform);
        GameObject handLeft = Primitive.CreateSphere("HandLeft", new Vector3(0, -0.35f, 0), Vector3.one * 0.25f, skinColor, armLeftPivotTransform);
        handLeftPivotTransform = handLeft.transform;
        parts.Add(armLeftPivot);

        GameObject armRightPivot = new GameObject("ArmRightPivot");
        armRightPivot.transform.SetParent(this.transform, false);
        armRightPivot.transform.localPosition = new Vector3(0.5f, 0.3f, 0);
        armRightPivotTransform = armRightPivot.transform;
        Primitive.CreateCylinder( "ArmRight", new Vector3(0, -0.15f, 0), new Vector3(0.2f, 0.2f, 0.2f), bodyColor, armRightPivotTransform);
        GameObject handRight = Primitive.CreateSphere("HandRight", new Vector3(0, -0.35f, 0), Vector3.one * 0.25f, skinColor, armRightPivotTransform);
        handRightPivotTransform = handRight.transform;
        parts.Add(armRightPivot);

        GameObject legLeftPivot = new GameObject("LegLeftPivot");
        legLeftPivot.transform.SetParent(this.transform, false);
        // initial approximate pivot position;  will adjust so shoe bottom sits at y=0
        legLeftPivot.transform.localPosition = new Vector3(-0.2f, -0.3f, 0);
        legLeftPivotTransform = legLeftPivot.transform;
        Primitive.CreateCube( "LegLeftUpper", new Vector3(0, -0.075f, 0), new Vector3(0.25f, 0.15f, 0.3f), pantsColor, legLeftPivotTransform);
        GameObject shoeLeft = Primitive.CreateCube( "ShoeLeft", new Vector3(0, -0.2f, 0), new Vector3(0.25f, 0.15f, 0.35f), shoeColor, legLeftPivotTransform);
        parts.Add(legLeftPivot);

        GameObject legRightPivot = new GameObject("LegRightPivot");
        legRightPivot.transform.SetParent(this.transform, false);
        legRightPivot.transform.localPosition = new Vector3(0.2f, -0.3f, 0);
        legRightPivotTransform = legRightPivot.transform;
        Primitive.CreateCube( "LegRightUpper", new Vector3(0, -0.075f, 0), new Vector3(0.25f, 0.15f, 0.3f), pantsColor, legRightPivotTransform);
        GameObject shoeRight = Primitive.CreateCube( "ShoeRight", new Vector3(0, -0.2f, 0), new Vector3(0.25f, 0.15f, 0.35f), shoeColor, legRightPivotTransform);
        parts.Add(legRightPivot);

        // Adjust all child local positions so that the lowest shoe bottom sits at y=0
        float minShoeBottomY = float.PositiveInfinity;
        if (shoeLeft != null)
        {
            float bottom = shoeLeft.transform.position.y - (shoeLeft.transform.lossyScale.y * 0.5f);
            minShoeBottomY = Mathf.Min(minShoeBottomY, bottom);
        }
        if (shoeRight != null)
        {
            float bottom = shoeRight.transform.position.y - (shoeRight.transform.lossyScale.y * 0.5f);
            minShoeBottomY = Mathf.Min(minShoeBottomY, bottom);
        }

        if (minShoeBottomY != float.PositiveInfinity)
        {
            float delta = minShoeBottomY; // amount above ground
            // move every direct child of character down by delta to place shoe bottom at y=0
            for (int i = 0; i < this.transform.childCount; i++)
            {
                Transform child = this.transform.GetChild(i);
                child.localPosition = new Vector3(child.localPosition.x, child.localPosition.y - delta, child.localPosition.z);
            }
        }

        Bounds bounds = GetHierarchyBounds(transform);
        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        Vector3 localScale = transform.lossyScale;
        Vector3 colliderSize;
        colliderSize.x = bounds.size.x / Mathf.Max(Mathf.Abs(localScale.x), 1e-6f);
        colliderSize.y = bounds.size.y / Mathf.Max(Mathf.Abs(localScale.y), 1e-6f);
        colliderSize.z = bounds.size.z / Mathf.Max(Mathf.Abs(localScale.z), 1e-6f);
        collider.size = colliderSize;
        collider.center = transform.InverseTransformPoint(bounds.center);

        Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
        rigidbody.useGravity = true;
        rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
    }

    public void PlayAnimation(CharacterState state)
    {
        if(currentState == state)
        {
            return;
        }

        currentState = state;
        animationElapsedTime = 0.0f;
    }

    private void AnimateAttack()
    {
        // 공격 진행도 계산 (0 ~ 1)
        float attackProgress = Mathf.Clamp01(animationElapsedTime / attackDuration);
        
        // 공격 3단계로 나누기
        float preparePhase = 0.3f;  // 준비 단계 (0 ~ 10%)
        float strikePhase = 0.7f;   // 공격 단계 (10% ~ 70%)
        float liftingAngle = 120.0f; // 팔을 들어올리는 최대 각도
        float swingAngle = -30.0f;   // 팔을 내리는 각도

        Quaternion rightArmTarget;
        Quaternion leftArmTarget = Quaternion.identity;
        Quaternion legNeutral = Quaternion.identity;
        
        if (attackProgress < preparePhase)
        {
            // 단계 1: 팔을 뒤로 들어올리기 (준비 동작) - 방향 반전
            float t = attackProgress / preparePhase;
            float raiseAngle = Mathf.Lerp(0f, liftingAngle, t);  // 0도에서 liftingAngle도로 들어 올림
            rightArmTarget = Quaternion.Euler(-raiseAngle, 0, 0);
        }
        else if (attackProgress < strikePhase)
        {
            // 단계 2: 팔을 강하게 내려치기 (공격 동작) - 방향 반전
            float t = (attackProgress - preparePhase) / (strikePhase - preparePhase);
            float strikeAngle = Mathf.Lerp(liftingAngle, swingAngle, t);  // 내부 보간은 동일하지만 부호 반전 적용
            rightArmTarget = Quaternion.Euler(-strikeAngle, 0, 0);
        }
        else
        {
            // 단계 3: 원래 위치로 복귀 - 방향 반전
            float t = (attackProgress - strikePhase) / (1f - strikePhase);
            float returnAngle = Mathf.Lerp(swingAngle, 0f, t);
            rightArmTarget = Quaternion.Euler(-returnAngle, 0, 0);
        }
        
        ApplyRotations(leftArmTarget, rightArmTarget, legNeutral, legNeutral);
        
        // 애니메이션 시간 업데이트
        animationElapsedTime += Time.deltaTime;
        
        // 공격 애니메이션이 완료되면 Idle 상태로 변경
        if (animationElapsedTime >= attackDuration)
        {
            PlayAnimation(CharacterState.Idle);
        }
    }

    void AnimateHurt()
    {
        // 피격 진행도 계산 (0 ~ 1)
        float hitProgress = Mathf.Clamp01(animationElapsedTime / hurtDuration);
        
        // 피격 2단계로 나누기
        float shockPhase = 0.5f;  // 충격 단계 (0 ~ 50%)
        
        Quaternion leftArmTarget, rightArmTarget;
        Quaternion legNeutral = Quaternion.identity;

        float progress = animationElapsedTime / hurtDuration;
        float headSnap = 15f * Mathf.Sin(progress * Mathf.PI);
        headPivotTransform.localRotation = Quaternion.Euler(headSnap, 0, 0);
        // body tilts opposite the head snap for visual impact
        float bodyTilt = -headSnap * 0.5f;
        // combine saved yaw with new pitch tilt so facing direction remains the same

        float savedYawAngle = this.transform.localEulerAngles.y;
        this.transform.localRotation = Quaternion.Euler(bodyTilt, savedYawAngle, 0);

        if (hitProgress < shockPhase)
        {
            // 단계 1: 충격 받은 순간 팔이 벌어지고 뒤로 흔들림 (0 ~ 50%)
            float t = hitProgress / shockPhase;
            float armSpreadAngle = Mathf.Lerp(0f, 45f, t);  // 팔을 양쪽으로 벌어짐
            float bodyTiltAngle = Mathf.Lerp(0f, -20f, t);  // 몸이 뒤로 흔들림
            
            leftArmTarget = Quaternion.Euler(bodyTiltAngle, 0, -armSpreadAngle);
            rightArmTarget = Quaternion.Euler(bodyTiltAngle, 0, armSpreadAngle);
        }
        else
        {
            // 단계 2: 충격에서 복구되며 원래 위치로 돌아감 (50% ~ 100%)
            float t = (hitProgress - shockPhase) / (1f - shockPhase);
            float armSpreadAngle = Mathf.Lerp(45f, 0f, t);  // 팔이 원래 위치로
            float bodyTiltAngle = Mathf.Lerp(-20f, 0f, t);  // 몸이 원래 위치로
            
            leftArmTarget = Quaternion.Euler(bodyTiltAngle, 0, -armSpreadAngle);
            rightArmTarget = Quaternion.Euler(bodyTiltAngle, 0, armSpreadAngle);
        }
        
        ApplyRotations(leftArmTarget, rightArmTarget, legNeutral, legNeutral);
        
        // 애니메이션 시간 업데이트
        animationElapsedTime += Time.deltaTime;
        
        // 피격 애니메이션이 완료되면 Idle 상태로 변경
        if (animationElapsedTime >= hurtDuration)
        {
            PlayAnimation(CharacterState.Idle);
        }
    }

    void AnimateIdle()
    {
        float breathe01 = (Mathf.Sin(Time.time * idleSpeed) + 1f) * 0.5f;
        float armZAngle = Mathf.Lerp(idleArmAngleRange.x, idleArmAngleRange.y, breathe01);
        Quaternion leftArmTarget = Quaternion.Euler(0, 0, -armZAngle);
        Quaternion rightArmTarget = Quaternion.Euler(0, 0, armZAngle);
        Quaternion legNeutral = Quaternion.identity;
        ApplyRotations(leftArmTarget, rightArmTarget, legNeutral, legNeutral);
    }

    void AnimateWalk()
    {
        float swing = Mathf.Sin(Time.time * walkSpeed);
        float currentAngle = swing * walkAngle;
        Quaternion leftArmTarget = Quaternion.Euler(currentAngle, 0, -5f);
        Quaternion rightArmTarget = Quaternion.Euler(-currentAngle, 0, 5f);
        Quaternion leftLegTarget = Quaternion.Euler(-currentAngle, 0, 0);
        Quaternion rightLegTarget = Quaternion.Euler(currentAngle, 0, 0);
        ApplyRotations(leftArmTarget, rightArmTarget, leftLegTarget, rightLegTarget);

        animationElapsedTime += Time.deltaTime;

        float walkHalfCycleTime = float.PositiveInfinity;
        if(0 < walkSpeed)
        {
            walkHalfCycleTime = Mathf.PI / walkSpeed;
        }
        // 공격 애니메이션이 완료되면 Idle 상태로 변경
        if (animationElapsedTime >= walkHalfCycleTime * 2)
        {
            PlayAnimation(CharacterState.Idle);
        }
    }

    void AnimateDead()
    {
        // death progress 0..1
        float progress = Mathf.Clamp01(animationElapsedTime / deathDuration);

        // body falls backward (0 -> ~-100 degrees) so character lies on its back
        float fallAngle = Mathf.SmoothStep(0f, -60f, progress);

        float savedYawAngle = this.transform.localEulerAngles.y;
        this.transform.localRotation = Quaternion.Euler(fallAngle, savedYawAngle, 0);

        // head tilts with the body (so face points upward when lying on back)
        if (headPivotTransform != null)
        {
            float headTilt = Mathf.Lerp(0f, -40f, progress);
            headPivotTransform.localRotation = Quaternion.Euler(headTilt, 0, 0);
        }

        // arms go limp outward/above body when lying on back
        Quaternion leftArmDead = Quaternion.Euler(160f, 0f, -30f);
        Quaternion rightArmDead = Quaternion.Euler(160f, 0f, 30f);

        // legs relax outward slightly
        Quaternion leftLegDead = Quaternion.Euler(-30f, 0f, 0f);
        Quaternion rightLegDead = Quaternion.Euler(30f, 0f, 0f);

        ApplyRotations(leftArmDead, rightArmDead, leftLegDead, rightLegDead);

        // advance timer but do not revert state when finished
        animationElapsedTime += Time.deltaTime;
    }

    void ApplyRotations(Quaternion lArm, Quaternion rArm, Quaternion lLeg, Quaternion rLeg)
    {
        float dt = Time.deltaTime * smoothTime;
        if (armLeftPivotTransform != null)
        {
            armLeftPivotTransform.localRotation = Quaternion.Slerp(armLeftPivotTransform.localRotation, lArm, dt);
        }
        if (armRightPivotTransform != null)
        {
            armRightPivotTransform.localRotation = Quaternion.Slerp(armRightPivotTransform.localRotation, rArm, dt);
        }
        if (legLeftPivotTransform != null)
        {
            legLeftPivotTransform.localRotation = Quaternion.Slerp(legLeftPivotTransform.localRotation, lLeg, dt);
        }
        if (legRightPivotTransform != null)
        {
            legRightPivotTransform.localRotation = Quaternion.Slerp(legRightPivotTransform.localRotation, rLeg, dt);
        }
    }

    void BuildHair(Transform headParent)
    {
        Primitive.CreateCube( "HairTop", new Vector3(0, 0.6f, 0), new Vector3(1.4f, 0.4f, 1.2f), hairColor, headParent);
        Primitive.CreateCube( "HairFrontUpper", new Vector3(0, 0.4f, 0.55f), new Vector3(1.3f, 0.3f, 0.2f), hairColor, headParent);
        Primitive.CreateCube( "HairBack", new Vector3(0, 0.1f, -0.5f), new Vector3(1.4f, 1.0f, 0.4f), hairColor, headParent);
        Primitive.CreateCube( "HairSideLeft", new Vector3(-0.65f, 0.1f, 0), new Vector3(0.3f, 1.0f, 1.1f), hairColor, headParent);
        Primitive.CreateCube( "HairSideRight", new Vector3(0.65f, 0.1f, 0), new Vector3(0.3f, 1.0f, 1.1f), hairColor, headParent);
    }

    float GetHeight()
    {
        var (yMin, yMax) = GetHeight(this.transform);
        return yMax - yMin;
    }

    (float yMin, float yMax) GetHeight(Transform transform)
    {
        float yMin = float.PositiveInfinity;
        float yMax = float.NegativeInfinity;

        for(int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            var (childYMin, childYMax) = GetHeight(child);
            yMin = Mathf.Min(yMin, child.position.y - (child.lossyScale.y * 0.5f));
            yMin = Mathf.Min(yMin, childYMin);
            yMax = Mathf.Max(yMax, child.position.y + (child.lossyScale.y * 0.5f));
            yMax = Mathf.Max(yMax, childYMax);
        }
        return (yMin, yMax);
    }

    public static Bounds GetHierarchyBounds(Transform root)
    {
        // 1. root와 그 아래 모든 자식들로부터 Renderer 컴포넌트를 전부 가져옵니다.
        // 이 함수가 재귀 탐색의 역할을 대신해 줍니다.
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

        // 2. 렌더러가 하나도 없으면, 계산할 수 없으므로 크기가 0인 Bounds를 반환합니다.
        if (renderers.Length == 0)
        {
            Debug.LogWarning("GetHierarchyBounds: 지정된 오브젝트와 그 자식들 내에 Renderer 컴포넌트가 없습니다.", root);
            return new Bounds(root.position, Vector3.zero);
        }

        // 3. 첫 번째 렌더러의 경계를 초기 전체 경계(Total Bounds)로 설정합니다.
        Bounds totalBounds = renderers[0].bounds;

        // 4. 나머지 모든 렌더러들의 경계를 순회하며 전체 경계에 포함시킵니다(Encapsulate).
        // Encapsulate 메서드는 기존 Bounds를 확장하여 파라미터로 받은 다른 Bounds를
        // 완전히 포함하도록 만듭니다. 이것이 경계를 병합하는 가장 효율적인 방법입니다.
        for (int i = 1; i < renderers.Length; i++)
        {
            totalBounds.Encapsulate(renderers[i].bounds);
        }

        // 5. 최종적으로 계산된 전체 경계를 반환합니다.
        return totalBounds;
    }
}
