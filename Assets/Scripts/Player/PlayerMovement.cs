using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimiento 2D con Rigidbody2D para Unity 6 (usa linearVelocity y el nuevo Input System).
/// Incluye: movimiento lateral, salto (coyote + buffer + altura variable),
/// wall slide / wall jump (las paredes son Ground), dash apilable con salto
/// que conserva el momentum en el aire, y subida automática de bordillos (stepping).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Detección (Layer Ground para suelo y paredes)")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.08f;
    [SerializeField] private float wallCheckDistance = 0.08f;

    [Header("Movimiento lateral")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float groundAcceleration = 90f;
    [SerializeField] private float groundDeceleration = 70f;
    [SerializeField] private float airAcceleration = 50f;
    [SerializeField] private float airDeceleration = 20f;
    [Tooltip("Frenado en el aire cuando vas por encima de maxSpeed (momentum del dash). Más bajo = conservas más.")]
    [SerializeField] private float overspeedAirDrag = 3f;

    [Header("Salto")]
    [SerializeField] private float jumpVelocity = 14f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField] private float fallGravityMultiplier = 1.8f;
    [Tooltip("Gravedad extra si sueltas el botón mientras subes (salto de altura variable).")]
    [SerializeField] private float lowJumpGravityMultiplier = 2.5f;
    [SerializeField] private float maxFallSpeed = 25f;

    [Header("Wall slide / Wall jump")]
    [SerializeField] private float wallSlideSpeed = 2.5f;
    [SerializeField] private Vector2 wallJumpVelocity = new Vector2(9f, 13f);
    [SerializeField] private float wallJumpControlLockTime = 0.15f;
    [SerializeField] private float wallCoyoteTime = 0.1f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.25f;
    [SerializeField] private int maxAirDashes = 1;
    [Tooltip("Fracción de la velocidad del dash que se conserva al terminar (momentum).")]
    [Range(0f, 1f)] [SerializeField] private float dashExitMomentum = 0.8f;

    [Header("Escalones (stepping)")]
    [Tooltip("Altura máxima de bordillo que se sube automáticamente.")]
    [SerializeField] private float maxStepHeight = 0.4f;
    [Tooltip("Distancia frontal a la que se detecta el bordillo.")]
    [SerializeField] private float stepCheckDistance = 0.12f;
    [Tooltip("Velocidad horizontal mínima tras subir (para no perder impulso al chocar).")]
    [SerializeField] private float stepMinSpeed = 4f;
    [Tooltip("Duración de la subida del escalón (segundos). Más alto = más suave/lento.")]
    [SerializeField] private float stepDuration = 0.1f;

    [Header("Otros")]
    [SerializeField] private bool flipByScale = true;

    // --- Estado público de solo lectura (útil para animaciones) ---
    public bool IsGrounded { get; private set; }
    public bool IsWallSliding { get; private set; }
    public bool IsDashing { get; private set; }
    public bool IsStepping { get; private set; }
    public int Facing { get; private set; } = 1;

    private Rigidbody2D rb;
    private Collider2D col;
    private float baseGravityScale;

    // Input
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;
    private float moveInput;
    private bool jumpHeld;

    // Timers
    private float coyoteTimer;
    private float wallCoyoteTimer;
    private float jumpBufferTimer;
    private float wallJumpLockTimer;
    private float dashTimer;
    private float dashCooldownTimer;
    private float ignoreGroundTimer;
    private bool dashBuffered;

    // Estado
    private int wallDir;          // -1 izquierda, 1 derecha, 0 ninguna (solo en el aire)
    private int lastWallDir;
    private int dashDir;
    private int airDashesLeft;
    private bool isJumping;

    // Step suave
    private int stepDir;
    private float stepTimer;
    private float stepStartY;
    private float stepTargetY;

    #region Unity

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        baseGravityScale = rb.gravityScale;

        // Acciones definidas por código (sin necesidad de un asset de Input Actions)
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");
        moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/leftArrow")
            .With("Positive", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick/x");

        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        dashAction = new InputAction("Dash", InputActionType.Button);
        dashAction.AddBinding("<Keyboard>/leftShift");
        dashAction.AddBinding("<Gamepad>/buttonWest");
        dashAction.AddBinding("<Gamepad>/rightTrigger");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();

        jumpAction.performed += OnJumpPerformed;
        jumpAction.canceled += OnJumpCanceled;
        dashAction.performed += OnDashPerformed;
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJumpPerformed;
        jumpAction.canceled -= OnJumpCanceled;
        dashAction.performed -= OnDashPerformed;

        moveAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
        jumpAction?.Dispose();
        dashAction?.Dispose();
    }

    private void Update()
    {
        // Solo lectura de input; la física va en FixedUpdate
        float raw = moveAction.ReadValue<float>();
        moveInput = Mathf.Abs(raw) > 0.2f ? Mathf.Sign(raw) : 0f;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        UpdateTimers(dt);
        CheckSurroundings();

        if (IsDashing)
        {
            HandleDash(dt);
            return;
        }

        TryStartDash();
        if (IsDashing) return;

        if (IsStepping)
        {
            HandleStepClimb(dt);
            if (IsStepping)
            {
                UpdateFacing();
                return;
            }
        }

        HandleJump();
        HandleHorizontal(dt);
        HandleStep();
        HandleGravity();
        UpdateFacing();
    }

    #endregion

    #region Input callbacks

    private void OnJumpPerformed(InputAction.CallbackContext _)
    {
        jumpBufferTimer = jumpBufferTime;
        jumpHeld = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext _) => jumpHeld = false;

    private void OnDashPerformed(InputAction.CallbackContext _) => dashBuffered = true;

    #endregion

    #region Checks / timers

    private void UpdateTimers(float dt)
    {
        jumpBufferTimer -= dt;
        coyoteTimer -= dt;
        wallCoyoteTimer -= dt;
        wallJumpLockTimer -= dt;
        dashCooldownTimer -= dt;
        ignoreGroundTimer -= dt;
    }

    private void CheckSurroundings()
    {
        Bounds b = col.bounds;

        // Suelo
        bool groundHit = Physics2D.OverlapBox(
            new Vector2(b.center.x, b.min.y - groundCheckDistance * 0.5f),
            new Vector2(b.size.x * 0.9f, groundCheckDistance),
            0f, groundLayer);

        IsGrounded = groundHit && ignoreGroundTimer <= 0f;

        // Paredes (el terreno procedural también es Ground)
        Vector2 wallBoxSize = new Vector2(wallCheckDistance, b.size.y * 0.7f);
        float sideOffset = b.extents.x + wallCheckDistance * 0.5f;

        bool leftWall = Physics2D.OverlapBox(new Vector2(b.center.x - sideOffset, b.center.y), wallBoxSize, 0f, groundLayer);
        bool rightWall = Physics2D.OverlapBox(new Vector2(b.center.x + sideOffset, b.center.y), wallBoxSize, 0f, groundLayer);

        int touchingWall = rightWall ? 1 : (leftWall ? -1 : 0);

        // Si tocas ambas, prioriza la que empujas
        if (leftWall && rightWall && moveInput != 0) touchingWall = (int)moveInput;

        wallDir = IsGrounded ? 0 : touchingWall;

        if (IsGrounded)
        {
            coyoteTimer = coyoteTime;
            airDashesLeft = maxAirDashes;
            isJumping = false;
        }

        if (wallDir != 0)
        {
            lastWallDir = wallDir;
            wallCoyoteTimer = wallCoyoteTime;
            airDashesLeft = maxAirDashes; // opcional: recargar dash al tocar pared
        }
    }

    #endregion

    #region Salto / Wall jump

    private void HandleJump()
    {
        if (jumpBufferTimer <= 0f) return;

        // Salto normal (suelo o coyote time)
        if (coyoteTimer > 0f)
        {
            DoJump();
            return;
        }

        // Wall jump (pared actual o coyote de pared)
        if (!IsGrounded && (wallDir != 0 || wallCoyoteTimer > 0f))
        {
            DoWallJump(wallDir != 0 ? wallDir : lastWallDir);
        }
    }

    private void DoJump()
    {
        Vector2 v = rb.linearVelocity;
        v.y = jumpVelocity;          // v.x se conserva (momentum)
        rb.linearVelocity = v;

        ConsumeJump();
        coyoteTimer = 0f;
        isJumping = true;
    }

    private void DoWallJump(int wallSide)
    {
        // Empuja en dirección contraria a la pared
        rb.linearVelocity = new Vector2(-wallSide * wallJumpVelocity.x, wallJumpVelocity.y);

        Facing = -wallSide;
        wallJumpLockTimer = wallJumpControlLockTime;
        wallCoyoteTimer = 0f;
        ConsumeJump();
        isJumping = true;
    }

    private void ConsumeJump()
    {
        jumpBufferTimer = 0f;
        ignoreGroundTimer = 0.1f; // evita re-detectar suelo el frame siguiente
        IsGrounded = false;
    }

    #endregion

    #region Movimiento horizontal

    private void HandleHorizontal(float dt)
    {
        // Tras un wall jump se bloquea el control un instante para que el impulso se note
        if (wallJumpLockTimer > 0f) return;

        Vector2 v = rb.linearVelocity;
        float vx = v.x;
        float target = moveInput * maxSpeed;

        // ¿Vamos por encima de maxSpeed en la dirección del movimiento (o sin input)?
        bool overspeed = Mathf.Abs(vx) > maxSpeed + 0.01f &&
                         (moveInput == 0f || Mathf.Sign(vx) == moveInput);

        float accel;
        if (IsGrounded)
        {
            accel = moveInput != 0f ? groundAcceleration : groundDeceleration;
            if (overspeed) target = Mathf.Sign(vx) * maxSpeed;
        }
        else if (overspeed)
        {
            // Momentum en el aire: casi no frena
            accel = overspeedAirDrag;
            target = Mathf.Sign(vx) * maxSpeed;
        }
        else
        {
            accel = moveInput != 0f ? airAcceleration : airDeceleration;
        }

        v.x = Mathf.MoveTowards(vx, target, accel * dt);
        rb.linearVelocity = v;
    }

    #endregion

    #region Step up (bordillos)

    private void HandleStep()
    {
        if (!IsGrounded || IsDashing || moveInput == 0f) return;
        if (rb.linearVelocity.y > 0.1f) return;           // no durante un salto
        if (wallJumpLockTimer > 0f) return;

        const float skin = 0.02f;
        int dir = (int)moveInput;
        Bounds b = col.bounds;
        float feetY = b.min.y;
        float frontX = b.center.x + dir * (b.extents.x + stepCheckDistance * 0.5f);

        // 1) ¿Hay un obstáculo a la altura de los pies (por debajo del límite)?
        float lowH = maxStepHeight - skin;
        bool lowHit = Physics2D.OverlapBox(
            new Vector2(frontX, feetY + skin + lowH * 0.5f),
            new Vector2(stepCheckDistance, lowH),
            0f, groundLayer);
        if (!lowHit) return;

        // 2) ¿Sigue bloqueado por encima de la altura máxima? Entonces es una pared, no un escalón
        float highH = b.size.y - maxStepHeight - skin * 2f;
        if (highH > 0f)
        {
            bool highHit = Physics2D.OverlapBox(
                new Vector2(frontX, feetY + maxStepHeight + skin + highH * 0.5f),
                new Vector2(stepCheckDistance, highH),
                0f, groundLayer);
            if (highHit) return;
        }

        // 3) Medir la altura real del escalón con un raycast hacia abajo
        Vector2 origin = new Vector2(
            b.center.x + dir * (b.extents.x + stepCheckDistance),
            feetY + maxStepHeight + skin);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, maxStepHeight + skin * 2f, groundLayer);
        if (!hit.collider) return;

        float stepHeight = hit.point.y - feetY;
        if (stepHeight <= 0.01f || stepHeight > maxStepHeight) return;

        // 4) ¿Cabe el cuerpo encima del escalón?
        Vector2 offset = new Vector2(dir * (stepCheckDistance * 0.5f), stepHeight + skin);
        bool blocked = Physics2D.OverlapBox(
            (Vector2)b.center + offset,
            b.size * 0.95f,
            0f, groundLayer);
        if (blocked) return;

        // 5) Iniciar la subida suave (sin teletransporte)
        IsStepping = true;
        stepDir = dir;
        stepTimer = 0f;
        stepStartY = rb.position.y;
        stepTargetY = rb.position.y + stepHeight + skin;
        rb.gravityScale = 0f;
    }

    private void HandleStepClimb(float dt)
    {
        // Saltar durante la subida la cancela y deja que el salto normal se ejecute
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            EndStepClimb();
            return;
        }

        stepTimer += dt;
        float t = Mathf.Clamp01(stepTimer / Mathf.Max(stepDuration, 0.0001f));

        // Curva suave (ease in-out) hacia la altura objetivo
        float desiredY = Mathf.Lerp(stepStartY, stepTargetY, Mathf.SmoothStep(0f, 1f, t));

        Vector2 v = rb.linearVelocity;
        v.x = stepDir * Mathf.Max(Mathf.Abs(v.x), stepMinSpeed);
        v.y = (desiredY - rb.position.y) / dt;
        rb.linearVelocity = v;

        if (t >= 1f)
            EndStepClimb();
    }

    private void EndStepClimb()
    {
        IsStepping = false;
        rb.gravityScale = baseGravityScale;

        Vector2 v = rb.linearVelocity;
        v.y = Mathf.Min(v.y, 0f);
        rb.linearVelocity = v;
    }

    #endregion

    #region Gravedad / Wall slide

    private void HandleGravity()
    {
        Vector2 v = rb.linearVelocity;

        IsWallSliding = wallDir != 0 && v.y < 0f && moveInput == wallDir && !GroundNearBelow(0.3f);

        if (IsWallSliding)
        {
            rb.gravityScale = baseGravityScale;
            v.y = Mathf.Max(v.y, -wallSlideSpeed);
        }
        else if (v.y < 0f)
        {
            rb.gravityScale = baseGravityScale * fallGravityMultiplier;
        }
        else if (v.y > 0f && !jumpHeld && isJumping)
        {
            rb.gravityScale = baseGravityScale * lowJumpGravityMultiplier;
        }
        else
        {
            rb.gravityScale = baseGravityScale;
        }

        v.y = Mathf.Max(v.y, -maxFallSpeed);
        rb.linearVelocity = v;
    }

    private bool GroundNearBelow(float distance)
    {
        Bounds b = col.bounds;
        return Physics2D.BoxCast(b.center, new Vector2(b.size.x * 0.9f, b.size.y),
                                0f, Vector2.down, distance, groundLayer);
    }

    #endregion

    #region Dash

    private void TryStartDash()
    {
        if (!dashBuffered) return;
        dashBuffered = false;

        if (dashCooldownTimer > 0f) return;
        if (!IsGrounded && airDashesLeft <= 0) return;

        if (!IsGrounded) airDashesLeft--;

        dashDir = moveInput != 0f ? (int)moveInput : Facing;
        Facing = dashDir;

        IsDashing = true;
        IsStepping = false;
        IsWallSliding = false;
        dashTimer = dashDuration;
        dashCooldownTimer = dashDuration + dashCooldown;
        isJumping = false;

        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
        UpdateFacing();
    }

    private void HandleDash(float dt)
    {
        dashTimer -= dt;

        // Dash-jump: saltar durante el dash lo cancela y conserva la velocidad horizontal
        if (jumpBufferTimer > 0f && (IsGrounded || coyoteTimer > 0f))
        {
            EndDash(keepMomentum: true);
            DoJump();
            return;
        }

        // Si chocamos con una pared en la dirección del dash, se corta
        if (IsTouchingWallInDirection(dashDir))
        {
            EndDash(keepMomentum: false);
            return;
        }

        rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);

        if (dashTimer <= 0f)
            EndDash(keepMomentum: true);
    }

    private void EndDash(bool keepMomentum)
    {
        IsDashing = false;
        rb.gravityScale = baseGravityScale;

        float vx = 0f;
        if (keepMomentum)
            vx = dashDir * Mathf.Max(maxSpeed, dashSpeed * dashExitMomentum);

        rb.linearVelocity = new Vector2(vx, 0f);
    }

    private bool IsTouchingWallInDirection(int dir)
    {
        Bounds b = col.bounds;
        float offset = b.extents.x + wallCheckDistance * 0.5f;
        return Physics2D.OverlapBox(
            new Vector2(b.center.x + dir * offset, b.center.y),
            new Vector2(wallCheckDistance, b.size.y * 0.7f),
            0f, groundLayer);
    }

    #endregion

    #region Facing

    private void UpdateFacing()
    {
        if (!IsDashing && wallJumpLockTimer <= 0f && moveInput != 0f)
            Facing = (int)moveInput;

        if (flipByScale)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * Facing;
            transform.localScale = s;
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Collider2D c = col != null ? col : GetComponent<Collider2D>();
        if (c == null) return;

        Bounds b = c.bounds;

        // Suelo
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(
            new Vector3(b.center.x, b.min.y - groundCheckDistance * 0.5f),
            new Vector3(b.size.x * 0.9f, groundCheckDistance));

        // Paredes
        Gizmos.color = Color.cyan;
        float off = b.extents.x + wallCheckDistance * 0.5f;
        Vector3 size = new Vector3(wallCheckDistance, b.size.y * 0.7f);
        Gizmos.DrawWireCube(new Vector3(b.center.x - off, b.center.y), size);
        Gizmos.DrawWireCube(new Vector3(b.center.x + off, b.center.y), size);

        // Escalones
        Gizmos.color = Color.yellow;
        float fx = b.extents.x + stepCheckDistance * 0.5f;
        Vector3 stepSize = new Vector3(stepCheckDistance, maxStepHeight);
        Gizmos.DrawWireCube(new Vector3(b.center.x - fx, b.min.y + maxStepHeight * 0.5f), stepSize);
        Gizmos.DrawWireCube(new Vector3(b.center.x + fx, b.min.y + maxStepHeight * 0.5f), stepSize);
    }
#endif
}