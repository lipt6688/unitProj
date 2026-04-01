using UnityEngine;

/// <summary>商店等界面暂停：统一改 timeScale，并供玩家/输入侧做额外冻结。</summary>
public static class GamePause
{
    public static bool IsShopOpen { get; private set; }

    public static void ResetAfterRestart()
    {
        IsShopOpen = false;
    }

    public static void SetShopOpen(bool open)
    {
        IsShopOpen = open;
        if (open)
        {
            Time.timeScale = 0f;
            return;
        }
        if (UIManager.instance != null && (UIManager.instance.IsGameOver || UIManager.instance.IsVictory))
            return;
        Time.timeScale = 1f;
    }
}
