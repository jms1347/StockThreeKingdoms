using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>성 마스터의 <c>adjacentIdsRaw</c>를 따라 인접 성 사이에 선(도로)을 그립니다.</summary>
public class CastleRoadNetwork : MonoBehaviour
{
    [SerializeField] float lineWidth = 0.065f;
    [SerializeField] int sortingOrder = -8;
    [SerializeField] float lineZ = -0.04f;
    [SerializeField] float lineAlpha = 0.55f;
    [Tooltip("모든 도로를 동일한 회색으로 표시합니다.")]
    [SerializeField] Color roadGray = new Color(0.55f, 0.55f, 0.58f, 1f);

    Transform _roadRoot;

    public void RebuildFromCastles(Transform castleParent, CountryColorProvider _colorsIgnored)
    {
        EnsureRoadRoot(castleParent);
        ClearRoadChildren();

        var castles = castleParent.GetComponentsInChildren<Castle>(true);
        var byMaster = new Dictionary<string, Castle>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in castles)
        {
            if (c == null || string.IsNullOrEmpty(c.MasterId)) continue;
            byMaster[c.MasterId] = c;
        }

        if (byMaster.Count == 0) return;

        var drawn = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in castles)
        {
            if (c == null || string.IsNullOrEmpty(c.MasterId) || string.IsNullOrWhiteSpace(c.AdjacentIdsRaw))
                continue;

            var selfId = c.MasterId;
            foreach (var nid in SplitAdjacentIds(c.AdjacentIdsRaw))
            {
                if (string.Equals(selfId, nid, StringComparison.OrdinalIgnoreCase)) continue;
                if (!byMaster.TryGetValue(nid, out var other) || other == null) continue;

                string a = string.CompareOrdinal(selfId, nid) < 0 ? selfId : nid;
                string b = string.CompareOrdinal(selfId, nid) < 0 ? nid : selfId;
                var key = a + "|" + b;
                if (!drawn.Add(key)) continue;

                var p0 = c.transform.position;
                var p1 = other.transform.position;
                if ((p0 - p1).sqrMagnitude < 0.0001f) continue;

                var gray = roadGray;
                gray.a = lineAlpha;
                CreateEdge(p0, p1, gray);
            }
        }
    }

    static IEnumerable<string> SplitAdjacentIds(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        var parts = raw.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            var s = parts[i].Trim();
            if (!string.IsNullOrEmpty(s)) yield return s;
        }
    }

    void EnsureRoadRoot(Transform castleParent)
    {
        if (_roadRoot != null) return;
        var t = castleParent.Find("Roads");
        if (t == null)
        {
            var go = new GameObject("Roads");
            go.transform.SetParent(castleParent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            t = go.transform;
        }

        _roadRoot = t;
    }

    void ClearRoadChildren()
    {
        if (_roadRoot == null) return;
        for (int i = _roadRoot.childCount - 1; i >= 0; i--)
            Destroy(_roadRoot.GetChild(i).gameObject);
    }

    void CreateEdge(Vector3 worldA, Vector3 worldB, Color color)
    {
        var go = new GameObject("RoadEdge");
        go.transform.SetParent(_roadRoot, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(worldA.x, worldA.y, lineZ));
        lr.SetPosition(1, new Vector3(worldB.x, worldB.y, lineZ));
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.sortingOrder = sortingOrder;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        var shader = Shader.Find("Sprites/Default")
                       ?? Shader.Find("Unlit/Color")
                       ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader != null)
        {
            var mat = new Material(shader);
            lr.material = mat;
        }

        lr.startColor = color;
        lr.endColor = color;
    }
}
