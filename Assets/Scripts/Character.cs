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

    [Header("Direction Indicator Settings")]
    private GameObject directionIndicator;
    private Transform directionIndicatorTransform;

    [Header("Mouse Control Settings")]
    public float mouseSensitivity = 2f;

    [Header("Interaction Settings")]
    public float interactionDistance = 10f;
    
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

        // 마우스 커서 잠금 및 숨김 설정
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 방향 지시자(삼각형) 생성
        CreateDirectionIndicator();
    }

    /// <summary>
    /// 캐릭터의 머리 위에 방향을 가리키는 삼각형 객체를 생성합니다.
    /// </summary>
    private void CreateDirectionIndicator()
    {
        // 삼각형 메쉬 생성
        Mesh triangleMesh = new Mesh();
        triangleMesh.name = "DirectionTriangle";

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(0, 0, 5.0f),    // 위쪽 꼭짓점 (앞쪽)
            new Vector3(2.5f, 0, -2.5f),   // 오른쪽 아래
            new Vector3(-2.5f, 0, -2.5f), // 왼쪽 아래
        };

        // 삼각형의 인덱스 정의
        int[] triangles = new int[] { 0, 1, 2 };

        // 메쉬에 데이터 할당
        triangleMesh.vertices = vertices;
        triangleMesh.triangles = triangles;
        triangleMesh.RecalculateNormals();
        triangleMesh.RecalculateBounds();

        // 게임 오브젝트 생성
        directionIndicator = new GameObject("DirectionIndicator");
        directionIndicator.transform.SetParent(voxelCharacter.Head, false);
        directionIndicator.transform.localPosition = new Vector3(0, 6.0f, 0);
        directionIndicatorTransform = directionIndicator.transform;

        // MeshFilter 추가
        MeshFilter meshFilter = directionIndicator.AddComponent<MeshFilter>();
        meshFilter.mesh = triangleMesh;

        // MeshRenderer 추가
        MeshRenderer meshRenderer = directionIndicator.AddComponent<MeshRenderer>();
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = Color.red;
        meshRenderer.material = material;

        Debug.Log("Direction indicator created successfully!");
    }

    // Update is called once per frame
    void Update()
    {
        HandleInput();
        MoveCharacter();
        UpdateCameraPosition();
        UpdateDirectionIndicator();
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

        // ESC 키: 마우스 락 토글
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMouseLock();
            return;
        }

        // 스페이스 키: 상호작용
        if (Input.GetKeyDown(KeyCode.Space))
        {
            InteractObject();
            return;
        }

        // 마우스 왼쪽 클릭: Projectile 발사
        if (Input.GetMouseButtonDown(0))
        {
            voxelCharacter.PlayAnimation(VoxelCharacter.CharacterState.Attack);
            ShootProjectile();
            return;
        }

        // 마우스 이동으로 캐릭터 방향 조절
        HandleMouseRotation();

        // 이동 입력 처리
        HandleMovementInput();
    }

    /// <summary>
    /// 마우스 락 상태를 토글합니다 (ESC 키로 실행).
    /// </summary>
    void ToggleMouseLock()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("Mouse unlocked");
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("Mouse locked");
        }
    }

    /// <summary>
    /// 마우스 좌우 이동으로 캐릭터의 방향을 조절합니다.
    /// </summary>
    void HandleMouseRotation()
    {
        float mouseX = Input.GetAxis("Mouse X");
        
        if (Mathf.Abs(mouseX) > 0.01f)
        {
            // 마우스 X 움직임에 따라 Y축 회전
            float rotationAmount = mouseX * mouseSensitivity;
            transform.Rotate(0, rotationAmount, 0, Space.Self);
        }
    }

    /// <summary>
    /// W, A, S, D 키로 캐릭터 이동을 처리합니다.
    /// </summary>
    void HandleMovementInput()
    {
        moveDirection = Vector3.zero;
        bool isMoving = false;

        // W 키: 전진
        if (Input.GetKey(KeyCode.W))
        {
            moveDirection += transform.forward;
            isMoving = true;
        }

        // S 키: 후진
        if (Input.GetKey(KeyCode.S))
        {
            moveDirection -= transform.forward;
            isMoving = true;
        }

        // D 키: 우측
        if (Input.GetKey(KeyCode.D))
        {
            moveDirection += transform.right;
            isMoving = true;
        }

        // A 키: 좌측
        if (Input.GetKey(KeyCode.A))
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
            
            // 이동 벡터 계산
            Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;
            Vector3 newPosition = transform.position + movement;

            GameObject collidingObject = IsCollidingWithWall(newPosition);
            if (null != collidingObject)
            {
                // 충돌한 벽의 Z축 법선 벡터 계산
                if (Input.GetKey(KeyCode.W))
                {
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
                        Vector3 projectionVector = dotProduct * wallNormal;
                        Vector3 slideVector = movement - projectionVector;
                        if (slideVector.magnitude > 0.01f)
                        {
                            Quaternion slideRotation = Quaternion.LookRotation(slideVector);
                            transform.rotation = Quaternion.Lerp(transform.rotation, slideRotation, Time.deltaTime * 5f);
                        }
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

    /// <summary>
    /// 방향 지시자(삼각형)를 캐릭터의 forward 방향에 맞게 회전시킵니다.
    /// </summary>
    private void UpdateDirectionIndicator()
    {
        if (directionIndicatorTransform == null)
            return;

        // 삼각형이 항상 캐릭터의 forward 방향을 가리키도록 설정
        // 부모(머리)의 로컬 좌표계에서 회전
        directionIndicatorTransform.localRotation = Quaternion.identity;
    }

    private void InteractObject()
    {
        // 캐릭터의 중심에서 전방으로 레이캐스팅 시작
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 rayDirection = transform.forward;

        RaycastHit hit;
        bool hasHit = Physics.Raycast(rayOrigin, rayDirection, out hit, interactionDistance);

        if (hasHit)
        {
            Debug.Log($"상호작용 오브젝트 탐지됨: {hit.collider.gameObject.name}");
            Debug.Log($"거리: {hit.distance:F2}");
            Debug.Log($"위치: {hit.point}");

            // 탐지된 오브젝트에서 IInteractable 인터페이스 확인
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact();
                Debug.Log($"상호작용 실행: {hit.collider.gameObject.name}");
            }
            else
            {
                Debug.Log($"오브젝트 '{hit.collider.gameObject.name}'에 상호작용 컴포넌트가 없습니다.");
            }
        }
        else
        {
            Debug.Log($"상호작용 범위({interactionDistance}m) 내에 오브젝트가 없습니다.");
        }

        // 디버그: 레이캐스트 시각화
        Debug.DrawRay(rayOrigin, rayDirection * interactionDistance, hasHit ? Color.green : Color.red, 1f);
    }
}

