using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class RangedMonster : Monster
    {
        public enum State
        {
            Walking,
            Shooting
        }

        [SerializeField] protected Transform projectileSpawnPosition;
        protected new RangedMonsterBlueprint monsterBlueprint;
        protected float timeSinceLastAttack;
        protected State state;
        protected float outOfRangeTime;
        protected int projectileIndex;
        protected LayerMask effectiveTargetLayer;
        protected float safeAttackInterval = 0.8f;
        protected float safeRange = 3.5f;

        public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);
            this.monsterBlueprint = (RangedMonsterBlueprint) monsterBlueprint;
            projectileIndex = entityManager.AddPoolForProjectile(this.monsterBlueprint.projectilePrefab);
            effectiveTargetLayer = this.monsterBlueprint.targetLayer;
            if (playerCharacter != null)
                effectiveTargetLayer |= 1 << playerCharacter.gameObject.layer;
            outOfRangeTime = 0;
            projectileSpawnPosition = projectileSpawnPosition != null ? projectileSpawnPosition : transform;
            safeAttackInterval = 1f / Mathf.Max(0.15f, this.monsterBlueprint.atkspeed);
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

            if (alive)
            {
                Vector2 toPlayer = (playerCharacter.transform.position - transform.position);
                float distance = toPlayer.magnitude;
                Vector2 dirToPlayer = distance > 0.0001f ? toPlayer / distance : Vector2.right;
                switch (state)
                {
                    case State.Walking:
                        rb.velocity += dirToPlayer * monsterBlueprint.acceleration * Time.fixedDeltaTime;
                        entityManager.Grid.UpdateClient(this);
                        if (distance <= safeRange)
                        {
                            state = State.Shooting;
                            monsterSpriteAnimator.StopAnimating();
                        }
                        break;

                    case State.Shooting:
                        timeSinceLastAttack += Time.fixedDeltaTime;
                        // rb.velocity *= 0.95f;
                        if (timeSinceLastAttack >= safeAttackInterval)
                        {
                            LaunchProjectile(dirToPlayer);
                            timeSinceLastAttack = 0;//Mathf.Repeat(timeSinceLastAttack, 1.0f/monsterBlueprint.atkspeed);
                        }
                        if (distance <= safeRange)
                            outOfRangeTime = 0;
                        else
                            outOfRangeTime += Time.deltaTime;
                        if (outOfRangeTime > monsterBlueprint.timeAllowedOutsideRange)
                        {
                            state = State.Walking;
                            monsterSpriteAnimator.StartAnimating();
                            //rb.bodyType = RigidbodyType2D.Dynamic;
                            //rb.mass = 1;
                        }
                        break;
                }
                // if (!knockedBack && rb.velocity.magnitude > monsterBlueprint.movespeed)
                //      rb.velocity = rb.velocity.normalized * monsterBlueprint.movespeed;
            }
        }

        protected void LaunchProjectile(Vector2 direction)
        {
            if (projectileSpawnPosition == null)
                return;
            Projectile projectile = entityManager.SpawnProjectile(projectileIndex, projectileSpawnPosition.position, monsterBlueprint.atk, 0, monsterBlueprint.projectileSpeed, effectiveTargetLayer);
            projectile.Launch(direction);
        }
    }
}
