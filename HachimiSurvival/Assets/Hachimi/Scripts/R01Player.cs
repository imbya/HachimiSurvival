using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
public sealed class R01Player : MonoBehaviour
{
    private R01GameRoot game;
    private R01CombatConfig config;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer faceRenderer;
    private GameObject noseObject;
    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.right;
    private Vector2 lockedAttackDirection = Vector2.right;
    private float currentHealth;
    private float hissEnergy;
    private float dodgeCooldownRemaining;
    private float dodgeTimer;
    private float dodgeTravelled;
    private float damageProtectionRemaining;
    private float hissBuffRemaining;
    private float attackCycleTimer;
    private float attackShotTimer;
    private int shotsRemaining;
    private bool dodgeInvulnerable;
    private bool initialized;

    public Vector2 Position => transform.position;
    public float Radius => config == null ? 0.35f : config.playerRadius;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => config == null ? 100f : config.playerMaxHealth;
    public float HissEnergy => hissEnergy;
    public float MaxHissEnergy => config == null ? 100f : config.hissEnergyMax;
    public float DodgeCooldownRemaining => dodgeCooldownRemaining;
    public bool IsDodgeReady => dodgeCooldownRemaining <= 0f && !IsDead;
    public bool IsDodging => dodgeTimer > 0f;
    public bool IsInvulnerable => dodgeInvulnerable;
    public bool IsHissBuffActive => hissBuffRemaining > 0f;
    public float HissBuffRemaining => hissBuffRemaining;
    public bool IsDead { get; private set; }
    public Vector2 LastMoveDirection => lastMoveDirection;
    public R01Enemy CurrentTarget { get; private set; }
    public float CurrentTargetAge { get; private set; } = 999f;

    public void Initialize(R01GameRoot owner, Vector2 startPosition)
    {
        game = owner;
        config = owner.Config;
        transform.position = owner.ClampToArena(startPosition, config.playerRadius);
        currentHealth = config.playerMaxHealth;
        hissEnergy = Mathf.Clamp(owner.StartingHissEnergy, 0f, config.hissEnergyMax);
        dodgeCooldownRemaining = owner.StartWithDodgeOnCooldown ? config.dodgeCooldown : 0f;
        attackCycleTimer = config.attackCycle;
        CurrentTargetAge = 999f;
        CreateVisual();
        initialized = true;
    }

