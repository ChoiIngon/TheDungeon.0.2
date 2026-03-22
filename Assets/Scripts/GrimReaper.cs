using System.Collections.Generic;
using UnityEngine;

public class GrimReaper : ActorModel
{

    [Header("Color Settings")]
    public Color robeColor = new Color32(40, 40, 40, 255);
    public Color eyeColor = Color.red;
    public Color scytheHandleColor = new Color32(101, 67, 33, 255);
    public Color scytheBladeColor = new Color32(200, 200, 200, 255);

    public float baseHeight = 1.0f; // 캐릭터의 기본 높이 (로브 포함)
    [Header("Idle Animation Settings")]
    public float idleSpeed = 2.0f;
    public Vector2 idleArmAngleRange = new Vector2(5f, 10f);
    public float floatingHeight = 0.1f; // 떠다니는 높이

    [Header("Walk Animation Settings")]
    public float walkSpeed = 10.0f;
    public float walkAngle = 35.0f;

    [Header("Attack Animation Settings")]
    public float attackDuration = 0.5f;

    [Header("Animation Duration Settings")]
    public float hurtDuration = 0.3f;
    public float deathDuration = 1.5f;

    // 컨텍스트 메뉴를 통해 에디터에서 Build 함수를 실행할 수 있게 합니다.
    [ContextMenu("Build Character")]
    public void Build()
    {
        // --- 모델 생성 ---

        // 몸체 -> 긴 로브(Robe)로 변경
        GameObject robe = Primitive.CreateCube("Robe", new Vector3(0, 0, 0), new Vector3(0.9f, 1.4f, 0.6f), robeColor, this.transform);
        
        // 머리 -> 후드(Hood)로 변경
        GameObject headPivotObject = new GameObject("HeadPivot");
        headPivotObject.transform.SetParent(this.transform, false);
        headPivotObject.transform.localPosition = new Vector3(0, 0.8f, 0);
        this.headPivot = headPivotObject.transform;
        
        GameObject hood = Primitive.CreateCube("Hood", Vector3.zero, new Vector3(1.2f, 1.1f, 1.0f), robeColor, headPivot);
        Primitive.CreateCube("HoodFrontOpening", new Vector3(0, -0.1f, 0.4f), new Vector3(0.8f, 0.7f, 0.35f), Color.black, hood.transform); // 후드 안쪽 어두운 공간

        // 왼쪽 눈
        GameObject eyeLeftObj = new GameObject();
        eyeLeftObj.name = "EyeLeft";
        eyeLeftObj.transform.SetParent(hood.transform, false);
        eyeLeftObj.transform.localPosition = new Vector3(-0.2f, 0.1f, 0.6f);
        AddPointLightToEye(eyeLeftObj, eyeColor);
        
        // 오른쪽 눈
        GameObject eyeRightObj = new GameObject();
        eyeRightObj.name = "EyeRight";
        eyeRightObj.transform.SetParent(hood.transform, false);
        eyeRightObj.transform.localPosition = new Vector3(0.2f, 0.1f, 0.6f);
        AddPointLightToEye(eyeRightObj, eyeColor);

        // 팔
        GameObject armLeftPivot = new GameObject("ArmLeftPivot");
        armLeftPivot.transform.SetParent(this.transform, false);
        armLeftPivot.transform.localPosition = new Vector3(-0.5f, 0.4f, 0);
        this.armLeftPivot = armLeftPivot.transform;
        
        Primitive.CreateCube("ArmLeft", new Vector3(0, -0.3f, 0), new Vector3(0.25f, 0.6f, 0.25f), robeColor, this.armLeftPivot);
        GameObject handLeft = Primitive.CreateSphere("HandLeft", new Vector3(0, -0.6f, 0), Vector3.one * 0.25f, robeColor, this.armLeftPivot);
        this.handLeftPivot = handLeft.transform;

        GameObject armRightPivot = new GameObject("ArmRightPivot");
        armRightPivot.transform.SetParent(this.transform, false);
        armRightPivot.transform.localPosition = new Vector3(0.5f, 0.4f, 0);
        this.armRightPivot = armRightPivot.transform;
        
        Primitive.CreateCube("ArmRight", new Vector3(0, -0.3f, 0), new Vector3(0.25f, 0.6f, 0.25f), robeColor, this.armRightPivot);
        GameObject handRight = Primitive.CreateSphere("HandRight", new Vector3(0, -0.6f, 0), Vector3.one * 0.25f, robeColor, this.armRightPivot);
        this.handRightPivot = handRight.transform;

        // 다리 제거 -> 떠다니는 효과를 위해

        // --- 물리 설정 ---
        Bounds bounds = GetHierarchyBounds();
        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        Vector3 localScale = transform.lossyScale;
        collider.size = new Vector3(bounds.size.x / localScale.x, bounds.size.y / localScale.y, bounds.size.z / localScale.z);
        collider.center = transform.InverseTransformPoint(bounds.center);

        Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
        rigidbody.useGravity = false; // 중력 비활성화
        rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    /// <summary>
    /// 눈 오브젝트에 Point Light를 추가합니다.
    /// </summary>
    private void AddPointLightToEye(GameObject eyeObject, Color lightColor)
    {
        if (eyeObject == null)
        {
            return;
        }

        // Point Light 컴포넌트 추가
        Light pointLight = eyeObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = lightColor;
        pointLight.intensity = 1.5f;
        pointLight.range = 0.1f;  // 눈 사이즈만한 범위

        Debug.Log($"Point Light added to {eyeObject.name}");
    }

    override protected void AnimateIdle()
    {
        // 로컬 좌표에서 살짝 떠다니는 움직임 추가
        float floatingY = Mathf.Sin(Time.time * idleSpeed * 0.5f) * floatingHeight;
        transform.localPosition = new Vector3(transform.localPosition.x, floatingY + baseHeight, transform.localPosition.z);

        float breathe01 = (Mathf.Sin(Time.time * idleSpeed) + 1f) * 0.5f;
        float armZAngle = Mathf.Lerp(idleArmAngleRange.x, idleArmAngleRange.y, breathe01);
        Quaternion leftArmTarget = Quaternion.Euler(0, 0, -armZAngle);
        Quaternion rightArmTarget = Quaternion.Euler(0, 0, armZAngle);
        Quaternion legNeutral = Quaternion.identity;

        ApplyRotations(leftArmTarget, rightArmTarget, legNeutral, legNeutral);
    }

    override protected void AnimateWalk()
    {
        float swing = Mathf.Sin(Time.time * walkSpeed);
        float currentAngle = swing * walkAngle;
        Quaternion leftArmTarget = Quaternion.Euler(currentAngle, 0, -5f);
        Quaternion rightArmTarget = Quaternion.Euler(-currentAngle, 0, 5f);
        Quaternion legNeutral = Quaternion.identity;

        ApplyRotations(leftArmTarget, rightArmTarget, legNeutral, legNeutral);

        // 걷기 중에도 baseHeight 유지
        Vector3 currentPos = transform.position;
        transform.position = new Vector3(currentPos.x, baseHeight, currentPos.z);

        float walkHalfCycleTime = (0 < walkSpeed) ? (Mathf.PI / walkSpeed) : float.PositiveInfinity;
        if (animationElapsedTime >= walkHalfCycleTime * 2)
        {
            PlayAnimation(ActorState.Idle);
        }
    }

    override protected void AnimateAttack()
    {
        float attackProgress = Mathf.Clamp01(animationElapsedTime / attackDuration);
        
        // 애니메이션 단계 정의
        float phase1End = 0.45f;       // 1단계: 팔을 들어올림 (0 ~ 50%)
        float phase2End = 0.90f;       // 2단계: 팔을 들어올린 상태에서 멈춤 (50% ~ 80%)
        float phase3End = 0.98f;       // 3단계: 팔을 내려침 (80% ~ 90%)
        // 4단계: 공격자세 유지 (90% ~ 100%)
        
        float liftingAngle = 120.0f;
        float swingAngle = -30.0f;
        Quaternion rightArmTarget;
        Quaternion leftArmTarget = Quaternion.identity;
        Quaternion legNeutral = Quaternion.identity;

        if (attackProgress < phase1End)
        {
            // 단계 1: 팔을 들어올리기 (0 ~ 50%)
            float t = attackProgress / phase1End;
            rightArmTarget = Quaternion.Euler(-Mathf.Lerp(0f, liftingAngle, t), 0, 0);
        }
        else if (attackProgress < phase2End)
        {
            // 단계 2: 팔을 들어올린 상태에서 멈춤 (50% ~ 80%)
            rightArmTarget = Quaternion.Euler(-liftingAngle, 0, 0);
        }
        else if (attackProgress < phase3End)
        {
            // 단계 3: 팔을 내려침 (80% ~ 90%)
            float t = (attackProgress - phase2End) / (phase3End - phase2End);
            rightArmTarget = Quaternion.Euler(-Mathf.Lerp(liftingAngle, swingAngle, t), 0, 0);
        }
        else
        {
            // 단계 4: 공격자세 유지 (90% ~ 100%)
            rightArmTarget = Quaternion.Euler(swingAngle, 0, 0);
        }

        ApplyRotations(leftArmTarget, rightArmTarget, legNeutral, legNeutral);

        // 공격 중에도 baseHeight 유지
        Vector3 currentPos = transform.position;
        transform.position = new Vector3(currentPos.x, baseHeight, currentPos.z);

        if (animationElapsedTime >= attackDuration)
        {
            PlayAnimation(ActorState.Idle);
        }
    }

    override protected void AnimateHurt()
    {
        float hitProgress = Mathf.Clamp01(animationElapsedTime / hurtDuration);
        float shockPhase = 0.5f;
        Quaternion leftArmTarget, rightArmTarget;
        Quaternion legNeutral = Quaternion.identity;

        if (hitProgress < shockPhase)
        {
            float t = hitProgress / shockPhase;
            leftArmTarget = Quaternion.Euler(Mathf.Lerp(0f, -20f, t), 0, -Mathf.Lerp(0f, 45f, t));
            rightArmTarget = Quaternion.Euler(Mathf.Lerp(0f, -20f, t), 0, Mathf.Lerp(0f, 45f, t));
        }
        else
        {
            float t = (hitProgress - shockPhase) / (1f - shockPhase);
            leftArmTarget = Quaternion.Euler(Mathf.Lerp(-20f, 0f, t), 0, -Mathf.Lerp(45f, 0f, t));
            rightArmTarget = Quaternion.Euler(Mathf.Lerp(-20f, 0f, t), 0, Mathf.Lerp(45f, 0f, t));
        }

        ApplyRotations(leftArmTarget, rightArmTarget, legNeutral, legNeutral);

        // 피격 중에도 baseHeight 유지
        Vector3 currentPos = transform.position;
        transform.position = new Vector3(currentPos.x, baseHeight, currentPos.z);

        if (animationElapsedTime >= hurtDuration)
        {
            PlayAnimation(ActorState.Idle);
        }
    }

    override protected void AnimateDead()
    {
        float progress = Mathf.Clamp01(animationElapsedTime / deathDuration);
        float fallAngle = Mathf.SmoothStep(0f, -60f, progress);
        float savedYawAngle = this.transform.localEulerAngles.y;
        this.transform.localRotation = Quaternion.Euler(fallAngle, savedYawAngle, 0);

        if (headPivot != null)
        {
            headPivot.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -40f, progress), 0, 0);
        }

        Quaternion leftArmDead = Quaternion.Euler(160f, 0f, -30f);
        Quaternion rightArmDead = Quaternion.Euler(160f, 0f, 30f);
        Quaternion legNeutral = Quaternion.identity;

        ApplyRotations(leftArmDead, rightArmDead, legNeutral, legNeutral);

        // 사망 중에도 baseHeight 유지
        Vector3 currentPos = transform.position;
        transform.position = new Vector3(currentPos.x, baseHeight, currentPos.z);
    }
}

