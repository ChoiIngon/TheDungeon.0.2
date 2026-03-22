# ChromaDB Document Index Examples

현재 프로젝트 기준으로 ChromaDB에 저장될 문서 예제

---

## 1. CameraOcclusion 클래스 - 전체 시스템 문서

### Document ID
```
camera-occlusion-system-001
```

### Document Content
```json
{
  "id": "camera-occlusion-system-001",
  "file_path": "Assets/Scripts/CameraOcclusion.cs",
  "class_name": "CameraOcclusion",
  "document_type": "system_overview",
  "content": "URP 환경에서 카메라와 플레이어 사이의 오브젝트를 투명하게 만드는 카메라 가림막 처리 시스템. URP/Lit 셰이더의 렌더링 모드를 런타임에 Opaque에서 Transparent로 동적 변경하며, 원본 머티리얼 백업 및 복원 기능을 제공합니다.",
  "system_category": "Rendering/Camera",
  "responsibilities": [
    "카메라 시점 가림막 감지",
    "동적 머티리얼 투명화",
    "URP 셰이더 런타임 설정",
    "머티리얼 상태 관리"
  ],
  "key_features": [
    "BoxCastAll을 이용한 효율적 오브젝트 피킹",
    "프레임별 상태 비교로 불필요한 처리 최소화",
    "머티리얼 백업으로 안전한 복원",
    "LayerMask를 통한 성능 최적화"
  ],
  "dependencies": [
    "GameManager",
    "Player (Transform + BoxCollider)",
    "UnityEngine.Rendering (URP)"
  ],
  "lifecycle": "LateUpdate에서 매 프레임 실행",
  "performance_impact": "High - Physics.BoxCastAll 매 프레임 실행",
  "keywords": ["transparency", "occlusion", "camera", "urp", "shader", "rendering", "material"],
  "related_systems": ["GameManager", "Player", "Enemy"]
}
```

---

## 2. GetCurrentFrameOccluders 메서드 - 개별 메서드 문서

### Document ID
```
camera-occlusion-method-get-occluders-002
```

### Document Content
```json
{
  "id": "camera-occlusion-method-get-occluders-002",
  "file_path": "Assets/Scripts/CameraOcclusion.cs",
  "class_name": "CameraOcclusion",
  "method_name": "GetCurrentFrameOccluders",
  "document_type": "method_detail",
  "method_signature": "private HashSet<Renderer> GetCurrentFrameOccluders()",
  "content": "현재 프레임에서 카메라와 플레이어 사이를 가리는 모든 렌더러를 찾습니다. BoxCastAll을 사용하여 플레이어 콜라이더 크기 기준으로 카메라-플레이어 경로상의 모든 충돌 객체를 검사합니다.",
  "purpose": "카메라 시점 가림막 감지",
  "algorithm": [
    "1. 카메라 위치 → 플레이어 위치 방향 벡터 계산",
    "2. 박스 크기를 플레이어 콜라이더의 1/3로 설정",
    "3. Physics.BoxCastAll로 occlusionLayers 레이어 검사",
    "4. 플레이어 자식 객체 필터링",
    "5. 렌더러 컴포넌트만 추출"
  ],
  "parameters": [],
  "return_type": "HashSet<Renderer>",
  "return_description": "카메라와 플레이어 사이의 가림막 객체들의 렌더러 집합",
  "performance_notes": "Physics.BoxCastAll은 매 프레임 호출되므로 occlusionLayers 설정으로 검사 범위 최소화 필요",
  "optimization_tips": [
    "occlusionLayers에 불필요한 레이어 추가 금지",
    "플레이어 콜라이더 크기 조정으로 검사 범위 제어",
    "BoxCastAll 결과 캐싱 검토"
  ],
  "related_methods": ["MakeMaterialsTransparent", "RestoreOriginalMaterials"],
  "called_by": ["LateUpdate"],
  "keywords": ["physics", "boxcast", "detection", "occlusion", "performance"]
}
```

---

## 3. SetMaterialToTransparent_URP 메서드 - 셰이더 설정 문서

