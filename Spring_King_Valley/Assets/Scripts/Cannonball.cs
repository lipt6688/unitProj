using UnityEngine;

public class Cannonball : MonoBehaviour
{
    [HideInInspector] public Vector3 targetPos;
    public float speed = 30f;
    public float aoeRadius = 6.5f;
    public int minDamage = 150;
    public int maxDamage = 300;

    public GameObject boomEffect;
    public GameObject damageCanvas;

    private void Start()
    {
        transform.position = targetPos + new Vector3(0, 20f, 0);
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) <= 0.05f)
        {
            Explode();
        }
    }

    private void Explode()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, aoeRadius);
        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Enemy"))
                continue;

            ITakenDamage enemy = hit.GetComponent<ITakenDamage>();
            if (enemy != null && !enemy.isAttack)
            {
                int hi = maxDamage <= minDamage ? minDamage + 1 : maxDamage;
                int dmg = Random.Range(minDamage, hi);
                enemy.TakenDamage(dmg);

                if (damageCanvas != null)
                {
                    DamageNum damagable = Instantiate(damageCanvas, hit.transform.position, Quaternion.identity).GetComponent<DamageNum>();
                    damagable.ShowDamage(dmg);
                }
            }
        }

        if (boomEffect != null)
        {
            Instantiate(boomEffect, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, aoeRadius);
    }
}
