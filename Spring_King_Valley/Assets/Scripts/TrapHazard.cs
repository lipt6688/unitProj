using UnityEngine;
using Vampire;

[RequireComponent(typeof(BoxCollider2D))]
public class TrapHazard : MonoBehaviour
{
    [Range(0.1f, 1f)]
    public float playerRespawnDelay = 0.28f;

    [Range(0.1f, 2f)]
    public float enemyDropScatter = 0.65f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleContact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
            return;

        HandleContact(collision.collider);
    }

    private void HandleContact(Collider2D other)
    {
        if (other == null)
            return;

        GameObject root = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (root == null)
            return;

        if (root.CompareTag("Player") || root.GetComponentInParent<PlayerHealth>() != null)
        {
            PlayerTrapResponder responder = root.GetComponentInParent<PlayerTrapResponder>();
            if (responder == null)
            {
                GameObject playerRoot = root.transform.root != null ? root.transform.root.gameObject : root;
                responder = playerRoot.GetComponent<PlayerTrapResponder>();
                if (responder == null)
                    responder = playerRoot.AddComponent<PlayerTrapResponder>();
            }

            responder.TriggerTrap(transform.position, playerRespawnDelay);
            return;
        }

        Enemy legacyEnemy = root.GetComponentInParent<Enemy>();
        if (legacyEnemy != null)
        {
            legacyEnemy.FallIntoTrap(transform.position, enemyDropScatter);
            return;
        }

        Monster monster = root.GetComponentInParent<Monster>();
        if (monster != null)
        {
            monster.FallIntoTrap(transform.position, enemyDropScatter);
        }
    }
}
