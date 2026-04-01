using UnityEngine;

namespace Vampire
{
    public enum CoinType 
    {
        Bronze1 = 1,
        Silver2 = 2,
        Gold5 = 5,
        Pouch30 = 30,
        Bag50 = 50
    }

    public class Coin : Collectable
    {
        [Header("Auto Pickup")]
        [SerializeField, Range(0.5f, 12f)] private float autoPickupRange = 2.0f;

        [Header("Coin Dependencies")]
        [SerializeField] protected CoinBlueprint coinBlueprint;
        protected SpriteRenderer spriteRenderer;
        protected CoinType coinType;
        public CoinType CoinType { get => coinType; }

        protected override bool AutoCollectInRange => true;
        protected override float AutoCollectRange => autoPickupRange;
        protected override bool IgnoreInventoryCapacity => true;

        protected override void Awake()
        {
            base.Awake();
            if (autoPickupRange < 0.5f)
                autoPickupRange = 2.0f;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void Setup(Vector2 position, CoinType coinType = CoinType.Bronze1, bool spawnAnimation = true, bool collectableDuringSpawn = true)
        {
            this.coinType = coinType;
            spriteRenderer.sprite = coinBlueprint.coinSprites[coinType];
            transform.position = position;
            base.Setup(spawnAnimation, collectableDuringSpawn);
        }

        protected override void OnCollected()
        {
            int amount = (int)coinType;
            if (CoinWallet.Instance != null)
                CoinWallet.Instance.AddCoins(amount);
            DropCounterBoard.AddCoins(amount);
            entityManager.DespawnCoin(this);
        }
    }
}
