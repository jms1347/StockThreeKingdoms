using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>월드 스페이스 Canvas + TMP UGUI로 성 이름·이벤트를 표시(3D TMP 한글 깨짐 방지).</summary>
public class CastleWorldHud : MonoBehaviour
{
    [Tooltip("아이콘 위 경계에서 추가로 띄울 간격(성 로컬 Y, 월드 단위).")]
    [SerializeField] float gapAboveIcon = 0.05f;

    [Tooltip("스프라이트를 못 찾을 때 이름판 기준 Y.")]
    [SerializeField] float fallbackHudBottomY = 0.38f;

    [SerializeField] Vector3 hudLocalScale = new Vector3(0.0042f, 0.0042f, 0.0042f);
    [SerializeField] Vector2 hudSize = new Vector2(520f, 128f);

    Canvas _canvas;
    Transform _hudRoot;
    TextMeshProUGUI _nameText;
    TextMeshProUGUI _statusText;
    Castle _owner;
    string _masterId;

    public void Bind(Castle owner, CountryColorProvider _)
    {
        _owner = owner;
        _masterId = owner != null ? owner.MasterId : string.Empty;
        EnsureHud();
        RepositionHudAboveIcon(owner);
        if (_nameText != null)
        {
            _nameText.text = owner != null ? owner.DisplayCastleName : string.Empty;
            WorldMapTmpFontSupport.Apply(_nameText);
        }

        Refresh();
    }

    /// <summary>이름판 하단이 스프라이트 상단에 붙도록 성 로컬 좌표를 맞춥니다.</summary>
    void RepositionHudAboveIcon(Castle castle)
    {
        if (_hudRoot == null || castle == null) return;
        float bottomY = ComputeHudBottomLocalY(castle);
        _hudRoot.localPosition = new Vector3(0f, bottomY + gapAboveIcon, -0.001f);
    }

    float ComputeHudBottomLocalY(Castle castle)
    {
        var sr = castle.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
            return fallbackHudBottomY;

        var b = sr.bounds;
        var worldTop = b.center + new Vector3(0f, b.extents.y, 0f);
        var lp = castle.transform.InverseTransformPoint(worldTop);
        return lp.y;
    }

    public void Refresh()
    {
        if (_statusText == null) return;

        var dm = DataManager.InstanceOrNull;
        WorldMapCastleLiveState.MergeSnapshot(dm, _masterId, _owner, out var snap);

        if (!snap.AnyFlag)
        {
            _statusText.gameObject.SetActive(false);
            return;
        }

        var sb = new StringBuilder(64);
        if (snap.pendingRumor)
            sb.Append("<color=#c9a8ff>행군·임박</color>");
        if (snap.isWar)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append("<color=#ff6b6b>전쟁중</color>");
        }

        if (snap.isDisaster)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append("<color=#ffb347>재해</color>");
        }

        if (snap.isFavorableEvent)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append("<color=#7bed9f>호재</color>");
        }

        _statusText.richText = true;
        _statusText.text = sb.ToString();
        _statusText.gameObject.SetActive(true);
        WorldMapTmpFontSupport.Apply(_statusText);
    }

    void EnsureHud()
    {
        if (_hudRoot != null) return;

        var root = new GameObject("WorldHud");
        root.transform.SetParent(transform, false);
        _hudRoot = root.transform;
        _hudRoot.localPosition = new Vector3(0f, fallbackHudBottomY, 0f);
        _hudRoot.localScale = hudLocalScale;

        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 92;
        _canvas.worldCamera = Camera.main;

        var cg = root.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = hudSize;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;

        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.LowerCenter;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(6, 6, 4, 4);
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        _nameText = CreateLine(rt, "CastleNameLine", 36f, FontStyles.Bold, Color.white);
        _statusText = CreateLine(rt, "CastleStatusLine", 28f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.95f));
    }

    static TextMeshProUGUI CreateLine(RectTransform parent, string name, float fontSize, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = fontSize + 18f;
        le.preferredHeight = fontSize + 18f;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.margin = Vector4.zero;
        WorldMapTmpFontSupport.Apply(t);
        return t;
    }

    void LateUpdate()
    {
        if (_canvas != null)
        {
            var cam = Camera.main;
            _canvas.worldCamera = cam;
        }

        if (_hudRoot != null && Camera.main != null)
            _hudRoot.rotation = Camera.main.transform.rotation;
    }
}
