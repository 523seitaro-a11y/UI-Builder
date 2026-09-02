using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class UiSizeBlock : MonoBehaviour,
    BlockManager.IBlockOperationState,
    BlockManager.IPlayModeBlockState
{
    private static readonly Vector2 OptionsAreaSize = new Vector2(2f, 2f);
    private static readonly Vector2 OptionsAreaCenter = new Vector2(0f, 1.5f);
    private static readonly Vector2 ButtonSize = new Vector2(2f, 0.6f);
    private const float OptionsAnimationDuration = 0.11f / 3f;
    private const float MaximumLaunchSpeed = 12f * 1.7320508f;
    private static readonly Vector2[] ButtonCenters =
    {
        new Vector2(0f, 2.2f),
        new Vector2(0f, 1.54f),
        new Vector2(0f, 0.88f)
    };

    [SerializeField] private BlockManager blockManager;
    [SerializeField] private BoxCollider2D baseCollider;
    [SerializeField] private SpriteRenderer baseRenderer;
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private GameObject optionsRoot;
    [SerializeField] private BoxCollider2D[] optionColliders;
    [SerializeField] private BoxCollider2D optionsAreaCollider;
    [SerializeField] private Transform[] optionTransforms;
    [SerializeField] private SpriteRenderer[] optionRenderers;
    [SerializeField] private Sprite[] normalOptionSprites;
    [SerializeField] private Sprite[] selectedOptionSprites;
    [SerializeField] private Sprite baseSprite;
    [SerializeField] private Vector3 baseVisualScale;
    [SerializeField] private Vector3 baseVisualPosition;

    private Camera pointerCamera;
    private Coroutine optionsAnimation;
    private bool isPlayMode;
    private bool isOpen;
    private bool isAnimating;
    private bool isCarryingPlayer;
    private int selectedOptionIndex;
    private float playerCarryStartProgress;
    private float previousLiftSurfaceY;
    private float liftReleaseSpeed;
    private readonly RaycastHit2D[] liftHits = new RaycastHit2D[16];

    public bool IsOperating => false;

    public void Configure(
        BlockManager manager,
        BoxCollider2D mainCollider,
        SpriteRenderer mainRenderer,
        Rigidbody2D targetPlayerBody,
        Collider2D targetPlayerCollider,
        Sprite fullSprite,
        Sprite compactSprite,
        Sprite hiddenSprite,
        Sprite fullSelectedSprite,
        Sprite compactSelectedSprite,
        Sprite hiddenSelectedSprite)
    {
        blockManager = manager;
        baseCollider = mainCollider;
        baseRenderer = mainRenderer;
        if (baseRenderer != null)
        {
            baseSprite = baseRenderer.sprite;
            baseVisualScale = baseRenderer.transform.localScale;
            baseVisualPosition = baseRenderer.transform.localPosition;
        }
        playerBody = targetPlayerBody;
        playerCollider = targetPlayerCollider;
        optionsRoot = new GameObject("SizeOptions");
        optionsRoot.transform.SetParent(transform, false);

        normalOptionSprites = new[] { fullSprite, compactSprite, hiddenSprite };
        selectedOptionSprites =
            new[] { fullSelectedSprite, compactSelectedSprite, hiddenSelectedSprite };
        optionColliders = new BoxCollider2D[ButtonCenters.Length];
        optionTransforms = new Transform[ButtonCenters.Length];
        optionRenderers = new SpriteRenderer[ButtonCenters.Length];
        for (int i = 0; i < ButtonCenters.Length; i++)
        {
            GameObject visual = new GameObject($"SizeOption{i}");
            visual.transform.SetParent(optionsRoot.transform, false);
            optionTransforms[i] = visual.transform;
            visual.transform.localPosition = new Vector3(
                ButtonCenters[i].x,
                ButtonCenters[i].y,
                -0.04f);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            optionRenderers[i] = renderer;
            renderer.sprite = normalOptionSprites[i];
            renderer.sortingOrder = 20;
            FitRenderer(renderer, ButtonSize);

            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = ButtonSize;
            collider.offset = ButtonCenters[i];
            optionColliders[i] = collider;
        }

        optionsAreaCollider = gameObject.AddComponent<BoxCollider2D>();
        optionsAreaCollider.isTrigger = false;
        optionsAreaCollider.size = OptionsAreaSize;
        optionsAreaCollider.offset = OptionsAreaCenter;

        SetSelectedMode(BlockManager.BigUiSizeMode.Full);
        ApplyClosedState();
    }

    public void OnPlayModeEntered()
    {
        isPlayMode = true;
        SetSelectedMode(BlockManager.BigUiSizeMode.Full);
        ApplyClosedState();
    }

    public void OnBuildModeEntered()
    {
        isPlayMode = false;
        ApplyClosedState();
    }

    private void OnMouseDown()
    {
        if (Input.touchCount == 0)
        {
            HandlePointer(Input.mousePosition);
        }
    }

    private void Update()
    {
        if (!isPlayMode || Input.touchCount == 0)
        {
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began && IsPointerInside(touch.position))
        {
            HandlePointer(touch.position);
        }
    }

    public void BeginOperation()
    {
        if (!isPlayMode)
        {
            return;
        }

        Vector2 screenPosition = Input.touchCount > 0
            ? Input.GetTouch(0).position
            : (Vector2)Input.mousePosition;
        HandlePointer(screenPosition);
    }

    public void CancelOperation()
    {
    }

    private void HandlePointer(Vector2 screenPosition)
    {
        if (!isPlayMode || isAnimating)
        {
            return;
        }

        Vector2 localPoint = transform.InverseTransformPoint(ScreenToWorld(screenPosition));
        if (isOpen)
        {
            for (int i = 0; i < ButtonCenters.Length; i++)
            {
                if (!Contains(localPoint, ButtonCenters[i], ButtonSize))
                {
                    continue;
                }

                BlockManager.BigUiSizeMode mode = i switch
                {
                    0 => BlockManager.BigUiSizeMode.Full,
                    1 => BlockManager.BigUiSizeMode.Compact,
                    _ => BlockManager.BigUiSizeMode.Hidden
                };
                bool changed = blockManager == null || blockManager.SetBigUiSizeMode(mode);
                if (changed)
                {
                    SetSelectedMode(mode);
                }
                return;
            }
        }

        if (baseCollider != null && Contains(localPoint, baseCollider.offset, baseCollider.size))
        {
            if (isOpen)
            {
                StartCloseAnimation();
            }
            else
            {
                ApplyOpenState();
            }
        }
    }

    private void ApplyOpenState()
    {
        isOpen = true;
        isAnimating = true;
        isCarryingPlayer = IsPlayerStandingOnSize();
        optionsRoot?.SetActive(true);
        PlaceBaseRendererInFront();
        SetOptionCollidersEnabled(false);
        if (optionsAreaCollider != null)
        {
            optionsAreaCollider.enabled = false;
        }

        SetOptionPositions(false);
        BeginPlayerCarry(0f);
        optionsAnimation = StartCoroutine(AnimateOptions(true));
    }

    private void ApplyClosedState()
    {
        if (optionsAnimation != null)
        {
            StopCoroutine(optionsAnimation);
            optionsAnimation = null;
        }

        isOpen = false;
        isAnimating = false;
        isCarryingPlayer = false;
        optionsRoot?.SetActive(false);
        SetOptionCollidersEnabled(false);
        if (optionsAreaCollider != null)
        {
            optionsAreaCollider.enabled = false;
        }

        SetOptionPositions(false);
    }

    private void StartCloseAnimation()
    {
        isOpen = false;
        isAnimating = true;
        SetOptionCollidersEnabled(false);
        optionsAnimation = StartCoroutine(AnimateOptions(false));
    }

    private IEnumerator AnimateOptions(bool opening)
    {
        Vector3[] startPositions = new Vector3[optionTransforms.Length];
        for (int i = 0; i < optionTransforms.Length; i++)
        {
            startPositions[i] = optionTransforms[i].localPosition;
        }

        float elapsed = 0f;
        while (elapsed < OptionsAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / OptionsAnimationDuration);
            float easedProgress = opening
                ? progress * progress
                : progress * progress * (3f - 2f * progress);
            for (int i = 0; i < optionTransforms.Length; i++)
            {
                Vector3 target = opening
                    ? GetOpenPosition(i)
                    : GetClosedPosition(i);
                optionTransforms[i].localPosition =
                    Vector3.LerpUnclamped(startPositions[i], target, easedProgress);
            }

            if (opening)
            {
                TryCatchAirbornePlayer(progress);
                CarryPlayerWithFullOption(Time.unscaledDeltaTime);
            }

            yield return null;
        }

        SetOptionPositions(opening);
        isAnimating = false;
        optionsAnimation = null;
        if (opening)
        {
            ReleaseCarriedPlayer();
            if (optionsAreaCollider != null)
            {
                optionsAreaCollider.enabled = true;
            }
            SetOptionCollidersEnabled(true);
        }
        else
        {
            optionsRoot?.SetActive(false);
            if (optionsAreaCollider != null)
            {
                optionsAreaCollider.enabled = false;
            }
        }
    }

    private void SetOptionPositions(bool open)
    {
        if (optionTransforms == null)
        {
            return;
        }

        for (int i = 0; i < optionTransforms.Length; i++)
        {
            if (optionTransforms[i] != null)
            {
                optionTransforms[i].localPosition = open
                    ? GetOpenPosition(i)
                    : GetClosedPosition(i);
            }
        }
    }

    private static Vector3 GetOpenPosition(int index) =>
        new Vector3(ButtonCenters[index].x, ButtonCenters[index].y, -0.04f);

    private static Vector3 GetClosedPosition(int index) =>
        new Vector3(0f, 0f, -0.04f);

    private void PlaceBaseRendererInFront()
    {
        if (baseRenderer == null || optionRenderers == null)
        {
            return;
        }

        int optionSortingOrder = baseRenderer.sortingOrder - 1;
        foreach (SpriteRenderer optionRenderer in optionRenderers)
        {
            if (optionRenderer != null)
            {
                optionRenderer.sortingLayerID = baseRenderer.sortingLayerID;
                optionRenderer.sortingOrder = optionSortingOrder;
            }
        }
    }

    public void SetSelectedMode(BlockManager.BigUiSizeMode mode)
    {
        selectedOptionIndex = mode switch
        {
            BlockManager.BigUiSizeMode.Full => 0,
            BlockManager.BigUiSizeMode.Compact => 1,
            _ => 2
        };

        if (optionRenderers == null || normalOptionSprites == null ||
            selectedOptionSprites == null)
        {
            return;
        }

        for (int i = 0; i < optionRenderers.Length; i++)
        {
            if (optionRenderers[i] == null)
            {
                continue;
            }

            Sprite selectedSprite = i < selectedOptionSprites.Length
                ? selectedOptionSprites[i]
                : null;
            optionRenderers[i].sprite = i == selectedOptionIndex && selectedSprite != null
                ? selectedSprite
                : normalOptionSprites[i];
        }
    }

    public bool IsSelectedRenderer(SpriteRenderer renderer) =>
        renderer != null && optionRenderers != null &&
        selectedOptionIndex >= 0 && selectedOptionIndex < optionRenderers.Length &&
        optionRenderers[selectedOptionIndex] == renderer;

    public void SetDragPreview(bool dragging, Sprite popupSprite)
    {
        if (baseRenderer == null)
        {
            return;
        }

        if (!dragging || popupSprite == null)
        {
            baseRenderer.sprite = baseSprite;
            baseRenderer.transform.localScale = baseVisualScale;
            baseRenderer.transform.localPosition = baseVisualPosition;
            return;
        }

        baseRenderer.sprite = popupSprite;
        Vector2 spriteSize = popupSprite.bounds.size;
        float scaleX = spriteSize.x > Mathf.Epsilon ? 2f / spriteSize.x : 1f;
        float scaleY = spriteSize.y > Mathf.Epsilon ? 3f / spriteSize.y : 1f;
        baseRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        Vector3 spriteCenter = popupSprite.bounds.center;
        baseRenderer.transform.localPosition = new Vector3(
            -spriteCenter.x * scaleX,
            1f - spriteCenter.y * scaleY,
            baseVisualPosition.z);
    }

    private bool IsPlayerStandingOnSize()
    {
        if (playerBody == null || playerCollider == null || baseCollider == null ||
            !playerBody.simulated || !playerCollider.enabled || !baseCollider.enabled)
        {
            return false;
        }

        Physics2D.SyncTransforms();
        Bounds sizeBounds = baseCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        const float contactTolerance = 0.2f;
        bool overlapsHorizontally =
            playerBounds.max.x > sizeBounds.min.x &&
            playerBounds.min.x < sizeBounds.max.x;
        bool isAboveSize = playerBounds.center.y > sizeBounds.center.y;
        float verticalGap = playerBounds.min.y - sizeBounds.max.y;
        ColliderDistance2D distance = playerCollider.Distance(baseCollider);
        bool isInContact = playerCollider.IsTouching(baseCollider) ||
                           distance.isOverlapped ||
                           distance.distance <= contactTolerance ||
                           Mathf.Abs(verticalGap) <= contactTolerance;
        return overlapsHorizontally && isAboveSize && isInContact;
    }

    private void BeginPlayerCarry(float openingProgress)
    {
        if (!isCarryingPlayer)
        {
            return;
        }

        playerCarryStartProgress = Mathf.Clamp01(openingProgress);
        previousLiftSurfaceY = GetLiftSurfaceY();
        liftReleaseSpeed = 0f;
        Vector2 velocity = playerBody.linearVelocity;
        playerBody.linearVelocity = new Vector2(velocity.x, 0f);
        playerBody.WakeUp();
    }

    private void TryCatchAirbornePlayer(float openingProgress)
    {
        if (isCarryingPlayer || playerBody == null || playerCollider == null ||
            !playerBody.simulated || !playerCollider.enabled || optionRenderers == null)
        {
            return;
        }

        Physics2D.SyncTransforms();
        Bounds playerBounds = playerCollider.bounds;
        foreach (SpriteRenderer optionRenderer in optionRenderers)
        {
            if (optionRenderer == null || !optionRenderer.enabled ||
                !optionRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds optionBounds = optionRenderer.bounds;
            bool overlaps =
                playerBounds.max.x > optionBounds.min.x &&
                playerBounds.min.x < optionBounds.max.x &&
                playerBounds.max.y > optionBounds.min.y &&
                playerBounds.min.y < optionBounds.max.y;
            if (!overlaps)
            {
                continue;
            }

            isCarryingPlayer = true;
            BeginPlayerCarry(openingProgress);
            return;
        }
    }

    private void CarryPlayerWithFullOption(float deltaTime)
    {
        if (!isCarryingPlayer || playerBody == null || playerCollider == null)
        {
            return;
        }

        float surfaceY = GetLiftSurfaceY();
        if (deltaTime > Mathf.Epsilon)
        {
            liftReleaseSpeed = Mathf.Max(0f, (surfaceY - previousLiftSurfaceY) / deltaTime);
        }
        previousLiftSurfaceY = surfaceY;

        Physics2D.SyncTransforms();
        Bounds playerBounds = playerCollider.bounds;
        float verticalCorrection = surfaceY - playerBounds.min.y + 0.01f;
        if (verticalCorrection > 0f)
        {
            float allowedDistance = GetAllowedUpwardDistance(verticalCorrection);
            playerBody.position += Vector2.up * allowedDistance;
            if (allowedDistance < verticalCorrection - 0.001f)
            {
                blockManager?.ReportPlayerCrushedByMovingBlock();
                isCarryingPlayer = false;
            }
        }
        Vector2 velocity = playerBody.linearVelocity;
        playerBody.linearVelocity = new Vector2(velocity.x, 0f);
        playerBody.WakeUp();
        Physics2D.SyncTransforms();
    }

    private float GetAllowedUpwardDistance(float requestedDistance)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false
        };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(playerCollider.gameObject.layer));

        int hitCount = playerCollider.Cast(
            Vector2.up,
            filter,
            liftHits,
            requestedDistance + 0.001f);
        float allowedDistance = requestedDistance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = liftHits[i];
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null || hitCollider == playerCollider ||
                hitCollider == baseCollider || hitCollider.isTrigger ||
                !hitCollider.enabled || Vector2.Dot(hit.normal, Vector2.up) >= -0.5f)
            {
                continue;
            }

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance));
        }

        return allowedDistance;
    }

    private float GetLiftSurfaceY()
    {
        float surfaceY = baseCollider != null ? baseCollider.bounds.max.y : transform.position.y;
        if (optionRenderers != null && optionRenderers.Length > 0 &&
            optionRenderers[0] != null && optionRenderers[0].gameObject.activeInHierarchy)
        {
            surfaceY = Mathf.Max(surfaceY, optionRenderers[0].bounds.max.y);
        }

        return surfaceY;
    }

    private void ReleaseCarriedPlayer()
    {
        if (!isCarryingPlayer || playerBody == null)
        {
            isCarryingPlayer = false;
            return;
        }

        Vector2 velocity = playerBody.linearVelocity;
        float attachedTimeRatio = 1f - Mathf.Clamp01(playerCarryStartProgress);
        float nominalLiftSpeed = ButtonCenters[0].y / OptionsAnimationDuration;
        float upwardVelocity = Mathf.Clamp(
            Mathf.Max(liftReleaseSpeed, nominalLiftSpeed),
            0f,
            MaximumLaunchSpeed) * attachedTimeRatio;
        playerBody.linearVelocity = new Vector2(velocity.x, upwardVelocity);
        playerBody.WakeUp();
        isCarryingPlayer = false;
    }

    private void SetOptionCollidersEnabled(bool enabled)
    {
        if (optionColliders == null)
        {
            return;
        }

        foreach (BoxCollider2D optionCollider in optionColliders)
        {
            if (optionCollider != null)
            {
                optionCollider.enabled = enabled;
            }
        }
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        pointerCamera ??= Camera.main;
        if (pointerCamera == null)
        {
            return transform.position;
        }

        float depth = Mathf.Abs(pointerCamera.transform.position.z - transform.position.z);
        return pointerCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
    }

    private bool IsPointerInside(Vector2 screenPosition)
    {
        Vector2 worldPoint = ScreenToWorld(screenPosition);
        if (baseCollider != null && baseCollider.enabled && baseCollider.OverlapPoint(worldPoint))
        {
            return true;
        }

        if (optionColliders == null)
        {
            return false;
        }

        foreach (BoxCollider2D optionCollider in optionColliders)
        {
            if (optionCollider != null && optionCollider.enabled && optionCollider.OverlapPoint(worldPoint))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(Vector2 point, Vector2 center, Vector2 size)
    {
        Vector2 halfSize = size * 0.5f;
        return point.x >= center.x - halfSize.x && point.x <= center.x + halfSize.x &&
               point.y >= center.y - halfSize.y && point.y <= center.y + halfSize.y;
    }

    private static void FitRenderer(SpriteRenderer renderer, Vector2 targetSize)
    {
        if (renderer.sprite == null)
        {
            renderer.transform.localScale = new Vector3(targetSize.x, targetSize.y, 1f);
            return;
        }

        Vector2 spriteSize = renderer.sprite.bounds.size;
        float scaleX = spriteSize.x > Mathf.Epsilon ? targetSize.x / spriteSize.x : 1f;
        float scaleY = spriteSize.y > Mathf.Epsilon ? targetSize.y / spriteSize.y : 1f;
        float uniformScale = Mathf.Min(scaleX, scaleY);
        renderer.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }
}
