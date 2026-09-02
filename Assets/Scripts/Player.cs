using UnityEngine;

[DisallowMultipleComponent]
public class Player : MonoBehaviour
{
    public readonly struct AnimationSnapshot
    {
        internal readonly int State;
        internal readonly int FrameIndex;
        internal readonly float FrameTimer;
        internal readonly float StateTimer;
        internal readonly bool WasGrounded;
        internal readonly bool HasStateSample;
        public readonly Sprite Sprite;
        public readonly bool FlipX;
        public readonly bool FlipY;

        internal AnimationSnapshot(
            int state,
            int savedFrameIndex,
            float savedFrameTimer,
            float savedStateTimer,
            bool savedWasGrounded,
            bool savedHasStateSample,
            Sprite sprite,
            bool flipX,
            bool flipY)
        {
            State = state;
            FrameIndex = savedFrameIndex;
            FrameTimer = savedFrameTimer;
            StateTimer = savedStateTimer;
            WasGrounded = savedWasGrounded;
            HasStateSample = savedHasStateSample;
            Sprite = sprite;
            FlipX = flipX;
            FlipY = flipY;
        }
    }

    private enum AnimationState
    {
        Idle,
        Run,
        JumpStart,
        Rising,
        Falling,
        Landing
    }

    [Header("参照")]
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private StageManager stageManager;
    [SerializeField] private BlockManager blockManager;

    [Header("Idleスプライト")]
    [SerializeField] private Sprite idle0;
    [SerializeField] private Sprite idle1;
    [Min(0.01f)] [SerializeField] private float idle0Duration = 0.4f;
    [Min(0.01f)] [SerializeField] private float idle1Duration = 0.4f;

    [Header("Runスプライト")]
    [SerializeField] private Sprite run0;
    [SerializeField] private Sprite run1;
    [SerializeField] private Sprite run2;
    [SerializeField] private Sprite run3;
    [Min(0.01f)] [SerializeField] private float run0Duration = 0.1f;
    [Min(0.01f)] [SerializeField] private float run1Duration = 0.1f;
    [Min(0.01f)] [SerializeField] private float run2Duration = 0.1f;
    [Min(0.01f)] [SerializeField] private float run3Duration = 0.1f;

    [Header("Jumpスプライト")]
    [SerializeField] private Sprite jump0;
    [SerializeField] private Sprite jump1;
    [SerializeField] private Sprite jump2;
    [SerializeField] private Sprite jump3;
    [Tooltip("ジャンプ開始時にJump0を表示する秒数です。")]
    [Min(0f)] [SerializeField] private float jumpStartDuration = 0.08f;
    [Tooltip("着地後にJump3を表示する秒数です。")]
    [Min(0f)] [SerializeField] private float landingDuration = 0.3f;

    [Header("状態判定")]
    [Tooltip("この速度以上なら走っていると判定します。")]
    [Min(0f)] [SerializeField] private float runSpeedThreshold = 0.05f;
    [Tooltip("上昇から下降へ切り替える垂直速度の境目です。")]
    [Min(0f)] [SerializeField] private float apexVelocityThreshold = 0.05f;
    [Tooltip("接触情報がない場合に使用する下向き接地判定距離です。")]
    [Min(0f)] [SerializeField] private float groundCheckDistance = 0.08f;
    [Range(0f, 1f)] [SerializeField] private float minimumGroundNormalY = 0.5f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    private AnimationState animationState;
    private int frameIndex;
    private float frameTimer;
    private float stateTimer;
    private bool wasGrounded;
    private bool hasStateSample;

    private void Awake()
    {
        playerBody ??= GetComponent<Rigidbody2D>();
        playerCollider ??= GetComponent<Collider2D>();
        playerRenderer ??= GetComponent<SpriteRenderer>();
        stageManager ??= FindFirstObjectByType<StageManager>();
        blockManager ??= FindFirstObjectByType<BlockManager>();
        ResetToIdle();
    }

    private void OnEnable() => ResetToIdle();

