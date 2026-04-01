using System.Collections;
using UnityEngine;
using Vampire;

public class Enemy : MonoBehaviour, ITakenDamage
{
    [SerializeField] private float moveSpeed;
    private Transform target;
    [SerializeField] private int maxHp;
    public int hp;

    [Header("Hurt")]
    private SpriteRenderer sp;
    public float hurtLength;//MARKER 效果持续多久
    private float timeBtwHurt;//MARKER 相当于计数器

    [HideInInspector] public bool isAttacked;
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private int coinReward = 5;
    private bool trapConsumed;

    public bool isAttack { get { return isAttacked; }  set { isAttacked = value; } }

    public GameObject bulletEffect;

    private void Start() 
    {
        hp = maxHp;
        target = GameObject.FindGameObjectWithTag("Player").GetComponent<Transform>();
        sp = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        FollowPlayer();

        timeBtwHurt -= Time.deltaTime;
        if (timeBtwHurt <= 0)
            sp.material.SetFloat("_FlashAmount", 0);
    }

    private void FollowPlayer()
    {
        transform.position = Vector2.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime);
    }

    public void TakenDamage(int _amount)
    {
        isAttack = true;
        StartCoroutine(isAttackCo());
        hp -= _amount;
        HurtEffect();

        if (hp <= 0)
        {
            DropLoot(transform.position, 0f);
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }

    public void FallIntoTrap(Vector2 trapCenter, float scatter = 0.65f)
    {
        if (trapConsumed)
            return;

        trapConsumed = true;
        DropLoot(trapCenter, scatter);
        Destroy(gameObject);
    }

    private void DropLoot(Vector2 center, float scatter)
    {
        EntityManager em = FindObjectOfType<EntityManager>();
        if (em != null)
        {
            Vector2 gemPos = center + Random.insideUnitCircle * scatter;
            Vector2 coinPos = center + Random.insideUnitCircle * scatter;
            em.SpawnExpGem(gemPos, GemType.White1, false);
            em.SpawnCoin(coinPos, ToCoinType(coinReward), false);
            return;
        }

        if (CoinWallet.Instance != null && coinReward > 0)
            CoinWallet.Instance.AddCoins(coinReward);
    }

    private static CoinType ToCoinType(int amount)
    {
        if (amount >= 50) return CoinType.Bag50;
        if (amount >= 30) return CoinType.Pouch30;
        if (amount >= 5) return CoinType.Gold5;
        if (amount >= 2) return CoinType.Silver2;
        return CoinType.Bronze1;
    }

    private void HurtEffect() 
    {
        sp.material.SetFloat("_FlashAmount", 1);
        timeBtwHurt = hurtLength;
    }

    IEnumerator isAttackCo()
    {
        yield return new WaitForSeconds(0.2f);
        isAttack = false;
    }

}
