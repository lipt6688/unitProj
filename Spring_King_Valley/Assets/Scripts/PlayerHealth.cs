using UnityEngine;
using System.Collections;
public class PlayerHealth : MonoBehaviour, ITakenDamage
{
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int hp;
    [SerializeField] private float invincibleDuration = 0.8f;
    [SerializeField] private Color hurtFlashColor = new Color(1f, 0.45f, 0.45f, 1f);
    [SerializeField] private float hurtFlashDuration = 0.12f;
    [SerializeField] private float fireKingOverlapCheckInterval = 0.05f;
    [SerializeField] private float fireKingOverlapSearchRadius = 3.5f;

    public bool isAttacked;
    public bool isAttack { get { return isAttacked; } set { isAttacked = value; } }

    private Animator anim;
    private bool hpUiSynced;
    private SpriteRenderer[] playerRenderers;
    private Coroutine hurtFlashCoroutine;
    private Collider2D[] playerColliders;
    private float nextFireKingOverlapCheckTime;

    private void Start()
    {
        maxHp = Mathf.Max(1, maxHp);
        maxHp = 200;
        hp = maxHp;
        anim = GetComponent<Animator>();
        playerRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        playerColliders = GetComponentsInChildren<Collider2D>(true);
        if (UIManager.instance != null)
        {
            UIManager.instance.UpdateHp(hp, maxHp);
            hpUiSynced = true;
        }
    }

    private void Update()
    {
        if (!hpUiSynced && UIManager.instance != null)
        {
            UIManager.instance.UpdateHp(hp, maxHp);
            hpUiSynced = true;
        }

        CheckFireKingOverlapDamage();
    }

    /// <summary>增益：提高生命上限并可选回复。</summary>
    public void AddMaxHp(int maxDelta, int healAmount)
    {
        maxHp += maxDelta;
        hp = Mathf.Min(maxHp, hp + healAmount);
        if (UIManager.instance != null)
            UIManager.instance.UpdateHp(hp, maxHp);
    }

    public void TakenDamage(int _amount)
    {
        if (!isAttack)
        {
            anim.SetTrigger("isHurt");
            int damage = Mathf.Max(1, _amount);
            hp = Mathf.Max(0, hp - damage);
            StartCoroutine(InvincibleCo());
            if (hurtFlashCoroutine != null)
                StopCoroutine(hurtFlashCoroutine);
            hurtFlashCoroutine = StartCoroutine(HurtFlashCo());
            if (UIManager.instance != null)
            {
                UIManager.instance.UpdateHp(hp, maxHp);
                UIManager.instance.TriggerPlayerHitFlash();
            }
            Debug.Log("Player Hurt");

            if (hp <= 0)
            {
                UIManager.instance.GameOverAnimation();
                FindObjectOfType<CameraController>().CameraShake(0.05f);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
            return;

        TryTakeContactDamage(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null)
            return;

        TryTakeContactDamage(collision.collider);
    }

    private void TryTakeContactDamage(Collider2D other)
    {
        if (other == null || other.isTrigger)
            return;

        Vampire.Monster monster = other.GetComponentInParent<Vampire.Monster>();
        if (monster == null)
            return;

        TakenDamage(GetMonsterContactDamage(other));
    }

    private void CheckFireKingOverlapDamage()
    {
        if (Time.time < nextFireKingOverlapCheckTime)
            return;

        nextFireKingOverlapCheckTime = Time.time + Mathf.Max(0.01f, fireKingOverlapCheckInterval);

        if (playerColliders == null || playerColliders.Length == 0)
            playerColliders = GetComponentsInChildren<Collider2D>(true);

        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, fireKingOverlapSearchRadius);
        for (int i = 0; i < nearby.Length; i++)
        {
            Collider2D other = nearby[i];
            if (other == null)
                continue;

            Vampire.Monster monster = other.GetComponentInParent<Vampire.Monster>();
            if (monster == null || !IsFireKingBoss(monster))
                continue;

            for (int c = 0; c < playerColliders.Length; c++)
            {
                Collider2D playerCol = playerColliders[c];
                if (playerCol == null)
                    continue;

                ColliderDistance2D distance = playerCol.Distance(other);
                if (distance.isOverlapped)
                {
                    TakenDamage(GetMonsterContactDamage(other));
                    return;
                }
            }
        }
    }

    private static bool IsFireKingBoss(Vampire.Monster monster)
    {
        if (!(monster is Vampire.BossMonster))
            return false;

        System.Type type = monster.GetType();
        while (type != null)
        {
            System.Reflection.FieldInfo field = type.GetField("monsterBlueprint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                object blueprint = field.GetValue(monster);
                if (blueprint is Vampire.MonsterBlueprint typedBlueprint)
                    return typedBlueprint.homeWorld == Vampire.MonsterWorldKind.Fire;
            }
            type = type.BaseType;
        }

        return false;
    }

    private IEnumerator HurtFlashCo()
    {
        if (playerRenderers == null || playerRenderers.Length == 0)
            yield break;

        Color[] originalColors = new Color[playerRenderers.Length];
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] == null)
                continue;
            originalColors[i] = playerRenderers[i].color;
            playerRenderers[i].color = hurtFlashColor;
        }

        yield return new WaitForSeconds(hurtFlashDuration);

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] == null)
                continue;
            playerRenderers[i].color = originalColors[i];
        }
    }

    private static int GetMonsterContactDamage(Collider2D other)
    {
        Vampire.Monster monster = other != null ? other.GetComponentInParent<Vampire.Monster>() : null;
        if (monster == null)
            return 1;

        System.Type type = monster.GetType();
        while (type != null)
        {
            System.Reflection.FieldInfo field = type.GetField("monsterBlueprint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                object blueprint = field.GetValue(monster);
                if (blueprint != null)
                {
                    System.Reflection.FieldInfo atkField = blueprint.GetType().GetField("atk", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (atkField != null)
                    {
                        object atkValue = atkField.GetValue(blueprint);
                        if (atkValue is int)
                            return Mathf.Max(1, (int)atkValue);
                        if (atkValue is float)
                            return Mathf.Max(1, Mathf.RoundToInt((float)atkValue));
                    }
                }
            }
            type = type.BaseType;
        }

        return 1;
    }

    IEnumerator InvincibleCo()
    {
        isAttack = true;
        yield return new WaitForSeconds(invincibleDuration);
        isAttack = false;
    }


}
