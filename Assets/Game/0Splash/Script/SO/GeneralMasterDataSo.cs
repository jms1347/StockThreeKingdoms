using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GeneralMasterData
{
    public string id;                // G001
    public string name;              // ����
    public Grade grade;              // SS, S, A...
    public int power;                // ����
    public int intel;                // ����
    public int charm;                // �ŷ�
    public string buffId;            // S�� �̻� ���� ID

    [Header("�ʱ� ��ġ ������")]
    public string initialNationId;   // �ʱ� �Ҽ� ���� (WEI, SHU, WU, OTHERS)
    public string initialCastleId;   // �ʱ� ��ġ �� ID (C01 ~ C50)

    [Header("UI")]
    [Tooltip("�� �� �¼� �ʻ�ȭ(������ �̴ϼ� ǥ��).")]
    public Sprite governorPortrait;

    public bool HasBuff => !string.IsNullOrEmpty(buffId);
}

[CreateAssetMenu(fileName = "GeneralMasterDataSo", menuName = "ScriptableObject/GeneralMasterDataSo")]
public class GeneralMasterDataSo : ScriptableObject
{
    public List<GeneralMasterData> list = new List<GeneralMasterData>();

}
