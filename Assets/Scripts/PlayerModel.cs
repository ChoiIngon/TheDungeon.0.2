using UnityEngine;

public class PlayerModel : ActorModel
{
    public Color hairColor = new Color32(143, 87, 48, 255);
    public Color skinColor = new Color32(255, 204, 184, 255);
    public Color bodyColor = new Color32(143, 143, 143, 255);
    public Color pantsColor = new Color32(87, 48, 87, 255);
    public Color shoeColor = Color.black;
    public Color eyeColor = Color.black;

    [Header("Idle Animation Settings")]
    public float idleSpeed = 2.0f;
    public Vector2 idleArmAngleRange = new Vector2(5f, 10f);

    [Header("Walk Animation Settings")]
    public float walkSpeed = 10.0f;
    public float walkAngle = 35.0f;

    [Header("Attack Animation Settings")]
    public float attackDuration = 0.4f;

    [Header("Hurt Animation Settings")]
    public float hurtDuration = 0.3f;

    [Header("Dead Animation Settings")]
    public float deathDuration = 1.5f;

    public void Build()
    {
        GameObject headPivotObject = new GameObject("HeadPivot");
        headPivotObject.transform.SetParent(this.transform, false);
        headPivotObject.transform.localPosition = new Vector3(0, 0.9f, 0);
        this.headPivot = headPivotObject.transform;

        GameObject faceObject = Primitive.CreateCube("Face", new Vector3(0, 0, 0), new Vector3(1.2f, 1.0f, 0.8f), skinColor, headPivot);
        Primitive.CreateCube("EyeLeft", new Vector3(-0.3f, 0.1f, 0.53f), new Vector3(0.15f, 0.25f, 0.1f), eyeColor, faceObject.transform);
        Primitive.CreateCube("EyeRight", new Vector3(0.3f, 0.1f, 0.53f), new Vector3(0.15f, 0.25f, 0.1f), eyeColor, faceObject.transform);

        BuildHair(headPivot);

        GameObject bodyObject = Primitive.CreateCube( "Body", new Vector3(0, 0.2f, 0), new Vector3(0.8f, 0.4f, 0.5f), bodyColor, this.transform);
        GameObject pantsObject = Primitive.CreateCube( "Pants", new Vector3(0, -0.15f, 0), new Vector3(0.8f, 0.3f, 0.5f), pantsColor, this.transform);

        GameObject armLeftPivotObject = new GameObject("ArmLeftPivot");
        armLeftPivotObject.transform.SetParent(this.transform, false);
        armLeftPivotObject.transform.localPosition = new Vector3(-0.5f, 0.3f, 0);
        this.armLeftPivot = armLeftPivotObject.transform;

        Primitive.CreateCylinder("ArmLeft", new Vector3(0, -0.15f, 0), new Vector3(0.2f, 0.2f, 0.2f), bodyColor, this.armLeftPivot);
        GameObject handLeftObject = Primitive.CreateSphere("HandLeftPivot", new Vector3(0, -0.35f, 0), Vector3.one * 0.25f, skinColor, this.armLeftPivot);
        this.handLeftPivot = handLeftObject.transform;

        GameObject armRightPivotObject = new GameObject("ArmRightPivot");
        armRightPivotObject.transform.SetParent(this.transform, false);
        armRightPivotObject.transform.localPosition = new Vector3(0.5f, 0.3f, 0);
        this.armRightPivot = armRightPivotObject.transform;

        Primitive.CreateCylinder("ArmRight", new Vector3(0, -0.15f, 0), new Vector3(0.2f, 0.2f, 0.2f), bodyColor, this.armRightPivot);
        GameObject handRightObject = Primitive.CreateSphere("HandRightPivot", new Vector3(0, -0.35f, 0), Vector3.one * 0.25f, skinColor, this.armRightPivot);
        handRightPivot = handRightObject.transform;

        GameObject legLeftPivotObject = new GameObject("LegLeftPivot");
        legLeftPivotObject.transform.SetParent(this.transform, false);
        legLeftPivotObject.transform.localPosition = new Vector3(-0.2f, -0.3f, 0);
        this.legLeftPivot = legLeftPivotObject.transform;

        Primitive.CreateCube("LegLeftUpper", new Vector3(0, -0.075f, 0), new Vector3(0.25f, 0.15f, 0.3f), pantsColor, this.legLeftPivot);
        GameObject shoeLeft = Primitive.CreateCube("ShoeLeft", new Vector3(0, -0.2f, 0), new Vector3(0.25f, 0.15f, 0.35f), shoeColor, this.legLeftPivot);

        GameObject legRightPivotObject = new GameObject("LegRightPivot");
        legRightPivotObject.transform.SetParent(this.transform, false);
        legRightPivotObject.transform.localPosition = new Vector3(0.2f, -0.3f, 0);
        this.legRightPivot = legRightPivotObject.transform;

        Primitive.CreateCube( "LegRightUpper", new Vector3(0, -0.075f, 0), new Vector3(0.25f, 0.15f, 0.3f), pantsColor, this.legRightPivot);
        GameObject shoeRight = Primitive.CreateCube( "ShoeRight", new Vector3(0, -0.2f, 0), new Vector3(0.25f, 0.15f, 0.35f), shoeColor, this.legRightPivot);

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

        Bounds bounds = GetHierarchyBounds();
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

    override protected void AnimateAttack()
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
        
        // 공격 애니메이션이 완료되면 Idle 상태로 변경
        if (animationElapsedTime >= attackDuration)
        {
            PlayAnimation(ActorState.Idle);
        }
    }

