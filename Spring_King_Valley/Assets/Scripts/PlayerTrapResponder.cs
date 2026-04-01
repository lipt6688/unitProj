using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerTrapResponder : MonoBehaviour
{
    [SerializeField, Range(0.1f, 1f)] private float defaultRespawnDelay = 0.28f;
    [SerializeField, Range(0f, 1f)] private float postRespawnProtection = 0.35f;
    [SerializeField, Min(1)] private int trapDamage = 5;

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private Rigidbody2D rb;
    private SpriteRenderer[] allRenderers;
    private Collider2D[] allColliders;

    private bool isFalling;
    private float nextTrapAllowedAt;
    private Vector3 previousFramePosition;
    private Vector3 lastSafePosition;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();
        allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        allColliders = GetComponentsInChildren<Collider2D>(true);

        previousFramePosition = transform.position;
        lastSafePosition = transform.position;
    }

    private void LateUpdate()
    {
        if (isFalling)
            return;

        if ((transform.position - previousFramePosition).sqrMagnitude > 0.0001f)
            lastSafePosition = previousFramePosition;

        previousFramePosition = transform.position;
    }

    public void TriggerTrap(Vector3 trapPosition, float customDelay = 0f)
    {
        if (isFalling)
            return;
        if (Time.unscaledTime < nextTrapAllowedAt)
            return;

        float delay = customDelay > 0f ? customDelay : defaultRespawnDelay;
        Vector3 respawnPosition = lastSafePosition;

        if ((respawnPosition - transform.position).sqrMagnitude < 0.0004f)
            respawnPosition = previousFramePosition;

        StartCoroutine(FallAndRespawn(respawnPosition, delay));
    }

    private IEnumerator FallAndRespawn(Vector3 respawnPosition, float delay)
    {
        isFalling = true;

        if (playerHealth != null)
            playerHealth.TakenDamage(trapDamage);

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false;
        }

        if (playerMovement != null)
            playerMovement.enabled = false;

        SetColliders(false);
        SetRenderers(false);

        yield return new WaitForSecondsRealtime(delay);

        transform.position = respawnPosition;
        if (rb != null)
        {
            rb.position = respawnPosition;
            rb.simulated = true;
        }

        SetRenderers(true);
        SetColliders(true);

        if (playerMovement != null)
            playerMovement.enabled = true;

        previousFramePosition = transform.position;
        lastSafePosition = transform.position;
        nextTrapAllowedAt = Time.unscaledTime + postRespawnProtection;
        isFalling = false;
    }

    private void SetRenderers(bool enabled)
    {
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null)
                allRenderers[i].enabled = enabled;
        }
    }

    private void SetColliders(bool enabled)
    {
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != null)
                allColliders[i].enabled = enabled;
        }
    }
}
