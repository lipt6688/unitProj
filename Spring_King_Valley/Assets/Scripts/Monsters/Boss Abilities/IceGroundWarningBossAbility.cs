using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class IceGroundWarningBossAbility : BossAbility
    {
        [Header("Ice Zone Strike")]
        [SerializeField] private float activationRange = 7f;
        [SerializeField] private float warningRadius = 1.5f;
        [SerializeField] private float warningDuration = 1.15f;
        [SerializeField] private float cooldown = 1.8f;
        [SerializeField] private float footOffsetY = -0.35f;
        [SerializeField] private int damage = 10;
        [SerializeField] private float slowChance = 0.75f;
        [SerializeField] private float slowDuration = 2.4f;
        [SerializeField] private float slowMultiplier = 0.55f;
        [SerializeField] private float freezeDuration = 1f;
        [SerializeField] private Color warningColorA = new Color(0.45f, 0.82f, 1f, 0.2f);
        [SerializeField] private Color warningColorB = new Color(0.72f, 0.94f, 1f, 0.45f);

        private static Sprite circleSprite;

        public override IEnumerator Activate()
        {
            active = true;
            if (monster != null)
                monster.Freeze();

            if (playerCharacter == null)
            {
                yield return new WaitForSeconds(0.2f);
                active = false;
                yield break;
            }

            Vector2 center = (Vector2)playerCharacter.transform.position + new Vector2(0f, footOffsetY);
            GameObject indicator = BuildWarningIndicator(center, warningRadius);

            float elapsed = 0f;
            while (elapsed < warningDuration)
            {
                if (indicator != null)
                {
                    SpriteRenderer sr = indicator.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 12f);
                        sr.color = Color.Lerp(warningColorA, warningColorB, pulse);
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (indicator != null)
                Destroy(indicator);

            TryHitPlayer(center, warningRadius);

            if (cooldown > 0f)
                yield return new WaitForSeconds(cooldown);

            active = false;
        }

        public override float Score()
        {
            if (monster == null || playerCharacter == null)
                return 0f;

            float distance = Vector2.Distance(monster.transform.position, playerCharacter.transform.position);
            if (distance > activationRange)
                return 0f;

            float nearFactor = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, activationRange));
            return 1.25f + nearFactor * 1.2f;
        }

        private void TryHitPlayer(Vector2 center, float radius)
        {
            if (playerCharacter == null)
                return;

            Vector2 playerPos = playerCharacter.transform.position;
            if (Vector2.Distance(playerPos, center) > radius)
                return;

            PlayerHealth hp = playerCharacter.GetComponentInParent<PlayerHealth>();
            if (hp != null)
            {
                hp.TakenDamage(Mathf.Max(1, damage));
            }
            else
            {
                playerCharacter.TakeDamage(Mathf.Max(1, damage), Vector2.zero);
            }

            PlayerIceDebuffController debuff = playerCharacter.GetComponent<PlayerIceDebuffController>();
            if (debuff == null)
                debuff = playerCharacter.gameObject.AddComponent<PlayerIceDebuffController>();

            bool applySlow = Random.value < Mathf.Clamp01(slowChance);
            if (applySlow)
                debuff.ApplySlow(Mathf.Max(0.2f, slowDuration), Mathf.Clamp(slowMultiplier, 0.05f, 1f));
            else
                debuff.ApplyFreeze(Mathf.Max(0.1f, freezeDuration));
        }

        private static GameObject BuildWarningIndicator(Vector2 center, float radius)
        {
            GameObject indicator = new GameObject("IceGroundWarning");
            indicator.transform.position = center;

            SpriteRenderer sr = indicator.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.sortingOrder = 90;
            sr.color = new Color(0.45f, 0.82f, 1f, 0.24f);

            float diameter = Mathf.Max(0.2f, radius * 2f);
            indicator.transform.localScale = new Vector3(diameter, diameter, 1f);
            return indicator;
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
                return circleSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    float fill = 1f - Mathf.Clamp01((dist - 0.72f) / 0.28f);
                    float edge = 1f - Mathf.Clamp01(Mathf.Abs(dist - 0.92f) / 0.06f);
                    float alpha = Mathf.Clamp01(fill * 0.45f + edge * 0.7f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            circleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return circleSprite;
        }
    }
}
