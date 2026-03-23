using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms.WorldMap
{
    public enum LordFaction
    {
        Wei = 0,
        Shu = 1,
        Wu = 2,
        Neutral = 3
    }

    [Serializable]
    public struct CityEntry
    {
        public int Id;
        public string CityName;
        public string GovernorName;
        public int Population;
        [Range(0f, 100f)] public float PublicSentiment;
        public float ChangeRatePercent;
        public LordFaction Faction;
        public bool IsWar;
        /// <summary>맵 로컬 좌표 (W, H). WorldMapManager Content 기준 픽셀.</summary>
        public Vector2 MapPosition;
    }

    [Serializable]
    public struct CityEdge
    {
        public int CityA;
        public int CityB;
    }

    [CreateAssetMenu(fileName = "CityDatabase", menuName = "Three Kingdoms/City Database", order = 0)]
    public class CityDatabase : ScriptableObject
    {
        public const int CityCount = 50;

        [SerializeField] CityEntry[] cities = new CityEntry[CityCount];
        [SerializeField] List<CityEdge> edges = new List<CityEdge>();

        public IReadOnlyList<CityEntry> Cities => cities;
        public IReadOnlyList<CityEdge> Edges => edges;

        public CityEntry GetCity(int id)
        {
            if (cities == null || id < 0 || id >= cities.Length) return default;
            return cities[id];
        }

        /// <summary>에디터 또는 런타임에서 50개 기본 데이터로 채웁니다.</summary>
        public void ResetToDefaultFifty()
        {
            cities = BuildDefaultCities();
            edges = BuildDefaultEdges(cities);
        }

        public static CityDatabase CreateRuntimeDefault()
        {
            var db = CreateInstance<CityDatabase>();
            db.cities = BuildDefaultCities();
            db.edges = BuildDefaultEdges(db.cities);
            return db;
        }

        static CityEntry[] BuildDefaultCities()
        {
            var names = new[]
            {
                "허창", "낙양", "업", "성도", "건업", "패양", "한중", "정안", "허비", "광릉",
                "강릉", "한단", "업경", "북평", "서주", "남군", "무릉", "장사", "계림", "교지",
                "합비", "수춘", "진류", "하비", "양주", "여남", "이도", "성조", "강하", "무관",
                "연교", "서량", "천수", "안정", "상용", "한흥", "판군", "절현", "해림", "양책",
                "저", "즉묵", "계양", "임해", "노국", "유구", "솔라", "탐라", "오환", "함양"
            };

            var governors = new[]
            {
                "조조", "사마의", "원소", "유비", "손권", "전예", "하후연", "사마사", "전욱", "육손",
                "주유", "공손찬", "장료", "공손도", "조비", "유표", "손책", "황충", "사마소", "사마염",
                "하후돈", "조인", "장합", "사마염", "등애", "강유", "제갈량", "관우", "장비", "조운",
                "마초", "위연", "방통", "서서", "법정", "황월영", "포통", "미축", "왕평", "비시",
                "비홍", "이엄", "오반", "장의", "사마휘", "사마사", "사마소", "사마염", "공손연", "한맹"
            };

            var entries = new CityEntry[CityCount];
            int cols = 5;
            float cellW = 200f;
            float cellH = 160f;
            float padX = 120f;
            float padY = 140f;
            float mapW = padX * 2 + (cols - 1) * cellW;
            float rows = Mathf.Ceil(CityCount / (float)cols);

            for (int i = 0; i < CityCount; i++)
            {
                int col = i % cols;
                int row = i / cols;
                var faction = (LordFaction)(i % 4);
                float x = padX + col * cellW + (row % 2) * 40f;
                float y = padY + row * cellH;

                entries[i] = new CityEntry
                {
                    Id = i,
                    CityName = names[i],
                    GovernorName = governors[i],
                    Population = 12000 + (i * 137) % 88000,
                    PublicSentiment = 35f + (i * 11) % 55,
                    ChangeRatePercent = ((i * 7) % 17 - 8) * 0.35f,
                    Faction = faction,
                    IsWar = (i % 7 == 0 || i % 11 == 3),
                    MapPosition = new Vector2(x, y)
                };
            }

            return entries;
        }

        static List<CityEdge> BuildDefaultEdges(CityEntry[] c)
        {
            var list = new List<CityEdge>();
            int cols = 5;
            for (int i = 0; i < c.Length; i++)
            {
                int col = i % cols;
                if (col < cols - 1 && i + 1 < c.Length)
                    list.Add(new CityEdge { CityA = i, CityB = i + 1 });
                int below = i + cols;
                if (below < c.Length)
                    list.Add(new CityEdge { CityA = i, CityB = below });
                if (col < cols - 1 && below + 1 < c.Length && (i + cols) < c.Length)
                    list.Add(new CityEdge { CityA = i, CityB = below + 1 });
            }

            list.Add(new CityEdge { CityA = 0, CityB = 14 });
            list.Add(new CityEdge { CityA = 4, CityB = 19 });
            list.Add(new CityEdge { CityA = 24, CityB = 35 });
            return list;
        }

#if UNITY_EDITOR
        [ContextMenu("Populate Default 50 Cities")]
        void EditorPopulate()
        {
            ResetToDefaultFifty();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
