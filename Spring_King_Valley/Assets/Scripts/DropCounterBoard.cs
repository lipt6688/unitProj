using UnityEngine;

namespace Vampire
{
    public static class DropCounterBoard
    {
        private const string LegacyPanelName = "DropCounterBoard";
        private static int expCollected;
        private static int coinsCollected;
        private static bool locked;

        public static int ExpCollected => expCollected;
        public static int CoinsCollected => coinsCollected;

        public static void ResetSessionCounters()
        {
            locked = false;
            expCollected = 0;
            coinsCollected = 0;
            RemoveLegacyTopBoard();
            CoinHud.SetSessionExp(expCollected);
        }

        public static void SetLocked(bool isLocked)
        {
            locked = isLocked;
        }

        public static void AddExp(int amount)
        {
            if (amount <= 0)
                return;
            if (locked)
                return;

            expCollected += amount;
            CoinHud.SetSessionExp(expCollected);
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0)
                return;
            if (locked)
                return;

            coinsCollected += amount;
        }

        private static void RemoveLegacyTopBoard()
        {
            GameObject legacyPanel = GameObject.Find(LegacyPanelName);
            if (legacyPanel != null)
                Object.Destroy(legacyPanel);
        }
    }
}
