using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum R01TargetMode
{
    DirectionPriority,
    Simple
}

[DefaultExecutionOrder(-100)]
public sealed class R01GameRoot : MonoBehaviour
{
    [Header("Scene configuration")]
    [SerializeField] private R01CombatConfig config;
    [SerializeField] private bool startMixedPreset;
    [SerializeField] private R01TargetMode startingTargetMode = R01TargetMode.DirectionPriority;
    [SerializeField] private bool startWithAutoAttack = true;

    private readonly List<R01Enemy> enemies = new List<R01Enemy>();
    private Transform worldRoot;
    private Transform actorRoot;
    private Transform effectsRoot;
    private R01Hud hud;
    private R01Enemy lastInterruptedEnemy;
    private Camera sceneCamera;
    private string message = "先看懂对峙猫的预警，再选择侧移、猫扑或 Q 哈气。";
    private float messageTimer;
    private float elapsedTime;
    private bool initialized;
    private bool mixedPreset;
    private bool autoAttack;
    private bool showDebug;
    private bool paused;
    private bool runEnded;
    private bool startWithEmptyHiss;
    private bool startWithDodgeOnCooldown;
    private R01TargetMode targetMode;

    public R01CombatConfig Config => config;
    public IReadOnlyList<R01Enemy> Enemies => enemies;
    public R01Player Player { get; private set; }
    public Transform EffectsRoot => effectsRoot;
    public bool IsPaused => paused;
    public bool IsRunEnded => runEnded;
    public bool SimulationActive => initialized && !paused && !runEnded;
    public bool MixedPreset => mixedPreset;
    public bool AutoAttack => autoAttack;
    public bool ShowDebug => showDebug;
    public float StartingHissEnergy => startWithEmptyHiss ? 0f : config.startingHissEnergy;
    public bool StartWithDodgeOnCooldown => startWithDodgeOnCooldown;
    public R01TargetMode TargetMode => targetMode;
    public float ElapsedTime => elapsedTime;
    public string Message => messageTimer > 0f ? message : string.Empty;
    public Vector2 ArenaMin => -config.arenaSize * 0.5f;
    public Vector2 ArenaMax => config.arenaSize * 0.5f;

    private void Awake()
    {
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<R01CombatConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
        }

