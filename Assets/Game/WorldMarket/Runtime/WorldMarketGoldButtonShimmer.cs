using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 천하 주요 CTA 버튼 장식 — <b>진행률/게이지가 아닙니다.</b>
/// 얇은 밝은 띠가 좌→우로 한 번 스치는 <b>쉬머(하이라이트)</b> 연출입니다.
/// 스트립이 버튼 밖으로 드러나지 않도록 <see cref="RectMask2D"/>로 클립합니다.
/// </summary>
[DisallowMultipleComponent]
public class WorldMarketGoldButtonShimmer : MonoBehaviour
{
    static Sprite _whiteBlock;

    static Sprite WhiteBlockSprite()
    {
        if (_whiteBlock != null) return _whiteBlock;
        var tex = Texture2D.whiteTexture;
        _whiteBlock = Sprite.Create(
            tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return _whiteBlock;
    }

    [SerializeField] Image shimmerImage;
    RectTransform _rt;

    void Awake()
    {
        _rt = transform as RectTransform;
        var btnImg = GetComponent<Image>();
        if (btnImg != null)
            btnImg.color = new Color(0.42f, 0.30f, 0.06f, 0.98f);

        // 자식 쉬머 이미지가 버튼 사각형 밖으로 나가도 잘리도록
        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();

        if (shimmerImage == null)
        {
            var go = new GameObject("GoldShimmer", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            shimmerImage = go.GetComponent<Image>();
            shimmerImage.raycastTarget = false;
            shimmerImage.sprite = WhiteBlockSprite();
            if (shimmerImage.sprite == null && btnImg != null && btnImg.sprite != null)
                shimmerImage.sprite = btnImg.sprite;
            shimmerImage.type = Image.Type.Simple;
            shimmerImage.color = new Color(1f, 0.92f, 0.55f, 0.38f);
            var srt = go.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.12f);
            srt.anchorMax = new Vector2(0f, 0.88f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(Mathf.Max(24f, _rt.rect.width * 0.18f), 0f);
            srt.anchoredPosition = Vector2.zero;
        }
    }

    void OnEnable() => Play();

    void OnDisable()
    {
        if (shimmerImage != null)
            shimmerImage.rectTransform.DOKill();
    }

    void Play()
    {
        if (shimmerImage == null || _rt == null) return;
        shimmerImage.rectTransform.DOKill();

        LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
        Canvas.ForceUpdateCanvases();

        float w = Mathf.Max(80f, _rt.rect.width);
        float stripW = Mathf.Clamp(w * 0.22f, 28f, w * 0.45f);
        var tr = shimmerImage.rectTransform;
        tr.anchorMin = new Vector2(0f, 0.12f);
        tr.anchorMax = new Vector2(0f, 0.88f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(stripW, 0f);
        // 왼쪽 밖에서 시작해 오른쪽 밖으로 나감 — RectMask2D가 버튼 영역만 노출
        float startX = -stripW;
        float endX = w + stripW;
        tr.anchoredPosition = new Vector2(startX, 0f);
        tr.DOAnchorPosX(endX, 1.45f).SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
    }
}