### Document ID
```
camera-occlusion-method-shader-setup-003
```

### Document Content
```json
{
  "id": "camera-occlusion-method-shader-setup-003",
  "file_path": "Assets/Scripts/CameraOcclusion.cs",
  "class_name": "CameraOcclusion",
  "method_name": "SetMaterialToTransparent_URP",
  "document_type": "method_detail",
  "method_signature": "public static void SetMaterialToTransparent_URP(Material material)",
  "content": "URP/Lit 셰이더 기반의 머티리얼을 런타임에서 Transparent 모드로 변경하는 핵심 함수입니다. Surface Type 설정, 블렌딩 모드 구성, 깊이 쓰기 비활성화, 셰이더 키워드 활성화 등의 작업을 수행합니다.",
  "purpose": "URP 머티리얼을 투명 렌더링 모드로 전환",
  "shader_operations": [
    {
      "step": 1,
      "operation": "Surface Type을 Transparent로 설정",
      "code": "material.SetFloat(\"_Surface\", 1.0f)",
      "importance": "critical"
    },
    {
      "step": 2,
      "operation": "표준 알파 블렌딩 모드 설정",
      "code": "material.SetFloat(\"_SrcBlend\", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); material.SetFloat(\"_DstBlend\", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);",
      "importance": "critical",
      "note": "_Blend는 deprecated, _SrcBlend/DstBlend 사용"
    },
    {
      "step": 3,
      "operation": "깊이 쓰기 비활성화",
      "code": "material.SetInt(\"_ZWrite\", 0)",
      "importance": "high",
      "reason": "투명 객체 뒤의 물체가 보이도록 함"
    },
    {
      "step": 4,
      "operation": "셰이더 키워드 활성화 및 렌더 큐 설정",
      "code": "material.EnableKeyword(\"_SURFACE_TYPE_TRANSPARENT\"); material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;",
      "importance": "high"
    }
  ],
  "parameters": [
    {
      "name": "material",
      "type": "Material",
      "description": "설정할 대상 머티리얼 (URP/Lit 셰이더 기반)"
    }
  ],
  "requirements": [
    "머티리얼은 URP/Lit 셰이더 기반이어야 함",
    "UnityEngine.Rendering 네임스페이스 필요"
  ],
  "troubleshooting": [
    {
      "issue": "투명화가 작동하지 않음",
      "solutions": [
        "머티리얼이 URP/Lit 셰이더인지 확인",
        "SetFloat 파라미터명이 정확한지 확인",
        "렌더 큐 설정 확인"
      ]
    },
    {
      "issue": "투명 객체가 깜빡임",
      "solutions": [
        "_ZWrite 설정 확인",
        "렌더 큐 우선순위 확인"
      ]
    }
  ],
  "related_methods": ["MakeMaterialsTransparent"],
  "called_by": ["MakeMaterialsTransparent"],
  "keywords": ["shader", "urp", "material", "blending", "transparency", "rendering"]
}
```

---

## 4. LateUpdate 생명주기 - 프로세스 흐름 문서

### Document ID
```
camera-occlusion-lifecycle-lateupdate-004
```

