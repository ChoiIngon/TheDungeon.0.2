using UnityEngine;
using static VoxelCharacter;

public class Character : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private VoxelCharacter voxelCharacter;
    private Camera mainCamera;
    
    [Header("Camera Settings")]
    public float cameraDistance = 5f;
    public float cameraHeight = 2f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    private Vector3 moveDirection = Vector3.zero;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform shootPoint;
    public float projectileSpeed = 20f;
    
    void Start()
    {
        voxelCharacter = GetComponent<VoxelCharacter>();
        voxelCharacter.Build();

        GameObject weapon = new GameObject("Sword");
        Sword sword = weapon.AddComponent<Sword>();
        sword.Build(voxelCharacter.RightHand);

        // 메인 카메라 설정
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found in the scene!");
        }

        // Shoot Point 설정 (카릭터의 오른손에서 projectile 발사)
        if (shootPoint == null)
        {
            shootPoint = voxelCharacter.RightHand;
        }

        // Physics 레이어 충돌 설정: Character와 Column 충돌 무시
        int characterLayer = gameObject.layer;
        int columnLayer = LayerMask.NameToLayer("DungeonColumn");
        Physics.IgnoreLayerCollision(characterLayer, columnLayer, true);
    }

    // Update is called once per frame
    void Update()
    {
        HandleInput();
        MoveCharacter();
        UpdateCameraPosition();
    }

    void HandleInput()
    {
        if(true == Input.GetKeyDown(KeyCode.H))
        {
            voxelCharacter.PlayAnimation(VoxelCharacter.CharacterState.Hurt);
            return;
        }

        if(true == Input.GetKeyDown(KeyCode.K))
        {
            voxelCharacter.PlayAnimation(VoxelCharacter.CharacterState.Dead);
            return;
        }

        // Spacebar: Projectile 발사
        if (Input.GetKeyDown(KeyCode.Space))
        {
            voxelCharacter.PlayAnimation(VoxelCharacter.CharacterState.Attack);
            ShootProjectile();
            return;
        }

        // 이동 입력 처리
        HandleMovementInput();
    }

    /// <summary>
    /// W, A, S, D 키로 캐릭터 이동을 처리합니다.
    /// </summary>
    void HandleMovementInput()
    {
        moveDirection = Vector3.zero;
        bool isMoving = false;

        // W 키: 전진
        if (Input.GetKey(KeyCode.UpArrow))
        {
            moveDirection += transform.forward;
            isMoving = true;
        }

        // S 키: 후진
        if (Input.GetKey(KeyCode.DownArrow))
        {
            moveDirection -= transform.forward;
            isMoving = true;
        }

        // D 키: 우측
        if (Input.GetKey(KeyCode.RightArrow))
        {
            moveDirection += transform.right;
            isMoving = true;
        }

        // A 키: 좌측
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            moveDirection -= transform.right;
            isMoving = true;
        }

        // 이동 중이면 Walk 애니메이션 재생
        if (isMoving)
        {
            moveDirection.Normalize();
            voxelCharacter.PlayAnimation(VoxelCharacter.CharacterState.Walk);
        }
    }

    /// <summary>
    /// Projectile을 transform.forward 방향으로 발사합니다.
    /// </summary>
    void ShootProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Projectile Prefab이 설정되지 않았습니다!");
            return;
        }

        // 캐릭터 앞 2.0f, 위 1.0f 위치에서 projectile 생성
        Vector3 spawnPosition = transform.position + transform.forward * 2.0f + Vector3.up * 1.0f;
        
        // Projectile 인스턴스 생성
        GameObject projectileObject = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        
        // Projectile 회전 설정 (발사 방향으로)
        projectileObject.transform.rotation = Quaternion.LookRotation(transform.forward);
        
        // Rigidbody가 있으면 속도 설정
        Rigidbody rb = projectileObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;  // 기존 속도 초기화
            rb.AddForce(transform.forward * projectileSpeed, ForceMode.VelocityChange);
        }
        else
        {
            Debug.LogWarning("Projectile에 Rigidbody 컴포넌트가 없습니다!");
        }

        Debug.Log($"Projectile 발사! 위치: {spawnPosition}, 방향: {transform.forward}, 속도: {projectileSpeed}");
    }

    /// <summary>
    /// 캐릭터를 이동 방향으로 움직입니다.
    /// </summary>
    void MoveCharacter()
    {
        if (moveDirection.magnitude > 0.01f)
        {
            // 캐릭터를 이동 방향으로 회전
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }

            // 이동 벡터 계산
            Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;
            Vector3 newPosition = transform.position + movement;

            GameObject collidingObject = IsCollidingWithWall(newPosition);
            if (null != collidingObject)
            {
                // 충돌한 벽의 Z축 법선 벡터 계산
                Vector3 wallNormal = collidingObject.transform.forward;
                // Y축 무시 (수평 평면만 고려)
                wallNormal.y = 0;
                wallNormal = wallNormal.normalized;
                // 내적 계산
                float dotProduct = Vector3.Dot(movement.normalized, wallNormal);
                
                // 로그 출력
                Debug.Log($"벽 충돌: {collidingObject.name}");
                Debug.Log($"Z축 법선 벡터: {wallNormal}");
                Debug.Log($"Movement 벡터: {movement.normalized}");
                Debug.Log($"내적 결과: {dotProduct:F4}");
                if (0.0f > dotProduct && dotProduct > -1.0f)
                {
                    Vector3 projectionVector = dotProduct* wallNormal;
                    Vector3 slideVector = movement - projectionVector;
                    if (slideVector.magnitude > 0.01f)
                    {
                        Quaternion slideRotation = Quaternion.LookRotation(slideVector);
                        transform.rotation = Quaternion.Lerp(transform.rotation, slideRotation, Time.deltaTime * 5f);
                    }
                }
                return;
            }
            transform.position = newPosition;
        }
    }

    /// <summary>
    /// 주어진 위치에서 Wall(MeshCollider)과의 충돌을 감지합니다.
    /// </summary>
    private GameObject IsCollidingWithWall(Vector3 position)
    {
        // Character의 BoxCollider 가져오기
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            return null;
        }

        // 주어진 위치에서의 BoxCollider 중심과 반지름
        Vector3 boxCenter = position + boxCollider.center;
        Vector3 boxHalfExtents = boxCollider.size * 0.5f;

        int layerIndex = LayerMask.NameToLayer("DungeonTile");
        int layerMask = 1 << layerIndex;

        Collider[] colliders = Physics.OverlapBox(boxCenter, boxHalfExtents, transform.rotation, layerMask);

        foreach (Collider col in colliders)
        {
            // 자신의 collider는 무시
            if (col.gameObject == gameObject)
                continue;

            // Trigger는 무시
            if (col.isTrigger)
                continue;

            // Wall 이름을 가진 MeshCollider 확인
            if (col is MeshCollider)
            {
                return col.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// 카메라를 캐릭터 중심으로 원호를 그리며 이동하도록 업데이트합니다.
    /// 항상 일정한 거리를 유지합니다.
    /// </summary>
    void UpdateCameraPosition()
    {
        if (mainCamera == null)
            return;

        // 캐릭터의 Y축 회전(Yaw)만 추출하여 카메라 방향 계산
        float characterYaw = transform.eulerAngles.y;
        
        // Y축만 회전한 방향 벡터 생성 (pitch는 0으로 고정)
        Vector3 cameraDirection = Quaternion.Euler(0, characterYaw + 180f, 0) * Vector3.forward;
        
        // 캐릭터 중심으로부터 카메라의 목표 위치 (항상 일정한 거리 유지)
        Vector3 targetPosition = transform.position + cameraDirection * cameraDistance + Vector3.up * cameraHeight;

        // 카메라 위치를 직접 설정 (smooth 없음)
        mainCamera.transform.position = targetPosition;

        // 카메라가 캐릭터를 바라보도록 설정
        mainCamera.transform.LookAt(transform.position + Vector3.up * (cameraHeight * 0.5f));
    }
}
