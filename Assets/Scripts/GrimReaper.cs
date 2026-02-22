using System.Collections.Generic;
using UnityEngine;

public class GrimReaper : MonoBehaviour
{
    private List<GameObject> parts = new List<GameObject>();
    private Transform armLeftPivotTransform, armRightPivotTransform, legLeftPivotTransform, legRightPivotTransform, handLeftPivotTransform, handRightPivotTransform, headPivotTransform;

    public enum CharacterState
    {
        Idle,
        Walk,
        Attack,
        Hurt,
        Dead
    }
    private CharacterState currentState = CharacterState.Idle;

    [Header("Color Settings")]
    public Color robeColor = new Color32(40, 40, 40, 255);
    public Color eyeColor = Color.red;
    public Color scytheHandleColor = new Color32(101, 67, 33, 255);
    public Color scytheBladeColor = new Color32(200, 200, 200, 255);

    public Transform Head { get { return headPivotTransform; } }
    public Transform RightHand { get { return handRightPivotTransform; } }
    public Transform LeftHand { get { return handLeftPivotTransform; } }

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

    private float animationElapsedTime = 0.0f;
    private float smoothTime = 10f;
    private Vector3 initialPosition;

    // 컨텍스트 메뉴를 통해 에디터에서 Build 함수를 실행할 수 있게 합니다.
    [ContextMenu("Build Character")]
    public void Build()
    {
        // 기존 파츠 제거
        foreach (var part in parts)
        {
            if (null != part)
            {
                // 에디터 모드와 플레이 모드 모두에서 작동하도록 DestroyImmediate 사용
                GameObject.DestroyImmediate(part);
            }
        }
        parts.Clear();

        // 기존 컴포넌트 제거 (재생성 위함)
        foreach (var component in GetComponents<Collider>()) { DestroyImmediate(component); }
        foreach (var component in GetComponents<Rigidbody>()) { DestroyImmediate(component); }


        // --- 모델 생성 ---

        // 몸체 -> 긴 로브(Robe)로 변경
        GameObject robe = Primitive.CreateCube("Robe", new Vector3(0, 0, 0), new Vector3(0.9f, 1.4f, 0.6f), robeColor, this.transform);
        parts.Add(robe);

        // 머리 -> 후드(Hood)로 변경
        GameObject headPivot = new GameObject("HeadPivot");
        headPivot.transform.SetParent(this.transform, false);
        headPivot.transform.localPosition = new Vector3(0, 0.8f, 0);
        headPivotTransform = headPivot.transform;
        parts.Add(headPivot);

        GameObject hood = Primitive.CreateCube("Hood", Vector3.zero, new Vector3(1.2f, 1.1f, 1.0f), robeColor, headPivotTransform);
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
        armLeftPivotTransform = armLeftPivot.transform;
        parts.Add(armLeftPivot);
        Primitive.CreateCube("ArmLeft", new Vector3(0, -0.3f, 0), new Vector3(0.25f, 0.6f, 0.25f), robeColor, armLeftPivotTransform);
        GameObject handLeft = Primitive.CreateSphere("HandLeft", new Vector3(0, -0.6f, 0), Vector3.one * 0.25f, robeColor, armLeftPivotTransform);
        handLeftPivotTransform = handLeft.transform;

        GameObject armRightPivot = new GameObject("ArmRightPivot");
        armRightPivot.transform.SetParent(this.transform, false);
        armRightPivot.transform.localPosition = new Vector3(0.5f, 0.4f, 0);
        armRightPivotTransform = armRightPivot.transform;
        parts.Add(armRightPivot);
        Primitive.CreateCube("ArmRight", new Vector3(0, -0.3f, 0), new Vector3(0.25f, 0.6f, 0.25f), robeColor, armRightPivotTransform);
        GameObject handRight = Primitive.CreateSphere("HandRight", new Vector3(0, -0.6f, 0), Vector3.one * 0.25f, robeColor, armRightPivotTransform);
        handRightPivotTransform = handRight.transform;

        // 낫 생성 및 오른손에 장착
        BuildScythe(handRightPivotTransform);

        // 다리 제거 -> 떠다니는 효과를 위해

        // --- 물리 설정 ---
        Bounds bounds = GetHierarchyBounds(transform);
        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        Vector3 localScale = transform.lossyScale;
        collider.size = new Vector3(bounds.size.x / localScale.x, bounds.size.y / localScale.y, bounds.size.z / localScale.z);
        collider.center = transform.InverseTransformPoint(bounds.center);

        Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
        rigidbody.useGravity = false; // 중력 비활성화
        rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

        initialPosition = transform.position;
    }