    public void CaptureFrameInput(Keyboard keyboard)
    {
        if (!initialized || IsDead || keyboard == null)
        {
            return;
        }

        moveInput = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            moveInput.y += 1f;
        }
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            moveInput.y -= 1f;
        }
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            moveInput.x -= 1f;
        }
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            moveInput.x += 1f;
        }

        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        if (moveInput.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = moveInput.normalized;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            TryDodge();
        }

        if (keyboard.qKey.wasPressedThisFrame)
        {
            game.TryHiss();
        }
    }

    private void Update()
    {
        if (!initialized || !game.SimulationActive || IsDead)
        {
            UpdateVisual();
            return;
        }

        float deltaTime = Time.deltaTime;
        dodgeCooldownRemaining = Mathf.Max(0f, dodgeCooldownRemaining - deltaTime);
        damageProtectionRemaining = Mathf.Max(0f, damageProtectionRemaining - deltaTime);
        hissBuffRemaining = Mathf.Max(0f, hissBuffRemaining - deltaTime);

        if (CurrentTarget != null)
        {
            CurrentTargetAge += deltaTime;
        }

        Move(deltaTime);
        UpdateAutomaticAttack(deltaTime);
        UpdateVisual();
    }

    private void Move(float deltaTime)
    {
        if (dodgeTimer > 0f)
        {
            float remaining = Mathf.Max(0f, config.dodgeDistance - dodgeTravelled);
            float speed = config.dodgeDistance / Mathf.Max(0.01f, config.dodgeDuration);
            float step = Mathf.Min(remaining, speed * deltaTime);
            Vector2 next = (Vector2)transform.position + lastMoveDirection * step;
            transform.position = game.ClampToArena(next, config.playerRadius);
            dodgeTravelled += step;
            dodgeTimer -= deltaTime;
            if (dodgeTimer <= 0f || dodgeTravelled >= config.dodgeDistance - 0.001f)
            {
                dodgeTimer = 0f;
                dodgeTravelled = 0f;
                dodgeInvulnerable = false;
            }

            return;
        }

        if (moveInput.sqrMagnitude > 0.0001f)
        {
            Vector2 next = (Vector2)transform.position + moveInput * config.playerSpeed * deltaTime;
            transform.position = game.ClampToArena(next, config.playerRadius);
        }
    }

    private void UpdateAutomaticAttack(float deltaTime)
    {
        if (!game.AutoAttack)
        {
            shotsRemaining = 0;
            return;
        }

        attackCycleTimer -= deltaTime;
        if (shotsRemaining > 0)
        {
            attackShotTimer -= deltaTime;
            if (attackShotTimer <= 0f)
            {
                float range = config.attackRange * (IsHissBuffActive ? config.hissAttackRangeMultiplier : 1f);
                game.SpawnWave(transform.position, lockedAttackDirection, config.attackDamage, range, config.attackWidth);
                shotsRemaining--;
                float multiplier = IsHissBuffActive ? config.hissAttackSpeedMultiplier : 1f;
                attackShotTimer = shotsRemaining > 0 ? config.attackGap / multiplier : 0f;
            }

            return;
        }

        if (attackCycleTimer <= 0f)
        {
            BeginAttack();
        }
    }

    private void BeginAttack()
    {
        float range = config.attackRange * (IsHissBuffActive ? config.hissAttackRangeMultiplier : 1f);
        R01Enemy target = game.FindTarget(transform.position, lastMoveDirection, range, CurrentTarget, CurrentTargetAge);
        if (target == null)
        {
            CurrentTarget = null;
            CurrentTargetAge = 999f;
            attackCycleTimer = 0.2f;
            return;
        }

        CurrentTarget = target;
        CurrentTargetAge = 0f;
        lockedAttackDirection = R01Math.SafeDirection(target.Position - Position, lastMoveDirection);
        shotsRemaining = Mathf.Max(1, config.attacksPerCycle);
        float multiplier = IsHissBuffActive ? config.hissAttackSpeedMultiplier : 1f;
        attackShotTimer = config.attackStartup / multiplier;
        attackCycleTimer = config.attackCycle / multiplier;
    }

    public void PrimeNextAttack(float maxWait)
    {
        if (!initialized || IsDead)
        {
            return;
        }

        attackCycleTimer = Mathf.Min(attackCycleTimer, Mathf.Max(0f, maxWait));
    }

    private void CreateVisual()
    {
        Color bodyColor = new Color(0.18f, 0.82f, 0.98f, 1f);
        GameObject bodyObject = R01VisualFactory.CreateCircle("Player Body", transform, config.playerRadius, bodyColor, 10);
        bodyRenderer = R01VisualFactory.GetRenderer(bodyObject);

        GameObject faceObject = R01VisualFactory.CreateCircle("Player Face", transform, config.playerRadius * 0.58f, new Color(0.96f, 0.86f, 0.58f, 1f), 11);
        faceObject.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        faceRenderer = R01VisualFactory.GetRenderer(faceObject);

        GameObject leftEye = R01VisualFactory.CreateCircle("Player Eye L", transform, 0.045f, new Color(0.03f, 0.05f, 0.08f, 1f), 13);
        leftEye.transform.localPosition = new Vector3(-0.12f, 0.12f, 0f);
        GameObject rightEye = R01VisualFactory.CreateCircle("Player Eye R", transform, 0.045f, new Color(0.03f, 0.05f, 0.08f, 1f), 13);
        rightEye.transform.localPosition = new Vector3(0.12f, 0.12f, 0f);

        noseObject = R01VisualFactory.CreateCircle("Player Nose", transform, 0.04f, new Color(0.25f, 0.08f, 0.08f, 1f), 13);
        noseObject.transform.localPosition = new Vector3(0f, -0.02f, 0f);
    }

    private void UpdateVisual()
    {
        if (bodyRenderer == null)
        {
            return;
        }

        Color bodyColor = new Color(0.18f, 0.82f, 0.98f, 1f);
        if (IsDead)
        {
            bodyColor = new Color(0.28f, 0.32f, 0.4f, 1f);
        }
        else if (IsDodging)
        {
            bodyColor = new Color(0.62f, 0.95f, 1f, 1f);
        }
        else if (IsHissBuffActive)
        {
            bodyColor = new Color(0.28f, 1f, 0.84f, 1f);
        }

        bodyRenderer.color = bodyColor;
        if (faceRenderer != null)
        {
            faceRenderer.color = IsDead ? new Color(0.35f, 0.38f, 0.42f, 1f) : new Color(0.96f, 0.86f, 0.58f, 1f);
        }
    }

    private void TryDodge()
    {
        if (!IsDodgeReady || dodgeTimer > 0f)
        {
            return;
        }

        lastMoveDirection = R01Math.SafeDirection(lastMoveDirection, Vector2.right);
        dodgeCooldownRemaining = config.dodgeCooldown;
        dodgeTimer = config.dodgeDuration;
        dodgeTravelled = 0f;
        dodgeInvulnerable = true;
    }

    public bool TrySpendHiss()
    {
        if (IsDead || hissEnergy < config.hissEnergyMax - 0.001f)
        {
            return false;
        }

        hissEnergy = 0f;
        return true;
    }

    public void StartHissBuff()
    {
        hissBuffRemaining = config.hissBuffDuration;
    }

    public void AddHissEnergy(float amount)
    {
        if (IsDead || IsHissBuffActive || amount <= 0f)
        {
            return;
        }

        hissEnergy = Mathf.Clamp(hissEnergy + amount, 0f, config.hissEnergyMax);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || game.IsPaused || damageProtectionRemaining > 0f || dodgeInvulnerable)
        {
            return;
        }

        float appliedDamage = Mathf.Max(0f, damage);
        currentHealth = Mathf.Max(0f, currentHealth - appliedDamage);
        damageProtectionRemaining = config.playerDamageProtection;
        game.NotifyPlayerDamaged(appliedDamage);

        if (currentHealth <= 0f)
        {
            IsDead = true;
            moveInput = Vector2.zero;
            shotsRemaining = 0;
            game.EndRunByPlayerDeath();
        }
    }
}
