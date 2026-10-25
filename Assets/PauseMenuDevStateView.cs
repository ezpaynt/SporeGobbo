using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PauseMenuDevStateView : MonoBehaviour
{
    TMP_Text leftContent;
    TMP_Text rightContent;

    public void Build(RectTransform root, TMP_FontAsset font)
    {
        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>(); viewport.SetParent(root, false); Stretch(viewport, 12f);
        viewportObject.GetComponent<Image>().color = new Color(0.055f, 0.16f, 0.09f, 0.98f);
        GameObject contentObject = new GameObject("DevStateColumns", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRect = contentObject.GetComponent<RectTransform>(); contentRect.SetParent(viewport, false);
        contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = Vector2.one; contentRect.pivot = new Vector2(.5f, 1f);
        contentRect.offsetMin = new Vector2(18f, 0f); contentRect.offsetMax = new Vector2(-18f, 0f);
        HorizontalLayoutGroup columns = contentObject.GetComponent<HorizontalLayoutGroup>();
        columns.spacing = 28f; columns.childControlHeight = true; columns.childControlWidth = true;
        columns.childForceExpandHeight = false; columns.childForceExpandWidth = true;
        ContentSizeFitter contentFit = contentObject.GetComponent<ContentSizeFitter>(); contentFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        leftContent = CreateColumn("LeftColumn", contentRect, font);
        rightContent = CreateColumn("RightColumn", contentRect, font);
        ScrollRect scroll = gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = contentRect;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45f;
        Refresh();
    }

    public void Refresh()
    {
        if (leftContent == null || rightContent == null) return;
        List<DevStateSection> left = new List<DevStateSection>();
        List<DevStateSection> right = new List<DevStateSection>();
        foreach (DevStateSection section in DevStateSnapshot.Build())
        {
            bool biographyOrCamp = section.heading.StartsWith("LEADER") || section.heading.StartsWith("BUDDY") ||
                section.heading == "DEATHS / MEMORIALS" || section.heading == "CAMP";
            (biographyOrCamp ? right : left).Add(section);
        }
        leftContent.text = DevStateSnapshot.Format(left);
        rightContent.text = DevStateSnapshot.Format(right);
    }

    static TMP_Text CreateColumn(string name, Transform parent, TMP_FontAsset font)
    {
        GameObject column = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        column.transform.SetParent(parent, false);
        LayoutElement width = column.GetComponent<LayoutElement>(); width.flexibleWidth = 1f; width.minWidth = 0f;
        TextMeshProUGUI text = column.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = 16f;
        text.color = new Color(.93f, .9f, .78f, 1f); text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect, float inset)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset); }
}