    private void BuildScythe(Transform parent)
    {
        GameObject scythe = new GameObject("Scythe");
        scythe.transform.SetParent(parent, false);
        scythe.transform.localPosition = new Vector3(0.55f, 0.4f, 0.77f);
        scythe.transform.localRotation = Quaternion.Euler(-20.0f, -70.0f, -55.0f);
        parts.Add(scythe);

        // 낫 자루
        Primitive.CreateCube("ScytheHandle", new Vector3(0, 0, 0), new Vector3(0.3f, 8f, 0.3f), scytheHandleColor, scythe.transform);

        // 낫 칼날
        GameObject bladeRoot = new GameObject("BladeRoot");
        bladeRoot.transform.SetParent(scythe.transform, false);
        bladeRoot.transform.localPosition = new Vector3(0, 3.6f, 0);

        GameObject bladePart1 = Primitive.CreateCube("BladePart1", new Vector3(1.2f, 0.24f, 0), new Vector3(2.0f, 0.7f, 0.1f), scytheBladeColor, bladeRoot.transform);
        bladePart1.transform.localRotation = Quaternion.Euler(0, 0, 20);

        GameObject bladePart2 = Primitive.CreateCube("BladePart2", new Vector3(2.65f, 0.22f, 0), new Vector3(2.0f, 0.6f, 0.1f), scytheBladeColor, bladeRoot.transform);
        bladePart2.transform.localRotation = Quaternion.Euler(0, 0, -25);
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

    // ... (이하 애니메이션 코드는 VoxelCharacter와 거의 동일, 다리 부분만 제거)

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

    public void PlayAnimation(CharacterState state)
    {
        if (currentState == state) return;
        currentState = state;
        animationElapsedTime = 0.0f;
    }

    void AnimateIdle()
    {
        // 로컬 좌표에서 살짝 떠다니는 움직임 추가
        float floatingY = Mathf.Sin(Time.time * idleSpeed * 0.5f) * floatingHeight;
        transform.localPosition = new Vector3(transform.localPosition.x, floatingY, transform.localPosition.z);

        float breathe01 = (Mathf.Sin(Time.time * idleSpeed) + 1f) * 0.5f;
        float armZAngle = Mathf.Lerp(idleArmAngleRange.x, idleArmAngleRange.y, breathe01);
        Quaternion leftArmTarget = Quaternion.Euler(0, 0, -armZAngle);
        Quaternion rightArmTarget = Quaternion.Euler(0, 0, armZAngle);

        ApplyRotations(leftArmTarget, rightArmTarget);
    }

    void AnimateWalk()
    {
        float swing = Mathf.Sin(Time.time * walkSpeed);
        float currentAngle = swing * walkAngle;
        Quaternion leftArmTarget = Quaternion.Euler(currentAngle, 0, -5f);
        Quaternion rightArmTarget = Quaternion.Euler(-currentAngle, 0, 5f);

        ApplyRotations(leftArmTarget, rightArmTarget);
        animationElapsedTime += Time.deltaTime;

        float walkHalfCycleTime = (0 < walkSpeed) ? (Mathf.PI / walkSpeed) : float.PositiveInfinity;
        if (animationElapsedTime >= walkHalfCycleTime * 2)
        {
            PlayAnimation(CharacterState.Idle);
        }
    }

    private void AnimateAttack()
    {
        float attackProgress = Mathf.Clamp01(animationElapsedTime / attackDuration);
        float preparePhase = 0.3f;
        float strikePhase = 0.7f;
        float liftingAngle = 120.0f;
        float swingAngle = -30.0f;
        Quaternion rightArmTarget;
        Quaternion leftArmTarget = Quaternion.identity;

        if (attackProgress < preparePhase)
        {
            float t = attackProgress / preparePhase;
            rightArmTarget = Quaternion.Euler(-Mathf.Lerp(0f, liftingAngle, t), 0, 0);
        }
        else if (attackProgress < strikePhase)
        {
            float t = (attackProgress - preparePhase) / (strikePhase - preparePhase);
            rightArmTarget = Quaternion.Euler(-Mathf.Lerp(liftingAngle, swingAngle, t), 0, 0);
        }
        else
        {
            float t = (attackProgress - strikePhase) / (1f - strikePhase);
            rightArmTarget = Quaternion.Euler(-Mathf.Lerp(swingAngle, 0f, t), 0, 0);
        }

        ApplyRotations(leftArmTarget, rightArmTarget);

        animationElapsedTime += Time.deltaTime;
        if (animationElapsedTime >= attackDuration)
        {
            PlayAnimation(CharacterState.Idle);
        }
    }

    void AnimateHurt()
    {
        float hitProgress = Mathf.Clamp01(animationElapsedTime / hurtDuration);
        float shockPhase = 0.5f;
        Quaternion leftArmTarget, rightArmTarget;

        // ... (Hurt 애니메이션 로직은 VoxelCharacter와 동일)
        // ...

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

        ApplyRotations(leftArmTarget, rightArmTarget);

        animationElapsedTime += Time.deltaTime;
        if (animationElapsedTime >= hurtDuration)
        {
            PlayAnimation(CharacterState.Idle);
        }
    }

    void AnimateDead()
    {
        float progress = Mathf.Clamp01(animationElapsedTime / deathDuration);
        float fallAngle = Mathf.SmoothStep(0f, -60f, progress);
        float savedYawAngle = this.transform.localEulerAngles.y;
        this.transform.localRotation = Quaternion.Euler(fallAngle, savedYawAngle, 0);

        if (headPivotTransform != null)
        {
            headPivotTransform.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -40f, progress), 0, 0);
        }

        Quaternion leftArmDead = Quaternion.Euler(160f, 0f, -30f);
        Quaternion rightArmDead = Quaternion.Euler(160f, 0f, 30f);

        ApplyRotations(leftArmDead, rightArmDead);

        animationElapsedTime += Time.deltaTime;
    }

    // 다리 파라미터가 없는 ApplyRotations
    void ApplyRotations(Quaternion lArm, Quaternion rArm)
    {
        float dt = Time.deltaTime * smoothTime;
        if (armLeftPivotTransform != null)
            armLeftPivotTransform.localRotation = Quaternion.Slerp(armLeftPivotTransform.localRotation, lArm, dt);
        if (armRightPivotTransform != null)
            armRightPivotTransform.localRotation = Quaternion.Slerp(armRightPivotTransform.localRotation, rArm, dt);
    }

    // VoxelCharacter의 GetHierarchyBounds 헬퍼 함수
    public Bounds GetHierarchyBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(root.position, Vector3.zero);
        }
        Bounds totalBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            totalBounds.Encapsulate(renderers[i].bounds);
        }
        return totalBounds;
    }
}

