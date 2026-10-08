using UnityEngine;

namespace PixelArt.Dungeon
{
    /// <summary>
    /// 문 타일 프리팹의 열림·닫힘. 문짝은 왼쪽 끝 경첩 축(hinge)을 기준으로 방 쪽(카메라 쪽, -Z)으로 openAngle만큼 돈다.
    /// 열림·닫힘 그림이 따로 있지 않다 (object-spec.md).
    /// </summary>
    public class DungeonDoor : MonoBehaviour
    {
        [Tooltip("문짝의 부모. 문 타일 왼쪽 문틀 안쪽 모서리에 놓인 세로 축")]
        public Transform hinge;

        [Tooltip("열린 각도. +90이면 문짝이 -Z(카메라 쪽)로 열린다")]
        public float openAngle = 90f;

        [SerializeField] bool isOpen;

        public bool IsOpen => isOpen;

        public void SetOpen(bool open)
        {
            isOpen = open;
            Apply();
        }

        public void Toggle() => SetOpen(!isOpen);

        void OnEnable() => Apply();

#if UNITY_EDITOR
        void OnValidate() => Apply();
#endif

        void Apply()
        {
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, isOpen ? openAngle : 0f, 0f);
        }
    }
}
