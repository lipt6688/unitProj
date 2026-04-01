using UnityEngine;

public class PlayerIceDebuffController : MonoBehaviour
{
    [SerializeField] private Color freezeTint = new Color(0.55f, 0.78f, 1f, 1f);

    private PlayerMovement playerMovement;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private bool tintApplied;

    private float slowUntil;
    private float slowMultiplier = 1f;
    private float freezeUntil;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers != null)
        {
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    originalColors[i] = renderers[i].color;
            }
        }
    }

    private void Update()
    {
        bool frozen = Time.time < freezeUntil;
        bool slowed = Time.time < slowUntil;

        float speedMultiplier = 1f;
        if (frozen)
            speedMultiplier = 0f;
        else if (slowed)
            speedMultiplier = Mathf.Clamp(slowMultiplier, 0.05f, 1f);

        if (playerMovement != null)
        {
            playerMovement.SetExternalSpeedMultiplier(speedMultiplier);
            playerMovement.SetMovementLocked(frozen);
        }

        if (frozen)
            ApplyFreezeTint();
        else
            ClearFreezeTint();
    }

    public void ApplySlow(float duration, float multiplier)
    {
        if (duration <= 0f)
            return;

        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
        slowMultiplier = Mathf.Min(slowMultiplier, Mathf.Clamp(multiplier, 0.05f, 1f));
    }

    public void ApplyFreeze(float duration)
    {
        if (duration <= 0f)
            return;

        freezeUntil = Mathf.Max(freezeUntil, Time.time + duration);
    }

    private void ApplyFreezeTint()
    {
        if (tintApplied || renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            originalColors[i] = renderers[i].color;
            renderers[i].color = freezeTint;
        }

        tintApplied = true;
    }

    private void ClearFreezeTint()
    {
        if (!tintApplied || renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].color = originalColors[i];
        }

        tintApplied = false;
    }

    private void OnDisable()
    {
        ClearFreezeTint();
        if (playerMovement != null)
        {
            playerMovement.SetExternalSpeedMultiplier(1f);
            playerMovement.SetMovementLocked(false);
        }
    }
}