### Document Content
```json
{
  "id": "camera-occlusion-lifecycle-lateupdate-004",
  "file_path": "Assets/Scripts/CameraOcclusion.cs",
  "class_name": "CameraOcclusion",
  "document_type": "lifecycle_flow",
  "lifecycle_method": "LateUpdate",
  "content": "매 프레임 LateUpdate에서 실행되는 카메라 가림막 처리의 핵심 생명주기. 4단계 프로세스로 현재 프레임의 가림막 객체를 감지하고, 이전 프레임의 상태와 비교하여 머티리얼을 투명화 또는 복원합니다.",
  "execution_timing": "Update 이후, 렌더링 이전 (LateUpdate)",
  "process_steps": [
    {
      "step": 1,
      "name": "플레이어 콜라이더 검색",
      "code": "playerCollider = player.GetComponent<BoxCollider>();",
      "purpose": "매 프레임 플레이어 콜라이더 참조 갱신",
      "condition": "playerCollider == null이면 조기 종료"
    },
    {
      "step": 2,
      "name": "현재 프레임 가림막 객체 감지",
      "code": "HashSet<Renderer> currentFrameOccluders = GetCurrentFrameOccluders();",
      "purpose": "이번 프레임에서 카메라와 플레이어 사이의 모든 객체 검사",
      "performance": "BoxCastAll 호출"
    },
    {
      "step": 3,
      "name": "복원 대상 객체 처리",
      "code": "List<Renderer> toRestore = occludingRenderers.Where(r => !currentFrameOccluders.Contains(r)).ToList();",
      "purpose": "이전 프레임에는 투명했지만 이제 더 이상 가려지지 않은 객체 찾기",
      "action": "각 객체의 원본 머티리얼 복원"
    },
    {
      "step": 4,
      "name": "새로 투명화할 객체 처리",
      "code": "foreach (var renderer in currentFrameOccluders) { if (!occludingRenderers.Contains(renderer)) { MakeMaterialsTransparent(renderer); } }",
      "purpose": "이번 프레임에 새로 가려지게 된 객체 감지",
      "action": "각 객체의 머티리얼을 투명으로 설정"
    },
    {
      "step": 5,
      "name": "상태 업데이트",
      "code": "occludingRenderers.Clear(); occludingRenderers.AddRange(currentFrameOccluders);",
      "purpose": "다음 프레임을 위해 현재 투명 객체 목록 갱신",
      "importance": "상태 불일치 방지"
    }
  ],
  "data_structures_involved": [
    {
      "name": "currentFrameOccluders",
      "type": "HashSet<Renderer>",
      "lifetime": "이번 프레임만"
    },
    {
      "name": "occludingRenderers",
      "type": "List<Renderer>",
      "lifetime": "클래스 전체",
      "purpose": "이전 프레임 상태 추적"
    },
    {
      "name": "originalMaterials",
      "type": "Dictionary<Renderer, Material[]>",
      "lifetime": "클래스 전체",
      "purpose": "원본 머티리얼 백업"
    }
  ],
  "frame_comparison_logic": {
    "description": "프레임 간 상태 비교로 효율성 확보",
    "example": {
      "scenario": "프레임 N → N+1 전환",
      "frame_N": ["Object A (transparent)", "Object B (transparent)"],
      "frame_N+1": ["Object A (transparent)", "Object C (transparent)"],
      "actions": [
        "Object B: RestoreOriginalMaterials 호출 (더 이상 가림)",
        "Object C: MakeMaterialsTransparent 호출 (새로 가림)"
      ]
    }
  },
  "keywords": ["lifecycle", "frame_comparison", "state_management", "efficiency"],
  "related_methods": ["GetCurrentFrameOccluders", "MakeMaterialsTransparent", "RestoreOriginalMaterials"]
}
```

---

## 5. 구성 설정 - Inspector 파라미터 문서

### Document ID
```
camera-occlusion-configuration-005
```

