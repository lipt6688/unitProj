using UnityEngine;

/// <summary>
/// 局内可叠加的增益（移速倍率、近战伤害加成），由金币商店等系统修改。
/// </summary>
public class PlayerRuntimeStats : MonoBehaviour
{
    [Min(0.1f)] public float moveSpeedMultiplier = 1f;
    public int damageBonusMin;
    public int damageBonusMax;
}
