using UnityEngine;

/// <summary>怪物仅从巢穴生成；控制频率与周围怪数量上限。</summary>
public class MonsterNest : MonoBehaviour
{
    [System.Serializable]
    private struct EnemyVisualVariant
    {
        public RuntimeAnimatorController controller;
        public Sprite fallbackSprite;
    }

    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject wizardPrefab;
    [SerializeField] private float spawnInterval = 4.5f;
    [SerializeField] private int maxNearby = 8;
    [SerializeField] private float countRadius = 14f;
    [SerializeField] private float spawnJitter = 1.5f;
    [Header("外观变体（仅影响 enemyPrefab，不改变机制）")]
    [SerializeField, Range(0f, 1f)] private float enemyVariantChance = 0.7f;
    [SerializeField] private EnemyVisualVariant[] enemyVisualVariants;

    private float _timer;

    private void Start()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = NestMarkerSprite.Get();
        sr.sortingOrder = 30;
        transform.localScale = Vector3.one * 1.35f;
    }

    public void BindPrefabs(GameObject enemy, GameObject wizard)
    {
        enemyPrefab = enemy;
        wizardPrefab = wizard;
    }

    public void BindEnemyVisualVariants(RuntimeAnimatorController[] controllers, Sprite[] fallbackSprites)
    {
        if (controllers == null && fallbackSprites == null)
        {
            enemyVisualVariants = null;
            return;
        }

        int controllerCount = controllers != null ? controllers.Length : 0;
        int spriteCount = fallbackSprites != null ? fallbackSprites.Length : 0;
        int count = Mathf.Max(controllerCount, spriteCount);
        enemyVisualVariants = new EnemyVisualVariant[count];
        for (int i = 0; i < count; i++)
        {
            enemyVisualVariants[i] = new EnemyVisualVariant
            {
                controller = i < controllerCount ? controllers[i] : null,
                fallbackSprite = i < spriteCount ? fallbackSprites[i] : null
            };
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;
        if (enemyPrefab == null && wizardPrefab == null)
            return;
        _timer += Time.deltaTime;
        if (_timer < spawnInterval)
            return;
        if (CountNearby() >= maxNearby)
            return;
        _timer = 0f;
        SpawnOne();
    }

    private int CountNearby()
    {
        int n = 0;
        foreach (Collider2D c in Physics2D.OverlapCircleAll(transform.position, countRadius))
        {
            if (c.CompareTag("Enemy"))
                n++;
        }
        return n;
    }

    private void SpawnOne()
    {
        GameObject prefab;
        bool isEnemyPrefab;
        if (enemyPrefab != null && wizardPrefab != null)
        {
            isEnemyPrefab = Random.value < 0.55f;
            prefab = isEnemyPrefab ? enemyPrefab : wizardPrefab;
        }
        else
        {
            isEnemyPrefab = enemyPrefab != null;
            prefab = enemyPrefab != null ? enemyPrefab : wizardPrefab;
        }

        Vector2 p = (Vector2)transform.position + Random.insideUnitCircle * spawnJitter;
        GameObject spawned = Instantiate(prefab, p, Quaternion.identity);
        if (isEnemyPrefab)
            TryApplyEnemyVisualVariant(spawned);
    }

    private void TryApplyEnemyVisualVariant(GameObject spawned)
    {
        if (spawned == null)
            return;
        if (enemyVisualVariants == null || enemyVisualVariants.Length == 0)
            return;
        if (Random.value > enemyVariantChance)
            return;

        EnemyVisualVariant variant = enemyVisualVariants[Random.Range(0, enemyVisualVariants.Length)];
        if (variant.controller != null)
        {
            Animator animator = spawned.GetComponent<Animator>();
            if (animator != null)
                animator.runtimeAnimatorController = variant.controller;
        }

        if (variant.fallbackSprite != null)
        {
            SpriteRenderer sr = spawned.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sprite = variant.fallbackSprite;
        }
    }
}