### Document Content
```json
{
  "id": "camera-occlusion-configuration-005",
  "file_path": "Assets/Scripts/CameraOcclusion.cs",
  "class_name": "CameraOcclusion",
  "document_type": "configuration",
  "content": "CameraOcclusion 컴포넌트의 Inspector 설정 파라미터 설명서. 올바른 설정을 통해 시스템 성능과 안정성을 확보할 수 있습니다.",
  "inspector_settings": [
    {
      "field_name": "player",
      "type": "Transform",
      "required": true,
      "default": "null",
      "description": "카메라가 추적할 대상(플레이어) 게임 오브젝트의 Transform",
      "validation": "반드시 설정되어야 함 (없으면 시스템 작동 중지)",
      "setup_steps": [
        "1. 플레이어 게임 오브젝트를 씬에서 선택",
        "2. CameraOcclusion 컴포넌트의 Player 필드에 드래그 앤 드롭",
        "3. 플레이어가 BoxCollider를 가지고 있는지 확인"
      ]
    },
    {
      "field_name": "occlusionLayers",
      "type": "LayerMask",
      "required": true,
      "default": "Everything",
      "description": "투명화될 오브젝트들이 속한 레이어를 선택합니다",
      "importance": "high",
      "optimization": "성능 최적화의 핵심 - 불필요한 레이어는 제외",
      "recommended_configuration": [
        "Wall 레이어만 선택 (일반적인 경우)",
        "또는 Obstacle 레이어",
        "플레이어 레이어는 반드시 제외"
      ],
      "performance_impact": {
        "include_all_layers": "낮음 (모든 객체 검사)",
        "selective_layers": "높음 (필요한 객체만 검사)"
      }
    },
    {
      "field_name": "transparency",
      "type": "float",
      "range": [0.0, 1.0],
      "default": 0.2,
      "description": "투명해졌을 때의 알파(Alpha) 값입니다",
      "unit": "Alpha channel (0=완전 투명, 1=완전 불투명)",
      "visual_reference": [
        "0.0: 완전 투명 (보이지 않음)",
        "0.2: 약간 보임 (권장값)",
        "0.5: 반투명",
        "1.0: 완전 불투명"
      ],
      "tuning_tips": [
        "플레이어가 가려지지 않을 정도의 최소 투명도 설정",
        "예: 0.15 ~ 0.3 범위 권장",
        "게임의 비주얼 스타일에 맞게 조정"
      ]
    }
  ],
  "setup_checklist": [
    "[ ] CameraOcclusion 컴포넌트를 Main Camera에 추가",
    "[ ] Player 필드에 플레이어 Transform 할당",
    "[ ] 플레이어가 BoxCollider를 가지고 있는지 확인",
    "[ ] occlusionLayers에 가려질 객체들의 레이어 선택",
    "[ ] transparency 값을 비주얼에 맞게 조정",
    "[ ] 게임을 실행하여 가림막 효과 확인"
  ],
  "common_issues": [
    {
      "issue": "아무것도 투명화되지 않음",
      "diagnosis": [
        "Player 필드가 설정되어 있는가?",
        "플레이어가 BoxCollider를 가지고 있는가?",
        "occlusionLayers가 올바르게 설정되어 있는가?"
      ]
    },
    {
      "issue": "원하지 않는 객체들이 투명화됨",
      "diagnosis": [
        "occlusionLayers에 불필요한 레이어가 포함되어 있는가?",
        "객체들의 레이어 설정을 확인"
      ]
    },
    {
      "issue": "게임이 느려짐",
      "diagnosis": [
        "occlusionLayers가 너무 많은 레이어를 포함하는가?",
        "플레이어 콜라이더 크기가 너무 큰가?"
      ]
    }
  ],
  "keywords": ["configuration", "setup", "inspector", "optimization", "parameters"]
}
```

---

## 6. 메모리 관리 - 캐싱 및 최적화 문서

### Document ID
```
camera-occlusion-memory-management-006
```

