using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10)]
public sealed class R01WaveProjectile : MonoBehaviour
{
    private R01GameRoot game;
    private Vector2 direction = Vector2.right;
    private float damage;
    private float range;
    private float width;
    private float speed;
    private float travelled;
    private readonly HashSet<R01Enemy> hitEnemies = new HashSet<R01Enemy>();
    private SpriteRenderer beamRenderer;

    public void Initialize(R01GameRoot owner, Vector2 startPosition, Vector2 travelDirection, float projectileDamage, float projectileRange, float projectileWidth, float projectileSpeed)
    {
        game = owner;
        direction = R01Math.SafeDirection(travelDirection, Vector2.right);
        damage = projectileDamage;
        range = projectileRange;
        width = projectileWidth;
        speed = projectileSpeed;
        transform.position = startPosition;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        GameObject beam = R01VisualFactory.CreateRect("Wave Beam", transform, 0.48f, Mathf.Max(0.08f, width * 0.45f), new Color(0.46f, 0.93f, 1f, 0.92f), 20);
        beamRenderer = R01VisualFactory.GetRenderer(beam);
        if (beamRenderer != null)
        {
            beamRenderer.color = new Color(0.46f, 0.93f, 1f, 0.92f);
        }
    }

    private void Update()
    {
        if (game == null || !game.SimulationActive)
        {
            return;
        }

        float step = speed * Time.deltaTime;
        Vector2 start = transform.position;
        Vector2 end = start + direction * step;
        transform.position = end;
        travelled += step;

        for (int i = 0; i < game.Enemies.Count; i++)
        {
            R01Enemy enemy = game.Enemies[i];
            if (enemy == null || !enemy.IsAlive || hitEnemies.Contains(enemy))
            {
                continue;
            }

            float hitDistance = width * 0.5f + enemy.Radius;
            if (R01Math.DistancePointToSegment(enemy.Position, start, end) <= hitDistance)
            {
                hitEnemies.Add(enemy);
                enemy.TakeDamage(damage);
            }
        }

        if (travelled >= range)
        {
            Destroy(gameObject);
        }
    }
}
