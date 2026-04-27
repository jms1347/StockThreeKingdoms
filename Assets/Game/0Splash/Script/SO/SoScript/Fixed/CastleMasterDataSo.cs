using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// 1. �� ������ ������ (���� ��Ʈ ������ ���� ������)
public enum Grade
{
    SS = 0,
    S = 1,
    A = 2,
    B = 3,
    C = 4,
    D = 5
}

[Serializable]
public class CastleMasterData
{
    [Header("�⺻ ����")]
    public string id;                // �� ���� ID (C01, C02...)
    public string name;              // �� �̸� (����, ��â...)
    public string regionId;          // ���� �ڵ� (R01~R12)
    public Grade grade;              // �� ��� (SS, S, A, B, C, D)
    public string initialNationId;   // �ʱ� ���� ���� (WEI, SHU, WU...)

    [Header("����/���� ����")]
    [Tooltip("�Լ�(�ż�) �� ¡�� ������(%). ��Ʈ F�� ? 20�̸� ������ 20%")]
    public float initialTaxRatePercent;
    public float baseValue;          // �ʱ� �׸鰡 (��Ʈ G��)
    public int maxTroops;            // �ִ� ���� ���뷮
    public int initPopulation;       // �ʱ� �鼺 ��

    [Header("���� �� ���� ������")]
    public float posX;               // �������� X ��ǥ (0 ~ 1000)
    public float posY;               // �������� Y ��ǥ (0 ~ 1000)

    [TextArea(2, 5)]
    public string adjacentIdsRaw;    // ���� �� ID ����Ʈ (��ǥ ����: "C02,C05,C10")

    // --- Helper Properties ---

    /// <summary>
    /// ��ǥ�� ���е� ������ �����͸� ����Ʈ�� ��ȯ�Ͽ� ��ȯ�մϴ�.
    /// </summary>
    public List<string> GetAdjacentIds()
    {
        if (string.IsNullOrWhiteSpace(adjacentIdsRaw)) return new List<string>();
        return adjacentIdsRaw.Split(',')
                             .Select(x => x.Trim())
                             .Where(x => !string.IsNullOrEmpty(x))
                             .ToList();
    }

    /// <summary>
    /// ��� ���Ǹ� ���� ��ǥ�� Vector2�� ��ȯ�մϴ�.
    /// </summary>
    public Vector2 GetPosition() => new Vector2(posX, posY);

    /// <summary><see cref="CastleStateData.currentLord"/> �ʱ�ȭ��. <see cref="initialNationId"/> �� WEI/SHU/WU/OTHERS(���� �� ��° ����).</summary>
    public Faction GetInitialLordFaction()
    {
        if (string.IsNullOrWhiteSpace(initialNationId))
            return Faction.OTHERS;
        string raw = initialNationId.Trim();
        if (int.TryParse(raw, out int n) && Enum.IsDefined(typeof(Faction), n))
            return NormalizeFourthNation((Faction)n);
        if (Enum.TryParse(raw, true, out Faction f))
            return NormalizeFourthNation(f);
        return Faction.OTHERS;
    }

    /// <summary>NONE���̱����� �����ˡ����� ������ �ϳ��� OTHERS �������� �����մϴ�.</summary>
    static Faction NormalizeFourthNation(Faction f) => f == Faction.NONE ? Faction.OTHERS : f;

    /// <summary>
    /// 구글 시트/에셋에 H(주둔 상한)·I(초기 인구)가 비어 0인 경우, <see cref="baseValue"/>·등급으로 보강합니다.
    /// (데이터 누락 시 인구·징병·월드 UI가 0으로 고정되는 것을 막기 위함.)
    /// </summary>
    public void EnsureDerivedDefaults()
    {
        if (baseValue < 1e-3f) baseValue = 1f;

        int g = Mathf.Clamp((int)grade, 0, 5);
        float tier = 1f + (5 - g) * 0.07f;

        if (initPopulation <= 0)
            initPopulation = Mathf.Max(2000, Mathf.RoundToInt(baseValue * 100f * tier));

        if (maxTroops <= 0)
        {
            int fromBase = Mathf.Max(1, Mathf.RoundToInt(baseValue * 0.45f * tier));
            int fromPop = Mathf.Max(1, initPopulation / 200);
            maxTroops = Mathf.Max(fromBase, fromPop);
        }
    }
}

[CreateAssetMenu(fileName = "CastleMasterDataSo", menuName = "ScriptableObject/CastleMasterDataSo")]
public class CastleMasterDataSo : ScriptableObject
{
    public List<CastleMasterData> list = new List<CastleMasterData>();

}
