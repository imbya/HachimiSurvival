using UnityEngine;

public enum R01EnemyKind
{
    Grunt,
    Duel
}

public enum R01DuelState
{
    Entering,
    Approaching,
    Observing,
    Locked,
    Pouncing,
    Recovering,
    Interrupted,
    Dead
}

[DefaultExecutionOrder(0)]
public sealed class R01Enemy : MonoBehaviour
{
    private R01GameRoot game;
    private R01CombatConfig config;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer faceRenderer;
    private SpriteRenderer warningRenderer;
    private GameObject warningObject;
    private float currentHealth;
    private float stateTimer;
    private float contactCooldown;
    private Vector2 lockedOrigin;
    private Vector2 lockedDirection = Vector2.left;
    private float lockedLength;
    private float lockedTravelled;
    private bool pounceHasHit;
    private bool initialized;

    public R01EnemyKind Kind { get; private set; }
    public R01DuelState DuelState { get; private set; }
    public Vector2 Position => transform.position;
    public float Radius { get; private set; }
    public float CurrentHealth => currentHealth;
    public float MaxHealth { get; private set; }
    public bool IsAlive => DuelState != R01DuelState.Dead && currentHealth > 0f;
    public bool IsTactical => Kind == R01EnemyKind.Duel;
    public bool IsInRevengeWindow => IsAlive && DuelState == R01DuelState.Interrupted && stateTimer > 0f;

    public string StateLabel
    {
        get
        {
            if (Kind == R01EnemyKind.Grunt)
            {
                return IsAlive ? "追击" : "死亡";
            }

            switch (DuelState)
            {
                case R01DuelState.Entering:
                    return "入场";
                case R01DuelState.Approaching:
                    return "接近";
                case R01DuelState.Observing:
                    return $"观察 {Mathf.Max(0f, stateTimer):0.00}";
                case R01DuelState.Locked:
                    return $"锁向预警 {Mathf.Max(0f, stateTimer):0.00}";
                case R01DuelState.Pouncing:
                    return "扑击";
                case R01DuelState.Recovering:
                    return $"舔爪恢复 {Mathf.Max(0f, stateTimer):0.00}";
                case R01DuelState.Interrupted:
                    return $"已老实 {Mathf.Max(0f, stateTimer):0.00}";
                default:
                    return "死亡";
            }
        }
    }

    public void Initialize(R01GameRoot owner, R01EnemyKind kind, Vector2 startPosition)
    {
        game = owner;
        config = owner.Config;
        Kind = kind;
        Radius = kind == R01EnemyKind.Duel ? config.duelRadius : config.gruntRadius;
        MaxHealth = kind == R01EnemyKind.Duel ? config.duelMaxHealth : config.gruntMaxHealth;
        currentHealth = MaxHealth;
        DuelState = kind == R01EnemyKind.Duel ? R01DuelState.Entering : R01DuelState.Approaching;
        stateTimer = kind == R01EnemyKind.Duel ? config.entryDelay : 0f;
        transform.position = owner.ClampToArena(startPosition, Radius);
        CreateVisual();
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        if (game.SimulationActive && IsAlive)
        {
            float deltaTime = Time.deltaTime;
            contactCooldown = Mathf.Max(0f, contactCooldown - deltaTime);
            if (Kind == R01EnemyKind.Grunt)
            {
                UpdateGrunt(deltaTime);
            }
            else
            {
                UpdateDuel(deltaTime);
            }
        }

        UpdateVisual();
    }

    private void UpdateGrunt(float deltaTime)
    {
        if (game.Player == null || game.Player.IsDead)
        {
            return;
        }

        Vector2 toPlayer = game.Player.Position - Position;
        float distance = toPlayer.magnitude;
        if (distance > Radius + game.Player.Radius)
        {
            Vector2 direction = R01Math.SafeDirection(toPlayer, Vector2.left);
            Vector2 next = Position + direction * config.gruntSpeed * deltaTime;
            transform.position = game.ClampToArena(next, Radius);
        }
        else if (contactCooldown <= 0f)
        {
            game.Player.TakeDamage(config.gruntContactDamage);
            contactCooldown = config.gruntContactCooldown;
        }
    }

