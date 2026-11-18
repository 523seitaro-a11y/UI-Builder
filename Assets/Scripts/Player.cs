using System.Collections.Generic;
using DG.Tweening;
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

    [Header("停止演出")]
    [SerializeField] private PlayerStopSpriteLibrary stopSpriteLibrary;
    [Min(1f)] [SerializeField] private float stopAppearScale = 1.75f;
    [Min(0f)] [SerializeField] private float stopAppearDuration = 0.5f;

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
    private readonly List<Vector2> colliderPath = new List<Vector2>();
    private Sprite colliderSprite;
    private bool colliderFlipX;
    private bool colliderFlipY;
    private float colliderVerticalScale;
    // Unity 6's current 2D solver leaves 0.015 world units between resting shapes.
    private const float FootContactInset = 0.015f;
    private GameObject stopVisualObject;
    private SpriteRenderer stopVisualRenderer;
    private Tween stopScaleTween;
    private bool stopOriginalRendererEnabled;
    private bool wasStopped;
    // The feet occupy the bottom 13% of the standing sprite.
    private const float ColliderBaseHeightRatio = 0.13f;
    // Keep the collision shape slightly inside the sprite so side contacts feel less strict.
    private const float ColliderHorizontalScale = 0.8f;

    private void Awake()
    {
        playerBody ??= GetComponent<Rigidbody2D>();
        playerCollider ??= GetComponent<Collider2D>();
        playerRenderer ??= GetComponent<SpriteRenderer>();
        stageManager ??= FindFirstObjectByType<StageManager>();
        blockManager ??= FindFirstObjectByType<BlockManager>();
        stopSpriteLibrary ??= Resources.Load<PlayerStopSpriteLibrary>("PlayerStopSpriteLibrary");
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
        SyncSpriteCollider();
    }

    // Keep the collision shape aligned when facing or scale changes.
    private void LateUpdate() => SyncSpriteCollider();

    private void SyncSpriteCollider()
    {
        playerCollider ??= GetComponent<Collider2D>();
        if (playerCollider is not PolygonCollider2D polygon || playerRenderer == null)
        {
            return;
        }

        // Animation frames have different feet. Changing the physical footprint can
        // remove support at a ledge, then restore it with the landing frame forever.
        // Use the same standing pentagon throughout movement, jumping, and landing.
        Sprite sprite = idle0 != null ? idle0 : playerRenderer.sprite;
        bool flipX = playerRenderer.flipX;
        bool flipY = playerRenderer.flipY;
        float verticalScale = transform.lossyScale.y;
        if (sprite == colliderSprite && flipX == colliderFlipX && flipY == colliderFlipY &&
            Mathf.Approximately(verticalScale, colliderVerticalScale))
        {
            return;
        }

        // Physics shape points already include the sprite pivot and pixels-per-unit.
        // Transform scale applies to both the sprite and collider automatically.
        polygon.offset = Vector2.zero;
        int shapeCount = sprite != null ? sprite.GetPhysicsShapeCount() : 0;
        polygon.pathCount = shapeCount > 0 ? 1 : 0;
        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        float bottom = float.PositiveInfinity;
        float top = float.NegativeInfinity;
        for (int path = 0; path < shapeCount; path++)
        {
            sprite.GetPhysicsShape(path, colliderPath);
            foreach (Vector2 point in colliderPath)
            {
                left = Mathf.Min(left, point.x);
                right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y);
                top = Mathf.Max(top, point.y);
            }
        }
        float height = top - bottom;
        float inset = Mathf.Min(FootContactInset / Mathf.Max(Mathf.Abs(verticalScale), 0.0001f),
            Mathf.Max(0f, height * 0.5f));
        if (shapeCount > 0)
        {
            // Triangle above, full-width rectangle below: no separate feet or notch.
            float shoulderY = bottom + height * ColliderBaseHeightRatio;
            float centerX = (left + right) * 0.5f;
            float halfWidth = (right - left) * 0.5f * ColliderHorizontalScale;
            float colliderLeft = centerX - halfWidth;
            float colliderRight = centerX + halfWidth;
            colliderPath.Clear();
            colliderPath.Add(new Vector2(centerX, top));
            colliderPath.Add(new Vector2(colliderRight, shoulderY));
            colliderPath.Add(new Vector2(colliderRight, bottom));
            colliderPath.Add(new Vector2(colliderLeft, bottom));
            colliderPath.Add(new Vector2(colliderLeft, shoulderY));
            if (flipY)
            {
                float previousBottom = bottom;
                bottom = -top;
                top = -previousBottom;
            }
            for (int point = 0; point < colliderPath.Count; point++)
            {
                Vector2 position = colliderPath[point];
                position.x *= flipX ? -1f : 1f;
                position.y *= flipY ? -1f : 1f;
                // Account for the solver's contact gap at the feet without moving the
                // sprite, Rigidbody, or head boundary. Keep the inset constant in world space.
                if (height > Mathf.Epsilon)
                {
                    position.y += verticalScale >= 0f
                        ? inset * (top - position.y) / height
                        : -inset * (position.y - bottom) / height;
                }
                colliderPath[point] = position;
            }
            polygon.SetPath(0, colliderPath);
        }

        colliderSprite = sprite;
        colliderFlipX = flipX;
        colliderFlipY = flipY;
        colliderVerticalScale = verticalScale;
    }

    private void Update()
    {
        UpdateStopVisual();
        UpdateAnimation(Time.deltaTime);
    }

    private void UpdateStopVisual()
    {
        bool isStopped = blockManager != null && blockManager.IsPlayerStopped;
        SetStoppedVisual(isStopped);
    }

    public void SetStoppedVisual(bool isStopped)
    {
        if (isStopped == wasStopped)
        {
            return;
        }

        wasStopped = isStopped;
        if (isStopped)
        {
            PlayStopVisual();
        }
        else
        {
            RestoreStopVisual();
        }
    }

    private void PlayStopVisual()
    {
        if (playerRenderer == null)
        {
            return;
        }

        stopScaleTween?.Kill();
        stopVisualObject = new GameObject("Stopped Player Visual");
        stopVisualObject.layer = playerRenderer.gameObject.layer;
        Transform visualTransform = stopVisualObject.transform;
        visualTransform.SetParent(playerRenderer.transform, false);
        stopVisualRenderer = stopVisualObject.AddComponent<SpriteRenderer>();
        CopyStopRenderer(playerRenderer, stopVisualRenderer);
        stopVisualRenderer.sprite = GetStoppedSprite(playerRenderer.sprite);

        stopOriginalRendererEnabled = playerRenderer.enabled;
        playerRenderer.enabled = false;
        Vector2 spriteCenter = playerRenderer.sprite != null
            ? playerRenderer.sprite.bounds.center
            : Vector2.zero;
        ApplyStopVisualScale(visualTransform, spriteCenter, stopAppearScale);

        if (stopAppearDuration <= 0f)
        {
            ApplyStopVisualScale(visualTransform, spriteCenter, 1f);
            return;
        }

        float scale = stopAppearScale;
        stopScaleTween = DOTween.To(
                () => scale,
                value =>
                {
                    scale = value;
                    if (visualTransform != null)
                    {
                        ApplyStopVisualScale(visualTransform, spriteCenter, value);
                    }
                },
                1f,
                stopAppearDuration)
            .SetEase(Ease.OutCubic);
    }

    private Sprite GetStoppedSprite(Sprite currentSprite)
    {
        if (stopSpriteLibrary == null || currentSprite == null)
        {
            return currentSprite;
        }

        if (currentSprite == idle0) return stopSpriteLibrary.idle0;
        if (currentSprite == idle1) return stopSpriteLibrary.idle1;
        if (currentSprite == run0) return stopSpriteLibrary.run0;
        if (currentSprite == run1) return stopSpriteLibrary.run1;
        if (currentSprite == run2) return stopSpriteLibrary.run2;
        if (currentSprite == run3) return stopSpriteLibrary.run3;
        if (currentSprite == jump0) return stopSpriteLibrary.jump0;
        if (currentSprite == jump1) return stopSpriteLibrary.jump1;
        if (currentSprite == jump2) return stopSpriteLibrary.jump2;
        if (currentSprite == jump3) return stopSpriteLibrary.jump3;
        return currentSprite;
    }

    private static void CopyStopRenderer(
        SpriteRenderer source,
        SpriteRenderer destination)
    {
        destination.sprite = source.sprite;
        destination.sharedMaterial = source.sharedMaterial;
        destination.color = source.color;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.maskInteraction = source.maskInteraction;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
    }

    private static void ApplyStopVisualScale(
        Transform visual,
        Vector2 spriteCenter,
        float scale)
    {
        visual.localScale = new Vector3(scale, scale, 1f);
        visual.localPosition = new Vector3(
            spriteCenter.x * (1f - scale),
            spriteCenter.y * (1f - scale),
            0f);
    }

    private void RestoreStopVisual()
    {
        stopScaleTween?.Kill();
        stopScaleTween = null;
        if (playerRenderer != null)
        {
            playerRenderer.enabled = stopOriginalRendererEnabled;
        }

        if (stopVisualRenderer != null)
        {
            stopVisualRenderer.enabled = false;
        }

        if (stopVisualObject != null)
        {
            Destroy(stopVisualObject);
        }

        stopVisualObject = null;
        stopVisualRenderer = null;
    }

    private void OnDestroy()
    {
        RestoreStopVisual();
    }

    private void UpdateAnimation(float deltaTime)
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
                // Walking off an edge starts falling immediately; only upward motion
                // should display the jump-start frame.
                EnterState(GetAirMovementState(includeJumpStart: true));
            }
            else if (animationState == AnimationState.JumpStart)
            {
                stateTimer += deltaTime;
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
            stateTimer += deltaTime;
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
        UpdateCurrentAnimation(deltaTime);
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

        return playerBody.linearVelocity.y > apexVelocityThreshold
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

    // UI演出も実際のプレイヤーと同じ歩行フレーム・間隔を使用します。
    public Sprite GetWalkingSprite(float elapsed)
    {
        float d0 = Mathf.Max(0.01f, run0Duration);
        float d1 = Mathf.Max(0.01f, run1Duration);
        float d2 = Mathf.Max(0.01f, run2Duration);
        float d3 = Mathf.Max(0.01f, run3Duration);
        float time = Mathf.Repeat(elapsed, d0 + d1 + d2 + d3);
        Sprite frame = time < d0 ? run0 : time < d0 + d1 ? run1 : time < d0 + d1 + d2 ? run2 : run3;
        return frame != null ? frame : idle0;
    }

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
        SyncSpriteCollider();
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
                !groundHits[i].collider.isTrigger &&
                groundHits[i].normal.y >= minimumGroundNormalY)
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
        SyncSpriteCollider();
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
