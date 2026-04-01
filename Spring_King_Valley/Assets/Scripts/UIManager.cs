using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vampire;

public class UIManager : MonoBehaviour
{
    public Text waveText;
    public Text coordinateText;
    public static UIManager instance;

    public Image[] hpImages;
    public Animator gameOverAnim;
    [SerializeField] private Vector2 coordinateUiOffset = new Vector2(-24f, 18f);
    [SerializeField] private int defaultHeartSlots = 5;
    [SerializeField] private float heartSpacing = 42f;
    [SerializeField] private Vector2 playerHpUiOffset = new Vector2(-24f, -20f);
    [SerializeField] private Color hpHighColor = new Color(0.17f, 0.8f, 0.26f, 1f);
    [SerializeField] private Color hpMidColor = new Color(0.96f, 0.77f, 0.18f, 1f);
    [SerializeField] private Color hpLowColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [SerializeField] private float lowHpThreshold = 0.25f;
    [SerializeField] private float hitFlashDuration = 0.14f;
    private Transform coordinateTarget;
    private GameObject bossHpRoot;
    private Image bossHpFill;
    private Text bossHpLabel;
    private Text bossHpValue;
    private GameObject playerHpRoot;
    private Image playerHpFill;
    private Text playerHpValue;
    private Canvas runtimeHudCanvas;
    private bool legacyHeartUiHidden;
    private float lastPlayerHpPercent = 1f;
    private float playerHpFlashUntil = -1f;

    /// <summary>游戏结束 UI 已激活时，不再弹出金币商店。</summary>
    public bool IsGameOver { get; private set; }

