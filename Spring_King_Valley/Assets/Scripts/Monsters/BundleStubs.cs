using UnityEngine;
using System.Collections.Generic;

namespace Vampire
{
    public interface ISpatialHashGridClient {}

    public class Character : IDamageable
    {
        public Vector2 Velocity { get; set; }
        public UnityEngine.Events.UnityEvent OnDeath = new UnityEngine.Events.UnityEvent();
        public UnityEngine.Events.UnityEvent<float> OnDealDamage = new UnityEngine.Events.UnityEvent<float>();
        public Collider2D CollectableCollider { get; set; }
        public Transform CenterTransform { get { return transform; } }

        public void Init(EntityManager entityManager, AbilityManager abilityManager, StatsManager statsManager) {}
        public override void TakeDamage(float damage, Vector2 knockback = default(Vector2)) {}
        public override void Knockback(Vector2 knockback) {}
        public void GainExp(float amount) {}
        public void GainHealth(float amount) {}
    }

    public class SpatialHashGrid
    {
        private Vector2[] bounds;
        private Vector2Int dimensions;

        public SpatialHashGrid(Vector2[] bounds, Vector2Int dimensions)
        {
            this.bounds = bounds;
            this.dimensions = dimensions;
        }

        public void Rebuild(Vector3 center) {}
        public void InsertClient(ISpatialHashGridClient client) {}
        public void RemoveClient(ISpatialHashGridClient client) {}
        public void UpdateClient(ISpatialHashGridClient client) {}
        public bool CloseToEdge(Character player) { return false; }
    }

    public class Ability : ScriptableObject
    {
        public Sprite Image { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Owned { get; set; }
        public int Level { get; set; }

        public void Select() {}
    }

    public class AbilityManager : MonoBehaviour
    {
        public int DamageUpgradeablesCount { get; set; }
        public int FireRateUpgradeablesCount { get; set; }
        public int WeaponCooldownUpgradeablesCount { get; set; }
        public int RecoveryCooldownUpgradeablesCount { get; set; }
        public int DurationUpgradeablesCount { get; set; }
        public int AOEUpgradeablesCount { get; set; }
        public int KnockbackUpgradeablesCount { get; set; }
        public int ProjectileSpeedUpgradeablesCount { get; set; }
        public int RecoveryChanceUpgradeablesCount { get; set; }
        public int BleedDamageUpgradeablesCount { get; set; }
        public int BleedRateUpgradeablesCount { get; set; }
        public int BleedDurationUpgradeablesCount { get; set; }
        public int MovementSpeedUpgradeablesCount { get; set; }
        public int RotationSpeedUpgradeablesCount { get; set; }
        public int ProjectileCountUpgradeablesCount { get; set; }
        public int RecoveryUpgradeablesCount { get; set; }
        public int ArmorUpgradeablesCount { get; set; }

        public void Init(LevelBlueprint levelBlueprint, EntityManager entityManager, Character playerCharacter, AbilityManager reference) {}
        
        public List<Ability> SelectAbilities() { return new List<Ability>(); }
        public void ReturnAbilities(List<Ability> displayedAbilities) {}
        public bool HasAvailableAbilities() { return false; }
    }
}
    namespace UnityEngine.Localization
    {
        public class Locale 
        {
            public LocaleIdentifier Identifier;
        }

        public class LocaleIdentifier {}

        [System.Serializable]
        public class LocalizedString
        {
            public string GetLocalizedString() { return "LOC_STUB"; }
            public object StringReference { get; set; }
        }
    }

    namespace UnityEngine.Localization.Settings
    {
        public class LocalizationSettings
        {
            public static object SelectedLocale { get; set; }
            public static event System.Action<UnityEngine.Localization.Locale> SelectedLocaleChanged { add {} remove {} }
            public static LocalesProvider AvailableLocales = new LocalesProvider();
            public static StringDatabaseProvider StringDatabase = new StringDatabaseProvider();
        }

        public class LocalesProvider
        {
            public System.Collections.Generic.List<UnityEngine.Localization.Locale> Locales = new System.Collections.Generic.List<UnityEngine.Localization.Locale>();
        }

        public class StringDatabaseProvider
        {
            public string GetLocalizedString(string table, string entry) { return "STUB"; }
        }
    }

    namespace UnityEngine.Localization.Tables
    {
        public class StringTableCollection
        {
            public void AddEntry(string key, string value) {}
            public object StringTable { get; set; }
            public string GenerateCharacterSet(object locale) { return ""; }
        }
    }

    namespace UnityEngine.ResourceManagement.AsyncOperations
    {
        public class AsyncOperationHandle<T>
        {
            public T Result { get; set; }
            public bool IsDone { get { return true; } }
            public event System.Action<AsyncOperationHandle<T>> Completed { add {} remove {} }
        }
    }