    override protected void AnimateHurt()
    {
        // 피격 진행도 계산 (0 ~ 1)
        float hitProgress = Mathf.Clamp01(animationElapsedTime / hurtDuration);
        
        // 피격 2단계로 나누기
        float shockPhase = 0.5f;  // 충격 단계 (0 ~ 50%)
        
        Quaternion leftArmTarget, rightArmTarget;
        Quaternion legNeutral = Quaternion.identity;

        float progress = animationElapsedTime / hurtDuration;
        float headSnap = 15f * Mathf.Sin(progress * Mathf.PI);
        headPivot.localRotation = Quaternion.Euler(headSnap, 0, 0);
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
        
        // 피격 애니메이션이 완료되면 Idle 상태로 변경
        if (animationElapsedTime >= hurtDuration)
        {
            PlayAnimation(ActorState.Idle);
        }
    }

    override protected void AnimateIdle()
    {
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
        Quaternion leftLegTarget = Quaternion.Euler(-currentAngle, 0, 0);
        Quaternion rightLegTarget = Quaternion.Euler(currentAngle, 0, 0);
        ApplyRotations(leftArmTarget, rightArmTarget, leftLegTarget, rightLegTarget);

        float walkHalfCycleTime = float.PositiveInfinity;
        if(0 < walkSpeed)
        {
            walkHalfCycleTime = Mathf.PI / walkSpeed;
        }
        // 공격 애니메이션이 완료되면 Idle 상태로 변경
        if (animationElapsedTime >= walkHalfCycleTime * 2)
        {
            PlayAnimation(ActorState.Idle);
        }
    }

    override protected void AnimateDead()
    {
        // death progress 0..1
        float progress = Mathf.Clamp01(animationElapsedTime / deathDuration);

        // body falls backward (0 -> ~-100 degrees) so character lies on its back
        float fallAngle = Mathf.SmoothStep(0f, -60f, progress);

        float savedYawAngle = this.transform.localEulerAngles.y;
        this.transform.localRotation = Quaternion.Euler(fallAngle, savedYawAngle, 0);

        // head tilts with the body (so face points upward when lying on back)
        if (headPivot != null)
        {
            float headTilt = Mathf.Lerp(0f, -40f, progress);
            headPivot.localRotation = Quaternion.Euler(headTilt, 0, 0);
        }

        // arms go limp outward/above body when lying on back
        Quaternion leftArmDead = Quaternion.Euler(160f, 0f, -30f);
        Quaternion rightArmDead = Quaternion.Euler(160f, 0f, 30f);

        // legs relax outward slightly
        Quaternion leftLegDead = Quaternion.Euler(-30f, 0f, 0f);
        Quaternion rightLegDead = Quaternion.Euler(30f, 0f, 0f);

        ApplyRotations(leftArmDead, rightArmDead, leftLegDead, rightLegDead);
    }

    private void BuildHair(Transform headParent)
    {
        Primitive.CreateCube( "HairTop", new Vector3(0, 0.6f, 0), new Vector3(1.4f, 0.4f, 1.2f), hairColor, headParent);
        Primitive.CreateCube( "HairFrontUpper", new Vector3(0, 0.4f, 0.55f), new Vector3(1.3f, 0.3f, 0.2f), hairColor, headParent);
        Primitive.CreateCube( "HairBack", new Vector3(0, 0.1f, -0.5f), new Vector3(1.4f, 1.0f, 0.4f), hairColor, headParent);
        Primitive.CreateCube( "HairSideLeft", new Vector3(-0.65f, 0.1f, 0), new Vector3(0.3f, 1.0f, 1.1f), hairColor, headParent);
        Primitive.CreateCube( "HairSideRight", new Vector3(0.65f, 0.1f, 0), new Vector3(0.3f, 1.0f, 1.1f), hairColor, headParent);
    }
}