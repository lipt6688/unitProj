using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 金币达里程碑时弹出三档价位增益，必须选购一项后关闭（暂停游戏）。
/// </summary>
public class CoinShopUI : MonoBehaviour
{
    public static CoinShopUI Instance { get; private set; }

    private enum BuffId
    {
        Toughness,
        Swiftness,
        Brutality
    }

    private readonly (BuffId id, string title, string desc)[] _buffDefs =
    {
        (BuffId.Toughness, "坚固", "最大生命 +1，并回复 1"),
        (BuffId.Swiftness, "迅捷", "移动速度 +10%"),
        (BuffId.Brutality, "重击", "斩击伤害 +2 ~ +4")
    };

    private Canvas _rootCanvas;
    private GameObject _coinBar;
    private GameObject _panel;
    private readonly Text[] _offerTitles = new Text[3];
    private readonly Text[] _offerPrices = new Text[3];
    private readonly Button[] _offerButtons = new Button[3];

    private BuffId[] _roll = new BuffId[3];
    private int[] _prices = new int[3];

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

    private void Start()
    {
        EnsureCoinUi();
        StartCoroutine(BuildUiNextFrame());
    }

    private IEnumerator BuildUiNextFrame()
    {
        yield return null;
        EnsureCoinUi();
    }

    /// <summary>在第一次加金币前也可能调用；避免 Start 晚于 OnDestroy 导致 HUD 未绑定。</summary>
    public void EnsureCoinUi()
    {
        if (_coinBar != null && _panel != null)
            return;
        BuildUi();
    }

