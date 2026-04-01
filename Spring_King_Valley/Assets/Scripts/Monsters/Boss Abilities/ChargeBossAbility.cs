using System.Reflection;
using System.Linq;
using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class ChargeBossAbility : BossAbility
    {
        [Header("Charge Details")]
        [SerializeField] protected SpriteRenderer chargeIndicator;
        [SerializeField] protected Color defaultColor, warningColor;
        [SerializeField] protected float warningTime = 1f;
        [SerializeField] protected float chargeDelay;
        [SerializeField] protected float chargeCooldown;
        [SerializeField] protected float chargeDistance;
        [SerializeField] protected float chargeSpeed;
        [SerializeField] protected float chargeWidth = 1.2f;
        [SerializeField] protected float chargeDamage = 30f;
        [Header("Dash Flame Trail")]
        [SerializeField] protected float flameLifetime = 8f;
        [SerializeField] protected float flameSpawnStep = 0.65f;
        [SerializeField] protected float flameDamagePerSecond = 10f;
        [SerializeField] protected float flameTickInterval = 0.2f;
        [SerializeField] protected Color flameColorA = new Color(1f, 0.28f, 0.05f, 0.85f);
        [SerializeField] protected Color flameColorB = new Color(1f, 0.92f, 0.15f, 0.98f);
        protected Vector2 chargeStartPosition;
        protected bool charging = false;

        public override void Init(BossMonster monster, EntityManager entityManager, Character playerCharacter)
        {
            base.Init(monster, entityManager, playerCharacter);
        }

        void FixedUpdate()
        {
            if (active && !charging)
            {
                Vector2 moveDirection = (playerCharacter.transform.position - monster.transform.position).normalized;
                monster.Move(moveDirection, Time.fixedDeltaTime);
                entityManager.Grid.UpdateClient(monster);
            }
        }

        protected IEnumerator ChargeAttack()
        {
            charging = true;
            monster.Freeze();

            chargeStartPosition = monster.Rigidbody.position;
            Vector2 direction = ((Vector2)playerCharacter.transform.position - chargeStartPosition).normalized;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;

            float dashLength = Mathf.Max(0.2f, chargeDistance);
            float dashWidth = Mathf.Max(0.2f, chargeWidth);
            float totalWarningTime = Mathf.Max(0.2f, warningTime);

            if (chargeIndicator != null)
            {
                chargeIndicator.color = defaultColor;
                chargeIndicator.enabled = true;
            }

            float t = 0f;
            while (t < totalWarningTime)
            {
                if (chargeIndicator != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 12f);
                    chargeIndicator.color = Color.Lerp(defaultColor, warningColor, pulse);
                    chargeIndicator.transform.localScale = new Vector3(dashLength, dashWidth, 1f);
                    chargeIndicator.transform.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.right, direction));
                    chargeIndicator.transform.position = chargeStartPosition + direction * (dashLength * 0.5f);
                }

                t += Time.deltaTime;
                yield return null;
            }

            if (chargeDelay > 0f)
                yield return new WaitForSeconds(chargeDelay);

            if (chargeIndicator != null)
                chargeIndicator.enabled = false;

            // Guarantee dash starts once warning phase has completed.
            float firstStep = Mathf.Max(0.2f, chargeSpeed * Time.fixedDeltaTime);
            monster.Rigidbody.position = monster.Rigidbody.position + direction * firstStep;

            float targetDistance = dashLength;
            float travelled = 0f;
            float nextFlameDistance = 0f;
            SpawnFlameAt(chargeStartPosition, dashWidth);
            nextFlameDistance += Mathf.Max(0.1f, flameSpawnStep);

            while (travelled < targetDistance)
            {
                float step = Mathf.Min(chargeSpeed * Time.fixedDeltaTime, targetDistance - travelled);
                Vector2 startPosition = monster.Rigidbody.position;
                Vector2 intendedPosition = startPosition + direction * step;
                monster.Rigidbody.MovePosition(intendedPosition);

                yield return new WaitForFixedUpdate();

                Vector2 currentPosition = monster.Rigidbody.position;
                float movedDistance = Vector2.Distance(startPosition, currentPosition);
                if (movedDistance <= 0.0005f)
                {
                    monster.Rigidbody.position = intendedPosition;
                    currentPosition = intendedPosition;
                    movedDistance = step;
                }

                if (IsPlayerInsideDashStrip(direction, dashLength, dashWidth, currentPosition))
                    playerCharacter.TakeDamage(chargeDamage, direction * Mathf.Max(0.1f, chargeSpeed * 0.15f));

                travelled += movedDistance;

                while (nextFlameDistance <= travelled)
                {
                    Vector2 flamePos = chargeStartPosition + direction * nextFlameDistance;
                    SpawnFlameAt(flamePos, dashWidth);
                    nextFlameDistance += Mathf.Max(0.1f, flameSpawnStep);
                }
            }

            monster.Rigidbody.velocity = Vector2.zero;
            yield return new WaitForSeconds(chargeCooldown);

            charging = false;
        }

        public override IEnumerator Activate()
        {
            active = true;
            yield return StartCoroutine(ChargeAttack());
        }

        public override float Score()
        {
            float distance = Vector2.Distance(monster.transform.position, playerCharacter.transform.position);
            return distance / (distance + 1f);
        }

        private void SpawnFlameAt(Vector2 position, float width)
        {
            if (monster == null)
                return;

            GameObject flame = new GameObject("ChargeFlame");
            flame.transform.position = position;
            flame.transform.SetParent(monster.transform.parent, true);

            SpriteRenderer sr = flame.AddComponent<SpriteRenderer>();
            BoxCollider2D trigger = flame.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(Mathf.Max(0.2f, width), Mathf.Max(0.2f, width * 0.65f));

            ChargeFlameHazard hazard = flame.AddComponent<ChargeFlameHazard>();
            hazard.Setup(sr, flameLifetime, flameDamagePerSecond, flameTickInterval, flameColorA, flameColorB);
        }

        private bool IsPlayerInsideDashStrip(Vector2 direction, float dashLength, float dashWidth, Vector2 currentPosition)
        {
            if (playerCharacter == null)
                return false;

            Vector2 playerPos = playerCharacter.transform.position;
            Vector2 start = chargeStartPosition;
            Vector2 end = currentPosition;
            Vector2 segment = end - start;
            float segmentLength = segment.magnitude;
            if (segmentLength < 0.0001f)
                return false;

            Vector2 segmentDir = segment / segmentLength;
            Vector2 toPlayer = playerPos - start;
            float forward = Vector2.Dot(toPlayer, segmentDir);
            if (forward < 0f || forward > dashLength)
                return false;

            float sideways = Mathf.Abs(Vector2.Dot(toPlayer, new Vector2(-segmentDir.y, segmentDir.x)));
            return sideways <= dashWidth * 0.5f;
        }

        public override void Deactivate()
        {
            base.Deactivate();
            charging = false;
            if (chargeIndicator != null)
                chargeIndicator.enabled = false;
            if (monster != null && monster.Rigidbody != null)
                monster.Rigidbody.velocity = Vector2.zero;
        }
    }
}
