using UnityEngine.UI;

public static class CoinHud
{
    private static Text _coinText;
    private static int _coins;
    private static int _sessionExp;

    public static void Bind(Text text)
    {
        _coinText = text;
        Render();
    }

    public static void Refresh(int coins)
    {
        _coins = coins;
        Render();
    }

    public static void SetSessionExp(int exp)
    {
        _sessionExp = exp < 0 ? 0 : exp;
        Render();
    }

    private static void Render()
    {
        if (_coinText != null)
            _coinText.text = "金币 " + _coins + "    EXP " + _sessionExp;
    }
}