    private void UpdateDuel(float deltaTime)
    {
        if (game.Player == null || game.Player.IsDead)
        {
            return;
        }

        switch (DuelState)
        {
            case R01DuelState.Entering:
                stateTimer -= deltaTime;
                if (stateTimer <= 0f)
                {
                    DuelState = R01DuelState.Approaching;
                }
                break;

            case R01DuelState.Approaching:
                Vector2 toPlayer = game.Player.Position - Position;
                if (toPlayer.magnitude <= config.duelApproachDistance)
                {
                    BeginObserving();
                }
                else
                {
                    Vector2 direction = R01Math.SafeDirection(toPlayer, Vector2.left);
                    transform.position = game.ClampToArena(Position + direction * config.duelSpeed * deltaTime, Radius);
                }
                break;

            case R01DuelState.Observing:
                stateTimer -= deltaTime;
                UpdateWarning(R01Math.SafeDirection(game.Player.Position - Position, Vector2.left), config.pounceLength, config.pounceWidth, new Color(1f, 0.78f, 0.22f, 0.28f));
                if (stateTimer <= 0f)
                {
                    BeginLockedPounce();
                }
                break;

            case R01DuelState.Locked:
                stateTimer -= deltaTime;
                UpdateWarning(lockedDirection, lockedLength, config.pounceWidth, new Color(1f, 0.24f, 0.16f, 0.42f));
                if (stateTimer <= 0f)
                {
                    BeginPounce();
                }
                break;

            case R01DuelState.Pouncing:
                UpdatePounce(deltaTime);
                break;

            case R01DuelState.Recovering:
                stateTimer -= deltaTime;
                if (stateTimer <= 0f)
                {
                    DuelState = R01DuelState.Approaching;
                }
                break;

            case R01DuelState.Interrupted:
                stateTimer -= deltaTime;
                if (stateTimer <= 0f)
                {
                    DuelState = R01DuelState.Approaching;
                }
                break;
        }
    }

    private void BeginObserving()
    {
        DuelState = R01DuelState.Observing;
        stateTimer = config.duelObserveDuration;
        UpdateWarning(R01Math.SafeDirection(game.Player.Position - Position, Vector2.left), config.pounceLength, config.pounceWidth, new Color(1f, 0.78f, 0.22f, 0.28f));
    }

    private void BeginLockedPounce()
    {
        lockedOrigin = Position;
        lockedDirection = R01Math.SafeDirection(game.Player.Position - lockedOrigin, Vector2.left);
        lockedLength = game.GetPounceLength(lockedOrigin, lockedDirection);
        lockedTravelled = 0f;
        pounceHasHit = false;
        DuelState = R01DuelState.Locked;
        stateTimer = config.duelLockDuration;
        UpdateWarning(lockedDirection, lockedLength, config.pounceWidth, new Color(1f, 0.24f, 0.16f, 0.42f));
    }

    private void BeginPounce()
    {
        DuelState = R01DuelState.Pouncing;
        HideWarning();
    }

    private void UpdatePounce(float deltaTime)
    {
        float step = config.pounceSpeed * deltaTime;
        float nextTravelled = Mathf.Min(lockedLength, lockedTravelled + step);
        Vector2 start = lockedOrigin + lockedDirection * lockedTravelled;
        Vector2 end = lockedOrigin + lockedDirection * nextTravelled;
        transform.position = end;

        if (!pounceHasHit && game.Player != null && !game.Player.IsDead)
        {
            float hitDistance = config.pounceWidth * 0.5f + game.Player.Radius;
            if (R01Math.DistancePointToSegment(game.Player.Position, start, end) <= hitDistance)
            {
                game.Player.TakeDamage(config.pounceDamage);
                pounceHasHit = true;
            }
        }

        lockedTravelled = nextTravelled;
        if (lockedTravelled >= lockedLength - 0.001f)
        {
            DuelState = R01DuelState.Recovering;
            stateTimer = config.duelRecoveryDuration;
        }
    }

    public bool TryInterruptWithHiss()
    {
        if (!IsAlive || Kind != R01EnemyKind.Duel || (DuelState != R01DuelState.Observing && DuelState != R01DuelState.Locked))
        {
            return false;
        }

        DuelState = R01DuelState.Interrupted;
        stateTimer = config.duelInterruptedDuration;
        HideWarning();
        game.RememberInterrupted(this);
        return true;
    }

    public void ApplyOrdinaryKnockback(Vector2 displacement)
    {
        if (!IsAlive || Kind == R01EnemyKind.Duel && (DuelState == R01DuelState.Locked || DuelState == R01DuelState.Pouncing || DuelState == R01DuelState.Interrupted))
        {
            return;
        }

        transform.position = game.ClampToArena(Position + displacement, Radius);
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive)
        {
            return;
        }

