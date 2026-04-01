using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerAttack : MonoBehaviour
{
    public enum WeaponTestMode
    {
        All = 0,
        Rifle = 1,
        Saber = 2,
        Gatling = 3,
        Knockback = 4,
        Cannon = 5,
        Shotgun = 6
    }
    public WeaponTestMode testMode = WeaponTestMode.Rifle;
    [Header("Damage")]
    [SerializeField, Min(1f)] private float globalDamageMultiplier = 1f;
    public KeyCode allWeaponsKey = KeyCode.Alpha0;
    public KeyCode rifleModeKey = KeyCode.Alpha1;
    public KeyCode saberModeKey = KeyCode.Alpha2;
    public KeyCode gatlingModeKey = KeyCode.Alpha3;
    public KeyCode knockbackModeKey = KeyCode.Alpha4;
    public KeyCode cannonModeKey = KeyCode.Alpha5;
    public KeyCode shotgunModeKey = KeyCode.Alpha6;
    public GameObject saberVisual;
    public Sprite saberWeaponSprite;
    public int saberSortingOrder = 200;
    public float saberAttackRate = 1.0f;
    public float saberAttackRange = 3f;
    public float saberThrustDistance = 1.7f;
    public float saberThrustDuration = 0.22f;
    public int saberMinAttack = 18;
    public int saberMaxAttack = 30;
    public float saberHitRadius = 0.7f;
    public Vector3 saberVisualScale = new Vector3(1.4f, 1.2f, 1.3f);

    private float nextSaberTime;
    private bool isSaberThrusting;
    private float saberThrustTimer;
    private readonly List<Collider2D> saberHitEnemies = new List<Collider2D>();
    private Vector2 saberAttackDirection = Vector2.right;
    public GameObject rifleVisual;
    public Sprite rifleWeaponSprite;
    public float rifleAttackRate = 0.8f;
    [Tooltip("Rifle max attack range")]
    public float rifleAttackRange = 18f;
    public float rifleBulletSpeed = 45f;
    public int rifleMinDamage = 95;
    public int rifleMaxDamage = 135;
    public GameObject rifleBulletPrefab;
    public Vector3 rifleVisualScale = new Vector3(1.45f, 0.95f, 1f);
    private float nextRifleTime;
    public GameObject cannonVisual;
    public Sprite cannonWeaponSprite;
    public float cannonCooldown = 3.5f;
    public float cannonAoeRadius = 2.8f;
    public int cannonMinDamage = 72;
    public int cannonMaxDamage = 108;
    public GameObject cannonballPrefab;
    public Vector3 cannonVisualScale = new Vector3(1.7f, 1.1f, 1f);
    private float nextCannonTime;
    public GameObject gatlingVisual;
    public Sprite gatlingWeaponSprite;
    public float gatlingAttackRate = 26f;
    public float gatlingAttackRange = 8f;
    public float gatlingBulletSpeed = 28f;
    public int gatlingMinDamage = 9;
    public int gatlingMaxDamage = 12;
    public float gatlingOrbitRadius = 1.2f;
    public float gatlingOrbitAngularSpeed = 5f;
    public GameObject gatlingBulletPrefab;
    public Vector3 gatlingVisualScale = new Vector3(0.16f, 0.16f, 0.8f);
    private float nextGatlingTime;

    [Header("6. Shotgun")]
    public GameObject shotgunVisual;
    public Sprite shotgunWeaponSprite;
    public float shotgunCooldown = 1.8f;
    public float shotgunRange = 7f;
    public float shotgunClusterRadius = 2.2f;
    public int shotgunPelletCount = 6;
    public float shotgunSpreadAngle = 24f;
    public float shotgunBulletSpeed = 24f;
    public int shotgunMinDamage = 30;
    public int shotgunMaxDamage = 42;
    public float shotgunKnockbackForce = 2f;
    public Vector3 shotgunVisualScale = new Vector3(1f, 1f, 1f);
    public Color shotgunProjectileColor = new Color(1f, 0.75f, 0.2f, 1f);
    public GameObject shotgunBulletPrefab;
    private float nextShotgunTime;
    public GameObject knockbackWeaponVisual;
    public Sprite knockbackWeaponSprite;
    public float knockbackOrbitRadius = 1.65f;
    public float knockbackRotationSpeed = 220f;
    public float knockbackHitRadius = 0.55f;
    public float knockbackHitCooldown = 0.35f;
    public int knockbackMinDamage = 15;
    public int knockbackMaxDamage = 22;
    public float knockbackPushDistance = 0.2f;
    public Vector3 knockbackVisualScale = new Vector3(2.1f, 2.1f, 1f);

    private float knockbackAngle;
    private readonly Dictionary<Collider2D, float> knockbackLastHitTime = new Dictionary<Collider2D, float>();

    private Vector3 centerPos;
    private GameObject hitEffectPref;
    private GameObject damageCanvasPref;
    private Sprite rifleProjectileSprite;
    private Sprite gatlingProjectileSprite;
    private Sprite cannonProjectileSprite;
    private bool damageMultiplierApplied;

    private void Start()
    {
        AutoAssignWeaponSprites();
        ApplyGlobalDamageMultiplierOnce();
        CacheProjectileSprites();
        Slash slash = GetComponentInChildren<Slash>(true);
        if (slash != null)
        {
            hitEffectPref = slash.hitEffect;
            damageCanvasPref = slash.damageCanvas;
        }

        BootstrapWeaponRuntime();

        Slash[] legacyMelee = GetComponentsInChildren<Slash>(true);
        foreach (Slash melee in legacyMelee)
        {
            melee.gameObject.SetActive(false);
        }

        EnsureKnockbackVisualReady();
        ApplyWeaponVisualScales();
        ApplyWeaponSprites();
        EnsureSaberVisualReady();
        EnsureWeaponVisualSorting();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || GamePause.IsShopOpen)
            return;
        if (UIManager.instance != null && (UIManager.instance.IsGameOver || UIManager.instance.IsVictory))
            return;
        if (Camera.main == null)
            return;

        HandleTestModeInput();

        centerPos = transform.parent != null ? transform.parent.position : transform.position;
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        bool saberEnabled = IsWeaponEnabled(WeaponTestMode.Saber);
        bool rifleEnabled = IsWeaponEnabled(WeaponTestMode.Rifle);
        bool cannonEnabled = IsWeaponEnabled(WeaponTestMode.Cannon);
        bool gatlingEnabled = IsWeaponEnabled(WeaponTestMode.Gatling);
        bool knockbackEnabled = IsWeaponEnabled(WeaponTestMode.Knockback);
        bool shotgunEnabled = IsWeaponEnabled(WeaponTestMode.Shotgun);

        if (saberEnabled)
        {
            if (saberVisual != null) saberVisual.SetActive(true);
            EnsureSaberVisualReady();
            UpdateSaberAttack();
        }
        else
        {
            if (saberVisual != null) saberVisual.SetActive(false);
            isSaberThrusting = false;
            saberThrustTimer = 0f;
            saberHitEnemies.Clear();
        }

        bool isCannonReady = Time.time >= nextCannonTime;
        if (cannonVisual != null) cannonVisual.SetActive(cannonEnabled && isCannonReady);

        bool cannonFirePressed = Input.GetMouseButtonDown(1) || (testMode == WeaponTestMode.Cannon && Input.GetMouseButtonDown(0));
        if (cannonEnabled && isCannonReady && cannonFirePressed)
        {
            FireCannon(mouseWorldPos);
            nextCannonTime = Time.time + cannonCooldown;
            if (cannonVisual != null) cannonVisual.SetActive(false);
        }

        if (rifleVisual != null)
        {
            rifleVisual.SetActive(rifleEnabled);
            if (rifleEnabled)
            {
                Vector2 facingDir = GetFixedHorizontalDirection();
                Vector2 viewDir = mouseWorldPos - centerPos;
                if (viewDir.sqrMagnitude < 0.0001f)
                    viewDir = facingDir;

                Vector2 aimDir = viewDir.normalized;
                bool canShootForward = Vector2.Dot(aimDir, facingDir) > 0f;
                if (!canShootForward)
                    aimDir = viewDir.y >= 0f ? Vector2.up : Vector2.down;

                float lookAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

                rifleVisual.transform.position = centerPos + (Vector3)(aimDir * 0.2f);
                rifleVisual.transform.rotation = Quaternion.Euler(0, 0, lookAngle);
                SpriteRenderer rifleSr = rifleVisual.GetComponent<SpriteRenderer>();
                if (rifleSr != null)
                {
                    rifleSr.flipY = aimDir.x < 0f;
                }

                if (canShootForward && Input.GetMouseButtonDown(0) && Time.time >= nextRifleTime)
                {
                    if (rifleBulletPrefab != null)
                    {
                        GameObject proj = FireProjectile(rifleBulletPrefab, lookAngle, rifleVisual.transform.position, rifleBulletSpeed);
                        if (proj != null)
                        {
                            Component pp = GetComponentByTypeName(proj, "PlayerProjectile");
                            SetFieldValue(pp, "lifeTime", rifleAttackRange / rifleBulletSpeed);
                            SetFieldValue(pp, "minAttack", rifleMinDamage);
                            SetFieldValue(pp, "maxAttack", rifleMaxDamage);
                            nextRifleTime = Time.time + 1f / rifleAttackRate;
                        }
                    }
                }
            }
        }

        if (shotgunVisual != null)
        {
            shotgunVisual.SetActive(shotgunEnabled);
            if (shotgunEnabled)
            {
                UpdateShotgunAttack();
            }
        }

        if (knockbackEnabled)
        {
            if (knockbackWeaponVisual != null) knockbackWeaponVisual.SetActive(true);
            UpdateKnockbackWeapon();
        }
        else if (knockbackWeaponVisual != null)
        {
            knockbackWeaponVisual.SetActive(false);
        }

        if (gatlingVisual != null)
        {
            gatlingVisual.SetActive(gatlingEnabled);
            if (gatlingEnabled)
            {
                float theta = Time.time * gatlingOrbitAngularSpeed;
                Vector2 gunDir = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
                gatlingVisual.transform.position = centerPos + (Vector3)(gunDir * gatlingOrbitRadius);

                // Keep gatling barrel facing outward from the player.
                float visualAngle = Mathf.Atan2(gunDir.y, gunDir.x) * Mathf.Rad2Deg + 180f;
                gatlingVisual.transform.rotation = Quaternion.Euler(0, 0, visualAngle);

                if (Time.time >= nextGatlingTime && gatlingBulletPrefab != null)
                {
                    float bulletAngle = visualAngle + 180f;
                    GameObject proj = FireProjectile(gatlingBulletPrefab, bulletAngle, gatlingVisual.transform.position, gatlingBulletSpeed);
                    if (proj != null)
                    {
                        Component pp = GetComponentByTypeName(proj, "PlayerProjectile");
                        SetFieldValue(pp, "lifeTime", gatlingAttackRange / gatlingBulletSpeed);
                        SetFieldValue(pp, "minAttack", gatlingMinDamage);
                        SetFieldValue(pp, "maxAttack", gatlingMaxDamage);
                        nextGatlingTime = Time.time + 1f / gatlingAttackRate;
                    }
                }
            }
        }
    }

    private void BootstrapWeaponRuntime()
    {
        if (saberVisual == null)
        {
            saberVisual = CreateWeaponVisual("SaberVisual", saberWeaponSprite, new Color(0.55f, 0.95f, 1f, 1f), saberVisualScale, saberSortingOrder);
        }

        if (rifleVisual == null)
        {
            rifleVisual = CreateWeaponVisual("RifleVisual", rifleWeaponSprite, new Color(0.2f, 0.85f, 0.3f, 1f), rifleVisualScale, 190);
        }

        if (cannonVisual == null)
        {
            cannonVisual = CreateWeaponVisual("CannonVisual", cannonWeaponSprite, new Color(0.1f, 0.1f, 0.1f, 1f), cannonVisualScale, 188);
        }

        if (gatlingVisual == null)
        {
            gatlingVisual = CreateWeaponVisual("GatlingVisual", gatlingWeaponSprite, new Color(1f, 0.78f, 0.2f, 1f), gatlingVisualScale, 192);
        }

        if (shotgunVisual == null)
        {
            shotgunVisual = CreateWeaponVisual("ShotgunVisual", shotgunWeaponSprite, new Color(1f, 0.75f, 0.2f, 1f), shotgunVisualScale, 191);
        }

        bool riflePrefabReady = EnsureProjectilePrefabReady(rifleBulletPrefab, "PlayerProjectile");

        if (!riflePrefabReady)
        {
            rifleBulletPrefab = CreateProjectileTemplate(
                "RuntimeRifleBullet",
                new Color(1f, 0.62f, 0.2f, 1f),
                0.14f,
                false,
                rifleProjectileSprite);
        }

        bool gatlingPrefabReady = EnsureProjectilePrefabReady(gatlingBulletPrefab, "PlayerProjectile");

        if (!gatlingPrefabReady)
        {
            gatlingBulletPrefab = CreateProjectileTemplate(
                "RuntimeGatlingBullet",
                new Color(1f, 0.85f, 0.35f, 1f),
                0.1f,
                false,
                gatlingProjectileSprite);
        }

        bool cannonballPrefabReady = EnsureProjectilePrefabReady(cannonballPrefab, "Cannonball");

        if (!cannonballPrefabReady)
        {
            cannonballPrefab = CreateCannonballTemplate(
                "RuntimeCannonball",
                new Color(0.08f, 0.08f, 0.08f, 1f),
                0.28f,
                cannonProjectileSprite);
        }
    }

    private GameObject CreateWeaponVisual(string name, Sprite sprite, Color fallbackColor, Vector3 scale, int sortingOrder)
    {
        GameObject weapon = new GameObject(name);
        weapon.transform.SetParent(transform, false);
        weapon.transform.localScale = scale;
        SpriteRenderer sr = weapon.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (sr.sprite == null)
        {
            sr.sprite = GetFallbackSprite();
            sr.color = fallbackColor;
        }
        ApplyForegroundSorting(sr, sortingOrder);
        return weapon;
    }

    private GameObject CreateProjectileTemplate(string name, Color color, float colliderRadius, bool piercing, Sprite projectileSprite)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = projectileSprite != null ? projectileSprite : GetFallbackSprite();
        sr.color = projectileSprite != null ? Color.white : color;
        sr.sortingOrder = 250;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = colliderRadius;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        Component projectile = AddComponentByTypeName(go, "PlayerProjectile");
        SetFieldValue(projectile, "isPiercing", piercing);
        SetFieldValue(projectile, "hitEffect", hitEffectPref);
        SetFieldValue(projectile, "damageCanvas", damageCanvasPref);
        return go;
    }

    private GameObject CreateCannonballTemplate(string name, Color color, float colliderRadius, Sprite projectileSprite)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = projectileSprite != null ? projectileSprite : GetFallbackSprite();
        sr.color = projectileSprite != null ? Color.white : color;
        sr.sortingOrder = 230;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = colliderRadius;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        Component cb = AddComponentByTypeName(go, "Cannonball");
        SetFieldValue(cb, "boomEffect", hitEffectPref);
        SetFieldValue(cb, "damageCanvas", damageCanvasPref);
        return go;
    }

    private bool EnsureProjectilePrefabReady(GameObject prefab, string scriptTypeName)
    {
        if (prefab == null)
            return false;

        return HasComponentByTypeName(prefab, scriptTypeName);
    }

    private static void EnsureProjectileInstanceAppearance(
        GameObject instance,
        Sprite projectileSprite,
        Color fallbackColor,
        float colliderRadius,
        int sortingOrder,
        float defaultScale,
        bool forcePreferredSprite)
    {
        if (instance == null)
            return;

        SpriteRenderer sr = instance.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = instance.AddComponent<SpriteRenderer>();

        bool hasUsableSprite = sr.sprite != null && !string.Equals(sr.sprite.name, "Square", System.StringComparison.OrdinalIgnoreCase);
        if (projectileSprite != null && (forcePreferredSprite || !hasUsableSprite))
        {
            sr.sprite = projectileSprite;
            sr.color = Color.white;
        }
        else if (sr.sprite == null)
        {
            sr.sprite = GetFallbackSprite();
            sr.color = fallbackColor;
        }
        sr.sortingOrder = Mathf.Max(sr.sortingOrder, sortingOrder);

        CircleCollider2D col = instance.GetComponent<CircleCollider2D>();
        if (col == null)
            col = instance.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        if (col.radius <= 0f)
            col.radius = colliderRadius;

        Rigidbody2D rb = instance.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = instance.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (instance.transform.localScale == Vector3.zero)
        {
            instance.transform.localScale = Vector3.one * defaultScale;
        }
    }

    private static Sprite GetFallbackSprite()
    {
        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color fill = Color.white;
        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                tex.SetPixel(x, y, fill);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16f);
    }

    private void EnsureKnockbackVisualReady()
    {
        if (knockbackWeaponVisual != null)
            return;

        GameObject generated = new GameObject("KnockbackWeaponVisual");
        generated.transform.SetParent(transform, false);
        SpriteRenderer sr = generated.AddComponent<SpriteRenderer>();
        if (saberVisual != null)
        {
            SpriteRenderer saberSr = saberVisual.GetComponent<SpriteRenderer>();
            if (saberSr != null)
            {
                sr.sprite = knockbackWeaponSprite != null ? knockbackWeaponSprite : saberSr.sprite;
                sr.sortingOrder = saberSr.sortingOrder;
                generated.transform.localScale = saberVisual.transform.localScale * 0.7f;
            }
        }
        if (sr.sprite == null)
        {
            sr.sprite = GetFallbackSprite();
            sr.color = new Color(0.8f, 0.8f, 1f, 1f);
            generated.transform.localScale = knockbackVisualScale;
        }
        else
        {
            generated.transform.localScale = knockbackVisualScale;
        }
        ApplyForegroundSorting(sr, saberSortingOrder);
        knockbackWeaponVisual = generated;
    }

    private void ApplyWeaponVisualScales()
    {
        if (saberVisual != null)
            saberVisual.transform.localScale = saberVisualScale;
        if (rifleVisual != null)
            rifleVisual.transform.localScale = rifleVisualScale;
        if (cannonVisual != null)
            cannonVisual.transform.localScale = cannonVisualScale;
        if (gatlingVisual != null)
            gatlingVisual.transform.localScale = gatlingVisualScale;
        if (shotgunVisual != null)
            shotgunVisual.transform.localScale = shotgunVisualScale;
        if (knockbackWeaponVisual != null)
            knockbackWeaponVisual.transform.localScale = knockbackVisualScale;
    }

    private void EnsureWeaponVisualSorting()
    {
        if (saberVisual != null)
        {
            SpriteRenderer sr = saberVisual.GetComponent<SpriteRenderer>();
            if (sr != null) ApplyForegroundSorting(sr, saberSortingOrder);
        }
        if (rifleVisual != null)
        {
            SpriteRenderer sr = rifleVisual.GetComponent<SpriteRenderer>();
            if (sr != null) ApplyForegroundSorting(sr, 190);
        }
        if (cannonVisual != null)
        {
            SpriteRenderer sr = cannonVisual.GetComponent<SpriteRenderer>();
            if (sr != null) ApplyForegroundSorting(sr, 188);
        }
        if (gatlingVisual != null)
        {
            SpriteRenderer sr = gatlingVisual.GetComponent<SpriteRenderer>();
            if (sr != null) ApplyForegroundSorting(sr, 192);
        }
        if (shotgunVisual != null)
        {
            SpriteRenderer sr = shotgunVisual.GetComponent<SpriteRenderer>();
            if (sr != null) ApplyForegroundSorting(sr, 191);
        }
        if (knockbackWeaponVisual != null)
        {
            SpriteRenderer sr = knockbackWeaponVisual.GetComponent<SpriteRenderer>();
            if (sr != null) ApplyForegroundSorting(sr, saberSortingOrder);
        }
    }

    private void ApplyForegroundSorting(SpriteRenderer renderer, int fallbackOrder)
    {
        if (renderer == null) return;

        Transform owner = transform.parent != null ? transform.parent : transform;
        SpriteRenderer ownerRenderer = owner.GetComponentInChildren<SpriteRenderer>();

        if (ownerRenderer != null)
        {
            renderer.sortingLayerID = ownerRenderer.sortingLayerID;
            renderer.sortingOrder = Mathf.Max(fallbackOrder, ownerRenderer.sortingOrder + 2);
        }
        else
        {
            renderer.sortingOrder = fallbackOrder;
        }
    }

    private static System.Type FindTypeByName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            System.Reflection.Assembly assembly = assemblies[i];
            System.Type directType = assembly.GetType(typeName);
            if (directType != null)
                return directType;

            System.Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch
            {
                continue;
            }

            for (int j = 0; j < types.Length; j++)
            {
                if (string.Equals(types[j].Name, typeName, System.StringComparison.Ordinal))
                    return types[j];
            }
        }

        return null;
    }

    private static Component GetComponentByTypeName(GameObject target, string typeName)
    {
        if (target == null) return null;
        System.Type type = FindTypeByName(typeName);
        if (type == null) return null;
        return target.GetComponent(type);
    }

    private static Component AddComponentByTypeName(GameObject target, string typeName)
    {
        if (target == null) return null;
        System.Type type = FindTypeByName(typeName);
        if (type == null || !typeof(Component).IsAssignableFrom(type)) return null;
        return target.AddComponent(type);
    }

    private static bool HasComponentByTypeName(GameObject target, string typeName)
    {
        return GetComponentByTypeName(target, typeName) != null;
    }

    private static void SetFieldValue(Component target, string fieldName, object value)
    {
        if (target == null || string.IsNullOrEmpty(fieldName)) return;
        System.Reflection.FieldInfo field = target.GetType().GetField(fieldName);
        if (field == null) return;

        if (value == null)
        {
            field.SetValue(target, null);
            return;
        }

        System.Type fieldType = field.FieldType;
        if (fieldType.IsInstanceOfType(value))
        {
            field.SetValue(target, value);
            return;
        }

        try
        {
            object converted = System.Convert.ChangeType(value, fieldType);
            field.SetValue(target, converted);
        }
        catch
        {
            // Keep running even if a field cannot be set.
        }
    }

    private static Sprite ResolvePreferredSprite(Sprite current, params string[] preferredNames)
    {
        Sprite preferred = TryLoadWeaponSprite(preferredNames);
        return preferred != null ? preferred : current;
    }

    private void CacheProjectileSprites()
    {
        rifleProjectileSprite = TryLoadWeaponSprite("Wizard_Bullet_0", "Wizard_Bullet", "Square");
        gatlingProjectileSprite = TryLoadWeaponSprite("Wizard_Bullet_1", "Wizard_Bullet_0", "Wizard_Bullet", "Square");
        cannonProjectileSprite = TryLoadWeaponSprite("PlayerBomb", "Bomb", "Square");
    }

    private void AutoAssignWeaponSprites()
    {
        saberWeaponSprite = ResolvePreferredSprite(saberWeaponSprite, "Lightsaber", "VS_Lightsaber", "Sword");
        rifleWeaponSprite = ResolvePreferredSprite(rifleWeaponSprite, "MachineGun");
        gatlingWeaponSprite = ResolvePreferredSprite(gatlingWeaponSprite, "GatlingGun_Clear");
        shotgunWeaponSprite = ResolvePreferredSprite(shotgunWeaponSprite, "penzi");
        cannonWeaponSprite = ResolvePreferredSprite(cannonWeaponSprite, "Bazooka");
        knockbackWeaponSprite = ResolvePreferredSprite(knockbackWeaponSprite, "VS_ShurikenAlt", "ShurikenAlt", "Shuriken");
    }

    private static Sprite TryLoadWeaponSprite(params string[] names)
    {
        if (names == null || names.Length == 0)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrEmpty(name))
                continue;

            Sprite fromResources = Resources.Load<Sprite>(name);
            if (fromResources != null)
                return fromResources;

            fromResources = Resources.Load<Sprite>("Weapons/" + name);
            if (fromResources != null)
                return fromResources;
        }

        Sprite[] loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            for (int j = 0; j < loadedSprites.Length; j++)
            {
                Sprite sprite = loadedSprites[j];
                if (sprite != null && string.Equals(sprite.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return sprite;
            }
        }

#if UNITY_EDITOR
        for (int i = 0; i < names.Length; i++)
        {
            string[] candidateFolders = { "Assets/Sprites/Weapons/", "Assets/Sprites/Enemy/", "Assets/Sprites/" };
            for (int f = 0; f < candidateFolders.Length; f++)
            {
                string requestedName = names[i];
                string directPath = candidateFolders[f] + requestedName + ".png";
                Sprite directSprite = AssetDatabase.LoadAssetAtPath<Sprite>(directPath);
                if (directSprite != null)
                    return directSprite;

                string baseName = requestedName;
                int underscore = requestedName.LastIndexOf('_');
                int suffixNumber;
                if (underscore > 0 && int.TryParse(requestedName.Substring(underscore + 1), out suffixNumber))
                {
                    baseName = requestedName.Substring(0, underscore);
                }

                string sheetPath = candidateFolders[f] + baseName + ".png";
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
                if (subAssets == null || subAssets.Length == 0)
                    continue;

                Sprite firstSprite = null;
                for (int a = 0; a < subAssets.Length; a++)
                {
                    Sprite subSprite = subAssets[a] as Sprite;
                    if (subSprite == null)
                        continue;

                    if (firstSprite == null)
                        firstSprite = subSprite;

                    if (string.Equals(subSprite.name, requestedName, System.StringComparison.OrdinalIgnoreCase))
                        return subSprite;
                }

                if (string.Equals(requestedName, baseName, System.StringComparison.OrdinalIgnoreCase) && firstSprite != null)
                    return firstSprite;
            }
        }
#endif

        return null;
    }

    private void HandleTestModeInput()
    {
        if (Input.GetKeyDown(allWeaponsKey)) SetTestMode(WeaponTestMode.All);
        if (Input.GetKeyDown(rifleModeKey)) SetTestMode(WeaponTestMode.Rifle);
        if (Input.GetKeyDown(saberModeKey)) SetTestMode(WeaponTestMode.Saber);
        if (Input.GetKeyDown(gatlingModeKey)) SetTestMode(WeaponTestMode.Gatling);
        if (Input.GetKeyDown(knockbackModeKey)) SetTestMode(WeaponTestMode.Knockback);
        if (Input.GetKeyDown(cannonModeKey)) SetTestMode(WeaponTestMode.Cannon);
        if (Input.GetKeyDown(shotgunModeKey)) SetTestMode(WeaponTestMode.Shotgun);
    }

    private void SetTestMode(WeaponTestMode mode)
    {
        if (testMode == mode) return;
        testMode = mode;
        Debug.Log("Weapon Test Mode: " + mode);
    }

    private bool IsWeaponEnabled(WeaponTestMode mode)
    {
        return testMode == WeaponTestMode.All || testMode == mode;
    }

    private void UpdateSaberAttack()
    {
        if (saberVisual == null) return;

        saberAttackDirection = GetFixedHorizontalDirection();

        float angle = Mathf.Atan2(saberAttackDirection.y, saberAttackDirection.x) * Mathf.Rad2Deg;
        float baseRadius = 0.8f;
        float currentRadius = baseRadius;

        if (isSaberThrusting)
        {
            saberThrustTimer += Time.deltaTime;
            float fraction = Mathf.Clamp01(saberThrustTimer / saberThrustDuration);
            currentRadius = baseRadius + Mathf.Sin(fraction * Mathf.PI) * saberThrustDistance;

            DamageEnemiesInSaberPath();

            if (saberThrustTimer >= saberThrustDuration)
            {
                isSaberThrusting = false;
                saberThrustTimer = 0f;
                saberHitEnemies.Clear();
            }
        }
        else if (Time.time >= nextSaberTime)
        {
            isSaberThrusting = true;
            saberThrustTimer = 0f;
            saberHitEnemies.Clear();
            nextSaberTime = Time.time + 1f / saberAttackRate;
        }

        UpdateWeaponTransform(saberVisual, angle, currentRadius);
    }

    private Vector2 GetFixedHorizontalDirection()
    {
        Transform owner = transform.parent != null ? transform.parent : transform;
        float yRotation = owner.eulerAngles.y;
        bool facingLeft = yRotation > 90f && yRotation < 270f;
        return facingLeft ? Vector2.left : Vector2.right;
    }

    private void ApplyWeaponSprites()
    {
        if (saberVisual != null)
        {
            SpriteRenderer sr = saberVisual.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = saberVisual.AddComponent<SpriteRenderer>();
            }
            if (saberWeaponSprite != null)
            {
                sr.sprite = saberWeaponSprite;
                sr.color = Color.white;
            }
        }

        if (rifleVisual != null)
        {
            SpriteRenderer sr = rifleVisual.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = rifleVisual.AddComponent<SpriteRenderer>();
            }
            if (rifleWeaponSprite != null)
            {
                sr.sprite = rifleWeaponSprite;
                sr.color = Color.white;
            }
        }

        if (cannonVisual != null)
        {
            SpriteRenderer sr = cannonVisual.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = cannonVisual.AddComponent<SpriteRenderer>();
            }
            if (cannonWeaponSprite != null)
            {
                sr.sprite = cannonWeaponSprite;
                sr.color = Color.white;
            }
        }

        if (gatlingVisual != null)
        {
            SpriteRenderer sr = gatlingVisual.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = gatlingVisual.AddComponent<SpriteRenderer>();
            }
            if (gatlingWeaponSprite != null)
            {
                sr.sprite = gatlingWeaponSprite;
                sr.color = Color.white;
            }
        }

        if (shotgunVisual != null)
        {
            SpriteRenderer sr = shotgunVisual.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = shotgunVisual.AddComponent<SpriteRenderer>();
            }
            if (shotgunWeaponSprite != null)
            {
                sr.sprite = shotgunWeaponSprite;
                sr.color = Color.white;
            }
        }

        if (knockbackWeaponVisual != null && knockbackWeaponSprite != null)
        {
            SpriteRenderer sr = knockbackWeaponVisual.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = knockbackWeaponSprite;
            }
        }
    }

    private void EnsureSaberVisualReady()
    {
        if (saberVisual == null) return;

        SpriteRenderer sr = saberVisual.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (saberWeaponSprite != null)
        {
            sr.sprite = saberWeaponSprite;
        }

        sr.enabled = true;
        sr.color = Color.white;
        if (sr.sortingOrder < saberSortingOrder)
        {
            sr.sortingOrder = saberSortingOrder;
        }
    }

    private void FireCannon(Vector3 targetLocation)
    {
        if (cannonballPrefab == null)
        {
            if (cannonProjectileSprite == null)
                CacheProjectileSprites();

            cannonballPrefab = CreateCannonballTemplate(
                "RuntimeCannonball",
                new Color(0.08f, 0.08f, 0.08f, 1f),
                0.28f,
                cannonProjectileSprite);

            if (cannonballPrefab == null)
                return;
        }

        Vector3 spawnPos = targetLocation + new Vector3(0, 20f, 0);
        GameObject cbObj = Instantiate(cannonballPrefab, spawnPos, Quaternion.identity);
        if (!cbObj.activeSelf)
            cbObj.SetActive(true);

        EnsureProjectileInstanceAppearance(
            cbObj,
            cannonProjectileSprite,
            new Color(0.08f, 0.08f, 0.08f, 1f),
            0.28f,
            230,
            0.28f,
            true);

        if (!HasComponentByTypeName(cbObj, "Cannonball"))
        {
            AddComponentByTypeName(cbObj, "Cannonball");
        }

        Component cb = GetComponentByTypeName(cbObj, "Cannonball");
        if (cb == null)
        {
            Destroy(cbObj);
            return;
        }

        SetFieldValue(cb, "targetPos", targetLocation);
        SetFieldValue(cb, "aoeRadius", cannonAoeRadius);
        SetFieldValue(cb, "minDamage", cannonMinDamage);
        SetFieldValue(cb, "maxDamage", cannonMaxDamage);
        SetFieldValue(cb, "boomEffect", hitEffectPref);
        SetFieldValue(cb, "damageCanvas", damageCanvasPref);
    }

    private void UpdateShotgunAttack()
    {
        if (shotgunVisual == null) return;

        Vector2 aimPoint;
        bool hasTarget = TryGetBestShotgunAimPoint(out aimPoint);
        Vector2 dir = hasTarget ? (aimPoint - (Vector2)centerPos).normalized : GetFixedHorizontalDirection();
        if (dir.sqrMagnitude < 0.001f)
        {
            dir = GetFixedHorizontalDirection();
        }

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        shotgunVisual.transform.position = centerPos + (Vector3)(dir * 0.32f);
        shotgunVisual.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer sr = shotgunVisual.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.flipY = dir.x < 0f;
        }

        if (!hasTarget || Time.time < nextShotgunTime) return;

        float targetDistance = Vector2.Distance(centerPos, aimPoint);
        FireShotgunBurst(angle, shotgunVisual.transform.position, targetDistance);
        nextShotgunTime = Time.time + shotgunCooldown;
    }

    private bool TryGetBestShotgunAimPoint(out Vector2 aimPoint)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Vector2 facingDir = GetFixedHorizontalDirection();
        int bestClusterCount = -1;
        float bestDistanceToPlayer = float.MaxValue;
        Vector2 bestPoint = Vector2.zero;
        bool found = false;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject candidateEnemy = enemies[i];
            if (candidateEnemy == null || !candidateEnemy.activeInHierarchy) continue;

            Vector2 candidatePoint = candidateEnemy.transform.position;
            Vector2 candidateDir = (candidatePoint - (Vector2)centerPos).normalized;
            if (candidateDir.sqrMagnitude < 0.0001f || Vector2.Dot(candidateDir, facingDir) <= 0f)
                continue;

            float distanceToPlayer = Vector2.Distance(centerPos, candidatePoint);
            if (distanceToPlayer > shotgunRange) continue;

            int clusterCount = 0;
            for (int j = 0; j < enemies.Length; j++)
            {
                GameObject nearbyEnemy = enemies[j];
                if (nearbyEnemy == null || !nearbyEnemy.activeInHierarchy) continue;

                if (Vector2.Distance(candidatePoint, nearbyEnemy.transform.position) <= shotgunClusterRadius)
                {
                    clusterCount++;
                }
            }

            if (!found || clusterCount > bestClusterCount || (clusterCount == bestClusterCount && distanceToPlayer < bestDistanceToPlayer))
            {
                found = true;
                bestClusterCount = clusterCount;
                bestDistanceToPlayer = distanceToPlayer;
                bestPoint = candidatePoint;
            }
        }

        aimPoint = bestPoint;
        return found;
    }

    private void FireShotgunBurst(float centerAngle, Vector2 spawnPos, float targetDistance)
    {
        GameObject pelletPrefab = shotgunBulletPrefab != null ? shotgunBulletPrefab : rifleBulletPrefab;
        if (pelletPrefab == null) return;

        int pelletCount = Mathf.Max(1, shotgunPelletCount);
        float spreadHalf = shotgunSpreadAngle * 0.5f;
        float normalizedDistance = shotgunRange > 0.01f ? Mathf.Clamp01(targetDistance / shotgunRange) : 1f;
        float distanceMultiplier = Mathf.Lerp(1.5f, 0.5f, normalizedDistance);
        int scaledMinDamage = Mathf.Max(1, Mathf.RoundToInt(shotgunMinDamage * distanceMultiplier));
        int scaledMaxDamage = Mathf.Max(scaledMinDamage + 1, Mathf.RoundToInt(shotgunMaxDamage * distanceMultiplier));

        for (int i = 0; i < pelletCount; i++)
        {
            float t = pelletCount == 1 ? 0.5f : (float)i / (pelletCount - 1);
            float pelletAngle = centerAngle + Mathf.Lerp(-spreadHalf, spreadHalf, t);

            GameObject proj = FireProjectile(pelletPrefab, pelletAngle, spawnPos, shotgunBulletSpeed);
            if (proj == null) continue;

            Component pp = GetComponentByTypeName(proj, "PlayerProjectile");
            SetFieldValue(pp, "lifeTime", shotgunRange / shotgunBulletSpeed);
            SetFieldValue(pp, "minAttack", scaledMinDamage);
            SetFieldValue(pp, "maxAttack", scaledMaxDamage);
            SetFieldValue(pp, "isPiercing", false);
            SetFieldValue(pp, "knockbackForce", shotgunKnockbackForce);
            SetFieldValue(pp, "projectileTint", shotgunProjectileColor);
        }
    }

    private void ApplyGlobalDamageMultiplierOnce()
    {
        if (damageMultiplierApplied)
            return;

        saberMinAttack = ScaleDamageStat(saberMinAttack);
        saberMaxAttack = ScaleDamageStat(saberMaxAttack);
        rifleMinDamage = ScaleDamageStat(rifleMinDamage);
        rifleMaxDamage = ScaleDamageStat(rifleMaxDamage);
        cannonMinDamage = ScaleDamageStat(cannonMinDamage);
        cannonMaxDamage = ScaleDamageStat(cannonMaxDamage);
        gatlingMinDamage = ScaleDamageStat(gatlingMinDamage);
        gatlingMaxDamage = ScaleDamageStat(gatlingMaxDamage);
        shotgunMinDamage = ScaleDamageStat(shotgunMinDamage);
        shotgunMaxDamage = ScaleDamageStat(shotgunMaxDamage);
        knockbackMinDamage = ScaleDamageStat(knockbackMinDamage);
        knockbackMaxDamage = ScaleDamageStat(knockbackMaxDamage);

        damageMultiplierApplied = true;
    }

    private int ScaleDamageStat(int value)
    {
        return Mathf.Max(1, Mathf.RoundToInt(value * globalDamageMultiplier));
    }

    private void DamageEnemiesInSaberPath()
    {
        if (saberVisual == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(saberVisual.transform.position, saberHitRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Enemy") && !saberHitEnemies.Contains(hit))
            {
                saberHitEnemies.Add(hit);
                ITakenDamage enemy = hit.GetComponent<ITakenDamage>();
                if (enemy != null && !enemy.isAttack)
                {
                    int dmg = Random.Range(saberMinAttack, saberMaxAttack);
                    enemy.TakenDamage(dmg);

                    if (hitEffectPref != null) Instantiate(hitEffectPref, hit.transform.position, Quaternion.identity);
                    if (damageCanvasPref != null)
                    {
                        DamageNum damagable = Instantiate(damageCanvasPref, hit.transform.position, Quaternion.identity).GetComponent<DamageNum>();
                        damagable.ShowDamage(dmg);
                    }
                }
            }
        }
    }

    private void UpdateKnockbackWeapon()
    {
        if (knockbackWeaponVisual == null) return;

        knockbackAngle += knockbackRotationSpeed * Time.deltaTime;
        if (knockbackAngle > 360f) knockbackAngle -= 360f;

        float rad = knockbackAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        knockbackWeaponVisual.transform.position = centerPos + (Vector3)(dir * knockbackOrbitRadius);
        knockbackWeaponVisual.transform.rotation = Quaternion.Euler(0f, 0f, knockbackAngle + 90f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(knockbackWeaponVisual.transform.position, knockbackHitRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (!hit.CompareTag("Enemy")) continue;
            if (IsBossCollider(hit)) continue;

            float lastHitTime;
            if (knockbackLastHitTime.TryGetValue(hit, out lastHitTime) && Time.time - lastHitTime < knockbackHitCooldown)
            {
                continue;
            }

            ITakenDamage enemy = hit.GetComponent<ITakenDamage>();
            if (enemy != null && !enemy.isAttack)
            {
                int dmg = Random.Range(knockbackMinDamage, knockbackMaxDamage);
                enemy.TakenDamage(dmg);

                Vector2 pushDir = ((Vector2)hit.transform.position - (Vector2)centerPos).normalized;
                ApplyStrongEnemyKnockback(hit, pushDir, knockbackPushDistance);

                if (hitEffectPref != null)
                {
                    Instantiate(hitEffectPref, hit.transform.position, Quaternion.identity);
                }

                if (damageCanvasPref != null)
                {
                    DamageNum damagable = Instantiate(damageCanvasPref, hit.transform.position, Quaternion.identity).GetComponent<DamageNum>();
                    damagable.ShowDamage(dmg);
                }

                knockbackLastHitTime[hit] = Time.time;
            }
        }
    }

    private static bool IsBossCollider(Collider2D hit)
    {
        if (hit == null) return false;
        return hit.GetComponentInParent<Vampire.BossMonster>() != null;
    }

    private static void ApplyStrongEnemyKnockback(Collider2D hit, Vector2 pushDir, float pushDistance)
    {
        if (hit == null) return;
        if (pushDir.sqrMagnitude < 0.0001f) pushDir = Vector2.right;
        pushDir.Normalize();

        if (IsBossCollider(hit))
            return;

        Vampire.Monster vampireMonster = hit.GetComponentInParent<Vampire.Monster>();
        if (vampireMonster != null)
        {
            vampireMonster.Knockback(pushDir * (pushDistance * 12f));
            vampireMonster.transform.position += (Vector3)(pushDir * pushDistance * 0.25f);
            return;
        }

        Rigidbody2D enemyRb = hit.attachedRigidbody;
        if (enemyRb != null)
        {
            enemyRb.velocity = Vector2.zero;
            enemyRb.AddForce(pushDir * (pushDistance * 18f), ForceMode2D.Impulse);
            enemyRb.transform.position += (Vector3)(pushDir * pushDistance * 0.18f);
            return;
        }

        hit.transform.position = (Vector2)hit.transform.position + pushDir * pushDistance;
    }

    private void UpdateWeaponTransform(GameObject weapon, float angle, float radius)
    {
        if (weapon == null) return;

        float rad = angle * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        weapon.transform.position = centerPos + (Vector3)offset;

        weapon.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private GameObject FireProjectile(GameObject prefab, float angle, Vector2 initPosition, float flySpeed = 10f)
    {
        if (prefab == null)
            return null;

        GameObject proj = Instantiate(prefab, initPosition, Quaternion.Euler(0, 0, angle));
        if (!proj.activeSelf)
            proj.SetActive(true);

        Sprite projectileSprite = rifleProjectileSprite;
        Color fallbackColor = new Color(1f, 0.62f, 0.2f, 1f);
        float colliderRadius = 0.14f;
        float defaultScale = 0.28f;
        if (prefab == gatlingBulletPrefab)
        {
            projectileSprite = gatlingProjectileSprite;
            fallbackColor = new Color(1f, 0.85f, 0.35f, 1f);
            colliderRadius = 0.1f;
            defaultScale = 0.24f;
        }
        else if (prefab == shotgunBulletPrefab)
        {
            fallbackColor = shotgunProjectileColor;
            colliderRadius = 0.13f;
            defaultScale = 0.27f;
        }

        EnsureProjectileInstanceAppearance(
            proj,
            projectileSprite,
            fallbackColor,
            colliderRadius,
            250,
            defaultScale,
            true);

        if (!HasComponentByTypeName(proj, "PlayerProjectile"))
        {
            AddComponentByTypeName(proj, "PlayerProjectile");
        }

        Component pp = GetComponentByTypeName(proj, "PlayerProjectile");
        if (pp == null)
        {
            Destroy(proj);
            return null;
        }

        SetFieldValue(pp, "speed", flySpeed);
        if (hitEffectPref != null) SetFieldValue(pp, "hitEffect", hitEffectPref);
        if (damageCanvasPref != null) SetFieldValue(pp, "damageCanvas", damageCanvasPref);
        return proj;
    }

    public Transform GetClosestEnemy(float maxRange)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform closest = null;
        float minDistance = maxRange;

        foreach (GameObject enemy in enemies)
        {
            if (enemy.activeInHierarchy)
            {
                float dist = Vector2.Distance(centerPos, enemy.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = enemy.transform;
                }
            }
        }
        return closest;
    }
}
