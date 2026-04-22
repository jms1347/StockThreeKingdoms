using System.Collections.Generic;
using UnityEngine;

/// <summary>원격 시트 미연동 시 런타임 목 데이터. 전역 맵을 덮도록 다수의 성을 배치합니다.</summary>
public static class MockCastleDataProvider
{
    public static CastleData CreateRuntimeMockAsset()
    {
        var asset = ScriptableObject.CreateInstance<CastleData>();
        asset.rows = BuildDefaultRows();
        asset.name = "RuntimeMockCastleData";
        return asset;
    }

    public static CastleSheetRow[] BuildDefaultRows()
    {
        var list = new List<CastleSheetRow>();
        int nextId = 1001;

        // 위(북·중원): 대략 동북~중원
        var wei = new (string city, string gov)[]
        {
            ("낙양", "조비"), ("허창", "화흔"), ("업", "만총"), ("진류", "전예"), ("조군", "장료"),
            ("이릉", "주환"), ("평원", "장거"), ("북해", "공융"), ("서주", "장각"), ("태원", "고유"),
            ("상당", "하진"), ("진양", "양부"), ("하내", "사마의"), ("홍농", "종요"), ("관도", "허유"),
            ("연교", "전욱"), ("포판", "왕릉"), ("하거", "하후돈"), ("수춘", "하후연"), ("양주", "장흠"),
        };
        PlaceInRect(list, ref nextId, CountryId.Wei, wei, 0.2f, 10.8f, -1.2f, 6.2f, 0);

        // 촉(서남·한중·익주)
        var shu = new (string city, string gov)[]
        {
            ("성도", "제갈량"), ("한중", "위연"), ("자동", "관우"), ("건녕", "장의"), ("영안", "이엄"),
            ("융중", "마속"), ("절관", "왕평"), ("남중", "주환"), ("무양", "등지"), ("상운", "마대"),
            ("강주", "관흥"), ("번군", "장남"), ("운남", "이회"), ("건위", "구력"), ("강양", "조운"),
            ("백제", "황충"), ("음평", "법정"), ("미두", "장비"), ("치도", "유선"), ("남향", "비의"),
        };
        PlaceInRect(list, ref nextId, CountryId.Shu, shu, -10.8f, -0.3f, -6.5f, 4.2f, 100);

        // 오(동남·강동)
        var wu = new (string city, string gov)[]
        {
            ("건업", "육逊"), ("회계", "주연"), ("오군", "손권"), ("신도", "육기"), ("장사", "포륜"),
            ("과업", "여몽"), ("계양", "태사자"), ("여강", "손정"), ("시작", "주치"), ("근해", "전종"),
            ("강도", "육항"), ("무릉", "반장"), ("합비", "장소"), ("여요", "정봉"), ("동탁", "황개"),
            ("임해", "고당"), ("영가", "사마"), ("안고", "보연사"), ("교지", "사사"), ("평남", "정신"),
        };
        PlaceInRect(list, ref nextId, CountryId.Wu, wu, -0.5f, 10.5f, -6.8f, 2.5f, 200);

        return list.ToArray();
    }

    static void PlaceInRect(
        List<CastleSheetRow> list,
        ref int nextId,
        CountryId country,
        (string city, string gov)[] towns,
        float xMin,
        float xMax,
        float yMin,
        float yMax,
        int seedSalt)
    {
        int n = towns.Length;
        int cols = Mathf.Max(4, Mathf.CeilToInt(Mathf.Sqrt(n * 1.15f)));
        int rows = Mathf.CeilToInt(n / (float)cols);

        for (int i = 0; i < n; i++)
        {
            int row = i / cols;
            int col = i % cols;
            float u = (col + 0.5f) / cols;
            float v = (row + 0.5f) / rows;
            // 격자에 약간의 지터를 넣어 자연스럽게
            float jx = Jitter(seedSalt + i * 17 + 1);
            float jy = Jitter(seedSalt + i * 17 + 3);
            u = Mathf.Clamp01(u + (jx - 0.5f) * 0.12f);
            v = Mathf.Clamp01(v + (jy - 0.5f) * 0.12f);

            float x = Mathf.Lerp(xMin, xMax, u);
            float y = Mathf.Lerp(yMin, yMax, v);
            x = Mathf.Clamp(x, WorldMapLayout.MapWorldMinX, WorldMapLayout.MapWorldMaxX);
            y = Mathf.Clamp(y, WorldMapLayout.MapWorldMinY, WorldMapLayout.MapWorldMaxY);

            var (city, gov) = towns[i];
            int h = Hash(seedSalt + i * 131 + (int)country * 7);
            list.Add(new CastleSheetRow
            {
                castleId = nextId++,
                castleName = city,
                countryId = country,
                governorName = gov,
                army = 200 + (h % 1200),
                population = 40000 + (h % 90000),
                publicSentiment = 20 + (h % 75),
                castleValue = 3000 + (h % 14000),
                mapPosition = new Vector2(x, y),
                grade = (Grade)(i % 6),
            });
        }
    }

    static float Jitter(int seed)
    {
        int v = Hash(seed);
        return (v % 10000) / 10000f;
    }

    static int Hash(int x)
    {
        unchecked
        {
            x = (x ^ 61) ^ (x >> 16);
            x *= 9;
            x = x ^ (x >> 4);
            x *= 0x27d4eb2d;
            x = x ^ (x >> 15);
            return Mathf.Abs(x);
        }
    }
}
