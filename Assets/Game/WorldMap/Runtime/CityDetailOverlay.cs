using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeKingdoms.WorldMap
{
    public class CityDetailOverlay : MonoBehaviour
    {
        [SerializeField] RectTransform panelRoot;
        [SerializeField] CanvasGroup rootCanvasGroup;
        [SerializeField] CanvasGroup dimCanvasGroup;
        [SerializeField] Image dimImage;
        [SerializeField] TextMeshProUGUI cityTitle;
        [SerializeField] TextMeshProUGUI warBadge;
        [SerializeField] TextMeshProUGUI governorName;
        [SerializeField] TextMeshProUGUI populationValue;
        [SerializeField] TextMeshProUGUI chartLabel;
        [SerializeField] Image chartFill;
        [SerializeField] Button supportButton;
        [SerializeField] Button withdrawButton;
        [SerializeField] Button dimButton;
        [SerializeField] TextMeshProUGUI supportLabel;
        [SerializeField] TextMeshProUGUI withdrawLabel;

        [SerializeField] float slideDuration = 0.38f;
        [SerializeField] AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        CityEntry current;
        bool visible;
        float hiddenY;
        Coroutine slideCo;

        void Awake()
        {
            if (dimButton != null)
                dimButton.onClick.AddListener(Hide);
            if (supportButton != null)
                supportButton.onClick.AddListener(OnSupportClicked);
            if (withdrawButton != null)
                withdrawButton.onClick.AddListener(OnWithdrawClicked);

            CacheHiddenY();
            if (panelRoot != null)
                panelRoot.anchoredPosition = new Vector2(panelRoot.anchoredPosition.x, hiddenY);
            if (rootCanvasGroup != null)
                rootCanvasGroup.alpha = 0f;
            if (dimCanvasGroup != null)
                dimCanvasGroup.alpha = 0f;
            else if (dimImage != null)
            {
                var c = dimImage.color;
                c.a = 0f;
                dimImage.color = c;
            }
            visible = false;
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (dimButton != null) dimButton.onClick.RemoveListener(Hide);
            if (supportButton != null) supportButton.onClick.RemoveListener(OnSupportClicked);
            if (withdrawButton != null) withdrawButton.onClick.RemoveListener(OnWithdrawClicked);
        }

        void CacheHiddenY()
        {
            if (panelRoot == null) return;
            hiddenY = -panelRoot.rect.height - 40f;
        }

        public void Show(CityEntry data)
        {
            current = data;
            gameObject.SetActive(true);
            CacheHiddenY();
            Inject(data);
            if (slideCo != null) StopCoroutine(slideCo);
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.blocksRaycasts = true;
                rootCanvasGroup.interactable = true;
            }
            slideCo = StartCoroutine(AnimateShow());
            visible = true;
        }

        public void Hide()
        {
            if (!visible) return;
            if (slideCo != null) StopCoroutine(slideCo);
            slideCo = StartCoroutine(AnimateHide());
        }

        void Inject(CityEntry data)
        {
            if (cityTitle != null) cityTitle.text = data.CityName;
            if (warBadge != null)
            {
                warBadge.gameObject.SetActive(data.IsWar);
                warBadge.text = "전쟁";
            }
            if (governorName != null) governorName.text = data.GovernorName;
            if (populationValue != null) populationValue.text = $"{data.Population:N0} 명";
            if (chartLabel != null) chartLabel.text = "민심";
            if (chartFill != null) chartFill.fillAmount = Mathf.Clamp01(data.PublicSentiment / 100f);

            if (data.IsWar)
            {
                if (supportLabel != null) supportLabel.text = "REINFORCE";
                if (withdrawLabel != null) withdrawLabel.text = "RETREAT";
            }
            else
            {
                if (supportLabel != null) supportLabel.text = "SUPPORT";
                if (withdrawLabel != null) withdrawLabel.text = "WITHDRAW";
            }
        }

        IEnumerator AnimateShow()
        {
            float t = 0f;
            panelRoot.anchoredPosition = new Vector2(panelRoot.anchoredPosition.x, hiddenY);
            while (t < slideDuration)
            {
                t += Time.deltaTime;
                float u = slideCurve.Evaluate(Mathf.Clamp01(t / slideDuration));
                float y = Mathf.Lerp(hiddenY, 0f, u);
                panelRoot.anchoredPosition = new Vector2(panelRoot.anchoredPosition.x, y);
                SetGroupsAlpha(u);
                yield return null;
            }
            panelRoot.anchoredPosition = new Vector2(panelRoot.anchoredPosition.x, 0f);
            SetGroupsAlpha(1f);
            slideCo = null;
        }

        IEnumerator AnimateHide()
        {
            float t = 0f;
            float startY = panelRoot.anchoredPosition.y;
            float startA = rootCanvasGroup != null ? rootCanvasGroup.alpha : 1f;
            while (t < slideDuration)
            {
                t += Time.deltaTime;
                float u = slideCurve.Evaluate(Mathf.Clamp01(t / slideDuration));
                float y = Mathf.Lerp(startY, hiddenY, u);
                panelRoot.anchoredPosition = new Vector2(panelRoot.anchoredPosition.x, y);
                float a = Mathf.Lerp(startA, 0f, u);
                SetGroupsAlpha(a);
                yield return null;
            }
            SetGroupsAlpha(0f);
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.blocksRaycasts = false;
                rootCanvasGroup.interactable = false;
            }
            visible = false;
            gameObject.SetActive(false);
            slideCo = null;
        }

        void SetGroupsAlpha(float a)
        {
            if (rootCanvasGroup != null) rootCanvasGroup.alpha = a;
            if (dimCanvasGroup != null) dimCanvasGroup.alpha = a * 0.95f;
            else if (dimImage != null)
            {
                var c = dimImage.color;
                c.a = a * 0.5f;
                dimImage.color = c;
            }
        }

        public event Action<CityEntry> SupportClicked;
        public event Action<CityEntry> WithdrawClicked;

        void OnSupportClicked()
        {
            SupportClicked?.Invoke(current);
        }

        void OnWithdrawClicked()
        {
            WithdrawClicked?.Invoke(current);
        }
    }
}
