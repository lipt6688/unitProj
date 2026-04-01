using UnityEngine;
using UnityEngine.Pool;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace Vampire
{
    /// <summary>
    /// Manager class for monsters, monster-dependent objects such as exp gems and coins, chests etc.
    /// </summary>
    public class EntityManager : MonoBehaviour
    {
        [Header("Monster Spawning Settings")]
        [SerializeField] private float monsterSpawnBufferDistance;  // Extra distance outside of the screen view at which monsters should be spawned
        [SerializeField] private float playerDirectionSpawnWeight;  // How much do we weight the player's movement direction in the spawning of monsters
        [SerializeField] private LayerMask monsterSpawnBlockedLayers;
        [SerializeField] private float monsterSpawnProbeRadius = 0.45f;
        [Header("Chest Spawning Settings")]
        [SerializeField] private  float chestSpawnRange = 5;
        [Header("Object Pool Settings")]
        [SerializeField] private GameObject monsterPoolParent;
        private MonsterPool[] monsterPools;
        [SerializeField] private GameObject projectilePoolParent;
        private List<ProjectilePool> projectilePools;
        private Dictionary<GameObject, int> projectileIndexByPrefab;
        [SerializeField] private GameObject throwablePoolParent;
        private List<ThrowablePool> throwablePools;
        private Dictionary<GameObject, int> throwableIndexByPrefab;
        [SerializeField] private GameObject boomerangPoolParent;
        private List<BoomerangPool> boomerangPools;
        private Dictionary<GameObject, int> boomerangIndexByPrefab;
        [SerializeField] private GameObject expGemPrefab;
        [SerializeField] private ExpGemPool expGemPool;
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private CoinPool coinPool;
        [SerializeField] private GameObject chestPrefab;
        [SerializeField] private ChestPool chestPool;
        [SerializeField] private GameObject textPrefab;
        [SerializeField] private DamageTextPool textPool;
        [Header("Spatial Hash Grid Settings")]
        [SerializeField] private Vector2 gridSize;
        [SerializeField] private Vector2Int gridDimensions;
        [Header("Dependencies")]
        [SerializeField] private SpriteRenderer flashSpriteRenderer;
        [SerializeField] private Camera playerCamera;  // 攝像頭
        private Character playerCharacter;  // 玩家的角色
        private StatsManager statsManager;
        private Inventory inventory;
        private InfiniteBackground infiniteBackground;
        private WorldMapCoordinator worldMapCoordinator;
        private int kingCitySpawnNonce;
        private bool playerInKingCity;
        private FastList<Monster> livingMonsters;
        private FastList<Collectable> magneticCollectables;
        public FastList<Chest> chests; 
        private float timeSinceLastMonsterSpawned;
        private float timeSinceLastChestSpawned;
        private float screenWidthWorldSpace, screenHeightWorldSpace, screenDiagonalWorldSpace;
        private float minSpawnDistance;
        private Coroutine flashCoroutine;
        private Coroutine shockwave;
        private SpatialHashGrid grid;
        public FastList<Monster> LivingMonsters { get => livingMonsters; }
        public FastList<Collectable> MagneticCollectables { get => magneticCollectables; }
        public Inventory Inventory { get => inventory; }
        public AbilitySelectionDialog AbilitySelectionDialog { get; private set; }
        public SpatialHashGrid Grid { get => grid; }
        public bool PlayerInKingCity { get => playerInKingCity; }

        public void Init(LevelBlueprint levelBlueprint, Character character, Inventory inventory, StatsManager statsManager, InfiniteBackground infiniteBackground, AbilitySelectionDialog abilitySelectionDialog)
        {
            this.playerCharacter = character;
            this.inventory = inventory;
            this.infiniteBackground = infiniteBackground;
            if (this.infiniteBackground != null)
                worldMapCoordinator = this.infiniteBackground.GetComponent<WorldMapCoordinator>() ?? this.infiniteBackground.GetComponentInParent<WorldMapCoordinator>() ?? FindObjectOfType<WorldMapCoordinator>();
            this.statsManager = statsManager;
            AbilitySelectionDialog = abilitySelectionDialog;

            // Determine the screen size in world space so that we can spawn enemies outside of it
            Vector2 bottomLeft = playerCamera.ViewportToWorldPoint(new Vector3(0, 0, playerCamera.nearClipPlane));
            Vector2 topRight = playerCamera.ViewportToWorldPoint(new Vector3(1, 1, playerCamera.nearClipPlane));
            screenWidthWorldSpace = topRight.x - bottomLeft.x;
            screenHeightWorldSpace = topRight.y - bottomLeft.y;
            screenDiagonalWorldSpace = (topRight - bottomLeft).magnitude;
            minSpawnDistance = screenDiagonalWorldSpace/2;

            // Init fast lists
            livingMonsters = new FastList<Monster>();
            magneticCollectables = new FastList<Collectable>();
            chests = new FastList<Chest>();
            
            // Initialize a monster pool for each monster prefab
            monsterPools = new MonsterPool[levelBlueprint.monsters.Length + 1];
            for (int i = 0; i < levelBlueprint.monsters.Length; i++)
            {
                monsterPools[i] = monsterPoolParent.AddComponent<MonsterPool>();
                monsterPools[i].Init(this, playerCharacter, levelBlueprint.monsters[i].monstersPrefab);
            }
            monsterPools[monsterPools.Length-1] = monsterPoolParent.AddComponent<MonsterPool>();
            monsterPools[monsterPools.Length-1].Init(this, playerCharacter, levelBlueprint.finalBoss.bossPrefab);
            // Initialize a projectile pool for each ranged projectile type
            projectileIndexByPrefab = new Dictionary<GameObject, int>();
            projectilePools = new List<ProjectilePool>();
            // Initialize a throwable pool for each throwable type
            throwableIndexByPrefab = new Dictionary<GameObject, int>();
            throwablePools = new List<ThrowablePool>();
            // Initialize a boomerang pool for each boomerang type
            boomerangIndexByPrefab = new Dictionary<GameObject, int>();
            boomerangPools = new List<BoomerangPool>();
            // Initialize remaining one-off object pools
            expGemPool.Init(this, playerCharacter, expGemPrefab);
            coinPool.Init(this, playerCharacter, coinPrefab);
            chestPool.Init(this, playerCharacter, chestPrefab);
            textPool.Init(this, playerCharacter, textPrefab);

            // Init spatial hash grid
            Vector2[] bounds = new Vector2[] { (Vector2)playerCharacter.transform.position - gridSize/2, (Vector2)playerCharacter.transform.position + gridSize/2 };
            grid = new SpatialHashGrid(bounds, gridDimensions);
        }

        public bool TryGetKingCitySpawnPosition(MonsterWorldKind world, out Vector2 position)
        {
            if (worldMapCoordinator == null)
            {
                position = default;
                return false;
            }

            position = worldMapCoordinator.GetRandomSpawnInKingCityRing(world, kingCitySpawnNonce++);
            return true;
        }

        public void SetPlayerInKingCity(bool inKingCity)
        {
            if (playerInKingCity == inKingCity)
                return;

            playerInKingCity = inKingCity;
            foreach (Monster monster in livingMonsters.ToList())
            {
                if (monster == null || monster is BossMonster)
                    continue;

                if (playerInKingCity)
                    monster.EnterKingCityCalmState();
                else
                    monster.ExitKingCityCalmState();
            }
        }

        public bool IsInsideAnyKingCityInterior(Vector2 position, float extraInsetCells = 0f)
        {
            if (worldMapCoordinator == null)
                return false;

            for (int i = 0; i < 3; i++)
            {
                Rect cityRect = worldMapCoordinator.GetKingCityInnerRectWorld((MonsterWorldKind)i, extraInsetCells);
                if (cityRect.Contains(position))
                    return true;
            }

            return false;
        }

        public bool TryProjectOutsideKingCityInterior(Vector2 position, out Vector2 projected, float padding = 0.25f)
        {
            projected = position;
            if (worldMapCoordinator == null)
                return false;

            bool changed = false;
            for (int i = 0; i < 3; i++)
            {
                Rect cityRect = worldMapCoordinator.GetKingCityInnerRectWorld((MonsterWorldKind)i, 0f);
                if (!cityRect.Contains(projected))
                    continue;

                float toLeft = Mathf.Abs(projected.x - cityRect.xMin);
                float toRight = Mathf.Abs(cityRect.xMax - projected.x);
                float toBottom = Mathf.Abs(projected.y - cityRect.yMin);
                float toTop = Mathf.Abs(cityRect.yMax - projected.y);

                float min = toLeft;
                int side = 0;
                if (toRight < min) { min = toRight; side = 1; }
                if (toBottom < min) { min = toBottom; side = 2; }
                if (toTop < min) { min = toTop; side = 3; }

                switch (side)
                {
                    case 0: projected.x = cityRect.xMin - padding; break;
                    case 1: projected.x = cityRect.xMax + padding; break;
                    case 2: projected.y = cityRect.yMin - padding; break;
                    case 3: projected.y = cityRect.yMax + padding; break;
                }

                changed = true;
            }

            return changed;
        }

        void Update()
        {
            // Rebuild the grid if the player gets close to the edge
            if (grid.CloseToEdge(playerCharacter))
            {
                grid.Rebuild(playerCharacter.transform.position);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Special Functions
        ////////////////////////////////////////////////////////////////////////////////
        public void CollectAllCoinsAndGems()
        {
            if (shockwave != null) StopCoroutine(shockwave);
            shockwave = StartCoroutine(infiniteBackground.Shockwave(screenDiagonalWorldSpace/2));
            foreach (Collectable collectable in magneticCollectables.ToList())
            {
                collectable.Collect();
            }
        }

        public void DamageAllVisibileEnemies(float damage)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(Flash());
            foreach (Monster monster in livingMonsters.ToList() )
            {
                if (TransformOnScreen(monster.transform, Vector2.one))
                    monster.TakeDamage(damage, Vector2.zero);
            }
        }

        public void KillAllMonsters()
        {
            foreach (Monster monster in livingMonsters.ToList() )
            {
                if (!(monster as BossMonster))
                    StartCoroutine(monster.Killed(false));
            }
        }

        private IEnumerator Flash()
        {
            flashSpriteRenderer.enabled = true;
            float t = 0;
            while (t < 1)
            {
                flashSpriteRenderer.color = new Color(1, 1, 1, 1-EasingUtils.EaseOutQuart(t));
                t += Time.unscaledDeltaTime * 4;
                yield return null;
            }
            flashSpriteRenderer.enabled = false;
        }

        public bool TransformOnScreen(Transform transform, Vector2 buffer = default(Vector2))
        {
            return (
                transform.position.x > playerCharacter.transform.position.x - screenWidthWorldSpace/2 - buffer.x &&
                transform.position.x < playerCharacter.transform.position.x + screenWidthWorldSpace/2 + buffer.x &&
                transform.position.y > playerCharacter.transform.position.y - screenHeightWorldSpace/2 - buffer.y &&
                transform.position.y < playerCharacter.transform.position.y + screenHeightWorldSpace/2 + buffer.y
            );
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Monster Spawning
        ////////////////////////////////////////////////////////////////////////////////
        public Monster SpawnMonsterRandomPosition(int monsterPoolIndex, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            Vector2 spawnPosition = GetRandomMonsterSpawnPosition();
            bool isBoss = monsterBlueprint is BossMonsterBlueprint;
            if (!isBoss)
            {
                for (int i = 0; i < 32 && IsBlockedSpawnPosition(spawnPosition); i++)
                    spawnPosition = GetRandomMonsterSpawnPosition();
            }
            // Vector2 spawnDirection = Random.insideUnitCircle.normalized;
            // Vector2 spawnPosition = (Vector2)playerCharacter.transform.position + spawnDirection * (minSpawnDistance + monsterSpawnBufferDistance);
            // Spawn the monster
            return SpawnMonster(monsterPoolIndex, spawnPosition, monsterBlueprint, hpBuff);
        }

        public Monster SpawnMonster(int monsterPoolIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            if (!(monsterBlueprint is BossMonsterBlueprint) && IsBlockedSpawnPosition(position))
            {
                for (int i = 0; i < 16; i++)
                {
                    Vector2 candidate = GetRandomMonsterSpawnPosition();
                    if (!IsBlockedSpawnPosition(candidate))
                    {
                        position = candidate;
                        break;
                    }
                }
            }

            Monster newMonster = monsterPools[monsterPoolIndex].Get();
            if (!MonsterBlueprintMatchesInstance(newMonster, monsterBlueprint))
            {
                Debug.LogError($"SpawnMonster: blueprint type mismatch. poolIndex={monsterPoolIndex}, monsterGO={newMonster.name}, monsterType={newMonster.GetType().Name}, blueprintType={(monsterBlueprint != null ? monsterBlueprint.GetType().Name : "null")}");
                newMonster.gameObject.SetActive(false);
                monsterPools[monsterPoolIndex].Release(newMonster);
                return null;
            }
            newMonster.Setup(monsterPoolIndex, position, monsterBlueprint, hpBuff);
            grid.InsertClient(newMonster);
            return newMonster;
        }

        private static bool MonsterBlueprintMatchesInstance(Monster monster, MonsterBlueprint blueprint)
        {
            if (monster == null || blueprint == null)
                return false;

            if (monster is MeleeMonster)
                return blueprint is MeleeMonsterBlueprint;
            if (monster is RangedMonster)
                return blueprint is RangedMonsterBlueprint;
            if (monster is BoomerangMonster)
                return blueprint is BoomerangMonsterBlueprint;
            if (monster is ThrowingMonster)
                return blueprint is ThrowingMonsterBlueprint;
            if (monster is BossMonster)
                return blueprint is BossMonsterBlueprint;

            return true;
        }

        public void DespawnMonster(int monsterPoolIndex, Monster monster, bool killedByPlayer = true)
        {
            if (killedByPlayer)
            {
                statsManager.IncrementMonstersKilled();
            }
            grid.RemoveClient(monster);
            monsterPools[monsterPoolIndex].Release(monster);
        }

        private Vector2 GetRandomMonsterSpawnPosition()
        {
            Vector2[] sideDirections = new Vector2[] { Vector2.left, Vector2.up, Vector2.right, Vector2.down };
            int sideIndex = Random.Range(0,4);
            Vector2 spawnPosition;
            if (sideIndex % 2 == 0)
            {
                spawnPosition = (Vector2)playerCharacter.transform.position + sideDirections[sideIndex] * (screenWidthWorldSpace/2+monsterSpawnBufferDistance) + Vector2.up * Random.Range(-screenHeightWorldSpace/2-monsterSpawnBufferDistance, screenHeightWorldSpace/2+monsterSpawnBufferDistance);
            }
            else
            {
                spawnPosition = (Vector2)playerCharacter.transform.position + sideDirections[sideIndex] * (screenHeightWorldSpace/2+monsterSpawnBufferDistance) + Vector2.right * Random.Range(-screenWidthWorldSpace/2-monsterSpawnBufferDistance, screenWidthWorldSpace/2+monsterSpawnBufferDistance);
            }
            return spawnPosition;
        }

        private bool IsBlockedSpawnPosition(Vector2 position)
        {
            float probeRadius = Mathf.Max(0.1f, monsterSpawnProbeRadius);
            if (monsterSpawnBlockedLayers.value == 0)
                monsterSpawnBlockedLayers = LayerMask.GetMask("Wall", "Walls", "Obstacle", "Obstacles", "Trap", "Traps", "RuntimeKingCityWalls");

            if (monsterSpawnBlockedLayers.value != 0)
            {
                Collider2D blocked = Physics2D.OverlapCircle(position, probeRadius, monsterSpawnBlockedLayers);
                if (blocked != null)
                    return true;
            }

            Collider2D[] nearby = Physics2D.OverlapCircleAll(position, probeRadius);
            for (int i = 0; i < nearby.Length; i++)
            {
                Collider2D hit = nearby[i];
                if (hit == null)
                    continue;

                string layerName = LayerMask.LayerToName(hit.gameObject.layer).ToLowerInvariant();
                string objectName = hit.gameObject.name.ToLowerInvariant();
                string parentName = hit.transform.parent != null ? hit.transform.parent.name.ToLowerInvariant() : string.Empty;
                if (layerName.Contains("wall") || layerName.Contains("trap") || objectName.Contains("wall") || objectName.Contains("trap") || parentName.Contains("wall") || parentName.Contains("trap"))
                    return true;
            }

            return false;
        }

        private Vector2 GetRandomMonsterSpawnPositionPlayerVelocity()
        {
            Vector2[] sideDirections = new Vector2[] { Vector2.left, Vector2.up, Vector2.right, Vector2.down };

            float[] sideWeights = new float[]
            {
                Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[0]),
                Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[1]),
                Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[2]),
                Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[3])
            };
            float extraWeight = sideWeights.Sum()/playerDirectionSpawnWeight;
            int badSideCount = sideWeights.Where(x => x <= 0).Count();
            for (int i = 0; i < sideWeights.Length; i++)
            {
                if (sideWeights[i] <= 0)
                    sideWeights[i] = extraWeight / badSideCount; 
            }
            float totalSideWeight = sideWeights.Sum();

            float rand = Random.Range(0f, totalSideWeight);
            float cumulative = 0;
            int sideIndex = -1;
            for (int i = 0; i < sideWeights.Length; i++)
            {
                cumulative += sideWeights[i];
                if (rand < cumulative)
                {
                    sideIndex = i;
                    break;
                }
            }

            Vector2 spawnPosition;
            if (sideIndex % 2 == 0)
            {
                spawnPosition = (Vector2)playerCharacter.transform.position + sideDirections[sideIndex] * (screenWidthWorldSpace/2+monsterSpawnBufferDistance) + Vector2.up * Random.Range(-screenHeightWorldSpace/2-monsterSpawnBufferDistance, screenHeightWorldSpace/2+monsterSpawnBufferDistance);
            }
            else
            {
                spawnPosition = (Vector2)playerCharacter.transform.position + sideDirections[sideIndex] * (screenHeightWorldSpace/2+monsterSpawnBufferDistance) + Vector2.right * Random.Range(-screenWidthWorldSpace/2-monsterSpawnBufferDistance, screenWidthWorldSpace/2+monsterSpawnBufferDistance);
            }
            return spawnPosition;
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Exp Gem Spawning
        ////////////////////////////////////////////////////////////////////////////////
        public ExpGem SpawnExpGem(Vector2 position, GemType gemType = GemType.White1, bool spawnAnimation = true)
        {
            ExpGem newGem = expGemPool.Get();
            newGem.Setup(position, gemType, spawnAnimation);
            return newGem;
        }

        public void DespawnGem(ExpGem gem)
        {
            expGemPool.Release(gem);
        }

        public void SpawnGemsAroundPlayer(int gemCount, GemType gemType = GemType.White1)
        {
            for (int i = 0; i < gemCount; i++)
            {
                Vector2 spawnDirection = Random.insideUnitCircle.normalized;
                Vector2 spawnPosition = (Vector2)playerCharacter.transform.position + spawnDirection * Mathf.Sqrt(Random.Range(1, Mathf.Pow(minSpawnDistance, 2)));
                SpawnExpGem(spawnPosition, gemType, false);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Coin Spawning
        ////////////////////////////////////////////////////////////////////////////////
        public Coin SpawnCoin(Vector2 position, CoinType coinType = CoinType.Bronze1, bool spawnAnimation = true)
        {
            Coin newCoin = coinPool.Get();
            newCoin.Setup(position, coinType, spawnAnimation);
            return newCoin;
        }

        public void DespawnCoin(Coin coin, bool pickedUpByPlayer = true)
        {
            if (pickedUpByPlayer)
            {
                statsManager.IncreaseCoinsGained((int)coin.CoinType);
            }
            coinPool.Release(coin);
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Chest Spawning
        ////////////////////////////////////////////////////////////////////////////////
        public Chest SpawnChest(ChestBlueprint chestBlueprint)
        {
            Chest newChest = chestPool.Get();
            newChest.Setup(chestBlueprint);
            // Ensure the chest is not spawned on top of another chest
            bool overlapsOtherChest = false;
            int tries = 0;
            do
            {
                Vector2 spawnDirection = Random.insideUnitCircle.normalized;
                Vector2 spawnPosition = (Vector2)playerCharacter.transform.position + spawnDirection * (minSpawnDistance + monsterSpawnBufferDistance + Random.Range(0, chestSpawnRange));
                newChest.transform.position = spawnPosition;
                overlapsOtherChest = false;
                foreach (Chest chest in chests)
                {
                    if (Vector2.Distance(chest.transform.position, spawnPosition) < 0.5f)
                    {
                        overlapsOtherChest = true;
                        break;
                    }
                }
            } while (overlapsOtherChest && tries++ < 100);
            chests.Add(newChest);
            return newChest;
        }

        public Chest SpawnChest(ChestBlueprint chestBlueprint, Vector2 position)
        {
            Chest newChest = chestPool.Get();
            newChest.transform.position = position;
            newChest.Setup(chestBlueprint);
            chests.Add(newChest);
            return newChest;
        }

        public void DespawnChest(Chest chest)
        {
            chests.Remove(chest);
            chestPool.Release(chest);
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Text Spawning
        ////////////////////////////////////////////////////////////////////////////////
        public DamageText SpawnDamageText(Vector2 position, float damage)
        {
            DamageText newText = textPool.Get();
            newText.Setup(position, damage);
            return newText;
        }

        public void DespawnDamageText(DamageText text)
        {
            textPool.Release(text);
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Projectile Spawning
        ////////////////////////////////////////////////////////////////////////////////
        public Projectile SpawnProjectile(int projectileIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
        {
            Projectile projectile = projectilePools[projectileIndex].Get();
            projectile.Setup(projectileIndex, position, damage, knockback, speed, targetLayer);
            return projectile;
        }
        
        public void DespawnProjectile(int projectileIndex, Projectile projectile)
        {
            projectilePools[projectileIndex].Release(projectile);
        }

        public int AddPoolForProjectile(GameObject projectilePrefab)
        {
            if (!projectileIndexByPrefab.ContainsKey(projectilePrefab))
            {
                projectileIndexByPrefab[projectilePrefab] = projectilePools.Count;
                ProjectilePool projectilePool = projectilePoolParent.AddComponent<ProjectilePool>();
                projectilePool.Init(this, playerCharacter, projectilePrefab);
                projectilePools.Add(projectilePool);
                return projectilePools.Count - 1;
            }
            return projectileIndexByPrefab[projectilePrefab];
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Throwable Spawning
        ////////////////////////////////////////////////////////////////////////////////
        public Throwable SpawnThrowable(int throwableIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
        {
            Throwable throwable = throwablePools[throwableIndex].Get();
            throwable.Setup(throwableIndex, position, damage, knockback, speed, targetLayer);
            return throwable;
        }

        public void DespawnThrowable(int throwableIndex, Throwable throwable)
        {
            throwablePools[throwableIndex].Release(throwable);
        }

        public int AddPoolForThrowable(GameObject throwablePrefab)
        {
            if (!throwableIndexByPrefab.ContainsKey(throwablePrefab))
            {
                throwableIndexByPrefab[throwablePrefab] = throwablePools.Count;
                ThrowablePool throwablePool = throwablePoolParent.AddComponent<ThrowablePool>();
                throwablePool.Init(this, playerCharacter, throwablePrefab);
                throwablePools.Add(throwablePool);
                return throwablePools.Count - 1;
            }
            return throwableIndexByPrefab[throwablePrefab];
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Boomerang Spawning
        ////////////////////////////////////////////////////////////////////////////////
        public Boomerang SpawnBoomerang(int boomerangIndex, Vector2 position, float damage, float knockback, float throwDistance, float throwTime, LayerMask targetLayer)
        {
            Boomerang boomerang = boomerangPools[boomerangIndex].Get();
            boomerang.Setup(boomerangIndex, position, damage, knockback, throwDistance, throwTime, targetLayer);
            return boomerang;
        }

        public void DespawnBoomerang(int boomerangIndex, Boomerang boomerang)
        {
            boomerangPools[boomerangIndex].Release(boomerang);
        }

        public int AddPoolForBoomerang(GameObject boomerangPrefab)
        {
            if (!boomerangIndexByPrefab.ContainsKey(boomerangPrefab))
            {
                boomerangIndexByPrefab[boomerangPrefab] = boomerangPools.Count;
                BoomerangPool boomerangPool = boomerangPoolParent.AddComponent<BoomerangPool>();
                boomerangPool.Init(this, playerCharacter, boomerangPrefab);
                boomerangPools.Add(boomerangPool);
                return boomerangPools.Count - 1;
            }
            return boomerangIndexByPrefab[boomerangPrefab];
        }
    }
}
