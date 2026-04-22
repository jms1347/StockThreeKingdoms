using UnityEngine;

/// <summary>출정 시 출발 성에서 목표 성으로 도로를 따라 이동하는 행군 표시. 도착 시 목표에 전쟁 플래그를 켭니다.</summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class MarchingTroopMarker : MonoBehaviour
{
    const float RoadZ = -0.028f;

    Castle _fromCastle;
    Castle _toCastle;
    string _generalName;
    int _troopCount;

    Vector3 _start;
    Vector3 _end;
    float _duration;
    float _elapsed;
    bool _arrived;

    SpriteRenderer _sr;

    public bool HasArrived => _arrived;
    public Castle TargetCastle => _toCastle;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = false;
        col.radius = 0.22f;
    }

    public void Begin(Castle from, Castle to, string generalName, int troopCount, float worldUnitsPerSecond)
    {
        _fromCastle = from;
        _toCastle = to;
        _generalName = string.IsNullOrEmpty(generalName) ? "(무장)" : generalName;
        _troopCount = Mathf.Max(0, troopCount);

        _start = from != null ? new Vector3(from.transform.position.x, from.transform.position.y, RoadZ) : Vector3.zero;
        _end = to != null ? new Vector3(to.transform.position.x, to.transform.position.y, RoadZ) : _start;

        float dist = Vector2.Distance(new Vector2(_start.x, _start.y), new Vector2(_end.x, _end.y));
        float speed = Mathf.Max(0.05f, worldUnitsPerSecond);
        _duration = Mathf.Max(0.35f, dist / speed);
        _elapsed = 0f;
        _arrived = false;

        transform.position = _start;
        transform.localScale = Vector3.one * 0.36f;

        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();
        _sr.sprite = MarchingTroopVisuals.GetOrCreateDotSprite();
        _sr.color = new Color(0.95f, 0.82f, 0.35f, 1f);
        _sr.sortingOrder = 25;

        var col = GetComponent<CircleCollider2D>();
        if (col != null)
            col.radius = 0.65f;
    }

    void Update()
    {
        if (_arrived || _duration <= 0f)
            return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        transform.position = Vector3.Lerp(_start, _end, t);

        if (t >= 1f)
            OnArrived();
    }

    void OnArrived()
    {
        if (_arrived) return;
        _arrived = true;
        transform.position = _end;

        if (_toCastle != null && _fromCastle != null)
        {
            if (_toCastle.Army < 1)
            {
                _fromCastle.AddArmy(_troopCount);
                _toCastle.SetArmy(0);
                Debug.Log($"[월드맵 출정] {_fromCastle.DisplayCastleName} → {_toCastle.DisplayCastleName} 도착 — 수비 병력 없음, 공격측 회군.");
                Destroy(gameObject);
                MapManager.InstanceOrNull?.RefreshAllCastleMapStatuses();
                return;
            }
            else if (WorldMapWarManager.InstanceOrNull != null &&
                     WorldMapWarManager.InstanceOrNull.TryBeginSiege(_fromCastle, _toCastle, _troopCount, this))
            {
                Debug.Log($"[월드맵 출정] {_fromCastle.DisplayCastleName} → {_toCastle.DisplayCastleName} 도착 — 공성 시작.");
            }
            else
            {
                _fromCastle.AddArmy(_troopCount);
                Debug.LogWarning(
                    $"[월드맵 출정] {_fromCastle.DisplayCastleName} → {_toCastle.DisplayCastleName} 도착했으나 공성 불가(전쟁 중 등) — 병력 회수.");
                Destroy(gameObject);
                MapManager.InstanceOrNull?.RefreshAllCastleMapStatuses();
                return;
            }
        }

        if (_sr != null)
            _sr.color = new Color(1f, 0.45f, 0.4f, 1f);

        MapManager.InstanceOrNull?.RefreshAllCastleMapStatuses();
    }

    void OnMouseDown()
    {
        if (_toCastle == null) return;

        if (_arrived &&
            WorldMapWarManager.InstanceOrNull != null &&
            WorldMapWarManager.InstanceOrNull.TryGetWarForMarch(this, out var siege))
        {
            MarchTroopInfoPopup.ShowSiegeBattle(
                siege.GeneralName,
                siege.AttackerTroops,
                siege.DefenderTroops,
                _toCastle.DisplayCastleName);
            return;
        }

        MarchTroopInfoPopup.ShowDetails(
            _generalName,
            _troopCount,
            _toCastle.DisplayCastleName,
            _arrived);
    }
}