### Document Content
```json
{
  "id": "camera-occlusion-memory-management-006",
  "file_path": "Assets/Scripts/CameraOcclusion.cs",
  "class_name": "CameraOcclusion",
  "document_type": "architecture_pattern",
  "pattern_name": "Material Backup & State Tracking",
  "content": "CameraOcclusion 시스템의 메모리 관리 전략. 원본 머티리얼 백업을 통한 안전한 복원과 프레임별 상태 추적을 통한 효율적 업데이트를 구현합니다.",
  "memory_structures": [
    {
      "structure": "originalMaterials",
      "type": "Dictionary<Renderer, Material[]>",
      "purpose": "원본 머티리얼 백업 저장",
      "lifecycle": "클래스 전체 (게임 종료 시까지)",
      "memory_impact": "백업된 렌더러 개수 × 머티리얼 개수",
      "management_strategy": {
        "add": "MakeMaterialsTransparent에서 처음 처리 시에만 추가",
        "remove": "RestoreOriginalMaterials에서 복원 후 제거",
        "clear": "Clear() 메서드에서 GameManager 호출 시 전체 삭제"
      }
    },
    {
      "structure": "occludingRenderers",
      "type": "List<Renderer>",
      "purpose": "이전 프레임 투명 객체 목록 추적",
      "lifecycle": "매 프레임 갱신",
      "memory_impact": "가려진 렌더러 개수 × 프레임 수",
      "efficiency_benefit": "상태 변화 객체만 처리 (불필요한 반복 작업 제거)"
    }
  ],
  "backup_strategy": {
    "description": "원본 머티리얼 백업 메커니즘",
    "workflow": [
      {
        "event": "객체 첫 투명화",
        "action": "originalMaterials에 원본 머티리얼 배열 백업",
        "code": "if (!originalMaterials.ContainsKey(renderer)) { originalMaterials[renderer] = renderer.sharedMaterials; }"
      },
      {
        "event": "객체 복원",
        "action": "백업된 머티리얼로 복원 후 딕셔너리에서 제거",
        "code": "renderer.sharedMaterials = originalMats; originalMaterials.Remove(renderer);"
      },
      {
        "event": "게임 초기화 (OnExitFound)",
        "action": "모든 백업 및 상태 초기화",
        "code": "Clear()"
      }
    ],
    "safety_guarantee": "원본 머티리얼은 항상 복원 가능한 상태 유지"
  },
  "state_comparison_optimization": {
    "description": "프레임 간 비교를 통한 효율성 확보",
    "concept": "변화가 없으면 작업하지 않기",
    "example": {
      "scenario": "객체 A가 5프레임 연속 가림",
      "without_optimization": "5번 × MakeMaterialsTransparent 호출",
      "with_optimization": "1번 × MakeMaterialsTransparent 호출 (첫 프레임만)",
      "benefit": "불필요한 셰이더 설정 작업 제거"
    }
  },
  "clear_operation": {
    "method": "Clear()",
    "trigger": "GameManager.OnExitFound() 호출 시",
    "operations": [
      "originalMaterials.Clear() - 모든 백업 삭제",
      "occludingRenderers.Clear() - 상태 추적 초기화"
    ],
    "important_note": "복원 안 된 객체는 투명 상태로 유지됨 (명시적 복원 필요)"
  },
  "memory_leak_prevention": [
    {
      "risk": "occludingRenderers에 삭제된 게임 오브젝트의 렌더러 참조 유지",
      "prevention": "매 프레임 비교 시 자동 제거",
      "mechanism": "currentFrameOccluders와의 차이점 비교"
    },
    {
      "risk": "originalMaterials에 복원 안 된 머티리얼 잔존",
      "prevention": "Clear() 호출 또는 RestoreOriginalMaterials 명시 호출",
      "recommendation": "OnExitFound 이벤트에서 Clear() 반드시 호출"
    }
  ],
  "performance_considerations": {
    "dictionary_operations": {
      "ContainsKey": "O(1) - 빠름",
      "Add/Remove": "O(1) - 빠름",
      "Clear": "O(n) - 선형 (단, 게임 시작 시에만 호출)"
    },
    "list_operations": {
      "Clear": "O(n) - 선형",
      "AddRange": "O(m) - 선형 (m = 추가할 요소 수)",
      "Where": "O(n) - 선형"
    }
  },
  "keywords": ["memory", "caching", "state_tracking", "optimization", "garbage_collection"]
}
```

---

## 7. 통합 예제 - 전체 워크플로우 문서

### Document ID
```
camera-occlusion-integration-example-007
```

