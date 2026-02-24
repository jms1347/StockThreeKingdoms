using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataModel : MonoBehaviour
{

}

// 1. 색깔 타입을 명확한 명칭(Enum)으로 정의
public enum UnitType
{
    Red,    // 불/힘
    Blue,   // 물/지능
    Green,  // 풀/민첩
    Black   // 암흑/특수
}

// 2. 각 색깔이 가질 데이터 정의 (인스펙터에서 설정 가능)
[System.Serializable]
public class UnitColorData
{
    public string name;           // 이름 (예: 화염 속성)
    public UnitType type;         // 타입 (Enum)
    public Color visualColor;     // 실제 표시될 색상 (RGB)

    [Header("Combat Balance")]
    public UnitType strongAgainst; // 이 속성이 강한 상대
    public float damageMultiplier = 1.5f; // 상성 우위일 때 데미지 배율
}