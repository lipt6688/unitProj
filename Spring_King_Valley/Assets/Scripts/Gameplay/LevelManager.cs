using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelBlueprint levelBlueprint;
        [SerializeField] private Character playerCharacter;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private AbilityManager abilityManager;
        [SerializeField] private AbilitySelectionDialog abilitySelectionDialog;
        [SerializeField] private InfiniteBackground infiniteBackground;
        [SerializeField] private WorldMapCoordinator worldMapCoordinator;
        [SerializeField] private WorldMapConfig worldMapConfig;
        [SerializeField] private Inventory inventory;
        [SerializeField] private StatsManager statsManager;
        [SerializeField] private GameOverDialog gameOverDialog;
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private bool requireThemeSelectionBeforeStart = false;
        [Header("Startup Boss")]
        [SerializeField] private BossMonsterBlueprint startupFireBossBlueprint;
        [SerializeField] private BossMonsterBlueprint startupGrassBossBlueprint;
        [SerializeField] private BossMonsterBlueprint startupIceBossBlueprint;
        [Header("Combat Tuning")]
        [SerializeField, Range(1f, 4f)] private float regularMonsterHpScale = 1f;
        [SerializeField] private float regularMonsterMinHp = 16f;
        [SerializeField, Range(1f, 5f)] private float bossMonsterHpScale = 1f;
        [SerializeField] private float bossMonsterMinHp = 180f;
        [SerializeField, Min(0.1f)] private float minRegularMonsterSpawnRate = 3f;
        private float levelTime = 0;
        private float timeSinceLastMonsterSpawned;
        private float timeSinceLastChestSpawned;
        private bool miniBossSpawned = false;
        private bool finalBossSpawned = false;
        private bool startupFireBossSpawned = false;
        private bool startupGrassBossSpawned = false;
        private bool startupIceBossSpawned = false;
        private float startupFireBossRetryTimer = 0f;
        private bool playerInAnyKingCity;
        private MonsterWorldKind? playerKingCityWorld;
        private BossMonster startupFireBossMonster;
        private BossMonster startupGrassBossMonster;
        private BossMonster startupIceBossMonster;
        private bool isInitialized;
        private bool waitingForThemeSelection;

        public void Init(LevelBlueprint levelBlueprint)
        {
            if (isInitialized)
                return;

            this.levelBlueprint = levelBlueprint;
            levelTime = 0;
            DropCounterBoard.ResetSessionCounters();
            
            if (entityManager != null) entityManager.Init(this.levelBlueprint, playerCharacter, inventory, statsManager, infiniteBackground, abilitySelectionDialog);
            // Initialize the ability manager
            if (abilityManager != null) abilityManager.Init(this.levelBlueprint, entityManager, playerCharacter, abilityManager);
            if (abilitySelectionDialog != null) abilitySelectionDialog.Init(abilityManager, entityManager, playerCharacter);
            // Initialize the character
            if (playerCharacter != null) {
                playerCharacter.Init(entityManager, abilityManager, statsManager);
                playerCharacter.OnDeath.AddListener(GameOver);
            }
            // Spawn initial gems
            if (entityManager != null) entityManager.SpawnGemsAroundPlayer(this.levelBlueprint.initialExpGemCount, this.levelBlueprint.initialExpGemType);
            // Spawn a singular chest
            if (entityManager != null) entityManager.SpawnChest(levelBlueprint.chestBlueprint);
            // Initialize the infinite background
            if (infiniteBackground != null)
            {
                if (worldMapCoordinator != null && worldMapConfig != null)
                {
                    infiniteBackground.Init(this.levelBlueprint.backgroundTexture, playerCharacter.transform, worldMapConfig.worldSeed, worldMapCoordinator);
                    worldMapCoordinator.Configure(worldMapConfig, infiniteBackground);
                    worldMapCoordinator.Rebuild();

                    var hud = FindObjectOfType<KingCityCoordinateHud>();
                    if (hud == null)
                        hud = gameObject.AddComponent<KingCityCoordinateHud>();
                    hud.Init(worldMapCoordinator);
                }
                else
                {
                    infiniteBackground.Init(this.levelBlueprint.backgroundTexture, playerCharacter.transform);
                }
            }
            // Initialize inventory
            if (inventory != null) inventory.Init();

            SpawnStartupKingCityBosses();

            isInitialized = true;
        }

        // Start is called before the first frame update
        void Start()
        {
            StartWithTheme(0);
        }

        // Update is called once per frame
        void Update()
        {
            if (!isInitialized)
                return;

            if (!startupFireBossSpawned || !startupGrassBossSpawned || !startupIceBossSpawned)
            {
                startupFireBossRetryTimer += Time.deltaTime;
                if (startupFireBossRetryTimer >= 1f)
                {
                    startupFireBossRetryTimer = 0f;
                    SpawnStartupKingCityBosses();
                }
            }

            UpdateKingCityPlayerState();
            UpdateBossHealthBar();

            // Time
            levelTime += Time.deltaTime;
            if (gameTimer != null) gameTimer.SetTime(levelTime);
            // Monster spawning timer
            if (levelTime < levelBlueprint.levelTime)
            {
                timeSinceLastMonsterSpawned += Time.deltaTime;
                float spawnRate = GetRegularMonsterSpawnRate();
                float monsterSpawnDelay = spawnRate > 0 ? 1.0f/spawnRate : float.PositiveInfinity;
                if (timeSinceLastMonsterSpawned >= monsterSpawnDelay)
                {
                    int monsterIndex = Random.Range(0, levelBlueprint.MonsterIndexMap.Count);
                    (int poolIndex, int blueprintIndex) = levelBlueprint.MonsterIndexMap[monsterIndex];
                    MonsterBlueprint monsterBlueprint = levelBlueprint.monsters[poolIndex].monsterBlueprints[blueprintIndex];
                    float hpBuff = ComputeSpawnHpBuff(monsterBlueprint, 0f, false);
                    if (!TryResolvePoolIndexForBlueprint(levelBlueprint, monsterBlueprint, out int resolvedPoolIndex))
                        resolvedPoolIndex = poolIndex;

                    entityManager.SpawnMonsterRandomPosition(resolvedPoolIndex, monsterBlueprint, hpBuff);
                    timeSinceLastMonsterSpawned = Mathf.Repeat(timeSinceLastMonsterSpawned, monsterSpawnDelay);
                }
            }
            // Boss spawning
            if (!playerInAnyKingCity && !miniBossSpawned && levelTime > levelBlueprint.miniBosses[0].spawnTime)
            {
                miniBossSpawned = true;
                float hpBuff = ComputeSpawnHpBuff(levelBlueprint.miniBosses[0].bossBlueprint, 0f, true);
                var bp = levelBlueprint.miniBosses[0].bossBlueprint;
                if (entityManager != null && entityManager.TryGetKingCitySpawnPosition(bp.homeWorld, out Vector2 bossPos))
                    entityManager.SpawnMonster(levelBlueprint.monsters.Length, bossPos, bp, hpBuff);
                else
                    entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length, bp, hpBuff);
            }
            // Boss spawning
            if (!playerInAnyKingCity && !finalBossSpawned && levelTime > levelBlueprint.levelTime)
            {
                //entityManager.KillAllMonsters();
                finalBossSpawned = true;
                float hpBuff = ComputeSpawnHpBuff(levelBlueprint.finalBoss.bossBlueprint, 0f, true);
                var bp = levelBlueprint.finalBoss.bossBlueprint;
                Monster finalBoss;
                if (entityManager != null && entityManager.TryGetKingCitySpawnPosition(bp.homeWorld, out Vector2 finalPos))
                    finalBoss = entityManager.SpawnMonster(levelBlueprint.monsters.Length, finalPos, bp, hpBuff);
                else
                    finalBoss = entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length, bp, hpBuff);
                finalBoss.OnKilled.AddListener(LevelPassed);
            }
            // Chest spawning timer
            timeSinceLastChestSpawned += Time.deltaTime;
            if (timeSinceLastChestSpawned >= levelBlueprint.chestSpawnDelay)
            {
                for (int i = 0; i < levelBlueprint.chestSpawnAmount; i++)
                {
                    entityManager.SpawnChest(levelBlueprint.chestBlueprint);
                }
                timeSinceLastChestSpawned = Mathf.Repeat(timeSinceLastChestSpawned, levelBlueprint.chestSpawnDelay);
            }
        }

        public void GameOver()
        {
            Time.timeScale = 0;
            int coinCount = PlayerPrefs.GetInt("Coins");
            if (statsManager != null) PlayerPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);
            if (gameOverDialog != null) gameOverDialog.Open(false, statsManager);
        }

        public void LevelPassed(Monster finalBossKilled)
        {
            Time.timeScale = 0;
            int coinCount = PlayerPrefs.GetInt("Coins");
            if (statsManager != null) PlayerPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);
            if (gameOverDialog != null) gameOverDialog.Open(true, statsManager);
        }

        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(0);
        }

        private void StartWithTheme(int themeIndex)
        {
            if (infiniteBackground != null)
                infiniteBackground.SetThemeByIndex(themeIndex);

            Time.timeScale = 1f;
            waitingForThemeSelection = false;
            Init(levelBlueprint);
        }

        private float ComputeSpawnHpBuff(MonsterBlueprint blueprint, float hpMultiplierFromTable, bool boss)
        {
            if (blueprint == null)
                return 0f;

            float tableScale = 1f + Mathf.Max(0f, hpMultiplierFromTable);
            float baseHpWithTable = blueprint.hp * tableScale;

            float scale = boss ? bossMonsterHpScale : regularMonsterHpScale;
            float minHp = boss ? bossMonsterMinHp : regularMonsterMinHp;

            float targetHp = Mathf.Max(baseHpWithTable * scale, minHp);
            return Mathf.Max(0f, targetHp - blueprint.hp);
        }

        private float GetRegularMonsterSpawnRate()
        {
            float tNormalized = levelBlueprint != null && levelBlueprint.levelTime > 0f
                ? Mathf.Clamp01(levelTime / levelBlueprint.levelTime)
                : 0f;

            float tableSpawnRate = 0f;
            if (levelBlueprint != null && levelBlueprint.monsterSpawnTable != null)
                tableSpawnRate = levelBlueprint.monsterSpawnTable.GetSpawnRate(tNormalized);

            if (!float.IsFinite(tableSpawnRate))
                tableSpawnRate = 0f;

            return Mathf.Max(minRegularMonsterSpawnRate, tableSpawnRate);
        }

        private static bool TryResolvePoolIndexForBlueprint(LevelBlueprint level, MonsterBlueprint blueprint, out int poolIndex)
        {
            poolIndex = 0;
            if (level == null || blueprint == null || level.monsters == null)
                return false;

            for (int i = 0; i < level.monsters.Length; i++)
            {
                var arr = level.monsters[i].monsterBlueprints;
                if (arr == null)
                    continue;
                for (int j = 0; j < arr.Length; j++)
                {
                    if (arr[j] == blueprint)
                    {
                        poolIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private void SpawnStartupFireBoss()
        {
            if (startupFireBossSpawned || entityManager == null || levelBlueprint == null)
                return;

            BossMonsterBlueprint bossBlueprint = startupFireBossBlueprint;
            if (bossBlueprint == null && levelBlueprint.finalBoss != null)
            {
                bossBlueprint = levelBlueprint.finalBoss.bossBlueprint;
            }
            if (bossBlueprint == null)
                return;

            Vector2 spawnPosition;
            if (worldMapCoordinator != null)
            {
                spawnPosition = worldMapCoordinator.GetKingCityCenterWorld(MonsterWorldKind.Fire);
            }
            else if (!entityManager.TryGetKingCitySpawnPosition(MonsterWorldKind.Fire, out spawnPosition))
            {
                spawnPosition = playerCharacter != null ? (Vector2)playerCharacter.transform.position : Vector2.zero;
            }

            float hpBuff = ComputeSpawnHpBuff(bossBlueprint, 0f, true);
            Monster startupBoss = entityManager.SpawnMonster(levelBlueprint.monsters.Length, spawnPosition, bossBlueprint, hpBuff);

            if (startupBoss == null)
                return;

            if (startupBoss is BossMonster boss && worldMapCoordinator != null)
            {
                Rect fireCityInner = worldMapCoordinator.GetKingCityInnerRectWorld(MonsterWorldKind.Fire, 2f);
                boss.SetLeashRect(fireCityInner);
                Rect fireCityActivation = worldMapCoordinator.GetKingCityInnerRectWorld(MonsterWorldKind.Fire, 0f);
                boss.SetChaseActivationRect(fireCityActivation);
            }

            if (startupBoss is BossMonster fireBoss)
            {
                startupFireBossMonster = fireBoss;
                startupBoss.OnKilled.AddListener(OnStartupBossKilled);
            }

            Debug.Log($"Startup Fire Boss spawned at ({startupBoss.transform.position.x:0.0}, {startupBoss.transform.position.y:0.0})");
            startupFireBossSpawned = true;
        }

        private void SpawnStartupKingCityBosses()
        {
            SpawnStartupFireBoss();

            BossMonsterBlueprint grassBoss = startupGrassBossBlueprint;
            if (grassBoss == null && levelBlueprint != null && levelBlueprint.miniBosses != null && levelBlueprint.miniBosses.Length > 0)
                grassBoss = levelBlueprint.miniBosses[0].bossBlueprint;

            BossMonsterBlueprint iceBoss = startupIceBossBlueprint;
            if (iceBoss == null && levelBlueprint != null && levelBlueprint.finalBoss != null)
                iceBoss = levelBlueprint.finalBoss.bossBlueprint;

            SpawnStartupKingCityBoss(MonsterWorldKind.Grass, grassBoss, false, ref startupGrassBossSpawned);
            SpawnStartupKingCityBoss(MonsterWorldKind.Ice, iceBoss, true, ref startupIceBossSpawned);
        }

        private void SpawnStartupKingCityBoss(MonsterWorldKind world, BossMonsterBlueprint bossBlueprint, bool markAsFinalBoss, ref bool spawned)
        {
            if (spawned || entityManager == null || levelBlueprint == null || bossBlueprint == null)
                return;

            Vector2 spawnPosition;
            if (worldMapCoordinator != null)
            {
                spawnPosition = worldMapCoordinator.GetKingCityCenterWorld(world);
            }
            else if (!entityManager.TryGetKingCitySpawnPosition(world, out spawnPosition))
            {
                spawnPosition = playerCharacter != null ? (Vector2)playerCharacter.transform.position : Vector2.zero;
            }

            float hpBuff = ComputeSpawnHpBuff(bossBlueprint, 0f, true);
            Monster startupBoss = entityManager.SpawnMonster(levelBlueprint.monsters.Length, spawnPosition, bossBlueprint, hpBuff);
            if (startupBoss == null)
                return;

            if (startupBoss is BossMonster boss && worldMapCoordinator != null)
            {
                Rect cityInner = worldMapCoordinator.GetKingCityInnerRectWorld(world, 2f);
                boss.SetLeashRect(cityInner);
                Rect cityActivation = worldMapCoordinator.GetKingCityInnerRectWorld(world, 0f);
                boss.SetChaseActivationRect(cityActivation);
            }

            if (startupBoss is BossMonster kingCityBoss)
            {
                if (world == MonsterWorldKind.Grass)
                    startupGrassBossMonster = kingCityBoss;
                else if (world == MonsterWorldKind.Ice)
                    startupIceBossMonster = kingCityBoss;

                startupBoss.OnKilled.AddListener(OnStartupBossKilled);
            }

            if (world == MonsterWorldKind.Grass)
                miniBossSpawned = true;

            if (markAsFinalBoss)
            {
                finalBossSpawned = true;
                startupBoss.OnKilled.AddListener(LevelPassed);
            }

            spawned = true;
            Debug.Log($"Startup {world} Boss spawned at ({startupBoss.transform.position.x:0.0}, {startupBoss.transform.position.y:0.0})");
        }

        private void OnGUI()
        {
            // Theme selection is intentionally disabled.
        }

        private void UpdateKingCityPlayerState()
        {
            bool inCity = false;
            MonsterWorldKind? currentWorld = null;
            if (worldMapCoordinator != null && playerCharacter != null)
            {
                Vector2 p = playerCharacter.transform.position;
                for (int i = 0; i < 3; i++)
                {
                    MonsterWorldKind world = (MonsterWorldKind)i;
                    if (!worldMapCoordinator.IsInsideKingCityInterior(world, p, 0f))
                        continue;

                    inCity = true;
                    currentWorld = world;
                    break;
                }
            }

            if (inCity == playerInAnyKingCity && currentWorld == playerKingCityWorld)
                return;

            playerInAnyKingCity = inCity;
            playerKingCityWorld = currentWorld;
            if (entityManager != null)
                entityManager.SetPlayerInKingCity(playerInAnyKingCity);
        }

        private void UpdateBossHealthBar()
        {
            if (UIManager.instance == null)
                return;

            if (!playerKingCityWorld.HasValue)
            {
                UIManager.instance.SetBossHpVisible(false);
                return;
            }

            BossMonster cityBoss = GetBossForWorld(playerKingCityWorld.Value);
            if (cityBoss == null || cityBoss.gameObject == null || !cityBoss.gameObject.activeInHierarchy)
            {
                UIManager.instance.SetBossHpVisible(false);
                return;
            }

            UIManager.instance.UpdateBossHp(GetBossLabel(playerKingCityWorld.Value), cityBoss.HP, cityBoss.MaxHP);
        }

        private BossMonster GetBossForWorld(MonsterWorldKind world)
        {
            switch (world)
            {
                case MonsterWorldKind.Fire: return startupFireBossMonster;
                case MonsterWorldKind.Grass: return startupGrassBossMonster;
                case MonsterWorldKind.Ice: return startupIceBossMonster;
                default: return null;
            }
        }

        private static string GetBossLabel(MonsterWorldKind world)
        {
            switch (world)
            {
                case MonsterWorldKind.Fire: return "火王城 BOSS";
                case MonsterWorldKind.Grass: return "草王城 BOSS";
                case MonsterWorldKind.Ice: return "冰王城 BOSS";
                default: return "BOSS";
            }
        }

        private void OnStartupBossKilled(Monster deadBoss)
        {
            if (deadBoss == startupFireBossMonster)
                startupFireBossMonster = null;
            if (deadBoss == startupGrassBossMonster)
                startupGrassBossMonster = null;
            if (deadBoss == startupIceBossMonster)
                startupIceBossMonster = null;

            UpdateBossHealthBar();
        }
    }
}
