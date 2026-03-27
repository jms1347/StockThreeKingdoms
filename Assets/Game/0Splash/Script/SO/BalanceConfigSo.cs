using UnityEngine;

/// <summary>
/// 노동/시장/병사등급 등 홈 경제 밸런스. <see cref="GameManager"/>가 런타임에 복사해 사용(에디터 SO 원본은 수정하지 않음).
/// </summary>
[CreateAssetMenu(fileName = "BalanceConfigSo", menuName = "ScriptableObject/BalanceConfigSo")]
public class BalanceConfigSo : ScriptableObject
{
    public BalanceConfig balance = new BalanceConfig();

    public BalanceConfig CreateRuntimeCopy()
    {
        return JsonUtility.FromJson<BalanceConfig>(JsonUtility.ToJson(balance));
    }
}
