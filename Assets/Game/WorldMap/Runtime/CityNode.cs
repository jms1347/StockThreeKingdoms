using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ThreeKingdoms.WorldMap
{
    [RequireComponent(typeof(Button))]
    public class CityNode : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image warBorder;
        [SerializeField] Image volumetricGlow;
        [SerializeField] Outline warOutline;
        [SerializeField] TextMeshProUGUI cityName;
        [SerializeField] TextMeshProUGUI sentiment;
        [SerializeField] TextMeshProUGUI changeRate;
        [SerializeField] Button button;

        static readonly Color Wei = new Color32(0x2B, 0x4A, 0x6F, 0xFF);
        static readonly Color Shu = new Color32(0x3D, 0x7A, 0x55, 0xFF);
        static readonly Color Wu = new Color32(0x8B, 0x3A, 0x3A, 0xFF);
        static readonly Color Neutral = new Color32(0x4A, 0x4F, 0x5A, 0xFF);

        static readonly Color UpColor = new Color32(0x5E, 0xD4, 0x9A, 0xFF);
        static readonly Color DownColor = new Color32(0xFF, 0x6B, 0x7A, 0xFF);
        static readonly Color FlatColor = new Color32(0xB8, 0xC0, 0xD8, 0xFF);

        CityEntry bound;
        UnityAction<CityEntry> clickHandler;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(OnClick);
        }

        void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClick);
        }

        void OnClick()
        {
            clickHandler?.Invoke(bound);
        }

        public void SetClickHandler(UnityAction<CityEntry> handler)
        {
            clickHandler = handler;
        }

        public void Apply(CityEntry data)
        {
            bound = data;
            if (background != null)
                background.color = FactionToColor(data.Faction);
            if (cityName != null)
                cityName.text = data.CityName;
            if (sentiment != null)
                sentiment.text = $"민심 {data.PublicSentiment:0.#}";
            if (changeRate != null)
            {
                float d = data.ChangeRatePercent;
                changeRate.text = d >= 0 ? $"+{d:0.##}%" : $"{d:0.##}%";
                changeRate.color = d > 0.01f ? UpColor : d < -0.01f ? DownColor : FlatColor;
            }

            SetWarVisual(data.IsWar, immediate: !data.IsWar);
        }

        public CityEntry GetData() => bound;

        Color FactionToColor(LordFaction f)
        {
            switch (f)
            {
                case LordFaction.Wei: return Wei;
                case LordFaction.Shu: return Shu;
                case LordFaction.Wu: return Wu;
                default: return Neutral;
            }
        }

        Coroutine warAnim;

        public void SetWarVisual(bool isWar, bool immediate = false)
        {
            if (warAnim != null)
            {
                StopCoroutine(warAnim);
                warAnim = null;
            }

            if (warBorder == null && warOutline == null) return;

            if (!isWar)
            {
                if (warBorder != null)
                {
                    var c = warBorder.color;
                    c.a = 0f;
                    warBorder.color = c;
                    warBorder.enabled = false;
                }
                if (warOutline != null)
                    warOutline.enabled = false;
                return;
            }

            if (warBorder != null)
                warBorder.enabled = true;
            if (warOutline != null)
            {
                warOutline.enabled = true;
                warOutline.effectColor = new Color32(0xFF, 0x66, 0x22, 0xE0);
                warOutline.effectDistance = new Vector2(3f, -3f);
            }

            if (!immediate)
                warAnim = StartCoroutine(WarPulse());
            else
                SetWarAlpha(0.85f);
        }

        System.Collections.IEnumerator WarPulse()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * 2.2f;
                float a = 0.45f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t));
                SetWarAlpha(a);
                yield return null;
            }
        }

        void SetWarAlpha(float a)
        {
            if (warBorder != null)
            {
                var c = warBorder.color;
                c.a = a;
                warBorder.color = c;
            }
        }
    }
}
