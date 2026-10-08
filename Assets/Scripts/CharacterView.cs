using System;
using UnityEngine;

/// <summary>
/// Aseprite Importer가 만든 캐릭터 스프라이트(SpriteRenderer + Animator)를 상태·방향으로 재생한다.
/// 애니메이터 상태 이름은 Aseprite 태그 이름 그대로다: &lt;동작&gt;_&lt;방향&gt; (idle_down, move_left …), 사망만 die.
/// 전이선 없이 코드에서 Animator.Play로 바로 상태를 바꾼다.
/// </summary>
public class CharacterView : MonoBehaviour
{
    public enum State { Idle, Move, Attack, Damage, Die }
    public enum Direction { Down, Up, Left, Right }

    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;

    [Tooltip("스프라이트를 카메라와 같은 각도로 기울여 정면으로 보이게 한다 (고정 카메라 기준)")]
    [SerializeField] bool faceCamera = true;

    [Tooltip("Attack·Damage가 끝나면 Idle로 돌아간다")]
    [SerializeField] bool returnToIdle = true;

    public State CurrentState { get; private set; } = State.Idle;
    public Direction CurrentDirection { get; private set; } = Direction.Down;

    /// <summary>한 번만 재생하는 동작(Attack, Damage, Die)이 끝났을 때</summary>
    public event Action<State> Finished;

    public Animator Animator => animator;
    public SpriteRenderer SpriteRenderer => spriteRenderer;

    static readonly int[,] stateHashes = BuildHashes();
    bool finishedNotified;

    static int[,] BuildHashes()
    {
        var states = (State[])Enum.GetValues(typeof(State));
        var dirs = (Direction[])Enum.GetValues(typeof(Direction));
        var hashes = new int[states.Length, dirs.Length];
        foreach (var s in states)
            foreach (var d in dirs)
                hashes[(int)s, (int)d] = Animator.StringToHash(StateName(s, d));
        return hashes;
    }

    public static string StateName(State state, Direction dir) =>
        state == State.Die ? "die" : $"{state.ToString().ToLowerInvariant()}_{dir.ToString().ToLowerInvariant()}";

    static bool IsOneShot(State state) => state == State.Attack || state == State.Damage || state == State.Die;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void OnEnable() => PlayState(CurrentState, CurrentDirection, restart: true);

    /// <summary>현재 방향으로 상태를 바꾼다. 같은 상태면 처음부터 다시 재생하지 않는다 (restart로 강제).</summary>
    public void Play(State state, bool restart = false) => Play(state, CurrentDirection, restart);

    public void Play(State state, Direction dir, bool restart = false)
    {
        if (CurrentState == State.Die && state != State.Die && !restart) return; // 사망 후에는 되살릴 때만 (restart)
        if (!restart && state == CurrentState && dir == CurrentDirection) return;

        // 같은 동작에서 방향만 바뀌면 진행 위치를 이어 간다 (걷다가 방향 전환 시 발 박자 유지)
        bool keepTime = !restart && state == CurrentState;
        PlayState(state, dir, restart: !keepTime);
    }

    /// <summary>방향만 바꾼다. 진행 중인 동작은 그대로 이어진다.</summary>
    public void SetDirection(Direction dir) => Play(CurrentState, dir);

    /// <summary>월드 이동 방향(XZ 평면)에서 상하좌우 중 가까운 쪽을 고른다. 0 벡터면 그대로 둔다.</summary>
    public void SetDirection(Vector3 worldDir)
    {
        if (worldDir.x * worldDir.x + worldDir.z * worldDir.z < 1e-6f) return;
        SetDirection(ToDirection(worldDir));
    }

    public static Direction ToDirection(Vector3 worldDir) =>
        Mathf.Abs(worldDir.x) > Mathf.Abs(worldDir.z)
            ? (worldDir.x > 0 ? Direction.Right : Direction.Left)
            : (worldDir.z > 0 ? Direction.Up : Direction.Down);

    void PlayState(State state, Direction dir, bool restart)
    {
        CurrentState = state;
        CurrentDirection = dir;
        finishedNotified = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;

        int hash = stateHashes[(int)state, (int)dir];
        if (!animator.HasState(0, hash))
        {
            Debug.LogWarning($"{name}: 애니메이터에 '{StateName(state, dir)}' 상태가 없습니다 (Aseprite 태그 이름 확인).", this);
            return;
        }

        float time = restart ? 0f : Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
        animator.speed = 1f;
        animator.Play(hash, 0, time);
    }

    void Update()
    {
        if (finishedNotified || !IsOneShot(CurrentState) || animator == null) return;

        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.shortNameHash != stateHashes[(int)CurrentState, (int)CurrentDirection] || info.normalizedTime < 1f)
            return;

        finishedNotified = true;
        var done = CurrentState;
        if (done == State.Die)
            animator.speed = 0f; // 루프 클립이어도 마지막 프레임에 멈춘다
        else if (returnToIdle)
            PlayState(State.Idle, CurrentDirection, restart: true);
        Finished?.Invoke(done);
    }

    void LateUpdate()
    {
        if (!faceCamera || animator == null) return;
        var cam = Camera.main;
        // 모델 루트(Animator)를 돌린다. 레이어별 임포트여도 레이어 배치가 함께 돈다
        if (cam != null) animator.transform.rotation = cam.transform.rotation;
    }
}
