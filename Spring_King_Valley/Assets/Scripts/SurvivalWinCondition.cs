using UnityEngine;

/// <summary>超平坦关卡：存活指定秒数即胜利（商店暂停时计时会停）。</summary>
public class SurvivalWinCondition : MonoBehaviour
{
    [SerializeField] private float surviveSeconds = 60f;

    private float _elapsed;

    private void LateUpdate()
    {
        if (UIManager.instance == null)
            return;
        if (UIManager.instance.IsGameOver || UIManager.instance.IsVictory)
            return;

        _elapsed += Time.deltaTime;

        if (UIManager.instance.waveText != null)
        {
            float left = Mathf.Max(0f, surviveSeconds - _elapsed);
            UIManager.instance.waveText.text = $"存活 {Mathf.CeilToInt(left)}s / {Mathf.RoundToInt(surviveSeconds)}s";
        }

        if (_elapsed >= surviveSeconds)
            UIManager.instance.ShowVictory();
    }
}
