using System.Runtime.CompilerServices;
using UnityEngine;

public class DoorStand : MonoBehaviour, IInteractable
{
    public bool isLocked = false;
    public bool isOpen = false;
    public Transform door;

    private const float OpenAngle = 120f;
    private const float CloseAngle = 0f;
    private const float AnimationDuration = 0.5f;

    private float animationElapsedTime = 0f;
    private bool isAnimating = false;
    private Quaternion targetRotation;
    private Quaternion startRotation;

    // Update is called once per frame
    void Update()
    {
        if (true == isAnimating)
        {
            animationElapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(animationElapsedTime / AnimationDuration);

            // door Transform만 회전 (DoorStand 전체가 아님)
            door.localRotation = Quaternion.Lerp(startRotation, targetRotation, progress);

            // 애니메이션 완료
            if (progress >= 1f)
            {
                isAnimating = false;
                animationElapsedTime = 0f;
                Debug.Log(isOpen ? "Door fully opened." : "Door fully closed.");
            }
        }
    }

    public void Open(bool open)
    {
        if (true == isLocked)
        {
            Debug.Log("Door is locked. Cannot open.");
            return;
        }

        if (true == open && false == isOpen)
        {
            StartDoorAnimation(true);
            isOpen = true;
            SetChildCollidersEnabled(false);
        }

        if (false == open && true == isOpen)
        {
            StartDoorAnimation(false);
            isOpen = false;
            SetChildCollidersEnabled(true);
        }
    }

    private void StartDoorAnimation(bool shouldOpen)
    {
        // door의 로컬 회전값을 저장 (DoorStand의 회전값이 아님)
        startRotation = door.localRotation;
        targetRotation = Quaternion.Euler(0, shouldOpen ? OpenAngle : CloseAngle, 0);
        animationElapsedTime = 0f;
        isAnimating = true;
        
        Debug.Log($"Door animation started. Opening: {shouldOpen}");
    }

    /// <summary>
    /// door의 자식 오브젝트들의 Collider를 활성화/비활성화 합니다.
    /// </summary>
    private void SetChildCollidersEnabled(bool enabled)
    {
        if (door == null)
        {
            Debug.LogWarning("Door Transform is not assigned.");
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider.gameObject != this.gameObject)  // 부모는 제외
            {
                collider.enabled = enabled;
                Debug.Log($"Collider '{collider.gameObject.name}' set to {(enabled ? "enabled" : "disabled")}");
            }
        }
    }

    public void Interact()
    {
        Open(!isOpen);
    }
}
