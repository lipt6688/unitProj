using UnityEngine;

namespace Vampire
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class ChargeFlameHazard : MonoBehaviour
    {
        private static Sprite pixelSprite;

        private SpriteRenderer spriteRenderer;
        private float lifetime;
        private float damagePerSecond;
        private float tickInterval;
        private float nextTickAt;
        private float endTime;
        private Color colorA;
        private Color colorB;

        public void Setup(SpriteRenderer sr, float life, float dps, float tick, Color a, Color b)
        {
            spriteRenderer = sr;
            lifetime = Mathf.Max(0.1f, life);
            damagePerSecond = Mathf.Max(0f, dps);
            tickInterval = Mathf.Max(0.05f, tick);
            colorA = a;
            colorB = b;

            endTime = Time.time + lifetime;
            nextTickAt = Time.time;

            if (spriteRenderer != null)
            {
                if (pixelSprite == null)
                {
                    Texture2D tex = new Texture2D(1, 1);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    pixelSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
                }

                spriteRenderer.sprite = pixelSprite;
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
                BoxCollider2D box = GetComponent<BoxCollider2D>();
                if (box != null)
                    spriteRenderer.size = box.size * 1.15f;
                spriteRenderer.sortingOrder = 80;
                spriteRenderer.color = colorA;
            }
        }

        private void Update()
        {
            if (Time.time >= endTime)
            {
                Destroy(gameObject);
                return;
            }

            if (spriteRenderer != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
                Color c = Color.Lerp(colorA, colorB, pulse);
                c.a = Mathf.Lerp(0.7f, 0.95f, pulse);
                spriteRenderer.color = c;
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other == null || Time.time < nextTickAt)
                return;

            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null)
                return;

            int tickDamage = Mathf.Max(1, Mathf.RoundToInt(damagePerSecond * tickInterval));
            player.TakenDamage(tickDamage);
            nextTickAt = Time.time + tickInterval;
        }
    }
}
