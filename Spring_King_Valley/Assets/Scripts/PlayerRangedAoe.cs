using UnityEngine;

/// <summary>
/// 旧版 AOE 攻击逻辑已停用，当前由 PlayerAttack(v2 武器系统)统一处理玩家攻击。
/// </summary>
public class PlayerRangedAoe : MonoBehaviour
{
    private void Awake()
    {
        enabled = false;
    }
}
