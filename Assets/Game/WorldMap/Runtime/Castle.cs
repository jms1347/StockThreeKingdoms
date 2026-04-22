using TMPro;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Castle : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] TextMeshPro nameLabel;

    [Header("맵 마커 크기 (등급 기반)")]
    [Tooltip("SS 등급(최상) 마커 월드 스케일 상한. 요청: 최대 약 0.5")]
    [SerializeField] float iconScaleMax = 0.5f;

    [Tooltip("D 등급(최하) 마커 월드 스케일 하한.")]
    [SerializeField] float iconScaleMin = 0.16f;

    [Tooltip("Sprite pixels per unit.")]
    [SerializeField] float iconSpritePpu = 42f;

    CastleSheetRow _definition;
    string _masterId;
    string _adjacentIdsRaw;
    string _displayCastleName;
    string _countryDisplayName;
    int _army;
    int _population;
    int _publicSentiment;
    int _castleValue;

    int _simWarDays;
    int _simDisasterDays;
    int _simFavorableDays;
    int _simRumorDays;

    public int SimWarDays => _simWarDays;
    public int SimDisasterDays => _simDisasterDays;
    public int SimFavorableDays => _simFavorableDays;
    public int SimRumorDays => _simRumorDays;

    public int CastleId => _definition.castleId;
    public string CastleName => _definition.castleName;

    /// <summary>맵 라벨·UI에 쓰는 표시용 이름(마스터 시트 이름 우선).</summary>
    public string DisplayCastleName => string.IsNullOrEmpty(_displayCastleName) ? CastleName : _displayCastleName;
    public CountryId CountryId => _definition.countryId;
    public string CountryDisplayName => _countryDisplayName;
    public string GovernorName => _definition.governorName;
    public int Army => _army;
    public int Population => _population;
    public int PublicSentiment => _publicSentiment;
    public int CastleValue => _castleValue;

    /// <summary>비어 있으면 인접 도로를 그리지 않습니다.</summary>
    public string MasterId => _masterId;

    public string AdjacentIdsRaw => _adjacentIdsRaw;

    public void Initialize(CastleSheetRow row, CountryColorProvider colors)
    {
        _definition = row;
        _masterId = string.IsNullOrWhiteSpace(row.masterId) ? string.Empty : row.masterId.Trim();
        _adjacentIdsRaw = row.adjacentIdsRaw ?? string.Empty;
        _countryDisplayName = colors != null
            ? colors.GetCountryDisplayName(row.countryId)
            : row.countryId.ToString();
        _army = row.army;
        _population = row.population;
        _publicSentiment = row.publicSentiment;
        _castleValue = row.castleValue;
        transform.position = new Vector3(row.mapPosition.x, row.mapPosition.y, 0f);
        gameObject.name = $"Castle_{row.castleId}_{row.castleName}";

        _displayCastleName = ResolveDisplayCastleName(row);

        if (spriteRenderer != null)
        {
            EnsureSprite();
            spriteRenderer.color = colors != null ? colors.GetColor(row.countryId) : Color.white;
            float s = IconScaleForGrade(row.grade);
            spriteRenderer.transform.localScale = Vector3.one * s;
        }

        FitColliderToIcon();

        if (nameLabel != null)
            nameLabel.gameObject.SetActive(false);

        var hud = GetComponent<CastleWorldHud>();
        if (hud == null)
            hud = gameObject.AddComponent<CastleWorldHud>();
        hud.Bind(this, colors);
    }

    static string ResolveDisplayCastleName(CastleSheetRow row)
    {
        if (row == null) return string.Empty;
        var dm = DataManager.InstanceOrNull;
        if (dm != null &&
            !string.IsNullOrWhiteSpace(row.masterId) &&
            dm.castleMasterDataMap != null &&
            dm.castleMasterDataMap.TryGetValue(row.masterId.Trim(), out var master) &&
            master != null)
        {
            var fromMaster = CastleMapDisplayName.FromMaster(master);
            if (!string.IsNullOrWhiteSpace(fromMaster))
                return fromMaster.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.castleName))
            return row.castleName.Trim();

        return string.IsNullOrWhiteSpace(row.masterId) ? $"#{row.castleId}" : row.masterId.Trim();
    }

    /// <summary>Grade: SS=0(최대) … D=5(최소). 높을수록 큰 마커.</summary>
    float IconScaleForGrade(Grade g)
    {
        int gi = Mathf.Clamp((int)g, 0, 5);
        float u = (5f - gi) / 5f;
        return Mathf.Lerp(iconScaleMin, iconScaleMax, u);
    }

    const int IconTexPixels = 56;
    static Texture2D s_sharedIconTex;

    void EnsureSprite()
    {
        if (spriteRenderer.sprite != null) return;

        var tex = GetOrCreateSharedWhiteTexture();
        float ppu = Mathf.Clamp(iconSpritePpu, 16f, 64f);
        spriteRenderer.sprite = Sprite.Create(
            tex,
            new Rect(0, 0, IconTexPixels, IconTexPixels),
            new Vector2(0.5f, 0.5f),
            ppu);
    }

    static Texture2D GetOrCreateSharedWhiteTexture()
    {
        if (s_sharedIconTex != null)
            return s_sharedIconTex;

        int n = IconTexPixels;
        s_sharedIconTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        s_sharedIconTex.wrapMode = TextureWrapMode.Clamp;
        s_sharedIconTex.filterMode = FilterMode.Bilinear;
        var c = Color.white;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
                s_sharedIconTex.SetPixel(x, y, c);
        }

        s_sharedIconTex.Apply(false, true);
        return s_sharedIconTex;
    }

    void FitColliderToIcon()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null || spriteRenderer == null || spriteRenderer.sprite == null) return;

        float wu = spriteRenderer.sprite.rect.width / spriteRenderer.sprite.pixelsPerUnit;
        float hu = spriteRenderer.sprite.rect.height / spriteRenderer.sprite.pixelsPerUnit;
        float sx = Mathf.Abs(spriteRenderer.transform.lossyScale.x);
        float sy = Mathf.Abs(spriteRenderer.transform.lossyScale.y);
        col.size = new Vector2(wu * sx * 0.92f, hu * sy * 0.92f);
        col.offset = Vector2.zero;
    }

    public void AddArmy(int delta) => _army = Mathf.Max(0, _army + delta);

    /// <summary>월드맵 공성 등에서 주둔 병력을 직접 설정합니다.</summary>
    public void SetArmy(int value) => _army = Mathf.Max(0, value);
    public void AddSentiment(int delta) => _publicSentiment = Mathf.Clamp(_publicSentiment + delta, 0, 100);
    public void AddCastleValue(int delta) => _castleValue = Mathf.Max(0, _castleValue + delta);

    public void ApplyArmyPercentLoss(int percent)
    {
        if (percent <= 0) return;
        int loss = Mathf.Max(1, Mathf.RoundToInt(_army * (percent / 100f)));
        _army = Mathf.Max(0, _army - loss);
    }

    public void ApplyPopulationPercentLoss(int percent)
    {
        if (percent <= 0) return;
        int loss = Mathf.Max(1, Mathf.RoundToInt(_population * (percent / 100f)));
        _population = Mathf.Max(0, _population - loss);
    }

    public void TickSimulationCounters()
    {
        if (_simWarDays > 0) _simWarDays--;
        if (_simDisasterDays > 0) _simDisasterDays--;
        if (_simFavorableDays > 0) _simFavorableDays--;
        if (_simRumorDays > 0) _simRumorDays--;
    }

    public void AddSimWarDays(int days)
    {
        if (days <= 0) return;
        _simWarDays = Mathf.Max(_simWarDays, days);
    }

    public void AddSimDisasterDays(int days)
    {
        if (days <= 0) return;
        _simDisasterDays = Mathf.Max(_simDisasterDays, days);
    }

    public void AddSimFavorableDays(int days)
    {
        if (days <= 0) return;
        _simFavorableDays = Mathf.Max(_simFavorableDays, days);
    }

    public void AddSimRumorDays(int days)
    {
        if (days <= 0) return;
        _simRumorDays = Mathf.Max(_simRumorDays, days);
    }

    void OnMouseDown()
    {
        if (MapManager.InstanceOrNull != null)
            MapManager.InstanceOrNull.SelectCastle(this);
    }
}
