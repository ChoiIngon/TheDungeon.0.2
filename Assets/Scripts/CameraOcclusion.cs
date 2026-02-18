using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// URP 환경에서 카메라와 대상(플레이어) 사이의 오브젝트를 투명하게 만드는 스크립트.
/// URP/Lit 셰이더의 렌더링 모드를 런타임에 Opaque에서 Transparent로 동적 변경합니다.
/// </summary>
public class CameraOcclusion : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("카메라가 추적할 대상(플레이어)입니다.")]
    public Transform player;

    [Tooltip("투명화될 오브젝트들이 속한 레이어를 선택합니다.")]
    public LayerMask occlusionLayers;

    [Tooltip("투명해졌을 때의 알파(Alpha) 값입니다.")]
    [Range(0.0f, 1.0f)]
    public float transparency = 0.2f;

    // --- 내부 변수 ---
    // 원본 머티리얼들을 백업하기 위한 딕셔너리
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    // 현재 투명하게 처리된 렌더러들을 추적하는 리스트
    private readonly List<Renderer> occludingRenderers = new List<Renderer>();

    private BoxCollider playerCollider; // 플레이어 콜라이더 (BoxCollider 등 다른 타입도 가능)
    public void Clear()
    {
        originalMaterials.Clear();
        occludingRenderers.Clear();
    }

    void LateUpdate()
    {
        playerCollider = player.GetComponent<BoxCollider>();
        if (playerCollider == null) return;

        // 1. 현재 프레임에서 가려지는 오브젝트들의 목록을 새로 가져옵니다.
        HashSet<Renderer> currentFrameOccluders = GetCurrentFrameOccluders();

        // 2. 지난 프레임에 투명했지만, 이제는 더 이상 가려지지 않는 오브젝트들을 찾아 원래 상태로 복원합니다.
        List<Renderer> toRestore = occludingRenderers.Where(r => !currentFrameOccluders.Contains(r)).ToList();
        foreach (var renderer in toRestore)
        {
            RestoreOriginalMaterials(renderer);
        }

        // 3. 이번 프레임에 새로 가려지게 된 오브젝트들을 찾아 투명하게 만듭니다.
        foreach (var renderer in currentFrameOccluders)
        {
            if (!occludingRenderers.Contains(renderer))
            {
                MakeMaterialsTransparent(renderer);
            }
        }

        // 4. 현재 투명한 오브젝트 목록을 최신 상태로 업데이트합니다.
        occludingRenderers.Clear();
        occludingRenderers.AddRange(currentFrameOccluders);
    }

    /// <summary>
    /// 현재 프레임에서 카메라와 플레이어 사이를 가리는 모든 렌더러를 찾습니다.
    /// </summary>
    private HashSet<Renderer> GetCurrentFrameOccluders()
    {
        Vector3 cameraPosition = transform.position;
        Vector3 playerCenter = player.position + (playerCollider.center);
        Vector3 direction = playerCenter - cameraPosition;
        float distance = direction.magnitude;

        Vector3 boxHalfExtents = playerCollider.size / 3f;

        // CapsuleCast를 사용하여 플레이어의 부피만큼 경로를 검사합니다.
        // BoxCast 등 다른 Shape Cast를 사용해도 됩니다.
        RaycastHit[] hits = Physics.BoxCastAll(
            cameraPosition,
            boxHalfExtents,
            direction,
            player.rotation,
            distance,
            occlusionLayers
        );

        return new HashSet<Renderer>(hits
            .Where(hit => hit.collider.GetComponent<Renderer>() != null && !hit.transform.IsChildOf(player))
            .Select(hit => hit.collider.GetComponent<Renderer>()));
    }

    /// <summary>
    /// 지정된 렌더러의 머티리얼들을 투명하게 만듭니다.
    /// </summary>
    private void MakeMaterialsTransparent(Renderer renderer)
    {
        // 원본 머티리얼을 아직 백업하지 않았다면 백업합니다.
        if (!originalMaterials.ContainsKey(renderer))
        {
            originalMaterials[renderer] = renderer.sharedMaterials;
        }

        // 런타임에서 사용할 새로운 머티리얼 인스턴스를 생성합니다.
        // renderer.materials는 renderer.sharedMaterials의 인스턴스를 생성하는 프로퍼티입니다.
        Material[] newMaterials = new Material[renderer.materials.Length];
        for (int i = 0; i < newMaterials.Length; i++)
        {
            Material mat = renderer.materials[i];
            SetMaterialToTransparent_URP(mat); // URP 전용 함수 호출

            Color color = mat.GetColor("_BaseColor");
            color.a = transparency;
            mat.SetColor("_BaseColor", color);

            newMaterials[i] = mat;
        }
        renderer.materials = newMaterials;
    }

    /// <summary>
    /// 지정된 렌더러를 원래의 불투명한 머티리얼로 복원합니다.
    /// </summary>
    private void RestoreOriginalMaterials(Renderer renderer)
    {
        if (originalMaterials.TryGetValue(renderer, out Material[] originalMats))
        {
            renderer.sharedMaterials = originalMats;
            originalMaterials.Remove(renderer);
        }
    }

    /// <summary>
    /// URP/Lit 셰이더 기반의 머티리얼을 런타임에서 Transparent 모드로 변경하는 핵심 함수입니다.
    /// </summary>
    public static void SetMaterialToTransparent_URP(Material material)
    {
        // 1. Surface Type을 Transparent로 설정
        material.SetFloat("_Surface", 1.0f);

        // 2. 블렌딩 모드 설정 (Standard Alpha Blending)
        material.SetFloat("_Blend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); // _BlendMode is deprecated, use _Blend
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        // 3. ZWrite(깊이 쓰기) 비활성화
        material.SetInt("_ZWrite", 0);

        // 4. 셰이더 키워드 활성화 및 렌더 큐 설정
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON"); // Alpha Clipping 비활성화
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