        mixedPreset = startMixedPreset;
        autoAttack = startWithAutoAttack;
        targetMode = startingTargetMode;
        BuildWorld();
    }

    private void Start()
    {
        ResetRun();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
            {
                ResetRun();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                showDebug = !showDebug;
                SetMessage(showDebug ? "调试面板：开启" : "调试面板：关闭", 1.2f);
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                mixedPreset = !mixedPreset;
                ResetRun();
                return;
            }

            if (keyboard.f3Key.wasPressedThisFrame)
            {
                TryApplyTestKnockback();
            }

            if (keyboard.f4Key.wasPressedThisFrame)
            {
                startWithEmptyHiss = !startWithEmptyHiss;
                ResetRun();
                SetMessage(startWithEmptyHiss ? "测试状态：Q 从 0 能量开始。" : "测试状态：Q 从满能量开始。", 1.8f);
                return;
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                startWithDodgeOnCooldown = !startWithDodgeOnCooldown;
                ResetRun();
                SetMessage(startWithDodgeOnCooldown ? "测试状态：猫扑从冷却中开始。" : "测试状态：猫扑从就绪开始。", 1.8f);
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                targetMode = R01TargetMode.DirectionPriority;
                SetMessage("选敌 A：方向优先", 1.2f);
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                targetMode = R01TargetMode.Simple;
                SetMessage("选敌 B：打断目标 → 当前目标 → 最近目标", 1.8f);
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                autoAttack = !autoAttack;
                SetMessage(autoAttack ? "自动攻击：开启" : "自动攻击：暂停", 1.2f);
            }

            if (SimulationActive && Player != null)
            {
                Player.CaptureFrameInput(keyboard);
            }
        }

        if (SimulationActive)
        {
            elapsedTime += Time.deltaTime;
            messageTimer = Mathf.Max(0f, messageTimer - Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (!runEnded && initialized && AliveEnemyCount == 0)
        {
            runEnded = true;
            SetMessage("场景清空。按 R 重置；F2 切换单猫／混编预设。", 999f);
        }

        if (hud != null)
        {
            hud.Refresh();
        }
    }

    public int AliveEnemyCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].IsAlive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private void BuildWorld()
    {
        if (worldRoot != null)
        {
            return;
        }

        worldRoot = new GameObject("R01_World").transform;
        actorRoot = new GameObject("Actors").transform;
        actorRoot.SetParent(worldRoot, false);
        effectsRoot = new GameObject("Effects").transform;
        effectsRoot.SetParent(worldRoot, false);

        CreateCamera();
        CreateArena();

        GameObject hudObject = new GameObject("R01_HUD");
        hud = hudObject.AddComponent<R01Hud>();
        hud.Initialize(this);
    }

    private void CreateCamera()
    {
        sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            GameObject cameraObject = new GameObject("R01 Camera");
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        sceneCamera.tag = "MainCamera";
        sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
        sceneCamera.transform.rotation = Quaternion.identity;
        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = config.arenaSize.y * 0.5f + 0.75f;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = new Color(0.035f, 0.055f, 0.09f, 1f);
        sceneCamera.nearClipPlane = 0.1f;
        sceneCamera.farClipPlane = 100f;
    }

    private void CreateArena()
    {
        Color floorColor = new Color(0.08f, 0.12f, 0.18f, 1f);
        Color borderColor = new Color(0.26f, 0.46f, 0.60f, 1f);
        GameObject floor = R01VisualFactory.CreateRect("Arena Floor", worldRoot, config.arenaSize.x, config.arenaSize.y, floorColor, -100);
        floor.transform.position = new Vector3(0f, 0f, 1f);

        Vector2 half = config.arenaSize * 0.5f;
        const float borderThickness = 0.12f;
        CreateArenaBorder("North Border", new Vector2(0f, half.y), new Vector2(config.arenaSize.x, borderThickness), borderColor);
        CreateArenaBorder("South Border", new Vector2(0f, -half.y), new Vector2(config.arenaSize.x, borderThickness), borderColor);
        CreateArenaBorder("East Border", new Vector2(half.x, 0f), new Vector2(borderThickness, config.arenaSize.y), borderColor);
        CreateArenaBorder("West Border", new Vector2(-half.x, 0f), new Vector2(borderThickness, config.arenaSize.y), borderColor);

        for (int x = -8; x <= 8; x++)
        {
            GameObject line = R01VisualFactory.CreateRect("Arena Grid X", worldRoot, 0.015f, config.arenaSize.y, new Color(0.12f, 0.2f, 0.28f, 0.32f), -90);
            line.transform.position = new Vector3(x, 0f, 0.8f);
        }

        for (int y = -4; y <= 4; y++)
        {
            GameObject line = R01VisualFactory.CreateRect("Arena Grid Y", worldRoot, config.arenaSize.x, 0.015f, new Color(0.12f, 0.2f, 0.28f, 0.32f), -90);
            line.transform.position = new Vector3(0f, y, 0.8f);
        }
    }

    private void CreateArenaBorder(string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject border = R01VisualFactory.CreateRect(name, worldRoot, size.x, size.y, color, -80);
        border.transform.position = new Vector3(position.x, position.y, 0.6f);
    }

    public void ResetRun()
    {
        if (actorRoot == null)
        {
            BuildWorld();
        }

        DestroyChildren(actorRoot);
        DestroyChildren(effectsRoot);
        enemies.Clear();
        lastInterruptedEnemy = null;
        elapsedTime = 0f;
        paused = false;
        runEnded = false;
        initialized = true;

        GameObject playerObject = new GameObject("Player");
        playerObject.transform.SetParent(actorRoot, false);
        Player = playerObject.AddComponent<R01Player>();
        Player.Initialize(this, new Vector2(0f, 0f));

        SpawnEnemy(R01EnemyKind.Duel, new Vector2(3.2f, 0f), "Duel Cat");
        if (mixedPreset)
        {
            SpawnEnemy(R01EnemyKind.Grunt, new Vector2(-3.2f, 2.4f), "Grunt Cat A");
            SpawnEnemy(R01EnemyKind.Grunt, new Vector2(-3.2f, -2.4f), "Grunt Cat B");
            SpawnEnemy(R01EnemyKind.Grunt, new Vector2(0f, 3.2f), "Grunt Cat C");
        }

        SetMessage(mixedPreset ? "低密度混编：三只杂兵＋一只对峙猫。" : "单猫对峙：先观察预警，再决定如何反制。", 3f);
    }

    private void DestroyChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private R01Enemy SpawnEnemy(R01EnemyKind kind, Vector2 position, string objectName)
    {
        GameObject enemyObject = new GameObject(objectName);
        enemyObject.transform.SetParent(actorRoot, false);
        R01Enemy enemy = enemyObject.AddComponent<R01Enemy>();
        enemy.Initialize(this, kind, position);
        enemies.Add(enemy);
        return enemy;
    }

    public R01WaveProjectile SpawnWave(Vector2 position, Vector2 direction, float damage, float range, float width)
    {
        GameObject waveObject = new GameObject("Lao Wu Wave");
        waveObject.transform.SetParent(effectsRoot, false);
        R01WaveProjectile wave = waveObject.AddComponent<R01WaveProjectile>();
        wave.Initialize(this, position, direction, damage, range, width, config.attackSpeed);
        return wave;
    }

    public bool TryHiss()
    {
        if (!SimulationActive || Player == null || !Player.TrySpendHiss())
        {
            if (Player != null && Player.HissEnergy < config.hissEnergyMax)
            {
                SetMessage("Q 需要满 100 能量。用老吴叫命中敌人充能。", 1.5f);
            }

            return false;
        }

        bool interrupted = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            R01Enemy enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            float distance = Vector2.Distance(Player.Position, enemy.Position);
            if (distance > config.hissRadius + enemy.Radius)
            {
                continue;
            }

            if (enemy.Kind == R01EnemyKind.Duel)
            {
                interrupted |= enemy.TryInterruptWithHiss();
            }
            else
            {
                Vector2 direction = R01Math.SafeDirection(enemy.Position - Player.Position, Vector2.right);
                enemy.ApplyOrdinaryKnockback(direction * config.hissKnockbackDistance);
            }
        }

        Player.StartHissBuff();
        if (interrupted)
        {
            Player.PrimeNextAttack(0.15f);
        }
        CreateHissPulse();
        SetMessage(interrupted ? "Q 哈气成功：打断叫阵，敌猫已老实 1.2 秒。" : "Q 哈气：强化 6 秒。", 2.2f);
        return true;
    }

    private void TryApplyTestKnockback()
    {
        if (!SimulationActive || Player == null)
        {
            return;
        }

        R01Enemy target = Player.CurrentTarget;
        if (target == null || !target.IsAlive)
        {
            target = FindNearestEnemy(Player.Position);
        }

        if (target == null)
        {
            SetMessage("测试击退：当前没有存活敌人。", 1.2f);
            return;
        }

        Vector2 direction = R01Math.SafeDirection(target.Position - Player.Position, Vector2.right);
        Vector2 before = target.Position;
        target.ApplyOrdinaryKnockback(direction * config.ordinaryTestKnockback);
        float moved = Vector2.Distance(before, target.Position);
        SetMessage(moved > 0.001f ? $"测试击退：{target.name} 移动 {moved:0.00}。" : $"测试击退：{target.name} 当前状态拒绝普通击退。", 1.5f);
    }

    private R01Enemy FindNearestEnemy(Vector2 origin)
    {
        R01Enemy nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < enemies.Count; i++)
        {
            R01Enemy enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            float distance = Vector2.Distance(origin, enemy.Position);
            if (distance < nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void CreateHissPulse()
    {
        GameObject pulseObject = R01VisualFactory.CreateRing("Hiss Pulse", effectsRoot, 1f, new Color(0.42f, 0.95f, 1f, 0.85f), 50);
        pulseObject.transform.position = Player.Position;
        SpriteRenderer renderer = R01VisualFactory.GetRenderer(pulseObject);
        R01PulseView pulse = pulseObject.AddComponent<R01PulseView>();
        pulse.Initialize(renderer, new Color(0.42f, 0.95f, 1f, 0.85f), 0.35f, 0.15f, config.hissRadius * 2f);
    }

    public R01Enemy FindTarget(Vector2 origin, Vector2 facing, float range, R01Enemy currentTarget, float currentTargetAge)
    {
        R01Enemy nearest = null;
        float nearestDistance = float.MaxValue;

        if (lastInterruptedEnemy != null && lastInterruptedEnemy.IsAlive && lastInterruptedEnemy.IsInRevengeWindow && IsInAttackRange(origin, lastInterruptedEnemy, range))
        {
            return lastInterruptedEnemy;
        }

        if (TargetMode == R01TargetMode.Simple && currentTarget != null && currentTarget.IsAlive && IsInAttackRange(origin, currentTarget, range))
        {
            return currentTarget;
        }

        Vector2 safeFacing = R01Math.SafeDirection(facing, Vector2.right);
        R01Enemy directional = null;
        float directionalDistance = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            R01Enemy enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive || !IsInAttackRange(origin, enemy, range))
            {
                continue;
            }

            float distance = Vector2.Distance(origin, enemy.Position);
            if (distance < nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }

            if (TargetMode == R01TargetMode.DirectionPriority && enemy.IsTactical)
            {
                Vector2 toEnemy = R01Math.SafeDirection(enemy.Position - origin, safeFacing);
                if (Vector2.Angle(safeFacing, toEnemy) <= 60f && distance < directionalDistance)
                {
                    directional = enemy;
                    directionalDistance = distance;
                }
            }
        }

        if (TargetMode == R01TargetMode.DirectionPriority && currentTarget != null && currentTarget.IsAlive && currentTarget.IsTactical && IsInAttackRange(origin, currentTarget, range))
        {
            Vector2 toCurrent = R01Math.SafeDirection(currentTarget.Position - origin, safeFacing);
            if (Vector2.Angle(safeFacing, toCurrent) <= 60f)
            {
                return currentTarget;
            }
        }

        if (directional != null)
        {
            return directional;
        }

        if (currentTarget != null && currentTarget.IsAlive && currentTargetAge <= 0.35f && IsInAttackRange(origin, currentTarget, range))
        {
            return currentTarget;
        }

        return nearest;
    }

    private bool IsInAttackRange(Vector2 origin, R01Enemy enemy, float range)
    {
        return Vector2.Distance(origin, enemy.Position) <= range + enemy.Radius;
    }

    public float GetPounceLength(Vector2 origin, Vector2 direction)
    {
        Vector2 safeDirection = R01Math.SafeDirection(direction, Vector2.right);
        float distance = config.pounceLength;

        if (safeDirection.x > 0.0001f)
        {
            distance = Mathf.Min(distance, (ArenaMax.x - origin.x) / safeDirection.x);
        }
        else if (safeDirection.x < -0.0001f)
        {
            distance = Mathf.Min(distance, (ArenaMin.x - origin.x) / safeDirection.x);
        }

        if (safeDirection.y > 0.0001f)
        {
            distance = Mathf.Min(distance, (ArenaMax.y - origin.y) / safeDirection.y);
        }
        else if (safeDirection.y < -0.0001f)
        {
            distance = Mathf.Min(distance, (ArenaMin.y - origin.y) / safeDirection.y);
        }

        return Mathf.Max(0f, distance);
    }

    public Vector2 ClampToArena(Vector2 position, float radius)
    {
        return new Vector2(
            Mathf.Clamp(position.x, ArenaMin.x + radius, ArenaMax.x - radius),
            Mathf.Clamp(position.y, ArenaMin.y + radius, ArenaMax.y - radius));
    }

    public void RememberInterrupted(R01Enemy enemy)
    {
        lastInterruptedEnemy = enemy;
    }

    public void RegisterEffectiveDamage(float damage)
    {
        if (Player == null || Player.IsHissBuffActive || damage <= 0f)
        {
            return;
        }

        Player.AddHissEnergy(damage * config.chargePerDamage);
    }

    public void RegisterEnemyKilled()
    {
        if (Player != null)
        {
            Player.AddHissEnergy(config.chargePerKill);
        }
    }

    public void NotifyPlayerDamaged(float damage)
    {
        SetMessage($"玩家受伤 -{damage:0}，获得 0.5 秒受伤保护。", 0.8f);
    }

    public void EndRunByPlayerDeath()
    {
        runEnded = true;
        SetMessage("玩家倒下。按 R 重试这一段对抗。", 999f);
    }

    public void TogglePause()
    {
        if (runEnded)
        {
            return;
        }

        paused = !paused;
        SetMessage(paused ? "已暂停。按 Esc 继续。" : "继续战斗。", 999f);
    }

    public void SetMessage(string text, float duration)
    {
        message = text;
        messageTimer = Mathf.Max(0.1f, duration);
    }
}
