using UnityEngine;

[CreateAssetMenu(menuName = "Hachimi/R01 Combat Config", fileName = "R01_CombatConfig")]
public sealed class R01CombatConfig : ScriptableObject
{
    [Header("Arena and player")]
    public Vector2 arenaSize = new Vector2(18f, 10f);
    public float playerRadius = 0.35f;
    public float playerMaxHealth = 100f;
    public float playerSpeed = 4.5f;

    [Header("Cat pounce")]
    public float dodgeDistance = 2.4f;
    public float dodgeDuration = 0.18f;
    public float dodgeCooldown = 3f;

    [Header("Lao Wu attack")]
    public float attackCycle = 1f;
    public float attackGap = 0.1f;
    public float attackStartup = 0.12f;
    public float attackRange = 6f;
    public float attackWidth = 0.5f;
    public float attackSpeed = 12f;
    public float attackDamage = 8f;
    public int attacksPerCycle = 2;

    [Header("Duel cat")]
    public float duelRadius = 0.45f;
    public float duelMaxHealth = 64f;
    public float duelSpeed = 2f;
    public float duelApproachDistance = 3.4f;
    public float duelObserveDuration = 0.3f;
    public float duelLockDuration = 0.5f;
    public float pounceLength = 4f;
    public float pounceWidth = 0.8f;
    public float pounceSpeed = 10f;
    public float pounceDamage = 20f;
    public float duelRecoveryDuration = 0.8f;
    public float duelInterruptedDuration = 1.2f;

    [Header("Grunt cat")]
    public float gruntRadius = 0.3f;
    public float gruntMaxHealth = 16f;
    public float gruntSpeed = 1.6f;
    public float gruntContactDamage = 8f;
    public float gruntContactCooldown = 0.8f;
    public float hissKnockbackDistance = 1f;

    [Header("Hiss and charge")]
    public float hissEnergyMax = 100f;
    public float startingHissEnergy = 100f;
    public float hissRadius = 3.5f;
    public float hissBuffDuration = 6f;
    public float hissAttackSpeedMultiplier = 1.3f;
    public float hissAttackRangeMultiplier = 1.15f;
    public float chargePerDamage = 0.07f;
    public float chargePerKill = 1f;

    [Header("Shared damage rules")]
    public float playerDamageProtection = 0.5f;
    public float entryDelay = 0.6f;
    public float ordinaryTestKnockback = 0.5f;
}

public static class R01Math
{
    public static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.000001f)
        {
            return Vector2.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return Vector2.Distance(point, start + segment * t);
    }

    public static Vector2 SafeDirection(Vector2 direction, Vector2 fallback)
    {
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallback.normalized;
    }
}