### Document Content
```json
{
  "id": "camera-occlusion-integration-example-007",
  "file_path": "Assets/Scripts/CameraOcclusion.cs",
  "document_type": "integration_example",
  "title": "CameraOcclusion 전체 통합 워크플로우",
  "content": "CameraOcclusion이 GameManager, Player, Enemy 시스템과 어떻게 통합되는지 보여주는 완전한 워크플로우 예제",
  "scenario": "플레이어가 던전을 탐색하다가 출구를 발견하는 상황",
  "sequence": [
    {
      "frame": 1,
      "event": "게임 시작",
      "actions": [
        "Main Camera에 CameraOcclusion 컴포넌트 추가",
        "Player, occlusionLayers, transparency 설정",
        "originalMaterials = {} (비어있음)",
        "occludingRenderers = [] (비어있음)"
      ]
    },
    {
      "frame": 100,
      "event": "플레이어가 벽 뒤로 이동",
      "actions": [
        "LateUpdate() 호출",
        "GetCurrentFrameOccluders() → Wall 객체 A, B 감지",
        "currentFrameOccluders = {A, B}",
        "occludingRenderers = [] (이전 프레임)",
        "복원 대상 = 없음",
        "A, B 투명화: MakeMaterialsTransparent() 호출",
        "originalMaterials[A] = A의 원본 머티리얼 백업",
        "originalMaterials[B] = B의 원본 머티리얼 백업",
        "occludingRenderers = [A, B]"
      ]
    },
    {
      "frame": 101,
      "event": "플레이어가 벽 사이 공간으로 이동",
      "actions": [
        "LateUpdate() 호출",
        "GetCurrentFrameOccluders() → Wall 객체 B만 감지",
        "currentFrameOccluders = {B}",
        "occludingRenderers = [A, B] (이전 프레임)",
        "복원 대상 = {A}",
        "A 복원: RestoreOriginalMaterials() 호출",
        "  - renderer.sharedMaterials = originalMaterials[A]",
        "  - originalMaterials.Remove(A)",
        "B는 이미 투명 → 스킵",
        "occludingRenderers = [B]"
      ]
    },
    {
      "frame": 150,
      "event": "플레이어가 출구 발견",
      "actions": [
        "GameManager.OnExitFound() 호출",
        "cameraOcclusion.Clear() 호출",
        "  - originalMaterials.Clear()",
        "  - occludingRenderers.Clear()",
        "주의: B는 투명 상태 그대로 유지 (명시 복원 없음)"
      ]
    },
    {
      "frame": 151,
      "event": "새 던전 생성 시작",
      "actions": [
        "dungeon.Generate() 호출",
        "기존 씬 오브젝트 제거",
        "새 씬 로드",
        "B의 투명 상태도 함께 제거됨 (씬 언로드)"
      ]
    }
  ],
  "integration_points": [
    {
      "system": "GameManager",
      "interaction": "OnExitFound() → cameraOcclusion.Clear()",
      "file": "Assets/Scripts/GameManager.cs",
      "code_snippet": "cameraOcclusion.Clear(); Debug.Log(\"CameraOcclusion cleared in OnExitFound()\");"
    },
    {
      "system": "Player",
      "interaction": "Transform + BoxCollider 참조",
      "requirement": "플레이어는 반드시 BoxCollider를 가져야 함"
    },
    {
      "system": "Rendering Pipeline",
      "interaction": "URP 셰이더를 통한 머티리얼 투명화",
      "requirement": "모든 가려질 객체는 URP/Lit 기반 셰이더 사용"
    }
  ],
  "data_flow": {
    "input": ["player.position", "camera.position", "BoxCollider dimensions"],
    "process": ["BoxCastAll → GetCurrentFrameOccluders → 상태 비교 → 머티리얼 갱신"],
    "output": ["투명화된 객체들의 시각 업데이트"]
  },
  "potential_issues_and_solutions": [
    {
      "issue": "투명화된 객체가 씬 전환 후에도 투명 상태 유지",
      "cause": "Clear() 호출 시점에 원본 머티리얼이 아직 백업되어 있지 않음",
      "solution": "OnExitFound에서 cameraOcclusion.Clear() 호출 전에 모든 객체 복원 보장"
    },
    {
      "issue": "성능 저하 (프레임 드롭)",
      "cause": "occlusionLayers에 너무 많은 레이어 포함",
      "solution": "occlusionLayers를 가려질 객체만으로 제한"
    },
    {
      "issue": "플레이어가 가려졌을 때도 투명화됨",
      "cause": "GetCurrentFrameOccluders의 필터 누락 또는 Player 레이어 오류",
      "solution": "Player 레이어를 occlusionLayers에서 제외"
    }
  ],
  "keywords": ["integration", "workflow", "GameManager", "lifecycle", "debugging"]
}
```

---

## 8. ChromaDB 저장 형식 템플릿

