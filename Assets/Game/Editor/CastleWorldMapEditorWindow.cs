#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 성 좌표(posX,posY)를 0~1000 맵 위에서 드래그로 조정. 변경 시 CastleMasterDataSo에 SetDirty 후 저장.
/// </summary>
public class CastleWorldMapEditorWindow : EditorWindow
{
    const string CastleSoPath = "Assets/Game/0Splash/Script/SO/So/CastleMasterDataSo.asset";
    const string RegionSoPath = "Assets/Game/0Splash/Script/SO/So/RegionMasterDataSo.asset";

    CastleMasterDataSo _castleSo;
    RegionMasterDataSo _regionSo;
    Dictionary<string, RegionMasterData> _castleIdToRegion;
    Vector2 _scroll;
    float _mapScale = 0.65f;
    int _selectedIndex = -1;
    bool _dragging;
    Vector2 _dragLastGui;
    bool _showCastleNames = true;
    bool _showLegend = true;
    bool _showRoads = true;

    GUIStyle _nameLabelStyle;

    [MenuItem("StockThreeKingdoms/성 지도 에디터 창", false, 0)]
    static void Open()
    {
        var w = GetWindow<CastleWorldMapEditorWindow>();
        w.titleContent = new GUIContent("성 지도");
        w.minSize = new Vector2(520, 520);
    }

    void OnEnable()
    {
        _castleSo = AssetDatabase.LoadAssetAtPath<CastleMasterDataSo>(CastleSoPath);
        _regionSo = AssetDatabase.LoadAssetAtPath<RegionMasterDataSo>(RegionSoPath);
        _castleIdToRegion = null;
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        _castleSo = (CastleMasterDataSo)EditorGUILayout.ObjectField("Castle SO", _castleSo, typeof(CastleMasterDataSo), false);
        _regionSo = (RegionMasterDataSo)EditorGUILayout.ObjectField("Region SO (지역명 표시용)", _regionSo, typeof(RegionMasterDataSo), false);
        if (EditorGUI.EndChangeCheck())
            _castleIdToRegion = null;

        if (_castleSo == null || _castleSo.list == null)
        {
            EditorGUILayout.HelpBox("CastleMasterDataSo를 지정하세요.", MessageType.Warning);
            return;
        }

        _mapScale = EditorGUILayout.Slider("맵 스케일", _mapScale, 0.35f, 1.2f);
        _showCastleNames = EditorGUILayout.ToggleLeft("성 이름 표시 (initialNationId 색)", _showCastleNames);
        _showLegend = EditorGUILayout.ToggleLeft("나라 범례", _showLegend);
        _showRoads = EditorGUILayout.ToggleLeft("인접 연결선 (adjacentIdsRaw)", _showRoads);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("SO에 변경사항 반영(SetDirty)"))
            PersistDirty();
        if (GUILayout.Button("디스크 저장(SaveAssets)"))
        {
            PersistDirty();
            AssetDatabase.SaveAssets();
        }
        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        float mapPx = 1000f * _mapScale;
        Rect mapRect = GUILayoutUtility.GetRect(mapPx, mapPx, GUILayout.ExpandWidth(false));
        DrawGrid(mapRect);
        DrawAdjacencyEdges(mapRect);
        HandleInput(mapRect);
        DrawCastles(mapRect);
        EditorGUILayout.EndScrollView();

        if (_showLegend)
            DrawNationLegend();

