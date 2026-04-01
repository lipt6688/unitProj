using UnityEngine;

/// <summary>
/// 局内金币与里程碑；达到阈值时由 CoinShopUI 弹出三选一增益。
/// </summary>
public class CoinWallet : MonoBehaviour
{
    public static CoinWallet Instance { get; private set; }

    [SerializeField] private int coinsPerMilestone = 25;
    [SerializeField] private int nextMilestoneAt = 25;

    private int _coins;
    private bool _shopOpen;
    private bool _locked;

    public int Coins => _coins;
    public int NextMilestoneAt => nextMilestoneAt;
    public bool ShopOpen => _shopOpen;
    public bool Locked => _locked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetShopOpen(bool open) => _shopOpen = open;

    public void SetLocked(bool locked)
    {
        _locked = locked;
    }

    /// <summary>购买成功后推进下一里程碑。</summary>
    public void AdvanceMilestone()
    {
        nextMilestoneAt += coinsPerMilestone;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;
        if (_locked)
            return;
        if (CoinShopUI.Instance != null)
            CoinShopUI.Instance.EnsureCoinUi();
        _coins += amount;
        CoinHud.Refresh(_coins);
        TryOfferShop();
    }

    public bool TrySpend(int amount)
    {
        if (_locked)
            return false;
        if (amount > _coins || amount < 0)
            return false;
        _coins -= amount;
        CoinHud.Refresh(_coins);
        return true;
    }

    private void TryOfferShop()
    {
        if (_shopOpen)
            return;
        if (UIManager.instance != null && (UIManager.instance.IsGameOver || UIManager.instance.IsVictory))
            return;
        if (_coins < nextMilestoneAt)
            return;
        if (CoinShopUI.Instance == null)
            return;
        CoinShopUI.Instance.OpenShop();
    }
}
