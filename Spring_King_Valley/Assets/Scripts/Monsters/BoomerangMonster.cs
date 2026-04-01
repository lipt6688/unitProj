using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BoomerangMonster : Monster
    {
        [SerializeField] protected Transform boomerangSpawnPosition;
        protected new BoomerangMonsterBlueprint monsterBlueprint;
        protected float timeSinceLastBoomerangAttack;
        protected float timeSinceLastMeleeAttack;
        protected float outOfRangeTime;
        protected int boomerangIndex;
        protected LayerMask effectiveTargetLayer;
        protected float safeBoomerangInterval = 1f;
        protected float safeRange = 3.4f;

        public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);
            this.monsterBlueprint = (BoomerangMonsterBlueprint) monsterBlueprint;
            boomerangIndex = entityManager.AddPoolForBoomerang(this.monsterBlueprint.boomerangPrefab);
            effectiveTargetLayer = this.monsterBlueprint.targetLayer;
            if (playerCharacter != null)
                effectiveTargetLayer |= 1 << playerCharacter.gameObject.layer;
            outOfRangeTime = 0;
            boomerangSpawnPosition = boomerangSpawnPosition != null ? boomerangSpawnPosition : transform;
            safeBoomerangInterval = 1f / Mathf.Max(0.15f, this.monsterBlueprint.boomerangAttackSpeed);
            safeRange = Mathf.Max(0.8f, this.monsterBlueprint.range);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!alive || playerCharacter == null || monsterBlueprint == null || rb == null || entityManager == null || entityManager.Grid == null)
                return;

            if (HandleKingCityCalmBehavior())
            {
                entityManager.Grid.UpdateClient(this);
                return;
            }

            Vector2 toPlayer = (playerCharacter.transform.position - transform.position);
            float distance = toPlayer.magnitude;
            Vector2 dirToPlayer = distance > 0.0001f ? toPlayer / distance : Vector2.right;
            entityManager.Grid.UpdateClient(this);
            timeSinceLastBoomerangAttack += Time.fixedDeltaTime;
            if (distance <= safeRange)
            {
                rb.velocity += dirToPlayer * monsterBlueprint.acceleration * Time.fixedDeltaTime / 2;
                if (timeSinceLastBoomerangAttack >= safeBoomerangInterval)
                {
                    ThrowBoomerang(playerCharacter.transform.position);
                    timeSinceLastBoomerangAttack = 0;
                }
            }
            else
            {
                rb.velocity += dirToPlayer * monsterBlueprint.acceleration * Time.fixedDeltaTime;
            }
        }

        protected void ThrowBoomerang(Vector2 targetPosition)
        {
            if (boomerangSpawnPosition == null)
                return;
            Boomerang boomerang = entityManager.SpawnBoomerang(boomerangIndex, boomerangSpawnPosition.position, monsterBlueprint.boomerangDamage, 0, monsterBlueprint.throwRange, monsterBlueprint.throwTime, effectiveTargetLayer);
            boomerang.Throw(boomerangSpawnPosition, targetPosition);
        }

        void OnCollisionStay2D(Collision2D col)
        {
            if (kingCityCalmActive)
                return;

            if (((effectiveTargetLayer & (1 << col.collider.gameObject.layer)) != 0) && timeSinceLastMeleeAttack >= 1.0f/monsterBlueprint.atkspeed)
            {
                playerCharacter.TakeDamage(monsterBlueprint.atk);
                timeSinceLastMeleeAttack = 0;
            }
        }
    }
}