    public bool IsVictory { get; private set; }

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            if(instance != this)
            {
                Destroy(gameObject);
            }
        }
        DontDestroyOnLoad(gameObject);
        DisableHeartMechanism();
        EnsureRuntimeHudCanvas();
        HideHeartIcons();
        RemoveLegacyHeartUi();
        ForceDisableLegacyHealthFrame();
        EnsureCoinSystems();
        EnsureCoordinateText();
        EnsurePlayerHpUi();
        EnsureBossHpUi();
    }

    private void Update()
    {
        EnsureCoordinateText();
        EnsurePlayerHpUi();
        HideHeartIcons();
        RemoveLegacyHeartUi();
        ForceDisableLegacyHealthFrame();
        RefreshPlayerHpBarVisual();

        if (coordinateTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                coordinateTarget = player.transform;
        }

        if (coordinateText == null)
            return;

        if (coordinateTarget == null)
        {
            coordinateText.text = "X: --  Y: --";
            return;
        }

        Vector3 p = coordinateTarget.position;
        coordinateText.text = string.Format("X: {0:0.0}  Y: {1:0.0}", p.x, p.y);
    }

    private void EnsureCoordinateText()
    {
        if (coordinateText != null)
            return;

        GameObject existing = GameObject.Find("PlayerCoordinateText");
        if (existing != null)
        {
            coordinateText = existing.GetComponent<Text>();
            if (coordinateText != null)
                return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        GameObject go = new GameObject("PlayerCoordinateText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = coordinateUiOffset;
        rt.sizeDelta = new Vector2(260f, 36f);

        coordinateText = go.GetComponent<Text>();
        coordinateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        coordinateText.fontSize = 22;
        coordinateText.alignment = TextAnchor.LowerRight;
        coordinateText.color = new Color(1f, 1f, 1f, 0.95f);
        coordinateText.text = "X: --  Y: --";
    }

    private void EnsureBossHpUi()
    {
        if (bossHpRoot != null)
            return;

        GameObject existing = GameObject.Find("BossHpBarRoot");
        if (existing != null)
        {
            bossHpRoot = existing;
            Transform fill = existing.transform.Find("BarBg/Fill");
            if (fill != null)
                bossHpFill = fill.GetComponent<Image>();
            Transform label = existing.transform.Find("BossName");
            if (label != null)
                bossHpLabel = label.GetComponent<Text>();
            Transform value = existing.transform.Find("HpValue");
            if (value != null)
                bossHpValue = value.GetComponent<Text>();
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        bossHpRoot = new GameObject("BossHpBarRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bossHpRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = bossHpRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, -12f);
        rootRt.sizeDelta = new Vector2(680f, 84f);

        Image rootImage = bossHpRoot.GetComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.5f);

        GameObject labelGo = new GameObject("BossName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(bossHpRoot.transform, false);
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.anchoredPosition = new Vector2(0f, -8f);
        labelRt.sizeDelta = new Vector2(-24f, 28f);
        bossHpLabel = labelGo.GetComponent<Text>();
        bossHpLabel.font = font;
        bossHpLabel.fontSize = 26;
        bossHpLabel.alignment = TextAnchor.MiddleCenter;
        bossHpLabel.color = new Color(1f, 0.96f, 0.86f, 1f);
        bossHpLabel.text = "BOSS";

        GameObject barBg = new GameObject("BarBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barBg.transform.SetParent(bossHpRoot.transform, false);
        RectTransform barBgRt = barBg.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot = new Vector2(0.5f, 0f);
        barBgRt.anchoredPosition = new Vector2(0f, 10f);
        barBgRt.sizeDelta = new Vector2(-28f, 32f);
        Image barBgImage = barBg.GetComponent<Image>();
        barBgImage.color = new Color(0.18f, 0.06f, 0.06f, 0.9f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(barBg.transform, false);
        RectTransform fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        bossHpFill = fillGo.GetComponent<Image>();
        bossHpFill.type = Image.Type.Filled;
        bossHpFill.fillMethod = Image.FillMethod.Horizontal;
        bossHpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        bossHpFill.color = new Color(0.88f, 0.14f, 0.14f, 1f);
        bossHpFill.fillAmount = 1f;

        GameObject valueGo = new GameObject("HpValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        valueGo.transform.SetParent(barBg.transform, false);
        RectTransform valueRt = valueGo.GetComponent<RectTransform>();
        valueRt.anchorMin = new Vector2(0f, 0f);
        valueRt.anchorMax = new Vector2(1f, 1f);
        valueRt.offsetMin = Vector2.zero;
        valueRt.offsetMax = Vector2.zero;
        bossHpValue = valueGo.GetComponent<Text>();
        bossHpValue.font = font;
        bossHpValue.fontSize = 22;
        bossHpValue.alignment = TextAnchor.MiddleCenter;
        bossHpValue.color = Color.white;
        bossHpValue.text = "0/0";

        bossHpRoot.SetActive(false);
    }

    private void EnsurePlayerHpUi()
    {
        EnsureRuntimeHudCanvas();

        if (playerHpRoot != null)
        {
            if (runtimeHudCanvas != null && playerHpRoot.transform.parent != runtimeHudCanvas.transform)
                playerHpRoot.transform.SetParent(runtimeHudCanvas.transform, false);
            return;
        }

        GameObject existing = GameObject.Find("PlayerHpBarRoot");
        if (existing != null)
        {
            playerHpRoot = existing;
            Transform fill = existing.transform.Find("BarBg/Fill");
            if (fill != null)
                playerHpFill = fill.GetComponent<Image>();
            NormalizePlayerHpValueText();
            return;
        }

        Canvas canvas = runtimeHudCanvas;
        if (canvas == null)
            return;

        playerHpRoot = new GameObject("PlayerHpBarRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        playerHpRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = playerHpRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 1f);
        rootRt.anchoredPosition = playerHpUiOffset;
        rootRt.sizeDelta = new Vector2(460f, 78f);

        Image rootImage = playerHpRoot.GetComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.45f);

        GameObject barBg = new GameObject("BarBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barBg.transform.SetParent(playerHpRoot.transform, false);
        RectTransform barBgRt = barBg.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 1f);
        barBgRt.offsetMin = new Vector2(12f, 14f);
        barBgRt.offsetMax = new Vector2(-12f, -24f);
        Image barBgImage = barBg.GetComponent<Image>();
        barBgImage.color = new Color(0.2f, 0.12f, 0.12f, 0.95f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(barBg.transform, false);
        RectTransform fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        playerHpFill = fillGo.GetComponent<Image>();
        playerHpFill.type = Image.Type.Filled;
        playerHpFill.fillMethod = Image.FillMethod.Horizontal;
        playerHpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        playerHpFill.color = new Color(0.17f, 0.8f, 0.26f, 1f);
        playerHpFill.fillAmount = 1f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject valueGo = new GameObject("HpValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        valueGo.transform.SetParent(barBg.transform, false);
        RectTransform valueRt = valueGo.GetComponent<RectTransform>();
        valueRt.anchorMin = new Vector2(0f, 0f);
        valueRt.anchorMax = new Vector2(1f, 1f);
        valueRt.offsetMin = Vector2.zero;
        valueRt.offsetMax = Vector2.zero;
        playerHpValue = valueGo.GetComponent<Text>();
        playerHpValue.font = font;
        playerHpValue.fontSize = 22;
        playerHpValue.alignment = TextAnchor.MiddleCenter;
        playerHpValue.color = Color.white;
        playerHpValue.text = "0/0";

        RefreshPlayerHpBarVisual();
    }

    private void NormalizePlayerHpValueText()
    {
        if (playerHpRoot == null)
            return;

        Transform barBg = playerHpRoot.transform.Find("BarBg");
        if (barBg == null)
            return;

        List<Text> hpValueTexts = new List<Text>();
        Text[] barTexts = barBg.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < barTexts.Length; i++)
        {
            if (barTexts[i] != null && barTexts[i].name == "HpValue")
                hpValueTexts.Add(barTexts[i]);
        }

        Transform strayRootValue = playerHpRoot.transform.Find("HpValue");
        if (strayRootValue != null)
        {
            Text strayText = strayRootValue.GetComponent<Text>();
            if (strayText != null && !hpValueTexts.Contains(strayText))
                hpValueTexts.Add(strayText);
        }

        Text selected = null;
        for (int i = 0; i < hpValueTexts.Count; i++)
        {
            Text candidate = hpValueTexts[i];
            if (candidate != null && candidate.transform.IsChildOf(barBg))
            {
                selected = candidate;
                break;
            }
        }

        if (selected == null && hpValueTexts.Count > 0)
            selected = hpValueTexts[0];

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (selected == null)
        {
            GameObject valueGo = new GameObject("HpValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            valueGo.transform.SetParent(barBg, false);
            selected = valueGo.GetComponent<Text>();
        }

        if (!selected.transform.IsChildOf(barBg))
            selected.transform.SetParent(barBg, false);

        RectTransform valueRt = selected.GetComponent<RectTransform>();
        valueRt.anchorMin = new Vector2(0f, 0f);
        valueRt.anchorMax = new Vector2(1f, 1f);
        valueRt.offsetMin = Vector2.zero;
        valueRt.offsetMax = Vector2.zero;

        selected.name = "HpValue";
        selected.font = font;
        selected.fontSize = 22;
        selected.alignment = TextAnchor.MiddleCenter;
        selected.color = Color.white;

        for (int i = 0; i < hpValueTexts.Count; i++)
        {
            Text duplicate = hpValueTexts[i];
            if (duplicate == null || duplicate == selected)
                continue;
            Destroy(duplicate.gameObject);
        }

        playerHpValue = selected;
        if (string.IsNullOrEmpty(playerHpValue.text))
            playerHpValue.text = "0/0";
    }

    private void EnsureRuntimeHudCanvas()
    {
        if (runtimeHudCanvas != null)
            return;

        GameObject existing = GameObject.Find("RuntimeHudOverlayCanvas");
        if (existing != null)
        {
            runtimeHudCanvas = existing.GetComponent<Canvas>();
            if (runtimeHudCanvas != null)
                return;
        }

        GameObject canvasGo = new GameObject("RuntimeHudOverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasGo);

        runtimeHudCanvas = canvasGo.GetComponent<Canvas>();
        runtimeHudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeHudCanvas.sortingOrder = 5000;
        runtimeHudCanvas.pixelPerfect = false;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void DisableHeartMechanism()
    {
        defaultHeartSlots = 0;
        heartSpacing = 0f;
    }

    private void HideHeartIcons()
    {
        if (hpImages == null)
            return;

        for (int i = 0; i < hpImages.Length; i++)
        {
            if (hpImages[i] != null)
                hpImages[i].gameObject.SetActive(false);
        }

        if (!legacyHeartUiHidden && hpImages.Length > 0 && hpImages[0] != null)
        {
            Transform parent = hpImages[0].transform.parent;
            if (parent != null)
            {
                parent.gameObject.SetActive(false);
                legacyHeartUiHidden = true;
            }
        }
    }

    private void RemoveLegacyHeartUi()
    {
        Image[] images = FindObjectsOfType<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            Transform t = image.transform;
            if (playerHpRoot != null && t.IsChildOf(playerHpRoot.transform))
                continue;
            if (bossHpRoot != null && t.IsChildOf(bossHpRoot.transform))
                continue;

            string lowerName = image.gameObject.name.ToLowerInvariant();
            bool looksLikeHeart = lowerName.Contains("heart") ||
                                  lowerName.Contains("health") ||
                                  lowerName.StartsWith("hp_") ||
                                  lowerName.StartsWith("heart_");
            if (!looksLikeHeart)
                continue;

            image.gameObject.SetActive(false);
            Transform parent = image.transform.parent;
            if (parent != null)
            {
                string parentName = parent.gameObject.name.ToLowerInvariant();
                if (parentName.Contains("heart") || parentName == "hp" || parentName.Contains("health"))
                    parent.gameObject.SetActive(false);
            }
        }
    }

    private void ForceDisableLegacyHealthFrame()
    {
        string[] knownLegacyNames = { "Health Bar", "HealthBar", "HP", "Hp", "HeartBar", "PlayerHealthBar" };
        for (int i = 0; i < knownLegacyNames.Length; i++)
        {
            GameObject go = GameObject.Find(knownLegacyNames[i]);
            if (go == null)
                continue;

            if (playerHpRoot != null && go.transform.IsChildOf(playerHpRoot.transform))
                continue;
            if (bossHpRoot != null && go.transform.IsChildOf(bossHpRoot.transform))
                continue;

            go.SetActive(false);
        }
    }

    private static void EnsureCoinSystems()
    {
        if (CoinWallet.Instance != null)
        {
            return;
        }
        GameObject go = new GameObject("CoinSystems");
        DontDestroyOnLoad(go);
        go.AddComponent<CoinWallet>();
        go.AddComponent<CoinShopUI>();
    }

    public void UpdateWaveText(int _num)
    {
        if (_num <= 9)
            waveText.text = "WAVE 0" + _num.ToString();
        else
            waveText.text = "WAVE " + _num.ToString();      
    }

    public void SetBossHpVisible(bool visible)
    {
        EnsureBossHpUi();
        if (bossHpRoot == null)
            return;

        if (bossHpRoot.activeSelf != visible)
            bossHpRoot.SetActive(visible);
    }

    public void UpdateBossHp(string bossName, float currentHp, float maxHp)
    {
        EnsureBossHpUi();
        if (bossHpRoot == null || bossHpFill == null)
            return;

        maxHp = Mathf.Max(1f, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);

        bossHpRoot.SetActive(true);
        if (bossHpLabel != null)
            bossHpLabel.text = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName;

        bossHpFill.fillAmount = currentHp / maxHp;
        if (bossHpValue != null)
            bossHpValue.text = $"{Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)}";
    }

    /// <summary>按当前血 / 上限比例点亮心形（上限可大于图标数量）。</summary>
    public void UpdateHp(int currentHp, int maxHp)
    {
        EnsurePlayerHpUi();
        HideHeartIcons();

        if (playerHpFill == null)
            return;

        maxHp = Mathf.Max(1, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        lastPlayerHpPercent = (float)currentHp / maxHp;
        playerHpFill.fillAmount = lastPlayerHpPercent;
        RefreshPlayerHpBarVisual();
        if (playerHpValue != null)
            playerHpValue.text = $"{currentHp}/{maxHp}";
    }

    public void TriggerPlayerHitFlash()
    {
        playerHpFlashUntil = Time.unscaledTime + hitFlashDuration;
        RefreshPlayerHpBarVisual();
    }

    private void RefreshPlayerHpBarVisual()
    {
        if (playerHpFill == null)
            return;

        if (Time.unscaledTime <= playerHpFlashUntil)
        {
            playerHpFill.color = Color.white;
            return;
        }

        if (lastPlayerHpPercent <= lowHpThreshold)
        {
            float pulse = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
            playerHpFill.color = Color.Lerp(hpLowColor, Color.white, pulse * 0.35f);
            return;
        }

        playerHpFill.color = Color.Lerp(hpLowColor, hpHighColor, Mathf.Clamp01((lastPlayerHpPercent - lowHpThreshold) / (1f - lowHpThreshold)));
        if (lastPlayerHpPercent <= 0.6f)
            playerHpFill.color = Color.Lerp(hpMidColor, playerHpFill.color, Mathf.InverseLerp(0.2f, 0.6f, lastPlayerHpPercent));
    }

    private void EnsureHeartSlots(int targetSlots)
    {
        if (targetSlots <= 0 || hpImages == null || hpImages.Length == 0)
            return;

        int current = hpImages.Length;
        if (current < targetSlots)
        {
            List<Image> expanded = new List<Image>(hpImages);
            Image template = hpImages[0];
            RectTransform templateRt = template != null ? template.GetComponent<RectTransform>() : null;
            Transform parent = templateRt != null ? templateRt.parent : null;

            if (template != null && templateRt != null && parent != null)
            {
                for (int i = current; i < targetSlots; i++)
                {
                    GameObject clone = Instantiate(template.gameObject, parent);
                    clone.name = $"HP_{i + 1}";
                    Image cloneImage = clone.GetComponent<Image>();
                    if (cloneImage != null)
                        expanded.Add(cloneImage);
                }
            }

            hpImages = expanded.ToArray();
        }

        RectTransform firstRt = hpImages[0] != null ? hpImages[0].GetComponent<RectTransform>() : null;
        if (firstRt == null)
            return;

        Vector2 basePos = firstRt.anchoredPosition;
        for (int i = 0; i < hpImages.Length; i++)
        {
            if (hpImages[i] == null)
                continue;

            RectTransform rt = hpImages[i].GetComponent<RectTransform>();
            if (rt == null)
                continue;

            rt.anchoredPosition = new Vector2(basePos.x + i * heartSpacing, basePos.y);
        }
    }

    public void GameOverAnimation()
    {
        IsGameOver = true;
        if (CoinWallet.Instance != null)
            CoinWallet.Instance.SetLocked(true);
        DropCounterBoard.SetLocked(true);
        GamePause.SetShopOpen(false);
        Time.timeScale = 0;
        gameOverAnim.SetTrigger("GameOver");
    }

    /// <summary>供 Game Over 面板 Restart 按钮调用；清 DDOL 管理器后重载当前场景。</summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        GamePause.ResetAfterRestart();

        GameObject victory = GameObject.Find("VictoryPanel");
        if (victory != null)
            Destroy(victory);

        GameObject coinSys = GameObject.Find("CoinSystems");
        if (coinSys != null)
            Destroy(coinSys);

        UIManager old = instance;
        instance = null;
        if (old != null)
            Destroy(old.gameObject);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
    }

    public void ShowVictory()
    {
        if (IsVictory || IsGameOver)
            return;
        IsVictory = true;
        if (CoinWallet.Instance != null)
            CoinWallet.Instance.SetLocked(true);
        DropCounterBoard.SetLocked(true);
        GamePause.SetShopOpen(false);
        Time.timeScale = 0f;
        BuildVictoryOverlay();
    }

    private void BuildVictoryOverlay()
    {
        Canvas c = FindObjectOfType<Canvas>();
        if (c == null)
            return;
        GameObject panel = new GameObject("VictoryPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(c.transform, false);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.transform.SetParent(panel.transform, false);
        RectTransform tr = txtGo.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 0.55f);
        tr.anchorMax = new Vector2(0.5f, 0.55f);
        tr.sizeDelta = new Vector2(720f, 120f);
        Text t = txtGo.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 36;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = "胜利！";
        panel.transform.SetAsLastSibling();
    }

}
