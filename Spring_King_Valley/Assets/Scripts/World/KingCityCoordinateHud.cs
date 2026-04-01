using UnityEngine;
using UnityEngine.UI;
using Vampire;

public class KingCityCoordinateHud : MonoBehaviour
{
    [SerializeField] private Vector2 uiOffset = new Vector2(24f, 18f);
    private Text text;
    private WorldMapCoordinator coordinator;

    public void Init(WorldMapCoordinator worldMapCoordinator)
    {
        coordinator = worldMapCoordinator;
    }

    private void Update()
    {
        EnsureText();
        if (text == null)
            return;

        if (coordinator == null)
        {
            text.text = "王城: --";
            return;
        }

        Vector2 f = coordinator.GetKingCityCenterWorld(MonsterWorldKind.Fire);
        Vector2 g = coordinator.GetKingCityCenterWorld(MonsterWorldKind.Grass);
        Vector2 i = coordinator.GetKingCityCenterWorld(MonsterWorldKind.Ice);

        text.text =
            $"火王城: ({f.x:0.0}, {f.y:0.0})\n" +
            $"草王城: ({g.x:0.0}, {g.y:0.0})\n" +
            $"冰王城: ({i.x:0.0}, {i.y:0.0})";
    }

    private void EnsureText()
    {
        if (text != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        GameObject existing = GameObject.Find("KingCityCoordinateText");
        if (existing != null)
        {
            text = existing.GetComponent<Text>();
            if (text != null)
                return;
        }

        GameObject go = new GameObject("KingCityCoordinateText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = uiOffset;
        rt.sizeDelta = new Vector2(320f, 90f);

        text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 18;
        text.alignment = TextAnchor.LowerLeft;
        text.color = new Color(1f, 1f, 1f, 0.95f);
        text.text = "王城: --";
    }
}