```python
# ChromaDB에 저장할 기본 구조
documents = [
    {
        "id": "unique-document-id",
        "content": "전체 문서 텍스트 (검색 대상)",
        "metadata": {
            "file_path": "Assets/Scripts/CameraOcclusion.cs",
            "class_name": "CameraOcclusion",
            "method_name": "GetCurrentFrameOccluders",  # 선택사항
            "document_type": "system_overview | method_detail | lifecycle_flow | configuration | architecture_pattern | integration_example",
            "keywords": ["transparency", "occlusion", "camera", "urp"],
            "related_ids": ["camera-occlusion-method-get-occluders-002"],
            "system_category": "Rendering/Camera",
            "performance_impact": "High | Medium | Low",
            "importance": "critical | high | medium | low"
        }
    }
]

# 저장 예제
collection = client.get_or_create_collection(
    name="TheDungeon_0.2_Documentation",
    metadata={"hnsw:space": "cosine"}
)

collection.add(
    ids=["camera-occlusion-system-001"],
    documents=["URP 환경에서 카메라와 플레이어 사이의 오브젝트를 투명하게..."],
    metadatas=[{
        "file_path": "Assets/Scripts/CameraOcclusion.cs",
        "class_name": "CameraOcclusion",
        "keywords": ["transparency", "occlusion"]
    }]
)
```

---

## 9. 쿼리 예제

```python
# ChromaDB 쿼리 예제

# 1. 기능 기반 검색
results = collection.query(
    query_texts=["카메라 시점의 가려진 객체를 투명하게 처리하는 방법"],
    n_results=5
)
# 반환: camera-occlusion-system-001, camera-occlusion-method-get-occluders-002 등

# 2. 메서드 기반 검색
results = collection.query(
    query_texts=["BoxCastAll을 사용한 오브젝트 감지"],
    n_results=3
)
# 반환: camera-occlusion-method-get-occluders-002

# 3. 성능 최적화 관련 검색
results = collection.query(
    query_texts=["LayerMask를 통한 성능 최적화"],
    n_results=5
)
# 반환: camera-occlusion-configuration-005, camera-occlusion-system-001

# 4. 메모리 관리 검색
results = collection.query(
    query_texts=["머티리얼 백업 및 복원"],
    n_results=3
)
# 반환: camera-occlusion-memory-management-006

# 5. 통합 워크플로우 검색
results = collection.query(
    query_texts=["GameManager와의 통합"],
    n_results=5
)
# 반환: camera-occlusion-integration-example-007
```

---

## 10. 메타데이터 필드 정의

```json
{
  "metadata_schema": {
    "file_path": {
      "type": "string",
      "description": "소스 파일 경로",
      "example": "Assets/Scripts/CameraOcclusion.cs"
    },
    "class_name": {
      "type": "string",
      "description": "클래스명",
      "example": "CameraOcclusion"
    },
    "method_name": {
      "type": "string",
      "optional": true,
      "description": "메서드명 (해당하는 경우)",
      "example": "GetCurrentFrameOccluders"
    },
    "document_type": {
      "type": "enum",
      "values": [
        "system_overview",
        "method_detail",
        "lifecycle_flow",
        "configuration",
        "architecture_pattern",
        "integration_example",
        "troubleshooting"
      ]
    },
    "keywords": {
      "type": "array",
      "description": "검색 키워드",
      "example": ["transparency", "occlusion", "shader"]
    },
    "system_category": {
      "type": "string",
      "description": "시스템 분류",
      "example": "Rendering/Camera"
    },
    "performance_impact": {
      "type": "enum",
      "values": ["Critical", "High", "Medium", "Low"]
    },
    "importance": {
      "type": "enum",
      "values": ["critical", "high", "medium", "low"]
    },
    "related_ids": {
      "type": "array",
      "description": "관련 문서 ID",
      "example": ["camera-occlusion-method-get-occluders-002"]
    },
    "version": {
      "type": "string",
      "description": "문서 버전",
      "example": "1.0"
    },
    "last_updated": {
      "type": "date",
      "description": "마지막 업데이트 날짜"
    }
  }
}
```