        float appliedDamage = Mathf.Max(0f, damage);
        if (DuelState == R01DuelState.Interrupted)
        {
            appliedDamage *= 1.25f;
        }

        currentHealth = Mathf.Max(0f, currentHealth - appliedDamage);
        game.RegisterEffectiveDamage(appliedDamage);
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        currentHealth = 0f;
        DuelState = R01DuelState.Dead;
        HideWarning();
        game.RegisterEnemyKilled();
    }

    private void CreateVisual()
    {
        Color bodyColor = Kind == R01EnemyKind.Duel
            ? new Color(0.84f, 0.34f, 0.86f, 1f)
            : new Color(1f, 0.52f, 0.28f, 1f);
        GameObject bodyObject = R01VisualFactory.CreateCircle("Enemy Body", transform, Radius, bodyColor, 10);
        bodyRenderer = R01VisualFactory.GetRenderer(bodyObject);

        GameObject faceObject = R01VisualFactory.CreateCircle("Enemy Face", transform, Radius * 0.56f, new Color(0.9f, 0.78f, 0.68f, 1f), 11);
        faceObject.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        faceRenderer = R01VisualFactory.GetRenderer(faceObject);

        GameObject eyeLeft = R01VisualFactory.CreateCircle("Enemy Eye L", transform, Radius * 0.12f, new Color(0.06f, 0.03f, 0.08f, 1f), 13);
        eyeLeft.transform.localPosition = new Vector3(-Radius * 0.25f, Radius * 0.2f, 0f);
        GameObject eyeRight = R01VisualFactory.CreateCircle("Enemy Eye R", transform, Radius * 0.12f, new Color(0.06f, 0.03f, 0.08f, 1f), 13);
        eyeRight.transform.localPosition = new Vector3(Radius * 0.25f, Radius * 0.2f, 0f);

        if (Kind == R01EnemyKind.Duel)
        {
            warningObject = R01VisualFactory.CreateRect("Pounce Warning", transform, 1f, 1f, new Color(1f, 0.78f, 0.22f, 0.28f), 5);
            warningRenderer = R01VisualFactory.GetRenderer(warningObject);
            warningObject.transform.localPosition = Vector3.zero;
            warningObject.SetActive(false);
        }
    }

    private void UpdateWarning(Vector2 direction, float length, float width, Color color)
    {
        if (warningObject == null || warningRenderer == null)
        {
            return;
        }

        Vector2 safeDirection = R01Math.SafeDirection(direction, Vector2.left);
        warningObject.SetActive(true);
        warningObject.transform.localPosition = new Vector3(safeDirection.x * length * 0.5f, safeDirection.y * length * 0.5f, 0.05f);
        warningObject.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg);
        warningObject.transform.localScale = new Vector3(length, width, 1f);
        warningRenderer.color = color;
    }

    private void HideWarning()
    {
        if (warningObject != null)
        {
            warningObject.SetActive(false);
        }
    }

    private void UpdateVisual()
    {
        if (bodyRenderer == null)
        {
            return;
        }

        Color color;
        if (!IsAlive)
        {
            color = new Color(0.24f, 0.25f, 0.31f, 1f);
        }
        else if (Kind == R01EnemyKind.Grunt)
        {
            color = new Color(1f, 0.52f, 0.28f, 1f);
        }
        else
        {
            switch (DuelState)
            {
                case R01DuelState.Observing:
                    color = new Color(1f, 0.72f, 0.2f, 1f);
                    break;
                case R01DuelState.Locked:
                case R01DuelState.Pouncing:
                    color = new Color(1f, 0.22f, 0.18f, 1f);
                    break;
                case R01DuelState.Interrupted:
                    color = new Color(0.3f, 1f, 0.52f, 1f);
                    break;
                case R01DuelState.Recovering:
                    color = new Color(0.55f, 0.58f, 1f, 1f);
                    break;
                default:
                    color = new Color(0.84f, 0.34f, 0.86f, 1f);
                    break;
            }
        }

        bodyRenderer.color = color;
        if (faceRenderer != null)
        {
            faceRenderer.color = IsAlive ? new Color(0.9f, 0.78f, 0.68f, 1f) : new Color(0.32f, 0.33f, 0.38f, 1f);
        }
    }
}
