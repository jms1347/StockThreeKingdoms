using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThreeKingdoms.WorldMap
{
    /// <summary>
    /// 50개 성 데이터 로드, CityNode 배치, LineRenderer 연결, 오버레이 연동.
    /// 씬에 빈 오브젝트만 두고 이 컴포넌트를 붙인 뒤 실행하면 UI·데이터를 자동 구성합니다.
    /// </summary>
    public class WorldMapManager : MonoBehaviour
    {
        const string ResCityNode = "WorldMap/CityNode";
        const string ResOverlay = "WorldMap/CityDetailOverlay";

        [Header("Optional — 비우면 Resources에서 로드")]
        [SerializeField] CityDatabase database;
        [SerializeField] GameObject cityNodePrefab;
        [SerializeField] CityDetailOverlay overlayInstance;

        [Header("자동 생성 UI")]
        [SerializeField] bool autoBootstrap = true;
        [SerializeField] Color canvasClearColor = new Color32(0x08, 0x0A, 0x12, 0xFF);

        [Header("연결선")]
        [SerializeField] Color edgeColor = new Color32(0x4A, 0x7C, 0xA8, 0x55);
        [SerializeField] float edgeWidth = 2.2f;

        readonly List<LineRenderer> edgeLines = new List<LineRenderer>();
        readonly List<CityNode> nodes = new List<CityNode>();

        RectTransform mapContent;
        Transform edgesRoot;
        Canvas rootCanvas;
        Camera uiCamera;

        void Awake()
        {
            if (database == null)
                database = CityDatabase.CreateRuntimeDefault();

            if (autoBootstrap)
                BootstrapWorldUi();

            if (cityNodePrefab == null)
                cityNodePrefab = Resources.Load<GameObject>(ResCityNode);

            if (overlayInstance == null)
            {
                var overlayGo = Resources.Load<GameObject>(ResOverlay);
                if (overlayGo != null && rootCanvas != null)
                {
                    var go = Instantiate(overlayGo, rootCanvas.transform, false);
                    overlayInstance = go.GetComponent<CityDetailOverlay>();
                }
            }

            if (mapContent == null || cityNodePrefab == null)
            {
                Debug.LogError("[WorldMapManager] mapContent 또는 CityNode 프리팹이 없습니다. Tools 메뉴로 프리팹을 생성하고 Resources에 복사했는지 확인하세요.");
                return;
            }

            BuildMap();
            WireOverlay();
        }

        void LateUpdate()
        {
            UpdateEdgePositions();
        }

        void BootstrapWorldUi()
        {
            if (Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            uiCamera = Camera.main;
            if (uiCamera == null)
            {
                var camGo = new GameObject("WorldMapCamera", typeof(Camera));
                uiCamera = camGo.GetComponent<Camera>();
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                uiCamera.backgroundColor = canvasClearColor;
                uiCamera.orthographic = true;
                uiCamera.orthographicSize = 5f;
                uiCamera.transform.position = new Vector3(0, 0, -10f);
                camGo.tag = "MainCamera";
            }

            rootCanvas = Object.FindObjectOfType<Canvas>();
            if (rootCanvas == null)
            {
                var canvasGo = new GameObject("WorldCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                rootCanvas = canvasGo.GetComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                rootCanvas.worldCamera = uiCamera;
                rootCanvas.planeDistance = 1f;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(canvasGo.transform, false);
                var bgRt = bg.GetComponent<RectTransform>();
                StretchFull(bgRt);
                var bgImg = bg.GetComponent<Image>();
                bgImg.color = new Color32(0x0A, 0x0C, 0x14, 0xFF);
                bgImg.raycastTarget = false;

                var vignette = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
                vignette.transform.SetParent(canvasGo.transform, false);
                var vigRt = vignette.GetComponent<RectTransform>();
                StretchFull(vigRt);
                var vigImg = vignette.GetComponent<Image>();
                vigImg.color = new Color32(0x00, 0x00, 0x00, 0x35);
                vigImg.raycastTarget = false;
            }
            else if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                rootCanvas.worldCamera = uiCamera;
                rootCanvas.planeDistance = 1f;
            }

            if (mapContent != null)
            {
                if (rootCanvas == null)
                    rootCanvas = mapContent.GetComponentInParent<Canvas>();
                EnsureEdgesRootUnderContent();
                return;
            }

            var scrollGo = new GameObject("WorldScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(rootCanvas.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            StretchFull(scrollRt);
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.02f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            StretchFull(vpRt);
            var vpImg = viewport.GetComponent<Image>();
            vpImg.color = new Color(1, 1, 1, 0.001f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("MapContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            mapContent = content.GetComponent<RectTransform>();
            mapContent.anchorMin = new Vector2(0f, 1f);
            mapContent.anchorMax = new Vector2(0f, 1f);
            mapContent.pivot = new Vector2(0f, 1f);
            mapContent.anchoredPosition = Vector2.zero;

            float maxX = 0f, maxY = 0f;
            for (int i = 0; i < CityDatabase.CityCount; i++)
            {
                var e = database.GetCity(i);
                maxX = Mathf.Max(maxX, e.MapPosition.x + 140f);
                maxY = Mathf.Max(maxY, e.MapPosition.y + 120f);
            }
            mapContent.sizeDelta = new Vector2(maxX + 80f, maxY + 80f);

            CreateEdgesRoot();

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = vpRt;
            sr.content = mapContent;
            sr.horizontal = true;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void EnsureEdgesRootUnderContent()
        {
            if (mapContent == null || edgesRoot != null) return;
            var existing = mapContent.Find("Edges");
            if (existing != null)
            {
                edgesRoot = existing;
                return;
            }
            CreateEdgesRoot();
        }

        void CreateEdgesRoot()
        {
            if (mapContent == null) return;
            var er = new GameObject("Edges", typeof(RectTransform));
            edgesRoot = er.transform;
            edgesRoot.SetParent(mapContent, false);
            StretchFull(er.GetComponent<RectTransform>());
            er.transform.SetAsFirstSibling();
        }

        void BuildMap()
        {
            if (mapContent != null && edgesRoot == null)
                EnsureEdgesRootUnderContent();

            foreach (var n in nodes)
                if (n != null) Destroy(n.gameObject);
            nodes.Clear();
            foreach (var lr in edgeLines)
                if (lr != null) Destroy(lr.gameObject);
            edgeLines.Clear();

            for (int i = 0; i < CityDatabase.CityCount; i++)
            {
                var entry = database.GetCity(i);
                var go = Instantiate(cityNodePrefab, mapContent);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(entry.MapPosition.x, -entry.MapPosition.y);

                var node = go.GetComponent<CityNode>();
                if (node != null)
                {
                    node.Apply(entry);
                    node.SetClickHandler(OnCityClicked);
                    nodes.Add(node);
                }
            }

            var edges = database.Edges;
            foreach (var edge in edges)
            {
                if (edge.CityA < 0 || edge.CityB < 0 || edge.CityA >= nodes.Count || edge.CityB >= nodes.Count)
                    continue;
                var lrGo = new GameObject($"Edge_{edge.CityA}_{edge.CityB}");
                lrGo.transform.SetParent(edgesRoot, false);
                var lr = lrGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = false;
                lr.positionCount = 2;
                lr.startWidth = edgeWidth;
                lr.endWidth = edgeWidth;
                lr.numCapVertices = 4;
                var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                var mat = new Material(sh);
                mat.color = edgeColor;
                lr.material = mat;
                lr.startColor = edgeColor;
                lr.endColor = edgeColor;
                lr.sortingOrder = 10;
                edgeLines.Add(lr);
            }
        }

        void UpdateEdgePositions()
        {
            var edges = database.Edges;
            int idx = 0;
            foreach (var edge in edges)
            {
                if (idx >= edgeLines.Count) break;
                if (edge.CityA < 0 || edge.CityB < 0 || edge.CityA >= nodes.Count || edge.CityB >= nodes.Count)
                {
                    idx++;
                    continue;
                }
                var a = nodes[edge.CityA].GetComponent<RectTransform>();
                var b = nodes[edge.CityB].GetComponent<RectTransform>();
                var lr = edgeLines[idx];
                lr.SetPosition(0, RectWorldCenter(a) + Vector3.forward * 0.02f);
                lr.SetPosition(1, RectWorldCenter(b) + Vector3.forward * 0.02f);
                idx++;
            }
        }

        static Vector3 RectWorldCenter(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
        }

        void OnCityClicked(CityEntry entry)
        {
            if (overlayInstance != null)
                overlayInstance.Show(entry);
        }

        void WireOverlay()
        {
            if (overlayInstance == null) return;
            overlayInstance.SupportClicked += OnOverlaySupport;
            overlayInstance.WithdrawClicked += OnOverlayWithdraw;
        }

        void OnDestroy()
        {
            if (overlayInstance != null)
            {
                overlayInstance.SupportClicked -= OnOverlaySupport;
                overlayInstance.WithdrawClicked -= OnOverlayWithdraw;
            }
        }

        void OnOverlaySupport(CityEntry e)
        {
            Debug.Log($"[World] SUPPORT / REINFORCE — {e.CityName} (war={e.IsWar})");
        }

        void OnOverlayWithdraw(CityEntry e)
        {
            Debug.Log($"[World] WITHDRAW / RETREAT — {e.CityName} (war={e.IsWar})");
        }
    }
}
