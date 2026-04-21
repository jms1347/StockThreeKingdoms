#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GlobalUIManager 프리팹에 프로필(아바타·작위 뱃지·닉네임 세로 스택) 계층을 추가하고 참조를 연결합니다.
/// 한 번 실행 후 인스펙터에서 미세 조정하면 됩니다.
/// </summary>
public static class GlobalUIManagerProfileLayoutPatcher
{
    const string PrefabPath = "Assets/Game/CommonUI/Prefabs/GlobalUIManager.prefab";

    [MenuItem("StockThreeKingdoms/UI/GlobalUIManager — 프로필 레이아웃 패치", false, 11)]
    public static void PatchPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var profileBox = root.transform.Find("TopBar/ProfileBox");
            if (profileBox == null)
            {
                EditorUtility.DisplayDialog("패치", "TopBar/ProfileBox 를 찾을 수 없습니다.", "OK");
                return;
            }

            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                EditorUtility.DisplayDialog("패치", "TMP 기본 폰트가 없습니다.", "OK");
                return;
            }

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            Transform userNameTf = profileBox.Find("UserNameText");
            if (userNameTf == null)
            {
                EditorUtility.DisplayDialog("패치", "UserNameText 를 찾을 수 없습니다.", "OK");
                return;
            }

            Transform profileColumn = profileBox.Find("ProfileTextColumn");
            if (profileColumn == null)
            {
                var colGo = new GameObject("ProfileTextColumn", typeof(RectTransform));
                colGo.transform.SetParent(profileBox, false);
                profileColumn = colGo.transform;
                var v = colGo.AddComponent<VerticalLayoutGroup>();
                v.spacing = 6f;
                v.padding = new RectOffset(0, 0, 2, 0);
                v.childAlignment = TextAnchor.UpperLeft;
                v.childControlWidth = true;
                v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
                var leCol = colGo.AddComponent<LayoutElement>();
                leCol.flexibleWidth = 1f;
                leCol.minWidth = 160f;
            }

            if (userNameTf.parent != profileColumn)
                userNameTf.SetParent(profileColumn, false);

            Transform titleBadgeTf = profileColumn.Find("TitleBadge");
            Image titleBgImg = null;
            TextMeshProUGUI titleTmp = null;
            Outline titleOutline = null;
            if (titleBadgeTf == null)
            {
                var badge = new GameObject("TitleBadge", typeof(RectTransform), typeof(Image), typeof(LayoutElement),
                    typeof(HorizontalLayoutGroup));
                badge.transform.SetParent(profileColumn, false);
                badge.transform.SetSiblingIndex(0);
                titleBadgeTf = badge.transform;
                titleBgImg = badge.GetComponent<Image>();
                titleBgImg.sprite = uiSprite;
                titleBgImg.type = Image.Type.Sliced;
                titleBgImg.color = new Color(0.22f, 0.24f, 0.28f, 0.88f);
                var badgeLe = badge.GetComponent<LayoutElement>();
                badgeLe.minHeight = 28f;
                badgeLe.preferredHeight = 30f;
                badgeLe.flexibleWidth = 1f;
                var hg = badge.GetComponent<HorizontalLayoutGroup>();
                hg.padding = new RectOffset(10, 10, 4, 6);
                hg.childAlignment = TextAnchor.MiddleLeft;
                hg.childControlWidth = true;
                hg.childControlHeight = true;
                hg.childForceExpandWidth = true;
                hg.childForceExpandHeight = true;

                var titleGo = new GameObject("TitleBadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(badge.transform, false);
                titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
                titleTmp.font = font;
                titleTmp.fontSize = 18f;
                titleTmp.fontStyle = FontStyles.Bold;
                titleTmp.alignment = TextAlignmentOptions.Left;
                titleTmp.color = new Color(0.92f, 0.93f, 0.95f);
                titleTmp.text = "평민";
                titleTmp.enableAutoSizing = true;
                titleTmp.fontSizeMin = 14;
                titleTmp.fontSizeMax = 18;
                titleTmp.overflowMode = TextOverflowModes.Ellipsis;
                titleTmp.raycastTarget = false;
                var titleRt = titleGo.GetComponent<RectTransform>();
                titleRt.anchorMin = Vector2.zero;
                titleRt.anchorMax = Vector2.one;
                titleRt.offsetMin = Vector2.zero;
                titleRt.offsetMax = Vector2.zero;

                titleOutline = badge.AddComponent<Outline>();
                titleOutline.effectColor = new Color(0.45f, 0.48f, 0.52f, 0.75f);
                titleOutline.effectDistance = new Vector2(0.65f, -0.65f);
            }
            else
            {
                titleBgImg = titleBadgeTf.GetComponent<Image>();
                titleTmp = titleBadgeTf.GetComponentInChildren<TextMeshProUGUI>(true);
                titleOutline = titleBadgeTf.GetComponent<Outline>();
            }

            Transform avatarTf = profileBox.Find("AvatarIcon");
            Image portraitImg = null;
            Outline portraitOutline = null;
            if (avatarTf != null)
            {
                if (avatarTf.GetComponent<RectMask2D>() == null)
                    avatarTf.gameObject.AddComponent<RectMask2D>();

                var portraitTf = avatarTf.Find("Portrait");
                if (portraitTf == null)
                {
                    var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image), typeof(Outline));
                    portraitGo.transform.SetParent(avatarTf, false);
                    portraitGo.transform.SetAsFirstSibling();
                    portraitImg = portraitGo.GetComponent<Image>();
                    portraitImg.sprite = uiSprite;
                    portraitImg.type = Image.Type.Sliced;
                    portraitImg.preserveAspect = true;
                    portraitImg.raycastTarget = false;
                    portraitImg.color = new Color(0.25f, 0.28f, 0.32f, 1f);
                    var prt = portraitGo.GetComponent<RectTransform>();
                    prt.anchorMin = Vector2.zero;
                    prt.anchorMax = Vector2.one;
                    prt.offsetMin = new Vector2(4f, 4f);
                    prt.offsetMax = new Vector2(-4f, -4f);
                    portraitOutline = portraitGo.GetComponent<Outline>();
                    portraitOutline.effectColor = new Color(0.4f, 0.45f, 0.5f, 0.75f);
                    portraitOutline.effectDistance = new Vector2(0.9f, -0.9f);
                }
                else
                {
                    portraitImg = portraitTf.GetComponent<Image>();
                    portraitOutline = portraitTf.GetComponent<Outline>();
                }
            }

            var foodRow = root.transform.Find("TopBar/ResourceBox/FoodRow/ValueText");
            TextMeshProUGUI foodTmp = null;
            if (foodRow != null)
                foodTmp = foodRow.GetComponent<TextMeshProUGUI>();

            var mgr = root.GetComponent<GlobalUIManager>();
            if (mgr != null)
            {
                var so = new SerializedObject(mgr);
                if (portraitImg != null) so.FindProperty("userPortraitImage").objectReferenceValue = portraitImg;
                if (titleBgImg != null) so.FindProperty("titleBadgeBackground").objectReferenceValue = titleBgImg;
                if (titleTmp != null) so.FindProperty("titleBadgeText").objectReferenceValue = titleTmp;
                if (titleOutline != null) so.FindProperty("titleBadgeOutline").objectReferenceValue = titleOutline;
                if (portraitOutline != null) so.FindProperty("avatarPortraitOutline").objectReferenceValue = portraitOutline;
                if (foodTmp != null) so.FindProperty("marchPointsText").objectReferenceValue = foodTmp;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("GlobalUIManager", "프로필 레이아웃 패치 완료.\n(행군 MP는 FoodRow ValueText에 연결됨)", "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
