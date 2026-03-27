using UnityEngine;

/// <summary>
/// <see cref="GlobalEconomy"/> 정적 필드 초기값(세션 시작 시 DataManager에서 1회 반영).
/// </summary>
[CreateAssetMenu(fileName = "GlobalEconomyDefaultsSo", menuName = "ScriptableObject/GlobalEconomyDefaultsSo")]
public class GlobalEconomyDefaultsSo : ScriptableObject
{
    public long initialTotalServerSoldiers;
    [Tooltip("1.0 = 기준. 인플레이션 등 시뮬에 사용.")]
    public float initialGrainPriceIndex = 1f;
}