        EditorGUILayout.Space(6);
        DrawInspectorSlot();
    }

    void DrawInspectorSlot()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _castleSo.list.Count) return;
        var c = _castleSo.list[_selectedIndex];
        if (c == null) return;
        string title = EditorCastleTitle(c);
        string sub = EditorCastleRegionSubtitle(c, title);
        EditorGUILayout.LabelField($"선택: {title}", EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(sub))
            EditorGUILayout.LabelField(sub, EditorStyles.miniLabel);
        EditorGUI.BeginChangeCheck();
        float x = EditorGUILayout.FloatField("posX", c.posX);
        float y = EditorGUILayout.FloatField("posY", c.posY);
        if (EditorGUI.EndChangeCheck())
        {
            c.posX = Mathf.Clamp(x, 0f, 1000f);
            c.posY = Mathf.Clamp(y, 0f, 1000f);
            EditorUtility.SetDirty(_castleSo);
        }
    }

    void DrawGrid(Rect mapRect)
    {
        EditorGUI.DrawRect(mapRect, new Color(0.12f, 0.14f, 0.18f, 1f));
        Handles.BeginGUI();
        var cDim = new Color(0.25f, 0.28f, 0.32f, 1f);
        for (float t = 0; t <= 1000f; t += 100f)
        {
            float px = mapRect.x + t * _mapScale;
            EditorGUI.DrawRect(new Rect(px, mapRect.y, 1, mapRect.height), cDim);
            float py = mapRect.y + t * _mapScale;
            EditorGUI.DrawRect(new Rect(mapRect.x, py, mapRect.width, 1), cDim);
        }
        // 낙양 중심 가이드 (C01)
        float cx = mapRect.x + 500f * _mapScale;
        float cy = mapRect.y + (1000f - 500f) * _mapScale;
        EditorGUI.DrawRect(new Rect(cx - 1, mapRect.y, 2, mapRect.height), new Color(0.9f, 0.75f, 0.2f, 0.35f));
        EditorGUI.DrawRect(new Rect(mapRect.x, cy - 1, mapRect.width, 2), new Color(0.9f, 0.75f, 0.2f, 0.35f));
        Handles.EndGUI();
    }

    /// <summary><see cref="CastleMasterData.adjacentIdsRaw"/> 기준으로 성 사이를 선으로 연결합니다. 양방향 중복은 한 번만 그립니다.</summary>
    void DrawAdjacencyEdges(Rect mapRect)
    {
        if (!_showRoads || _castleSo?.list == null) return;

        var byId = new Dictionary<string, CastleMasterData>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _castleSo.list.Count; i++)
        {
            var c = _castleSo.list[i];
            if (c == null || string.IsNullOrWhiteSpace(c.id)) continue;
            byId[c.id.Trim()] = c;
        }

        var drawn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string selId = _selectedIndex >= 0 && _selectedIndex < _castleSo.list.Count
            ? _castleSo.list[_selectedIndex]?.id?.Trim()
            : null;

        float lineW = Mathf.Lerp(1.2f, 2.4f, Mathf.InverseLerp(0.35f, 1.2f, _mapScale));

        Handles.BeginGUI();
        foreach (var c in _castleSo.list)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.id)) continue;
            string a = c.id.Trim();
            if (!byId.TryGetValue(a, out var nodeA)) continue;

            var neighbors = nodeA.GetAdjacentIds();
            if (neighbors == null || neighbors.Count == 0) continue;

            for (int n = 0; n < neighbors.Count; n++)
            {
                string b = neighbors[n]?.Trim();
                if (string.IsNullOrEmpty(b) || !byId.TryGetValue(b, out var nodeB)) continue;

                string lo = string.CompareOrdinal(a, b) < 0 ? a : b;
                string hi = string.CompareOrdinal(a, b) < 0 ? b : a;
                string edgeKey = lo + "|" + hi;
                if (!drawn.Add(edgeKey)) continue;

                Vector2 pA = WorldToGui(mapRect, nodeA.posX, nodeA.posY);
                Vector2 pB = WorldToGui(mapRect, nodeB.posX, nodeB.posY);
                var vA = new Vector3(pA.x, pA.y, 0f);
                var vB = new Vector3(pB.x, pB.y, 0f);

                bool highlight = !string.IsNullOrEmpty(selId) &&
                                 (string.Equals(a, selId, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(b, selId, StringComparison.OrdinalIgnoreCase));
                Color col = highlight
                    ? new Color(1f, 0.88f, 0.25f, 0.65f)
                    : new Color(0.72f, 0.76f, 0.82f, 0.28f);
                Handles.color = col;
                Handles.DrawAAPolyLine(lineW, vA, vB);
            }
        }

        Handles.EndGUI();
    }

    void HandleInput(Rect mapRect)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && mapRect.Contains(e.mousePosition))
        {
            _dragging = TryPickCastle(mapRect, e.mousePosition, out int idx);
            _dragLastGui = e.mousePosition;
            if (_dragging && idx >= 0)
            {
                _selectedIndex = idx;
                e.Use();
                Repaint();
            }
        }
        else if (e.type == EventType.MouseDrag && _dragging && _selectedIndex >= 0)
        {
            Vector2 delta = e.mousePosition - _dragLastGui;
            _dragLastGui = e.mousePosition;
            var c = _castleSo.list[_selectedIndex];
            if (c != null && _mapScale > 0.001f)
            {
                c.posX = Mathf.Clamp(c.posX + delta.x / _mapScale, 0f, 1000f);
                c.posY = Mathf.Clamp(c.posY - delta.y / _mapScale, 0f, 1000f);
                EditorUtility.SetDirty(_castleSo);
            }
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp)
        {
            if (_dragging)
            {
                _dragging = false;
                PersistDirty();
            }
        }
    }

    bool TryPickCastle(Rect mapRect, Vector2 mouse, out int index)
    {
        index = -1;
        float best = 400f;
        for (int i = 0; i < _castleSo.list.Count; i++)
        {
            var c = _castleSo.list[i];
            if (c == null) continue;
            Vector2 p = WorldToGui(mapRect, c.posX, c.posY);
            float d = (p - mouse).sqrMagnitude;
            if (d < best)
            {
                best = d;
                index = i;
            }
        }
        return index >= 0 && best < 18f * 18f;
    }

    Vector2 WorldToGui(Rect mapRect, float wx, float wy)
    {
        float gx = mapRect.x + wx * _mapScale;
        float gy = mapRect.y + (1000f - wy) * _mapScale;
        return new Vector2(gx, gy);
    }

    void DrawCastles(Rect mapRect)
    {
        EnsureNameLabelStyle();
        var selId = _selectedIndex >= 0 && _selectedIndex < _castleSo.list.Count ? _castleSo.list[_selectedIndex]?.id : null;
        float dotHalf = Mathf.Lerp(3f, 6f, Mathf.InverseLerp(0.35f, 1.2f, _mapScale));

        for (int i = 0; i < _castleSo.list.Count; i++)
        {
            var c = _castleSo.list[i];
            if (c == null) continue;
            Vector2 p = WorldToGui(mapRect, c.posX, c.posY);
            bool sel = c.id == selId;
            Color nationCol = NationColor(c.initialNationId);
            float h = sel ? dotHalf + 2f : dotHalf;

            if (sel)
            {
                EditorGUI.DrawRect(new Rect(p.x - h - 2, p.y - h - 2, (h + 2) * 2, (h + 2) * 2),
                    new Color(1f, 0.92f, 0.25f, 0.95f));
            }

            EditorGUI.DrawRect(new Rect(p.x - h, p.y - h, h * 2, h * 2), nationCol);

            if (!_showCastleNames)
            {
                if (sel)
                {
                    var idStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
                    GUI.Label(new Rect(p.x + h + 4, p.y - 9, 160, 18), EditorCastleTitle(c), idStyle);
                }
                continue;
            }

            string label = EditorCastleTitle(c);
            _nameLabelStyle.normal.textColor = nationCol;
            float labelW = Mathf.Clamp(28f + label.Length * 7f * Mathf.Max(0.85f, _mapScale), 72f, 200f);
            var lr = new Rect(p.x - labelW * 0.5f, p.y + h + 2f, labelW, 36f);
            GUI.Label(lr, label, _nameLabelStyle);
        }
    }

    void EnsureCastleRegionLookup()
    {
        if (_castleIdToRegion != null) return;
        _castleIdToRegion = new Dictionary<string, RegionMasterData>(StringComparer.OrdinalIgnoreCase);
        if (_regionSo?.list == null) return;
        foreach (var r in _regionSo.list)
        {
            if (r?.castleIds == null) continue;
            foreach (var cid in r.castleIds)
            {
                if (string.IsNullOrWhiteSpace(cid)) continue;
                _castleIdToRegion[cid.Trim()] = r;
            }
        }
    }

    RegionMasterData FindRegionByCode(string code)
    {
        if (_regionSo?.list == null || string.IsNullOrWhiteSpace(code)) return null;
        code = code.Trim();
        for (int i = 0; i < _regionSo.list.Count; i++)
        {
            var r = _regionSo.list[i];
            if (r != null && string.Equals(r.id, code, StringComparison.OrdinalIgnoreCase))
                return r;
        }
        return null;
    }

    string EditorCastleTitle(CastleMasterData c)
    {
        if (c == null) return "";
        EnsureCastleRegionLookup();
        RegionMasterData byCastle = null;
        if (!string.IsNullOrWhiteSpace(c.id))
            _castleIdToRegion?.TryGetValue(c.id.Trim(), out byCastle);
        RegionMasterData byRid = FindRegionByCode((c.regionId ?? "").Trim());
        return CastleDisplayLabels.GetCastleTitle(c, byCastle, byRid);
    }

    string EditorCastleRegionSubtitle(CastleMasterData c, string title)
    {
        if (c == null) return "";
        EnsureCastleRegionLookup();
        RegionMasterData byCastle = null;
        if (!string.IsNullOrWhiteSpace(c.id))
            _castleIdToRegion?.TryGetValue(c.id.Trim(), out byCastle);
        RegionMasterData byRid = FindRegionByCode((c.regionId ?? "").Trim());
        return CastleDisplayLabels.GetRegionSubtitle(c, byCastle, byRid, title);
    }

    /// <summary><see cref="CastleMasterData.initialNationId"/> → 점/라벨 색 (위·촉·오·기타).</summary>
    public static Color NationColor(string initialNationId)
    {
        if (string.IsNullOrWhiteSpace(initialNationId))
            return new Color(0.55f, 0.56f, 0.58f);

        string t = initialNationId.Trim();
        if (int.TryParse(t, out int n) && Enum.IsDefined(typeof(Faction), n))
            return NationColor((Faction)n);
        if (Enum.TryParse(t, true, out Faction f))
            return NationColor(f);

        return new Color(0.55f, 0.56f, 0.58f);
    }

    static Color NationColor(Faction f)
    {
        switch (f)
        {
            case Faction.WEI:
                return new Color(0.38f, 0.62f, 0.98f);
            case Faction.SHU:
                return new Color(0.40f, 0.88f, 0.52f);
            case Faction.WU:
                return new Color(0.98f, 0.42f, 0.38f);
            case Faction.OTHERS:
                return new Color(0.78f, 0.62f, 0.95f);
            default:
                return new Color(0.52f, 0.54f, 0.58f);
        }
    }

    void EnsureNameLabelStyle()
    {
        if (_nameLabelStyle != null) return;
        _nameLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 10,
            wordWrap = true
        };
    }

    void DrawNationLegend()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("나라 색", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        LegendSwatch("위(魏)", NationColor(Faction.WEI));
        LegendSwatch("촉(蜀)", NationColor(Faction.SHU));
        LegendSwatch("오(吳)", NationColor(Faction.WU));
        LegendSwatch("기타", NationColor(Faction.OTHERS));
        LegendSwatch("NONE", NationColor(Faction.NONE));
        EditorGUILayout.EndHorizontal();
    }

    static void LegendSwatch(string title, Color col)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(88));
        EditorGUILayout.LabelField(title, EditorStyles.miniLabel, GUILayout.Width(86));
        var r = GUILayoutUtility.GetRect(64, 12, GUILayout.Width(64));
        EditorGUI.DrawRect(r, col);
        EditorGUILayout.EndVertical();
    }

    void PersistDirty()
    {
        if (_castleSo != null)
            EditorUtility.SetDirty(_castleSo);
    }
}
#endif
