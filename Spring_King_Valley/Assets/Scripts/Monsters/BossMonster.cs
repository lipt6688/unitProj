using System.Collections;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    public class BossMonster : Monster
    {
        protected new BossMonsterBlueprint monsterBlueprint;
        protected BossAbility[] abilities;
        protected Coroutine act = null;
        public Rigidbody2D Rigidbody { get => rb; }
        public SpriteAnimator Animator { get => monsterSpriteAnimator; }
        protected float timeSinceLastMeleeAttack;
        private bool leashEnabled;
        private Rect leashRect;
        private bool chaseActivationEnabled;
        private Rect chaseActivationRect;

        public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);
            this.monsterBlueprint = (BossMonsterBlueprint) monsterBlueprint;
            abilities = new BossAbility[this.monsterBlueprint.abilityPrefabs.Length];
            for (int i = 0; i < abilities.Length; i++)
            {
                abilities[i] = Instantiate(this.monsterBlueprint.abilityPrefabs[i], transform).GetComponent<BossAbility>();
                abilities[i].Init(this, entityManager, playerCharacter);
            }
            act = StartCoroutine(Act());
        }

        protected override void Update()
        {
            base.Update();
            timeSinceLastMeleeAttack += Time.deltaTime;
        }
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            ApplyLeash();
        }

        public void SetLeashRect(Rect rect)
        {
            leashRect = rect;
            leashEnabled = true;
            ApplyLeash();
        }

        public void SetChaseActivationRect(Rect rect)
        {
            chaseActivationRect = rect;
            chaseActivationEnabled = true;
        }

        public void Move(Vector2 direction, float deltaTime)
        {
            if (!IsPlayerInChaseZone())
            {
                Freeze();
                return;
            }
            rb.velocity += direction * monsterBlueprint.acceleration * deltaTime;
        }

        public void Freeze()
        {
            rb.velocity = Vector2.zero;
        }

        public override void FallIntoTrap(Vector2 trapCenter, float scatter = 0.65f)
        {
            // Boss is immune to trap instant-kill.
        }

        private bool IsPlayerInChaseZone()
        {
            if (!chaseActivationEnabled || playerCharacter == null)
                return true;

            Vector2 p = playerCharacter.transform.position;
            const float margin = 1.25f;
            return p.x >= chaseActivationRect.xMin - margin &&
                   p.x <= chaseActivationRect.xMax + margin &&
                   p.y >= chaseActivationRect.yMin - margin &&
                   p.y <= chaseActivationRect.yMax + margin;
        }

        private void ApplyLeash()
        {
            if (!leashEnabled || rb == null)
                return;

            Vector2 pos = rb.position;
            float clampedX = Mathf.Clamp(pos.x, leashRect.xMin, leashRect.xMax);
            float clampedY = Mathf.Clamp(pos.y, leashRect.yMin, leashRect.yMax);

            if (!Mathf.Approximately(clampedX, pos.x) || !Mathf.Approximately(clampedY, pos.y))
            {
                rb.position = new Vector2(clampedX, clampedY);
            }

            Vector2 vel = rb.velocity;
            const float eps = 0.001f;
            if (rb.position.x <= leashRect.xMin + eps && vel.x < 0f) vel.x = 0f;
            if (rb.position.x >= leashRect.xMax - eps && vel.x > 0f) vel.x = 0f;
            if (rb.position.y <= leashRect.yMin + eps && vel.y < 0f) vel.y = 0f;
            if (rb.position.y >= leashRect.yMax - eps && vel.y > 0f) vel.y = 0f;
            rb.velocity = vel;
        }

        private IEnumerator Act()
        {
            while (true)
            {
                if (!IsPlayerInChaseZone())
                {
                    Freeze();
                    foreach (BossAbility ability in abilities)
                        ability.Deactivate();
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                float[] abilityScores = abilities.Select(a => a.Score()).ToArray();
                float totalScore = abilityScores.Sum();
                float rand = Random.Range(0f, totalScore);
                float cumulative = 0;
                int abilityIndex = -1;
                for (int i = 0; i < abilities.Length; i++)
                {
                    abilities[i].Deactivate();
                    cumulative += abilityScores[i];
                    if (abilityIndex == -1 && rand < cumulative)
                        abilityIndex = i;
                }
                if (abilityIndex == -1)
                {
                    Debug.Log(totalScore);
                    yield return new WaitForSeconds(1);
                }
                else
                    yield return abilities[abilityIndex].Activate();
            }
        }

        protected override void DropLoot()
        {
            base.DropLoot();
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            foreach (BossAbility ability in abilities)
                Destroy(ability.gameObject);
            StopCoroutine(act);
            yield return base.Killed(killedByPlayer);
        }

        void OnCollisionEnter2D(Collision2D col)
        {
            if (((monsterBlueprint.meleeLayer & (1 << col.collider.gameObject.layer)) != 0))
            {
                IDamageable damageable = col.collider.GetComponentInParent<IDamageable>();
                Vector2 knockbackDirection = (damageable.transform.position - transform.position).normalized;
                if (timeSinceLastMeleeAttack > monsterBlueprint.meleeAttackDelay)
                {
                    damageable.TakeDamage(monsterBlueprint.meleeDamage, monsterBlueprint.meleeKnockback * knockbackDirection);
                    timeSinceLastMeleeAttack = 0;
                }
                else
                {
                    damageable.TakeDamage(0, monsterBlueprint.meleeKnockback * knockbackDirection);
                }
            }

            if (col.gameObject.TryGetComponent<Chest>(out Chest chest))
            {
                chest.OpenChest(false);
            }
        }
    }
}
