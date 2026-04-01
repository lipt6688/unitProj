using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class ThrowingMonster : Monster
    {
        public enum State
        {
            Walking,
            Shooting
        }

        [SerializeField] protected Transform throwableSpawnPosition;
        protected new ThrowingMonsterBlueprint monsterBlueprint;
        protected float timeSinceLastAttack;
        protected State state;
        protected float outOfRangeTime;
        protected int throwableIndex;
        protected LayerMask effectiveTargetLayer;
        protected float safeAttackInterval = 0.95f;
        protected float safeRange = 3.2f;

        public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);
            this.monsterBlueprint = (ThrowingMonsterBlueprint) monsterBlueprint;
            throwableIndex = entityManager.AddPoolForThrowable(this.monsterBlueprint.throwablePrefab);
            effectiveTargetLayer = this.monsterBlueprint.targetLayer;
            if (playerCharacter != null)
                effectiveTargetLayer |= 1 << playerCharacter.gameObject.layer;
            outOfRangeTime = 0;
            throwableSpawnPosition = throwableSpawnPosition != null ? throwableSpawnPosition : transform;
            safeAttackInterval = 1f / Mathf.Max(0.15f, this.monsterBlueprint.atkspeed);
            safeRange = Mathf.Max(0.8f, this.monsterBlueprint.range);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!alive || playerCharacter == null || monsterBlueprint == null || rb == null)
                return;

            if (HandleKingCityCalmBehavior())
                return;

            if (alive)
            {
                Vector2 toPlayer = (playerCharacter.transform.position - transform.position);
                float distance = toPlayer.magnitude;
                Vector2 dirToPlayer = distance > 0.0001f ? toPlayer / distance : Vector2.right;
                switch (state)
                {
                    case State.Walking:
                        rb.velocity += dirToPlayer * monsterBlueprint.acceleration * Time.fixedDeltaTime;
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
                            LaunchThrowable(playerCharacter.transform.position);
                            timeSinceLastAttack = 0;
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

        protected void LaunchThrowable(Vector2 targetPosition)
        {
            if (throwableSpawnPosition == null)
                return;
            Throwable throwable = entityManager.SpawnThrowable(throwableIndex, throwableSpawnPosition.position, monsterBlueprint.atk, 0, -909, effectiveTargetLayer);
            targetPosition += playerCharacter.Velocity * throwable.ThrowTime;
            throwable.Throw(targetPosition);
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            LaunchThrowable(transform.position);
            yield return base.Killed(killedByPlayer);
        }
    }
}