    public AnimationSnapshot CaptureAnimationState() => new AnimationSnapshot(
        (int)animationState,
        frameIndex,
        frameTimer,
        stateTimer,
        wasGrounded,
        hasStateSample,
        playerRenderer != null ? playerRenderer.sprite : null,
        playerRenderer != null && playerRenderer.flipX,
        playerRenderer != null && playerRenderer.flipY);

    public void RestoreAnimationState(AnimationSnapshot snapshot)
    {
        animationState = (AnimationState)Mathf.Clamp(
            snapshot.State,
            0,
            (int)AnimationState.Landing);
        frameIndex = Mathf.Max(0, snapshot.FrameIndex);
        frameTimer = Mathf.Max(0f, snapshot.FrameTimer);
        stateTimer = Mathf.Max(0f, snapshot.StateTimer);
        wasGrounded = snapshot.WasGrounded;
        hasStateSample = snapshot.HasStateSample;

        if (playerRenderer != null)
        {
            if (snapshot.Sprite != null)
            {
                playerRenderer.sprite = snapshot.Sprite;
            }
            playerRenderer.flipX = snapshot.FlipX;
            playerRenderer.flipY = snapshot.FlipY;
        }
    }

    private void Update()
    {
        if (!CanAnimate())
        {
            hasStateSample = false;
            return;
        }

        UpdateFacingDirection();
        bool isGrounded = IsGrounded();
        if (!hasStateSample)
        {
            hasStateSample = true;
            wasGrounded = isGrounded;
            EnterState(isGrounded
                ? GetGroundedMovementState()
                : GetAirMovementState(includeJumpStart: true));
        }
        else if (!isGrounded)
        {
            if (wasGrounded)
            {
                EnterState(AnimationState.JumpStart);
            }
            else if (animationState == AnimationState.JumpStart)
            {
                stateTimer += Time.deltaTime;
                if (stateTimer >= jumpStartDuration)
                {
                    EnterState(GetAirMovementState(includeJumpStart: false));
                }
            }
            else
            {
                AnimationState airState = GetAirMovementState(includeJumpStart: false);
                if (animationState != airState)
                {
                    EnterState(airState);
                }
            }
        }
        else if (!wasGrounded)
        {
            EnterState(HasMovementBlockInput()
                ? GetGroundedMovementState()
                : AnimationState.Landing);
        }
        else if (animationState == AnimationState.Landing)
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= landingDuration)
            {
                EnterState(GetGroundedMovementState());
            }
        }
        else
        {
            AnimationState groundedState = GetGroundedMovementState();
            if (animationState != groundedState)
            {
                EnterState(groundedState);
            }
        }

        wasGrounded = isGrounded;
        UpdateCurrentAnimation(Time.deltaTime);
    }

    private bool CanAnimate() =>
        playerBody != null &&
        playerCollider != null &&
        playerRenderer != null &&
        playerBody.simulated &&
        (blockManager == null || !blockManager.IsPlayerStopped) &&
        (stageManager == null ||
         (stageManager.CurrentMode == StageManager.StageMode.Play && !stageManager.IsPaused));

    private bool HasMovementBlockInput() =>
        blockManager != null && blockManager.HasPlayerMovementInput;

    private void UpdateFacingDirection()
    {
        if (playerBody.linearVelocity.x > runSpeedThreshold)
        {
            playerRenderer.flipX = false;
        }
        else if (playerBody.linearVelocity.x < -runSpeedThreshold)
        {
            playerRenderer.flipX = true;
        }
    }

    private AnimationState GetGroundedMovementState() =>
        Mathf.Abs(playerBody.linearVelocity.x) >= runSpeedThreshold
            ? AnimationState.Run
            : AnimationState.Idle;

    private AnimationState GetAirMovementState(bool includeJumpStart)
    {
        if (includeJumpStart && playerBody.linearVelocity.y > apexVelocityThreshold)
        {
            return AnimationState.JumpStart;
        }

        return playerBody.linearVelocity.y >= -apexVelocityThreshold
            ? AnimationState.Rising
            : AnimationState.Falling;
    }

    private void EnterState(AnimationState nextState)
    {
        animationState = nextState;
        frameIndex = 0;
        frameTimer = 0f;
        stateTimer = 0f;
        ApplyCurrentSprite();
    }

    private void UpdateCurrentAnimation(float deltaTime)
    {
        if (animationState != AnimationState.Idle && animationState != AnimationState.Run)
        {
            ApplyCurrentSprite();
            return;
        }

        frameTimer += deltaTime;
        float duration = GetCurrentFrameDuration();
        while (frameTimer >= duration)
        {
            frameTimer -= duration;
            frameIndex = (frameIndex + 1) % GetFrameCount();
            duration = GetCurrentFrameDuration();
        }

        ApplyCurrentSprite();
    }

    private int GetFrameCount() => animationState == AnimationState.Run ? 4 : 2;

    private float GetCurrentFrameDuration()
    {
        if (animationState == AnimationState.Run)
        {
            return Mathf.Max(0.01f, frameIndex switch
            {
                0 => run0Duration,
                1 => run1Duration,
                2 => run2Duration,
                _ => run3Duration
            });
        }

        return Mathf.Max(0.01f, frameIndex == 0 ? idle0Duration : idle1Duration);
    }

    private void ApplyCurrentSprite()
    {
        Sprite sprite = animationState switch
        {
            AnimationState.Idle => frameIndex == 0 ? idle0 : idle1,
            AnimationState.Run => frameIndex switch
            {
                0 => run0,
                1 => run1,
                2 => run2,
                _ => run3
            },
            AnimationState.JumpStart => jump0,
            AnimationState.Rising => jump1,
            AnimationState.Falling => jump2,
            AnimationState.Landing => jump3,
            _ => idle0
        };

        if (sprite != null && playerRenderer.sprite != sprite)
        {
            playerRenderer.sprite = sprite;
        }
    }

    private bool IsGrounded()
    {
        if (playerBody.linearVelocity.y > apexVelocityThreshold)
        {
            return false;
        }

        int contactCount = playerBody.GetContacts(contacts);
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = contacts[i];
            if (contact.otherCollider != null &&
                !contact.otherCollider.isTrigger &&
                IsGroundLayer(contact.otherCollider.gameObject.layer) &&
                contact.normal.y >= minimumGroundNormalY)
            {
                return true;
            }
        }

        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false
        };
        filter.SetLayerMask(groundLayers);
        int hitCount = playerCollider.Cast(
            Vector2.down,
            filter,
            groundHits,
            groundCheckDistance);
        for (int i = 0; i < hitCount; i++)
        {
            if (groundHits[i].collider != null &&
                groundHits[i].collider != playerCollider &&
                !groundHits[i].collider.isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsGroundLayer(int layer) => (groundLayers.value & (1 << layer)) != 0;

    /// <summary>アニメーションの進行と向きをリセットし、Idle0を表示します。</summary>
    public void ResetToIdle()
    {
        playerRenderer ??= GetComponent<SpriteRenderer>();
        hasStateSample = false;
        wasGrounded = false;
        animationState = AnimationState.Idle;
        frameIndex = 0;
        frameTimer = 0f;
        stateTimer = 0f;
        if (playerRenderer != null)
        {
            playerRenderer.flipX = false;
            playerRenderer.flipY = false;
            if (idle0 != null)
            {
                playerRenderer.sprite = idle0;
            }
        }
    }

    private void OnValidate()
    {
        idle0Duration = Mathf.Max(0.01f, idle0Duration);
        idle1Duration = Mathf.Max(0.01f, idle1Duration);
        run0Duration = Mathf.Max(0.01f, run0Duration);
        run1Duration = Mathf.Max(0.01f, run1Duration);
        run2Duration = Mathf.Max(0.01f, run2Duration);
        run3Duration = Mathf.Max(0.01f, run3Duration);
        jumpStartDuration = Mathf.Max(0f, jumpStartDuration);
        landingDuration = Mathf.Max(0f, landingDuration);
        runSpeedThreshold = Mathf.Max(0f, runSpeedThreshold);
        apexVelocityThreshold = Mathf.Max(0f, apexVelocityThreshold);
        groundCheckDistance = Mathf.Max(0f, groundCheckDistance);
    }
}
