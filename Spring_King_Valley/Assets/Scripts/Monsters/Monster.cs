using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vampire
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Monster : IDamageable, ISpatialHashGridClient, ITakenDamage
    {
        public bool isAttack { get; set; }

        public void TakenDamage(int _amount)
        {
            if (isAttack) return;
            isAttack = true;
            StartCoroutine(ResetAttackCo());
            this.TakeDamage((float)_amount, Vector2.zero);
        }

        private IEnumerator ResetAttackCo()
        {
            yield return new WaitForSeconds(0.2f);
            isAttack = false;
        }

        [SerializeField] protected Material defaultMaterial, whiteMaterial, dissolveMaterial;
        [SerializeField] protected ParticleSystem deathParticles;
        [SerializeField] protected GameObject shadow;
        protected BoxCollider2D monsterHitbox;
        protected CircleCollider2D monsterLegsCollider;
        protected int monsterIndex;
        protected MonsterBlueprint monsterBlueprint;
        protected SpriteAnimator monsterSpriteAnimator;
        protected SpriteRenderer monsterSpriteRenderer;
        protected ZPositioner zPositioner;
        protected float currentHealth;  // 琛€閲?
        protected float maxHealth;
        protected EntityManager entityManager;  // 鎬墿姹?
        protected Character playerCharacter;  // 瑙掕壊
        protected Rigidbody2D rb;
        protected int currWalkSequenceFrame = 0;
        protected bool knockedBack = false;
        protected Coroutine hitAnimationCoroutine = null;
        protected bool alive = true;
        protected bool trapConsumed;
        protected bool kingCityCalmActive;
        protected bool kingCityCalmWander;
        protected Vector2 kingCityWanderDirection;
        protected float nextKingCityWanderChangeTime;
        protected Transform centerTransform;
        public Transform CenterTransform { get => centerTransform; }
        public UnityEvent<Monster> OnKilled { get; } = new UnityEvent<Monster>();
        public float HP => currentHealth;
        public float MaxHP => maxHealth;
        // Spatial Hash Grid Client Interface
        public Vector2 Position => transform.position;
        public Vector2 Size => monsterLegsCollider.bounds.size;
        public Dictionary<int, int> ListIndexByCellIndex { get; set; }
        public int QueryID { get; set; } = -1;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            monsterLegsCollider = GetComponent<CircleCollider2D>();
            monsterSpriteAnimator = GetComponentInChildren<SpriteAnimator>();
            monsterSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            zPositioner = gameObject.AddComponent<ZPositioner>();
            monsterHitbox = monsterSpriteRenderer.gameObject.AddComponent<BoxCollider2D>();
            monsterHitbox.isTrigger = false;
        monsterHitbox.gameObject.tag = "Enemy"; // For old weapon systems
        var proxy = monsterHitbox.gameObject.AddComponent<DamageableProxy>();
        proxy.target = this;
        }

        public virtual void Init(EntityManager entityManager, Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            zPositioner.Init(playerCharacter.transform);
        }

        public virtual void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            this.monsterIndex = monsterIndex;
            this.monsterBlueprint = monsterBlueprint;
            rb.position = position;
            transform.position = position;
            // Reset health to max
            currentHealth = monsterBlueprint.hp + hpBuff;
            maxHealth = currentHealth;
            // Toggle alive flag on
            alive = true;
            trapConsumed = false;
            kingCityCalmActive = false;
            // Add to list of living monsters
            entityManager.LivingMonsters.Add(this);
            // Initialize the animator
            monsterSpriteAnimator.Init(monsterBlueprint.walkSpriteSequence, monsterBlueprint.walkFrameTime, true);
            // Start and reset animation
            monsterSpriteAnimator.StartAnimating(true);
            float visualScale = Mathf.Max(0.01f, monsterBlueprint.visualScale);
            if (monsterSpriteRenderer != null)
                monsterSpriteRenderer.transform.localScale = new Vector3(visualScale, visualScale, 1f);
            // Ensure colliders are enabled and sized correctly
            monsterHitbox.enabled = true;
            monsterHitbox.size = monsterSpriteRenderer.bounds.size / visualScale;
            monsterHitbox.offset = Vector2.zero;
            monsterLegsCollider.radius = monsterHitbox.size.x/2.5f;

            if (IsFireKingBossBlueprint(monsterBlueprint))
            {
                monsterHitbox.size = new Vector2(monsterHitbox.size.x * 0.25f, monsterHitbox.size.y * 0.333f);
                monsterHitbox.offset = Vector2.zero;
                monsterLegsCollider.radius *= 0.65f;
                IgnoreCollisionWithPlayer();
            }
            else if (IsGrassKingBossBlueprint(monsterBlueprint))
            {
                monsterHitbox.size = new Vector2(monsterHitbox.size.x * (2f / 7f), monsterHitbox.size.y * 0.5f);
                monsterHitbox.offset = new Vector2(-monsterHitbox.size.x * 0.25f, 0f);
            }
            else if (IsIceKingBossBlueprint(monsterBlueprint))
            {
                monsterHitbox.size *= 0.5f;
                monsterHitbox.offset = Vector2.zero;
            }
            else if (IsFantasySpritesInjectedBlueprint(monsterBlueprint))
            {
                monsterHitbox.size *= (2f / 7f);
                if (IsMushroomOrGoblinBlueprint(monsterBlueprint))
                    monsterHitbox.size = new Vector2(monsterHitbox.size.x * 0.5f, monsterHitbox.size.y);
                monsterHitbox.offset = Vector2.zero;
            }

            centerTransform = (new GameObject("Center Transform")).transform;
            centerTransform.SetParent(transform);
            centerTransform.position = transform.position;
            // Set the drag based on acceleration and movespeed
            float spd = Random.Range(monsterBlueprint.movespeed-0.1f, monsterBlueprint.movespeed+0.1f);
            rb.drag = monsterBlueprint.acceleration / (spd * spd);
            // Reset the velocity
            rb.velocity = Vector2.zero;
            StopAllCoroutines();
        }

        private static bool IsFireKingBossBlueprint(MonsterBlueprint blueprint)
        {
            if (blueprint == null)
                return false;
            if (!(blueprint is BossMonsterBlueprint))
                return false;
            return blueprint.homeWorld == MonsterWorldKind.Fire;
        }

        private static bool IsIceKingBossBlueprint(MonsterBlueprint blueprint)
        {
            if (blueprint == null)
                return false;
            if (!(blueprint is BossMonsterBlueprint))
                return false;
            return blueprint.homeWorld == MonsterWorldKind.Ice;
        }

        private static bool IsGrassKingBossBlueprint(MonsterBlueprint blueprint)
        {
            if (blueprint == null)
                return false;
            if (!(blueprint is BossMonsterBlueprint))
                return false;
            return blueprint.homeWorld == MonsterWorldKind.Grass;
        }

        private static bool IsFantasySpritesInjectedBlueprint(MonsterBlueprint blueprint)
        {
            if (blueprint == null || string.IsNullOrEmpty(blueprint.name))
                return false;
            if (!blueprint.name.EndsWith("_Injected"))
                return false;
            if (blueprint.name == "Bat_Injected" || blueprint.name == "Wizard_Injected")
                return false;
            return true;
        }

        private static bool IsMushroomOrGoblinBlueprint(MonsterBlueprint blueprint)
        {
            if (blueprint == null || string.IsNullOrEmpty(blueprint.name))
                return false;

            string n = blueprint.name.ToLowerInvariant();
            return n.Contains("mushroom") || n.Contains("goblin");
        }

        private void IgnoreCollisionWithPlayer()
        {
            if (playerCharacter == null)
                return;

            Collider2D[] playerColliders = playerCharacter.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCol = playerColliders[i];
                if (playerCol == null)
                    continue;
                if (monsterLegsCollider != null)
                    Physics2D.IgnoreCollision(monsterLegsCollider, playerCol, true);
                if (monsterHitbox != null)
                    Physics2D.IgnoreCollision(monsterHitbox, playerCol, true);
            }
        }

        public virtual void FallIntoTrap(Vector2 trapCenter, float scatter = 0.65f)
        {
            if (!alive || trapConsumed)
                return;

            trapConsumed = true;
            alive = false;

            StopAllCoroutines();
            if (monsterHitbox != null)
                monsterHitbox.enabled = false;
            if (monsterLegsCollider != null)
                monsterLegsCollider.enabled = false;

            if (entityManager != null)
                entityManager.LivingMonsters.Remove(this);

            DropLootNear(trapCenter, scatter);

            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();

            if (entityManager != null)
                entityManager.DespawnMonster(monsterIndex, this, true);
            else
                Destroy(gameObject);
        }

        private void DropLootNear(Vector2 center, float scatter)
        {
            if (entityManager == null || monsterBlueprint == null)
                return;

            if (monsterBlueprint.gemLootTable.TryDropLoot(out GemType gemType))
                entityManager.SpawnExpGem(center + Random.insideUnitCircle * scatter, gemType);
            if (monsterBlueprint.coinLootTable.TryDropLoot(out CoinType coinType))
                entityManager.SpawnCoin(center + Random.insideUnitCircle * scatter, coinType);
        }

        protected virtual void Update()
        {
            // Direction
            monsterSpriteRenderer.flipX = ((playerCharacter.transform.position.x - rb.position.x) < 0);
        }

        private void OnDrawGizmosSelected()
        {
            DrawMonsterGizmos(new Color(0.15f, 0.9f, 1f, 1f), new Color(1f, 0.45f, 0.15f, 1f));
        }

        protected virtual void OnDrawGizmos()
        {
            DrawMonsterGizmos(new Color(0.15f, 0.9f, 1f, 0.75f), new Color(1f, 0.45f, 0.15f, 0.75f));
        }

        private void DrawMonsterGizmos(Color hitboxColor, Color bodyColor)
        {
            BoxCollider2D box = monsterHitbox != null ? monsterHitbox : GetComponentInChildren<BoxCollider2D>(true);
            CircleCollider2D circle = monsterLegsCollider != null ? monsterLegsCollider : GetComponent<CircleCollider2D>();

            if (box != null)
            {
                Gizmos.color = hitboxColor;
                Vector3 boxCenter = box.transform.TransformPoint(box.offset);
                Vector3 boxSize = Vector3.Scale(box.size, box.transform.lossyScale);
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawWireCube(boxCenter, boxSize);
            }

            if (circle != null)
            {
                Gizmos.color = bodyColor;
                Vector3 circleCenter = circle.transform.TransformPoint(circle.offset);
                float scale = Mathf.Max(circle.transform.lossyScale.x, circle.transform.lossyScale.y);
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawWireSphere(circleCenter, circle.radius * scale);
            }

            if (monsterBlueprint != null && monsterBlueprint.homeWorld == MonsterWorldKind.Fire)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.35f);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (!alive || rb == null || entityManager == null)
                return;

            if (this is BossMonster)
                return;
        }

        public virtual void EnterKingCityCalmState()
        {
            if (this is BossMonster)
                return;

            kingCityCalmActive = true;
            kingCityCalmWander = Random.value < 0.5f;
            kingCityWanderDirection = Random.insideUnitCircle.normalized;
            if (kingCityWanderDirection == Vector2.zero)
                kingCityWanderDirection = Vector2.right;
            nextKingCityWanderChangeTime = Time.time + Random.Range(0.7f, 1.6f);
            if (rb != null)
                rb.velocity = Vector2.zero;
        }

        public virtual void ExitKingCityCalmState()
        {
            kingCityCalmActive = false;
        }

        protected bool HandleKingCityCalmBehavior()
        {
            if (!kingCityCalmActive || rb == null || monsterBlueprint == null)
                return false;

            if (!kingCityCalmWander)
            {
                rb.velocity = Vector2.zero;
                return true;
            }

            if (Time.time >= nextKingCityWanderChangeTime)
            {
                kingCityWanderDirection = Random.insideUnitCircle.normalized;
                if (kingCityWanderDirection == Vector2.zero)
                    kingCityWanderDirection = Vector2.up;
                nextKingCityWanderChangeTime = Time.time + Random.Range(0.7f, 1.6f);
            }

            float wanderSpeed = Mathf.Max(0.3f, monsterBlueprint.movespeed * 0.4f);
            rb.velocity = kingCityWanderDirection * wanderSpeed;
            return true;
        }

        public override void Knockback(Vector2 knockback)
        {
            rb.velocity += knockback * Mathf.Sqrt(rb.drag);
        }

        public override void TakeDamage(float damage, Vector2 knockback = default(Vector2))
        {
            if (alive)
            {
                currentHealth -= damage;
                if (hitAnimationCoroutine != null) StopCoroutine(hitAnimationCoroutine);
                if (knockback != default(Vector2))
                {
                    rb.velocity += knockback * Mathf.Sqrt(rb.drag);
                    knockedBack = true;
                }
                if (currentHealth > 0)
                    hitAnimationCoroutine = StartCoroutine(HitAnimation());
                else
                    StartCoroutine(Killed());
            }
        }

        protected IEnumerator HitAnimation()
        {
            monsterSpriteRenderer.sharedMaterial = whiteMaterial;
            yield return new WaitForSeconds(0.15f);
            monsterSpriteRenderer.sharedMaterial = defaultMaterial;
            knockedBack = false;
        }

        public virtual IEnumerator Killed(bool killedByPlayer = true)
        {
            // Toggle alive flag off and disable hitbox
            alive = false;
            monsterHitbox.enabled = false;
            // Remove from list of living monsters
            entityManager.LivingMonsters.Remove(this);
            // Drop loot
            if (killedByPlayer)
                DropLoot();

            if (deathParticles != null)
            {       
                deathParticles.Play();
            }

            yield return HitAnimation();

            if (deathParticles != null)
            {
                monsterSpriteRenderer.enabled = false;
                shadow.SetActive(false);
                yield return new WaitForSeconds(deathParticles.main.duration - 0.15f);
                monsterSpriteRenderer.enabled = true;
                shadow.SetActive(true);
            }
            // monsterSpriteRenderer.material = dissolveMaterial;
            // float t = 0;
            // while (t < 1)
            // {
            //     monsterSpriteRenderer.material.SetFloat("_Dissolve", t);
            //     t += Time.deltaTime*2;
            //     yield return null;
            // }
            // monsterSpriteRenderer.sharedMaterial = defaultMaterial;
            //yield return new WaitForSeconds(0.2f);

            // Invoke monster killed callback and remove all listeners
            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();
            entityManager.DespawnMonster(monsterIndex, this, true);
        }

        protected virtual void DropLoot()
        {
            if (monsterBlueprint.gemLootTable.TryDropLoot(out GemType gemType))
                entityManager.SpawnExpGem((Vector2)transform.position, gemType);
            if (monsterBlueprint.coinLootTable.TryDropLoot(out CoinType coinType))
                entityManager.SpawnCoin((Vector2)transform.position, coinType);
        }
    }
}







