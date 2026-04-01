using System.Collections.Generic;
using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public float speed = 10f;
    public int minAttack = 5;
    public int maxAttack = 10;
    public float lifeTime = 2f;
    public bool isPiercing;
    public float knockbackForce = 0f;
    public Color projectileTint = Color.white;

    public GameObject hitEffect;
    public GameObject damageCanvas;

    private readonly List<Collider2D> hitEnemies = new List<Collider2D>();
    private bool destroyedByHit;

    private void Start()
    {
        Destroy(gameObject, lifeTime);

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = projectileTint;
        }
    }

    private void Update()
    {
        Vector2 start = transform.position;
        Vector2 end = start + (Vector2)(transform.right * speed * Time.deltaTime);

        if (!TrySweepHit(start, end))
        {
            transform.position = end;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleCollision(other);
    }

    private bool TrySweepHit(Vector2 start, Vector2 end)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(start, end);
        if (hits == null || hits.Length == 0) return false;

        float nearestDistance = float.MaxValue;
        Collider2D nearestCollider = null;
        Vector2 nearestPoint = end;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i].collider;
            if (collider == null) continue;

            if (!collider.CompareTag("Enemy") && !collider.CompareTag("Wall")) continue;

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                nearestCollider = collider;
                nearestPoint = hits[i].point;
            }
        }

        if (nearestCollider == null) return false;

        transform.position = nearestPoint;
        HandleCollision(nearestCollider);
        return destroyedByHit;
    }

    private void HandleCollision(Collider2D other)
    {
        if (destroyedByHit || other == null) return;

        if (other.CompareTag("Enemy") && !hitEnemies.Contains(other))
        {
            hitEnemies.Add(other);

            ITakenDamage enemy = other.GetComponent<ITakenDamage>();
            if (enemy != null && !enemy.isAttack)
            {
                int hi = maxAttack <= minAttack ? minAttack + 1 : maxAttack;
                int attackDamage = Random.Range(minAttack, hi);
                enemy.TakenDamage(attackDamage);

                Vector2 knockDir = transform.right;
                Rigidbody2D selfRb = GetComponent<Rigidbody2D>();
                if (selfRb != null && selfRb.velocity.sqrMagnitude > 0.0001f)
                {
                    knockDir = selfRb.velocity.normalized;
                }
                ApplyKnockback(other.gameObject, knockDir);

                if (hitEffect != null)
                {
                    Instantiate(hitEffect, transform.position, Quaternion.identity);
                }

                if (damageCanvas != null)
                {
                    DamageNum damagable = Instantiate(damageCanvas, other.transform.position, Quaternion.identity).GetComponent<DamageNum>();
                    damagable.ShowDamage(attackDamage);
                }

                if (!isPiercing)
                {
                    destroyedByHit = true;
                    Destroy(gameObject);
                }
            }
        }
        else if (other.CompareTag("Wall"))
        {
            if (hitEffect != null)
            {
                Instantiate(hitEffect, transform.position, Quaternion.identity);
            }
            destroyedByHit = true;
            Destroy(gameObject);
        }
    }

    private void ApplyKnockback(GameObject enemy, Vector2 knockDir)
    {
        if (knockbackForce <= 0f || enemy == null) return;
        if (enemy.GetComponentInParent<Vampire.BossMonster>() != null) return;

        if (knockDir.sqrMagnitude < 0.0001f)
            knockDir = Vector2.right;
        knockDir.Normalize();

        Vampire.Monster vampireMonster = enemy.GetComponentInParent<Vampire.Monster>();
        if (vampireMonster != null)
        {
            vampireMonster.Knockback(knockDir * (knockbackForce * 1.2f));
            vampireMonster.transform.position += (Vector3)(knockDir * knockbackForce * 0.1f);
            return;
        }

        Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
        if (enemyRb != null)
        {
            enemyRb.velocity = Vector2.zero;
            enemyRb.AddForce(knockDir * (knockbackForce * 2.2f), ForceMode2D.Impulse);
            enemyRb.transform.position += (Vector3)(knockDir * knockbackForce * 0.1f);
            return;
        }

        enemy.transform.position += (Vector3)(knockDir * knockbackForce * 0.3f);
    }
}