    private void BuildUi()
    {
        if (_coinBar != null && _panel != null)
            return;

        _rootCanvas = FindObjectOfType<Canvas>();
        if (_rootCanvas == null)
        {
            Debug.LogWarning("CoinShopUI: 未找到 Canvas，无法创建 UI。");
            return;
        }

        Font font = ResolveUiFont();

        Text[] existingCoinHudTexts = FindObjectsOfType<Text>(true);
        Text firstCoinHudText = null;
        for (int i = 0; i < existingCoinHudTexts.Length; i++)
        {
            Text text = existingCoinHudTexts[i];
            if (text == null || text.gameObject == null)
                continue;
            if (!string.Equals(text.gameObject.name, "CoinHudBar", System.StringComparison.Ordinal))
                continue;

            if (firstCoinHudText == null)
            {
                firstCoinHudText = text;
                continue;
            }

            Destroy(text.gameObject);
        }

        if (_coinBar == null)
        {
            if (firstCoinHudText != null)
            {
                _coinBar = firstCoinHudText.gameObject;
                _coinBar.transform.SetParent(_rootCanvas.transform, false);
                CoinHud.Bind(firstCoinHudText);
            }
            else
            {
                _coinBar = new GameObject("CoinHudBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
                _coinBar.transform.SetParent(_rootCanvas.transform, false);
                RectTransform coinRt = _coinBar.GetComponent<RectTransform>();
                coinRt.anchorMin = new Vector2(0f, 1f);
                coinRt.anchorMax = new Vector2(0f, 1f);
                coinRt.pivot = new Vector2(0f, 1f);
                coinRt.anchoredPosition = new Vector2(20f, -18f);
                coinRt.sizeDelta = new Vector2(320f, 48f);
                Text coinText = _coinBar.GetComponent<Text>();
                coinText.font = font;
                coinText.fontSize = 30;
                coinText.alignment = TextAnchor.MiddleLeft;
                coinText.color = Color.white;
                coinText.text = "金币 0";
                coinText.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline ol = _coinBar.GetComponent<Outline>();
                ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
                ol.effectDistance = new Vector2(2f, -2f);
                CoinHud.Bind(coinText);
            }
        }

        if (_panel != null)
        {
            if (CoinWallet.Instance != null)
                CoinHud.Refresh(CoinWallet.Instance.Coins);
            return;
        }

        if (font == null)
            font = ResolveUiFont();

        _panel = new GameObject("CoinShopPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(_rootCanvas.transform, false);
        _panel.SetActive(false);
        RectTransform panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        Image dim = _panel.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        GameObject row = new GameObject("Offers", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(_panel.transform, false);
        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = Vector2.zero;
        rowRt.sizeDelta = new Vector2(920f, 280f);
        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 24f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        h.padding = new RectOffset(24, 24, 24, 24);

        for (int i = 0; i < 3; i++)
        {
            GameObject card = new GameObject("Offer" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(row.transform, false);
            Image cardBg = card.GetComponent<Image>();
            cardBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            Button btn = card.GetComponent<Button>();
            _offerButtons[i] = btn;
            int index = i;
            btn.onClick.AddListener(() => OnPick(index));

            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGo.transform.SetParent(card.transform, false);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.45f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(12f, 0f);
            titleRt.offsetMax = new Vector2(-12f, -8f);
            _offerTitles[i] = titleGo.GetComponent<Text>();
            _offerTitles[i].font = font;
            _offerTitles[i].fontSize = 18;
            _offerTitles[i].alignment = TextAnchor.MiddleCenter;
            _offerTitles[i].color = Color.white;

            GameObject priceGo = new GameObject("Price", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            priceGo.transform.SetParent(card.transform, false);
            RectTransform priceRt = priceGo.GetComponent<RectTransform>();
            priceRt.anchorMin = new Vector2(0f, 0.12f);
            priceRt.anchorMax = new Vector2(1f, 0.42f);
            priceRt.offsetMin = new Vector2(12f, 0f);
            priceRt.offsetMax = new Vector2(-12f, 0f);
            _offerPrices[i] = priceGo.GetComponent<Text>();
            _offerPrices[i].font = font;
            _offerPrices[i].fontSize = 20;
            _offerPrices[i].fontStyle = FontStyle.Bold;
            _offerPrices[i].alignment = TextAnchor.MiddleCenter;
            _offerPrices[i].color = new Color(1f, 0.85f, 0.2f);
        }

        GameObject banner = new GameObject("ShopBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        banner.transform.SetParent(_panel.transform, false);
        RectTransform banRt = banner.GetComponent<RectTransform>();
        banRt.anchorMin = new Vector2(0.5f, 0.72f);
        banRt.anchorMax = new Vector2(0.5f, 0.72f);
        banRt.pivot = new Vector2(0.5f, 0.5f);
        banRt.anchoredPosition = Vector2.zero;
        banRt.sizeDelta = new Vector2(800f, 56f);
        Text banText = banner.GetComponent<Text>();
        banText.font = font;
        banText.fontSize = 28;
        banText.alignment = TextAnchor.MiddleCenter;
        banText.color = Color.white;
        banText.text = "选择一项增益（已暂停）";

        _panel.transform.SetAsLastSibling();
        _coinBar.transform.SetAsLastSibling();

        if (CoinWallet.Instance != null)
            CoinHud.Refresh(CoinWallet.Instance.Coins);
    }

    private static Font ResolveUiFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null)
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f == null)
        {
            try
            {
                f = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Microsoft YaHei", "SimHei", "Segoe UI" }, 48);
            }
            catch { /* ignore */ }
        }
        return f;
    }

    /// <summary>由 CoinWallet 在达到里程碑时调用。</summary>
    public void OpenShop()
    {
        EnsureCoinUi();
        if (_panel == null || CoinWallet.Instance == null)
            return;
        if (UIManager.instance != null && (UIManager.instance.IsGameOver || UIManager.instance.IsVictory))
            return;

        int c = CoinWallet.Instance.Coins;
        if (!ComputePrices(c, out int p0, out int p1, out int p2))
        {
            Debug.LogWarning("CoinShopUI: 金币不足以生成三档价格，推迟商店。");
            return;
        }

        _prices[0] = p0;
        _prices[1] = p1;
        _prices[2] = p2;

        List<BuffId> pool = new List<BuffId> { BuffId.Toughness, BuffId.Swiftness, BuffId.Brutality };
        for (int i = 0; i < 3; i++)
        {
            int r = Random.Range(0, pool.Count);
            _roll[i] = pool[r];
            pool.RemoveAt(r);
        }

        for (int i = 0; i < 3; i++)
        {
            var def = GetDef(_roll[i]);
            _offerTitles[i].text = def.title + "\n<size=14>" + def.desc + "</size>";
            _offerPrices[i].text = _prices[i] + " 金币";
        }

        CoinWallet.Instance.SetShopOpen(true);
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
        GamePause.SetShopOpen(true);
    }

    private static bool ComputePrices(int coins, out int low, out int mid, out int high)
    {
        low = Mathf.Max(1, coins / 10);
        mid = Mathf.Max(low + 1, coins / 4);
        high = Mathf.Max(mid + 1, coins * 2 / 5);
        if (high >= coins)
            high = coins - 1;
        if (mid >= high)
            mid = high - 1;
        if (low >= mid)
            low = Mathf.Max(1, mid - 1);
        return low > 0 && low < mid && mid < high && high < coins;
    }

    private (string title, string desc) GetDef(BuffId id)
    {
        for (int i = 0; i < _buffDefs.Length; i++)
        {
            if (_buffDefs[i].id == id)
                return (_buffDefs[i].title, _buffDefs[i].desc);
        }
        return ("?", "");
    }

    private void OnPick(int index)
    {
        if (CoinWallet.Instance == null)
            return;
        int price = _prices[index];
        if (!CoinWallet.Instance.TrySpend(price))
            return;

        ApplyBuff(_roll[index]);
        CoinWallet.Instance.AdvanceMilestone();
        CoinWallet.Instance.SetShopOpen(false);
        _panel.SetActive(false);
        GamePause.SetShopOpen(false);
    }

    private void ApplyBuff(BuffId id)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        switch (id)
        {
            case BuffId.Toughness:
                PlayerHealth ph = player.GetComponent<PlayerHealth>();
                if (ph != null)
                    ph.AddMaxHp(1, 1);
                break;
            case BuffId.Swiftness:
                PlayerRuntimeStats st = player.GetComponent<PlayerRuntimeStats>();
                if (st == null)
                    st = player.AddComponent<PlayerRuntimeStats>();
                st.moveSpeedMultiplier *= 1.1f;
                break;
            case BuffId.Brutality:
                PlayerRuntimeStats s2 = player.GetComponent<PlayerRuntimeStats>();
                if (s2 == null)
                    s2 = player.AddComponent<PlayerRuntimeStats>();
                s2.damageBonusMin += 2;
                s2.damageBonusMax += 4;
                break;
        }
    }
}
