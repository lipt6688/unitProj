using System.Collections;
using UnityEngine;

namespace Vampire
{
    public enum GemType 
    {
        White1 = 1,
        Blue2 = 2,
        Green10 = 10,
        Red50 = 50
    }

    public class ExpGem : Collectable
    {
        [Header("Auto Pickup")]
        [SerializeField, Range(0.5f, 12f)] private float autoPickupRange = 2.0f;

        [Header("Gem Dependencies")]
        [SerializeField] protected ExpGemBlueprint gemBlueprint;
        protected GemType gemType;
        protected TrailRenderer trailRenderer;
        protected SpriteRenderer spriteRenderer;

        protected override bool AutoCollectInRange => true;
        protected override float AutoCollectRange => autoPickupRange;
        protected override bool IgnoreInventoryCapacity => true;

        public TrailRenderer TrailRenderer { get => trailRenderer; }

        protected override void Awake()
        {
            base.Awake();
            if (autoPickupRange < 0.5f)
                autoPickupRange = 2.0f;
            trailRenderer = GetComponent<TrailRenderer>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void Setup(Vector2 position, GemType gemType = GemType.White1, bool spawnAnimation = true)
        {
            trailRenderer.emitting = false;
            transform.position = position;
            trailRenderer.emitting = true;
            this.gemType = gemType;
            (Sprite sprite, Color color) = gemBlueprint.gemSpritesAndColors[gemType];
            spriteRenderer.sprite = sprite;
            trailRenderer.startColor = color;
            trailRenderer.endColor = color;
            base.Setup(spawnAnimation);
        }

        protected override void OnCollected()
        {
            spriteRenderer.enabled = false;
            playerCharacter.GainExp((float)gemType);
            DropCounterBoard.AddExp((int)gemType);
            entityManager.DespawnGem(this);
            spriteRenderer.enabled = true;
        }
    }
}
