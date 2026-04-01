using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vampire;
using System.Linq;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemyManager : MonoBehaviour
{
    private LevelManager levelMan;
    private EntityManager entityMan;
    [Header("World Map")]
    public WorldMapConfig worldMapConfig;

    [Header("Old Visuals to Inject")]
    public GameObject oldEnemyPrefab; // e.g. Bat
    public GameObject oldWizardPrefab; // e.g. Mage
    [SerializeField] private bool injectExtraMonsterVisualsInEditor = true;

    private void Awake()
    {
        injectExtraMonsterVisualsInEditor = true;
        SetupNewMonsterSystem();
    }

    private void SetupNewMonsterSystem()
    {
        levelMan = gameObject.AddComponent<LevelManager>();
        entityMan = gameObject.AddComponent<EntityManager>();
        var statsMan = gameObject.AddComponent<StatsManager>();

        foreach (var p in GetComponents<Pool>()) Destroy(p);

        var expGemPool = gameObject.AddComponent<ExpGemPool>();
        var coinPool = gameObject.AddComponent<CoinPool>();
        var chestPool = gameObject.AddComponent<ChestPool>();
        var textPool = gameObject.AddComponent<DamageTextPool>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Character charComp = null;
        if (player != null)
        {
            charComp = player.GetComponent<Character>();
            if (charComp == null) charComp = player.AddComponent<Character>();
        }

#if UNITY_EDITOR
        var originalLevelAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelBlueprint>("Assets/Blueprints/Levels/Level 1.asset");
        var gemAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Exp Gem/經驗球.prefab");
        var coinAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin/金幣.prefab");
        var chestAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Chest/寶箱.prefab");
        var textAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Text/Damage Text.prefab");
        
        if (oldEnemyPrefab == null) oldEnemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy.prefab");
        if (oldWizardPrefab == null) oldWizardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Wizard.prefab");
        
        LevelBlueprint levelAsset = null;
        if (originalLevelAsset != null) {
            levelAsset = Instantiate(originalLevelAsset); // Clone to prevent modifying actual file!
        }
#else
        LevelBlueprint levelAsset = null;
        GameObject gemAsset = null, coinAsset = null, chestAsset = null, textAsset = null;
#endif

    BossMonsterBlueprint startupFireBossBlueprint = null;
    BossMonsterBlueprint startupGrassBossBlueprint = null;
    BossMonsterBlueprint startupIceBossBlueprint = null;

#if UNITY_EDITOR
        if (levelAsset != null && injectExtraMonsterVisualsInEditor)
        {
            Object[] enemyAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Enemy/Enemy.png");
            List<Sprite> batSprites = new List<Sprite>();
            foreach (var asset in enemyAssets)
            {
                if (asset is Sprite) batSprites.Add((Sprite)asset);
            }

            List<Sprite> wizardSprites = new List<Sprite>();
            for(int i=1; i<=4; i++) {
                var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Enemy/Wizard/wizard_run_0{i}.png");
                if (s != null) wizardSprites.Add(s);
            }

            List<Sprite[]> fantasySpriteSets = new List<Sprite[]>();
            List<string> fantasyNames = new List<string>();
            LoadFantasyMovementSequences(fantasyNames, fantasySpriteSets);

            // Figure out how many old blueprints exist.
            int originalBlueprintCount = 0;
            foreach (var container in levelAsset.monsters) {
                originalBlueprintCount += container.monsterBlueprints.Length;
            }

            // Create new Blueprints by cloning existing entries, only adding visuals.
            List<MonsterBlueprint> injectedBlueprints = new List<MonsterBlueprint>();

            MonsterBlueprint batBp = Instantiate(levelAsset.monsters[0].monsterBlueprints[0]);
            batBp.name = "Bat_Injected";
            if (batSprites.Count > 0) {
                batBp.walkSpriteSequence = batSprites.ToArray();
                batBp.walkFrameTime = 0.15f;
            }
            injectedBlueprints.Add(batBp);

            MonsterBlueprint wizardBp = Instantiate(levelAsset.monsters[1 % levelAsset.monsters.Length].monsterBlueprints[0]);
            wizardBp.name = "Wizard_Injected";
            if (wizardSprites.Count > 0) {
                wizardBp.walkSpriteSequence = wizardSprites.ToArray();
                wizardBp.walkFrameTime = 0.15f;
            }
            injectedBlueprints.Add(wizardBp);

            for (int i = 0; i < fantasySpriteSets.Count; i++)
            {
                MonsterBlueprint fantasyBp = Instantiate(levelAsset.monsters[0].monsterBlueprints[0]);
                fantasyBp.name = fantasyNames[i] + "_Injected";
                fantasyBp.walkSpriteSequence = fantasySpriteSets[i];
                fantasyBp.walkFrameTime = 0.12f;
                fantasyBp.visualScale = 3f;

                // Ensure injected fantasy monsters retain active melee behavior.
                fantasyBp.atk = Mathf.Max(1f, fantasyBp.atk);
                fantasyBp.atkspeed = Mathf.Max(0.8f, fantasyBp.atkspeed);
                fantasyBp.acceleration = Mathf.Max(2.2f, fantasyBp.acceleration);
                fantasyBp.movespeed = Mathf.Max(1.6f, fantasyBp.movespeed);
                injectedBlueprints.Add(fantasyBp);
            }

            // Append them as a new container
            var newContainer = new LevelBlueprint.MonstersContainer();
            newContainer.monstersPrefab = levelAsset.monsters[0].monstersPrefab; // just reuse melee prefab
            newContainer.monsterBlueprints = injectedBlueprints.ToArray();
            
            var ml = new List<LevelBlueprint.MonstersContainer>(levelAsset.monsters);
            ml.Add(newContainer);
            levelAsset.monsters = ml.ToArray();

            // Patch Spawn Chances so every monster has equal probability at all times.
            int injectedCount = injectedBlueprints.Count;
            int newTotal = originalBlueprintCount + injectedCount;
            int firstInjectedIndex = newTotal - injectedCount;

            if (levelAsset.monsterSpawnTable != null)
            {
                if (levelAsset.monsterSpawnTable.spawnChanceKeyframes != null)
                {
                    foreach (var kf in levelAsset.monsterSpawnTable.spawnChanceKeyframes)
                    {
                        float[] newChances = new float[newTotal];
                        float eachChance = newTotal > 0 ? (1f / newTotal) : 0f;
                        for (int i = 0; i < newTotal; i++)
                        {
                            newChances[i] = eachChance;
                        }

                        kf.spawnChances = newChances;
                    }
                }

                if (levelAsset.monsterSpawnTable.hpMultiplierKeyframes != null)
                {
                    foreach (var kf in levelAsset.monsterSpawnTable.hpMultiplierKeyframes)
                    {
                        float[] newHps = new float[newTotal];
                        for (int i = 0; i < kf.healthBuffs.Length; i++) {
                            newHps[i] = kf.healthBuffs[i];
                        }
                        // Injected monsters scale with the first monster's progression.
                        float injectedHp = kf.healthBuffs.Length > 0 ? kf.healthBuffs[0] : 1;
                        for (int i = 0; i < injectedCount; i++)
                        {
                            newHps[firstInjectedIndex + i] = injectedHp;
                        }

                        kf.healthBuffs = newHps;
                    }
                }
            }
        }

        if (levelAsset != null)
        {
            startupFireBossBlueprint = CreateAndInjectKingCityBossBlueprints(levelAsset);
            if (levelAsset.miniBosses != null && levelAsset.miniBosses.Length > 0)
                startupGrassBossBlueprint = levelAsset.miniBosses[0].bossBlueprint;
            if (levelAsset.finalBoss != null)
                startupIceBossBlueprint = levelAsset.finalBoss.bossBlueprint;
        }
#endif

        SetPrivate(entityMan, "expGemPrefab", gemAsset);
        SetPrivate(entityMan, "coinPrefab", coinAsset);
        SetPrivate(entityMan, "chestPrefab", chestAsset);
        SetPrivate(entityMan, "textPrefab", textAsset);

        SetPrivate(entityMan, "expGemPool", expGemPool);
        SetPrivate(entityMan, "coinPool", coinPool);
        SetPrivate(entityMan, "chestPool", chestPool);
        SetPrivate(entityMan, "textPool", textPool);

        var poolParent = new GameObject("VampirePools");
        SetPrivate(entityMan, "monsterPoolParent", poolParent);
        SetPrivate(entityMan, "projectilePoolParent", poolParent);
        SetPrivate(entityMan, "throwablePoolParent", poolParent);
        SetPrivate(entityMan, "boomerangPoolParent", poolParent);
        SetPrivate(entityMan, "playerCamera", Camera.main);
        SetPrivate(entityMan, "gridSize", new Vector2(4, 4));
        SetPrivate(entityMan, "gridDimensions", new Vector2Int(20, 20));
        SetPrivate(entityMan, "chestSpawnRange", 5f);
        SetPrivate(entityMan, "monsterSpawnBufferDistance", 2f);
        SetPrivate(entityMan, "playerDirectionSpawnWeight", 0.5f);

        SetPrivate(levelMan, "levelBlueprint", levelAsset);
        SetPrivate(levelMan, "playerCharacter", charComp);
        SetPrivate(levelMan, "entityManager", entityMan);
        SetPrivate(levelMan, "statsManager", statsMan);
        SetPrivate(levelMan, "startupFireBossBlueprint", startupFireBossBlueprint);
        SetPrivate(levelMan, "startupGrassBossBlueprint", startupGrassBossBlueprint);
        SetPrivate(levelMan, "startupIceBossBlueprint", startupIceBossBlueprint);

        InfiniteBackground mapBackground = FindObjectOfType<InfiniteBackground>();
        if (mapBackground == null)
        {
            GameObject host = new GameObject("RuntimeInfiniteTilemap");
            host.transform.SetParent(transform, false);
            mapBackground = host.AddComponent<InfiniteBackground>();
        }
        SetPrivate(levelMan, "infiniteBackground", mapBackground);

        // World/KingCity coordinator
        WorldMapCoordinator coordinator = FindObjectOfType<WorldMapCoordinator>();
        if (coordinator == null)
            coordinator = mapBackground.gameObject.AddComponent<WorldMapCoordinator>();

#if UNITY_EDITOR
        if (worldMapConfig == null)
            worldMapConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldMapConfig>("Assets/Blueprints/World/WorldMap.asset");
#endif
        if (worldMapConfig == null)
            worldMapConfig = ScriptableObject.CreateInstance<WorldMapConfig>();

        coordinator.Configure(worldMapConfig, mapBackground);
        mapBackground.SetWorldCoordinator(coordinator);
        coordinator.Rebuild();

        SetPrivate(levelMan, "worldMapCoordinator", coordinator);
        SetPrivate(levelMan, "worldMapConfig", worldMapConfig);

        // Disable camera clamping for infinite world.
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
        if (cam != null)
            cam.SetClampEnabled(false);

        DisableFiniteSceneWallColliders();
    }

    /// <summary>
    /// Scene <c>Tilemaps/Wall</c> uses a composite collider as a finite arena border; disable it so
    /// the player can walk with <see cref="InfiniteBackground"/> (king cities keep <c>RuntimeKingCityWalls</c>).
    /// </summary>
    private static void DisableFiniteSceneWallColliders()
    {
        GameObject tilemaps = GameObject.Find("Tilemaps");
        Transform wallT = tilemaps != null ? tilemaps.transform.Find("Wall") : null;
        if (wallT == null)
            return;
        foreach (var c in wallT.GetComponents<Collider2D>())
            c.enabled = false;
    }

    void SetPrivate(object obj, string name, object value)
    {
        var field = obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (field != null) field.SetValue(obj, value);
    }

#if UNITY_EDITOR
    private static void LoadFantasyMovementSequences(List<string> names, List<Sprite[]> spriteSets)
    {
        string rootPath = "Assets/Monsters Creatures Fantasy/Sprites";
        if (!AssetDatabase.IsValidFolder(rootPath))
            return;

        string[] monsterFolders = AssetDatabase.GetSubFolders(rootPath);
        for (int i = 0; i < monsterFolders.Length; i++)
        {
            string folderPath = monsterFolders[i];
            Sprite[] sequence = LoadBestMovementSequenceForFolder(folderPath);
            if (sequence == null || sequence.Length == 0)
                continue;

            string displayName = Path.GetFileName(folderPath);
            names.Add(displayName.Replace(" ", string.Empty));
            spriteSets.Add(sequence);
        }
    }

    private static Sprite[] LoadBestMovementSequenceForFolder(string folderPath)
    {
        string[] preferredFiles = new string[] { "Run.png", "Walk.png", "Flight.png", "Idle.png" };
        for (int i = 0; i < preferredFiles.Length; i++)
        {
            Sprite[] sequence = LoadSpriteSequenceFromAssetPath($"{folderPath}/{preferredFiles[i]}");
            if (sequence != null && sequence.Length > 0)
                return sequence;
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        if (textureGuids == null || textureGuids.Length == 0)
            return null;

        for (int i = 0; i < textureGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            Sprite[] fallbackSequence = LoadSpriteSequenceFromAssetPath(assetPath);
            if (fallbackSequence != null && fallbackSequence.Length > 0)
                return fallbackSequence;
        }

        return null;
    }

    private static Sprite[] LoadSpriteSequenceFromAssetPath(string assetPath)
    {
        List<Sprite> sprites = new List<Sprite>();
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                sprites.Add(sprite);
        }

        if (sprites.Count == 0)
        {
            Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (single != null)
                sprites.Add(single);
        }

        return sprites
            .OrderBy(s => s.name)
            .ToArray();
    }

    private BossMonsterBlueprint CreateAndInjectKingCityBossBlueprints(LevelBlueprint levelAsset)
    {
        if (levelAsset == null)
            return null;

        BossMonsterBlueprint finalBase = levelAsset.finalBoss != null ? levelAsset.finalBoss.bossBlueprint : null;
        BossMonsterBlueprint miniBase = (levelAsset.miniBosses != null && levelAsset.miniBosses.Length > 0)
            ? levelAsset.miniBosses[0].bossBlueprint
            : finalBase;

        BossMonsterBlueprint fireBoss = CreateEvilWizardStartupBossBlueprint(finalBase ?? miniBase);
        BossMonsterBlueprint grassBoss = CreateBringerOfDeathGrassBossBlueprint(miniBase ?? finalBase);
        BossMonsterBlueprint iceBoss = CreateWoodenAarakocraIceBossBlueprint(finalBase ?? miniBase);

        if (levelAsset.miniBosses != null && levelAsset.miniBosses.Length > 0 && grassBoss != null)
        {
            levelAsset.miniBosses[0].bossBlueprint = grassBoss;
            levelAsset.miniBosses[0].spawnTime = Mathf.Min(levelAsset.miniBosses[0].spawnTime, 210f);
        }

        if (levelAsset.finalBoss != null && iceBoss != null)
            levelAsset.finalBoss.bossBlueprint = iceBoss;

        return fireBoss;
    }

    private BossMonsterBlueprint CreateEvilWizardStartupBossBlueprint(BossMonsterBlueprint baseBoss)
    {
        if (baseBoss == null)
            return null;

        BossMonsterBlueprint evilWizardBoss = Instantiate(baseBoss);
        evilWizardBoss.name = "EvilWizard_StartupBoss";
        evilWizardBoss.homeWorld = MonsterWorldKind.Fire;
        evilWizardBoss.atk = 20f;
        evilWizardBoss.meleeDamage = 0f;
        evilWizardBoss.meleeKnockback = 0f;
        evilWizardBoss.meleeLayer = 0;

        GameObject evilWizardProjectile = CreateEvilWizardProjectileTemplate();

        List<GameObject> abilityTemplates = new List<GameObject>();
        GameObject walkAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Walk Boss Ability.prefab");
        GameObject chargeAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Charge Boss Ability.prefab");
        GameObject shotgunAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Shotgun Boss Ability.prefab");
        GameObject bulletHellAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Bullet Hell Boss Ability.prefab");

        if (walkAbilityAsset != null)
        {
            GameObject walkTemplate = Instantiate(walkAbilityAsset, transform);
            walkTemplate.name = "EvilWizard_WalkAbility_Template";
            walkTemplate.SetActive(true);
            abilityTemplates.Add(walkTemplate);
        }

        if (shotgunAbilityAsset != null)
        {
            GameObject shotgunTemplate = Instantiate(shotgunAbilityAsset, transform);
            shotgunTemplate.name = "EvilWizard_ShotgunAbility_Template";
            shotgunTemplate.SetActive(true);
            ShotgunBossAbility shotgun = shotgunTemplate.GetComponent<ShotgunBossAbility>();
            if (shotgun != null && evilWizardProjectile != null)
            {
                SetPrivate(shotgun, "bulletPrefab", evilWizardProjectile);
                SetPrivate(shotgun, "bulletCount", 8f);
                SetPrivate(shotgun, "spreadAngle", 42f);
                SetPrivate(shotgun, "fireRate", 3f);
                SetPrivate(shotgun, "bulletSpeedMin", 8f);
                SetPrivate(shotgun, "bulletSpeedMax", 12f);
            }
            abilityTemplates.Add(shotgunTemplate);
        }

        if (chargeAbilityAsset != null)
        {
            GameObject chargeTemplate = Instantiate(chargeAbilityAsset, transform);
            chargeTemplate.name = "EvilWizard_ChargeAbility_Template";
            chargeTemplate.SetActive(true);
            ChargeBossAbility charge = chargeTemplate.GetComponent<ChargeBossAbility>();
            if (charge != null)
            {
                SetPrivate(charge, "warningTime", 0.7f);
                SetPrivate(charge, "chargeDelay", 0f);
                SetPrivate(charge, "chargeCooldown", 0.55f);
                SetPrivate(charge, "chargeDistance", 7f);
                SetPrivate(charge, "chargeSpeed", 13f);
                SetPrivate(charge, "chargeCutoff", 1f);
                SetPrivate(charge, "chargeWidth", 1.2f);
                SetPrivate(charge, "chargeDamage", 20f);
                SetPrivate(charge, "flameLifetime", 8f);
                SetPrivate(charge, "defaultColor", new Color(1f, 0.35f, 0.2f, 0.2f));
                SetPrivate(charge, "warningColor", new Color(1f, 0.2f, 0.1f, 0.4f));
            }
            abilityTemplates.Add(chargeTemplate);
        }

        if (bulletHellAbilityAsset != null)
        {
            GameObject bulletHellTemplate = Instantiate(bulletHellAbilityAsset, transform);
            bulletHellTemplate.name = "EvilWizard_BulletHellAbility_Template";
            bulletHellTemplate.SetActive(true);
            BulletHellBossAbility bulletHell = bulletHellTemplate.GetComponent<BulletHellBossAbility>();
            if (bulletHell != null && evilWizardProjectile != null)
            {
                SetPrivate(bulletHell, "bulletPrefab", evilWizardProjectile);
                SetPrivate(bulletHell, "bulletCount", 14f);
                SetPrivate(bulletHell, "fireRate", 2f);
                SetPrivate(bulletHell, "bulletSpeed", 9f);
            }
            abilityTemplates.Add(bulletHellTemplate);
        }

        if (abilityTemplates.Count > 0)
            evilWizardBoss.abilityPrefabs = abilityTemplates.ToArray();
        else
            evilWizardBoss.abilityPrefabs = baseBoss.abilityPrefabs;

        Sprite[] moveSequence = LoadSpriteSequenceFromAssetPath("Assets/EVil Wizard/Sprites/Move.png");
        if (moveSequence == null || moveSequence.Length == 0)
            moveSequence = LoadSpriteSequenceFromAssetPath("Assets/EVil Wizard/Sprites/Idle.png");

        if (moveSequence != null && moveSequence.Length > 0)
        {
            evilWizardBoss.walkSpriteSequence = moveSequence;
            evilWizardBoss.walkFrameTime = 0.1f;
        }

        evilWizardBoss.visualScale = Mathf.Max(evilWizardBoss.visualScale * 1.65f, 4.8f);
        evilWizardBoss.hp = 2000f;
        evilWizardBoss.movespeed = Mathf.Max(evilWizardBoss.movespeed, 1f);
        return evilWizardBoss;
    }

    private BossMonsterBlueprint CreateBringerOfDeathGrassBossBlueprint(BossMonsterBlueprint baseBoss)
    {
        if (baseBoss == null)
            return null;

        BossMonsterBlueprint bringerBoss = Instantiate(baseBoss);
        bringerBoss.name = "BringerOfDeath_GrassBoss";
        bringerBoss.homeWorld = MonsterWorldKind.Grass;

        Sprite[] moveSequence = LoadSpriteSequenceFromAssetPath("Assets/Bringer Of Death/Sprite Sheet/Bringer-of-Death-SpritSheet_no-Effect.png");
        if (moveSequence == null || moveSequence.Length == 0)
            moveSequence = LoadSpriteSequenceFromAssetPath("Assets/Bringer Of Death/Sprite Sheet/Bringer-of-Death-SpritSheet.png");

        if (moveSequence != null && moveSequence.Length > 0)
        {
            bringerBoss.walkSpriteSequence = moveSequence;
            bringerBoss.walkFrameTime = moveSequence.Length > 1 ? 0.08f : 0.15f;
        }

        GameObject bringerProjectile = CreateProjectileTemplateFromSpriteSheet(
            "BringerOfDeath_Projectile_Template",
            "Assets/Bringer Of Death/Sprite Sheet/Bringer-of-Death-SpritSheet.png",
            0.85f,
            14
        );

        List<GameObject> abilityTemplates = new List<GameObject>();
        GameObject walkAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Walk Boss Ability.prefab");
        GameObject chargeAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Charge Boss Ability.prefab");
        GameObject shotgunAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Shotgun Boss Ability.prefab");
        GameObject bulletHellAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Bullet Hell Boss Ability.prefab");
        GameObject grenadeAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Grenade Boss Ability.prefab");

        if (walkAbilityAsset != null)
        {
            GameObject walkTemplate = Instantiate(walkAbilityAsset, transform);
            walkTemplate.name = "BringerOfDeath_WalkAbility_Template";
            walkTemplate.SetActive(true);
            WalkBossAbility walk = walkTemplate.GetComponent<WalkBossAbility>();
            if (walk != null)
                SetPrivate(walk, "walkTime", 2.1f);
            abilityTemplates.Add(walkTemplate);
        }

        if (chargeAbilityAsset != null)
        {
            GameObject chargeTemplate = Instantiate(chargeAbilityAsset, transform);
            chargeTemplate.name = "BringerOfDeath_ChargeAbility_Template";
            chargeTemplate.SetActive(true);
            ChargeBossAbility charge = chargeTemplate.GetComponent<ChargeBossAbility>();
            if (charge != null)
            {
                SetPrivate(charge, "chargeUpTime", 0.5f);
                SetPrivate(charge, "chargeDelay", 0.35f);
                SetPrivate(charge, "chargeCooldown", 0.6f);
                SetPrivate(charge, "chargeDistance", 7.4f);
                SetPrivate(charge, "chargeSpeed", 9.5f);
            }
            abilityTemplates.Add(chargeTemplate);
        }

        if (shotgunAbilityAsset != null)
        {
            GameObject shotgunTemplate = Instantiate(shotgunAbilityAsset, transform);
            shotgunTemplate.name = "BringerOfDeath_ShotgunAbility_Template";
            shotgunTemplate.SetActive(true);
            ShotgunBossAbility shotgun = shotgunTemplate.GetComponent<ShotgunBossAbility>();
            if (shotgun != null)
            {
                if (bringerProjectile != null)
                    SetPrivate(shotgun, "bulletPrefab", bringerProjectile);
                SetPrivate(shotgun, "bulletCount", 9f);
                SetPrivate(shotgun, "spreadAngle", 55f);
                SetPrivate(shotgun, "fireRate", 2.2f);
                SetPrivate(shotgun, "bulletSpeedMin", 8f);
                SetPrivate(shotgun, "bulletSpeedMax", 12f);
                SetPrivate(shotgun, "damage", 14f);
            }
            abilityTemplates.Add(shotgunTemplate);
        }

        if (bulletHellAbilityAsset != null)
        {
            GameObject bulletHellTemplate = Instantiate(bulletHellAbilityAsset, transform);
            bulletHellTemplate.name = "BringerOfDeath_BulletHellAbility_Template";
            bulletHellTemplate.SetActive(true);
            BulletHellBossAbility bulletHell = bulletHellTemplate.GetComponent<BulletHellBossAbility>();
            if (bulletHell != null)
            {
                if (bringerProjectile != null)
                    SetPrivate(bulletHell, "bulletPrefab", bringerProjectile);
                SetPrivate(bulletHell, "bulletCount", 16f);
                SetPrivate(bulletHell, "fireRate", 1.9f);
                SetPrivate(bulletHell, "bulletSpeed", 9f);
                SetPrivate(bulletHell, "damage", 10f);
            }
            abilityTemplates.Add(bulletHellTemplate);
        }

        if (grenadeAbilityAsset != null)
        {
            GameObject grenadeTemplate = Instantiate(grenadeAbilityAsset, transform);
            grenadeTemplate.name = "BringerOfDeath_GrenadeAbility_Template";
            grenadeTemplate.SetActive(true);
            GrenadeBossAbility grenade = grenadeTemplate.GetComponent<GrenadeBossAbility>();
            if (grenade != null)
            {
                SetPrivate(grenade, "fireRate", 1.5f);
                SetPrivate(grenade, "damage", 24f);
                SetPrivate(grenade, "knockback", 3f);
            }
            abilityTemplates.Add(grenadeTemplate);
        }

        if (abilityTemplates.Count > 0)
            bringerBoss.abilityPrefabs = abilityTemplates.ToArray();

        bringerBoss.visualScale = Mathf.Max(bringerBoss.visualScale * 1.85f, 4.4f);
        bringerBoss.hp = Mathf.Max(bringerBoss.hp * 2.6f, 3000f);
        bringerBoss.movespeed = Mathf.Max(bringerBoss.movespeed, 1.15f);
        bringerBoss.meleeDamage = Mathf.Max(bringerBoss.meleeDamage, 30f);
        return bringerBoss;
    }

    private BossMonsterBlueprint CreateWoodenAarakocraIceBossBlueprint(BossMonsterBlueprint baseBoss)
    {
        if (baseBoss == null)
            return null;

        BossMonsterBlueprint aarakocraBoss = Instantiate(baseBoss);
        aarakocraBoss.name = "Aarakocra_IceBoss";
        aarakocraBoss.homeWorld = MonsterWorldKind.Ice;

        Sprite[] moveSequence = LoadSpriteSequenceFromFolder("Assets/MMDevelopers/Wooden Arakocra/Art/Sprites/Idle");
        if (moveSequence == null || moveSequence.Length == 0)
            moveSequence = LoadSpriteSequenceFromAssetPath("Assets/MMDevelopers/Wooden Arakocra/Art/Sprites/Idle/Woode Arakocra Idle frame0000.png");

        if (moveSequence != null && moveSequence.Length > 0)
        {
            aarakocraBoss.walkSpriteSequence = moveSequence;
            aarakocraBoss.walkFrameTime = 0.1f;
        }

        GameObject iceProjectile = CreateProjectileTemplateFromFolder(
            "Aarakocra_Projectile_Template",
            "Assets/MMDevelopers/Wooden Arakocra/Art/Sprites/Attack 2",
            0.9f
        );

        List<GameObject> abilityTemplates = new List<GameObject>();
        GameObject walkAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Walk Boss Ability.prefab");
        GameObject shotgunAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Shotgun Boss Ability.prefab");
        GameObject bulletHellAbilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Boss Abilities/Bullet Hell Boss Ability.prefab");

        if (walkAbilityAsset != null)
        {
            GameObject walkTemplate = Instantiate(walkAbilityAsset, transform);
            walkTemplate.name = "Aarakocra_WalkAbility_Template";
            walkTemplate.SetActive(true);
            WalkBossAbility walk = walkTemplate.GetComponent<WalkBossAbility>();
            if (walk != null)
                SetPrivate(walk, "walkTime", 1.8f);
            abilityTemplates.Add(walkTemplate);
        }

        GameObject iceGroundTemplate = new GameObject("Aarakocra_IceGroundWarningAbility_Template");
        iceGroundTemplate.transform.SetParent(transform, false);
        IceGroundWarningBossAbility iceGround = iceGroundTemplate.AddComponent<IceGroundWarningBossAbility>();
        iceGroundTemplate.SetActive(true);
        SetPrivate(iceGround, "activationRange", 7f);
        SetPrivate(iceGround, "warningRadius", 1.45f);
        SetPrivate(iceGround, "warningDuration", 0.75f);
        SetPrivate(iceGround, "cooldown", 1.7f);
        SetPrivate(iceGround, "damage", 10);
        SetPrivate(iceGround, "slowChance", 0.75f);
        SetPrivate(iceGround, "slowDuration", 2.4f);
        SetPrivate(iceGround, "slowMultiplier", 0.55f);
        SetPrivate(iceGround, "freezeDuration", 1f);
        SetPrivate(iceGround, "warningColorA", new Color(0.45f, 0.82f, 1f, 0.2f));
        SetPrivate(iceGround, "warningColorB", new Color(0.72f, 0.94f, 1f, 0.45f));
        abilityTemplates.Add(iceGroundTemplate);

        if (shotgunAbilityAsset != null)
        {
            GameObject shotgunTemplate = Instantiate(shotgunAbilityAsset, transform);
            shotgunTemplate.name = "Aarakocra_ShotgunAbility_Template";
            shotgunTemplate.SetActive(true);
            ShotgunBossAbility shotgun = shotgunTemplate.GetComponent<ShotgunBossAbility>();
            if (shotgun != null)
            {
                if (iceProjectile != null)
                    SetPrivate(shotgun, "bulletPrefab", iceProjectile);
                SetPrivate(shotgun, "bulletCount", 10f);
                SetPrivate(shotgun, "spreadAngle", 70f);
                SetPrivate(shotgun, "fireRate", 2.4f);
                SetPrivate(shotgun, "bulletSpeedMin", 7f);
                SetPrivate(shotgun, "bulletSpeedMax", 11f);
                SetPrivate(shotgun, "damage", 13f);
            }
            abilityTemplates.Add(shotgunTemplate);
        }

        if (bulletHellAbilityAsset != null)
        {
            GameObject bulletHellTemplate = Instantiate(bulletHellAbilityAsset, transform);
            bulletHellTemplate.name = "Aarakocra_BulletHellAbility_Template";
            bulletHellTemplate.SetActive(true);
            BulletHellBossAbility bulletHell = bulletHellTemplate.GetComponent<BulletHellBossAbility>();
            if (bulletHell != null)
            {
                if (iceProjectile != null)
                    SetPrivate(bulletHell, "bulletPrefab", iceProjectile);
                SetPrivate(bulletHell, "bulletCount", 18f);
                SetPrivate(bulletHell, "fireRate", 1.8f);
                SetPrivate(bulletHell, "bulletSpeed", 8.5f);
                SetPrivate(bulletHell, "damage", 9f);
            }
            abilityTemplates.Add(bulletHellTemplate);
        }

        if (abilityTemplates.Count > 0)
            aarakocraBoss.abilityPrefabs = abilityTemplates.ToArray();

        float currentConfiguredScale = Mathf.Max(aarakocraBoss.visualScale * 2.0f, 5f);
        aarakocraBoss.visualScale = currentConfiguredScale / 3f;
        aarakocraBoss.hp = Mathf.Max(aarakocraBoss.hp * 2.8f, 3600f);
        aarakocraBoss.movespeed = Mathf.Max(aarakocraBoss.movespeed, 1.2f);
        return aarakocraBoss;
    }

    private static Sprite[] LoadSpriteSequenceFromFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
            return null;

        List<Sprite> sprites = new List<Sprite>();
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        for (int i = 0; i < textureGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            Sprite[] loaded = LoadSpriteSequenceFromAssetPath(assetPath);
            if (loaded != null && loaded.Length > 0)
                sprites.AddRange(loaded);
        }

        return sprites
            .Where(s => s != null)
            .OrderBy(s => s.name)
            .ToArray();
    }

    private GameObject CreateProjectileTemplateFromFolder(string templateName, string spriteFolderPath, float scale = 1f)
    {
        GameObject defaultProjectileAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectiles/Default Projectile.prefab");
        if (defaultProjectileAsset == null)
            return null;

        Sprite[] frames = LoadSpriteSequenceFromFolder(spriteFolderPath);
        if (frames == null || frames.Length == 0)
            return null;

        GameObject projectileTemplate = Instantiate(defaultProjectileAsset, transform);
        projectileTemplate.name = templateName;
        projectileTemplate.SetActive(false);

        Projectile projectile = projectileTemplate.GetComponent<Projectile>();
        SpriteRenderer projectileRenderer = null;
        if (projectile != null)
        {
            var f = typeof(Projectile).GetField("projectileSpriteRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null)
                projectileRenderer = f.GetValue(projectile) as SpriteRenderer;
        }

        if (projectileRenderer == null)
            projectileRenderer = projectileTemplate.GetComponentInChildren<SpriteRenderer>(true);

        if (projectileRenderer != null)
        {
            projectileRenderer.sprite = frames[0];
            projectileRenderer.color = Color.white;
            projectileRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        if (projectileRenderer != null && frames.Length > 1)
        {
            EvilWizardProjectileVisual visualAnim = projectileTemplate.GetComponent<EvilWizardProjectileVisual>();
            if (visualAnim == null)
                visualAnim = projectileTemplate.AddComponent<EvilWizardProjectileVisual>();
            visualAnim.Setup(projectileRenderer, frames, 0.07f);
        }

        TrailRenderer trail = projectileTemplate.GetComponent<TrailRenderer>();
        if (trail != null)
            trail.enabled = false;

        if (projectile != null)
        {
            var psField = typeof(Projectile).GetField("destructionParticleSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (psField != null)
                psField.SetValue(projectile, null);
        }

        return projectileTemplate;
    }

    private GameObject CreateProjectileTemplateFromSpriteSheet(string templateName, string spriteSheetPath, float scale = 1f, int maxFrames = 12)
    {
        GameObject defaultProjectileAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectiles/Default Projectile.prefab");
        if (defaultProjectileAsset == null)
            return null;

        Sprite[] frames = LoadSpriteSequenceFromAssetPath(spriteSheetPath);
        if (frames == null || frames.Length == 0)
            return null;

        if (maxFrames > 0 && frames.Length > maxFrames)
            frames = frames.Take(maxFrames).ToArray();

        GameObject projectileTemplate = Instantiate(defaultProjectileAsset, transform);
        projectileTemplate.name = templateName;
        projectileTemplate.SetActive(false);

        Projectile projectile = projectileTemplate.GetComponent<Projectile>();
        SpriteRenderer projectileRenderer = null;
        if (projectile != null)
        {
            var f = typeof(Projectile).GetField("projectileSpriteRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null)
                projectileRenderer = f.GetValue(projectile) as SpriteRenderer;
        }

        if (projectileRenderer == null)
            projectileRenderer = projectileTemplate.GetComponentInChildren<SpriteRenderer>(true);

        if (projectileRenderer != null)
        {
            projectileRenderer.sprite = frames[0];
            projectileRenderer.color = Color.white;
            projectileRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        if (projectileRenderer != null && frames.Length > 1)
        {
            EvilWizardProjectileVisual visualAnim = projectileTemplate.GetComponent<EvilWizardProjectileVisual>();
            if (visualAnim == null)
                visualAnim = projectileTemplate.AddComponent<EvilWizardProjectileVisual>();
            visualAnim.Setup(projectileRenderer, frames, 0.06f);
        }

        TrailRenderer trail = projectileTemplate.GetComponent<TrailRenderer>();
        if (trail != null)
            trail.enabled = false;

        if (projectile != null)
        {
            var psField = typeof(Projectile).GetField("destructionParticleSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (psField != null)
                psField.SetValue(projectile, null);
        }

        return projectileTemplate;
    }

    private GameObject CreateEvilWizardProjectileTemplate()
    {
        GameObject defaultProjectileAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectiles/Default Projectile.prefab");
        if (defaultProjectileAsset == null)
            return null;

        GameObject projectileTemplate = Instantiate(defaultProjectileAsset, transform);
        projectileTemplate.name = "EvilWizard_Projectile_Template";
        projectileTemplate.SetActive(false);

        Sprite[] attackSprites = LoadSpriteSequenceFromAssetPath("Assets/EVil Wizard/Sprites/Attack.png");
        Sprite[] takeHitSprites = LoadSpriteSequenceFromAssetPath("Assets/EVil Wizard/Sprites/Take Hit.png");
        Sprite[] moveSprites = LoadSpriteSequenceFromAssetPath("Assets/EVil Wizard/Sprites/Move.png");
        List<Sprite> projectileFrames = new List<Sprite>();
        if (attackSprites != null) projectileFrames.AddRange(attackSprites);
        if (takeHitSprites != null) projectileFrames.AddRange(takeHitSprites);
        if (projectileFrames.Count == 0 && moveSprites != null) projectileFrames.AddRange(moveSprites);
        Sprite projectileSprite = projectileFrames.Count > 0 ? projectileFrames[0] : null;

        Projectile projectile = projectileTemplate.GetComponent<Projectile>();
        SpriteRenderer projectileRenderer = null;
        if (projectile != null)
        {
            var f = typeof(Projectile).GetField("projectileSpriteRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null)
                projectileRenderer = f.GetValue(projectile) as SpriteRenderer;
        }

        if (projectileRenderer == null)
            projectileRenderer = projectileTemplate.GetComponentInChildren<SpriteRenderer>(true);

        if (projectileRenderer != null)
        {
            if (projectileSprite != null)
                projectileRenderer.sprite = projectileSprite;
            projectileRenderer.color = Color.white;
            projectileRenderer.transform.localScale = new Vector3(1.05f, 1.05f, 1f);
        }

        if (projectileRenderer != null && projectileFrames.Count > 1)
        {
            EvilWizardProjectileVisual visualAnim = projectileTemplate.GetComponent<EvilWizardProjectileVisual>();
            if (visualAnim == null)
                visualAnim = projectileTemplate.AddComponent<EvilWizardProjectileVisual>();
            visualAnim.Setup(projectileRenderer, projectileFrames.ToArray(), 0.06f);
        }

        TrailRenderer trail = projectileTemplate.GetComponent<TrailRenderer>();
        if (trail != null)
            trail.enabled = false;

        if (projectile != null)
        {
            var psField = typeof(Projectile).GetField("destructionParticleSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (psField != null)
                psField.SetValue(projectile, null);
        }

        return projectileTemplate;
    }
#endif
}

