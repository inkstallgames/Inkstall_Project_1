using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Applies an industry-standard "Edit Controls" chrome to HudCustomizerpanel:
/// dark floating toolbar, semantic action colors, helper tip, clearer dim.
/// Safe to call every time the panel opens; keeps existing Save/Reset/Close wiring.
/// </summary>
public static class HUDCustomizerPanelChrome
{
    // Xeno Attack palette — yellow primary, dark sci-fi chrome
    // Fully opaque so lobby chrome (HOST / JOIN) cannot show through.
    static readonly Color DimOverlay      = new Color(0.05f, 0.07f, 0.1f, 1f);
    static readonly Color PreviewClear    = new Color(0f, 0f, 0f, 0f);
    static readonly Color ToolbarBg       = new Color(0.07f, 0.09f, 0.13f, 0.94f);
    static readonly Color AccentYellow    = new Color(1f, 0.84f, 0.16f, 1f);
    static readonly Color SaveBg          = new Color(1f, 0.84f, 0.16f, 1f);
    static readonly Color SaveText        = new Color(0.08f, 0.08f, 0.1f, 1f);
    static readonly Color ResetBg         = new Color(0.16f, 0.18f, 0.24f, 1f);
    static readonly Color ResetText       = new Color(0.92f, 0.94f, 0.98f, 1f);
    static readonly Color CloseBg         = new Color(0.55f, 0.16f, 0.18f, 1f);
    static readonly Color CloseText       = Color.white;
    static readonly Color TipText         = new Color(0.78f, 0.82f, 0.88f, 0.95f);

    const string TipObjectName = "EditControlsTip";
    static Sprite _solidSprite;

    static Sprite SolidSprite
    {
        get
        {
            if (_solidSprite != null) return _solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            _solidSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return _solidSprite;
        }
    }

    public static void Apply(GameObject panelRoot)
    {
        if (panelRoot == null) return;

        // Full-screen cover + draw above lobby chrome (HOST / JOIN / etc.)
        RectTransform rootRt = panelRoot.transform as RectTransform;
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.localScale = Vector3.one;
        }
        panelRoot.transform.SetAsLastSibling();

        // Own canvas so the modal always sorts above sibling lobby UI.
        Canvas modalCanvas = panelRoot.GetComponent<Canvas>();
        if (modalCanvas == null) modalCanvas = panelRoot.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 500;
        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
            panelRoot.AddComponent<GraphicRaycaster>();

        // Root dim — solid fill so HOST / JOIN cannot show through
        Image rootImg = panelRoot.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.sprite = SolidSprite;
            rootImg.type = Image.Type.Simple;
            rootImg.color = DimOverlay;
            rootImg.raycastTarget = true;
        }

        Transform preview = FindChild(panelRoot.transform, "preview Panel");
        if (preview != null)
        {
            Image previewImg = preview.GetComponent<Image>();
            if (previewImg != null)
            {
                // Keep rect for HUD preview parenting, but remove milky wash
                previewImg.color = PreviewClear;
                previewImg.raycastTarget = false;
            }
        }

        Transform topUi = FindChild(panelRoot.transform, "TopUI");
        if (topUi == null) return;

        StyleToolbar(topUi as RectTransform);
        StyleTitle(topUi);
        StyleButtons(topUi);
        EnsureTip(topUi as RectTransform);
    }

    static void StyleToolbar(RectTransform topUi)
    {
        if (topUi == null) return;

        // Floating bar under the status area — wide enough for title + 3 actions
        topUi.anchorMin = new Vector2(0.5f, 1f);
        topUi.anchorMax = new Vector2(0.5f, 1f);
        topUi.pivot = new Vector2(0.5f, 1f);
        topUi.anchoredPosition = new Vector2(0f, -18f);
        topUi.sizeDelta = new Vector2(980f, 118f);

        Image bar = topUi.GetComponent<Image>();
        if (bar != null)
        {
            bar.color = ToolbarBg;
            bar.type = Image.Type.Sliced;
            bar.pixelsPerUnitMultiplier = 1.15f;
        }

        // Soft drop-shadow feel via Outline if present, else add one
        Outline outline = topUi.GetComponent<Outline>();
        if (outline == null) outline = topUi.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(0f, -3f);
        outline.useGraphicAlpha = true;
    }

    static void StyleTitle(Transform topUi)
    {
        Transform titleTf = FindChild(topUi, "Customize UI");
        if (titleTf == null) return;

        RectTransform rt = titleTf as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(28f, 12f);
            rt.sizeDelta = new Vector2(420f, 48f);
        }

        TextMeshProUGUI tmp = titleTf.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return;

        tmp.text = "Edit Controls";
        tmp.fontSize = 36f;
        tmp.enableAutoSizing = false;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = AccentYellow;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.margin = Vector4.zero;
        tmp.raycastTarget = false;
    }

    static void StyleButtons(Transform topUi)
    {
        Transform buttons = FindChild(topUi, "Buttons");
        if (buttons == null) return;

        RectTransform buttonsRt = buttons as RectTransform;
        if (buttonsRt != null)
        {
            buttonsRt.anchorMin = new Vector2(1f, 0.5f);
            buttonsRt.anchorMax = new Vector2(1f, 0.5f);
            buttonsRt.pivot = new Vector2(1f, 0.5f);
            buttonsRt.anchoredPosition = new Vector2(-24f, -8f);
            buttonsRt.sizeDelta = new Vector2(520f, 56f);
        }

        HorizontalLayoutGroup hlg = buttons.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.spacing = 14f;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.reverseArrangement = false;
        }

        StyleActionButton(FindChild(buttons, "Save"), SaveBg, SaveText, "Save");
        StyleActionButton(FindChild(buttons, "Reset"), ResetBg, ResetText, "Reset");
        StyleActionButton(FindChild(buttons, "Close"), CloseBg, CloseText, "Close");
    }

    static void StyleActionButton(Transform buttonTf, Color bg, Color labelColor, string label)
    {
        if (buttonTf == null) return;

        RectTransform rt = buttonTf as RectTransform;
        if (rt != null)
            rt.sizeDelta = new Vector2(150f, 52f);

        Image img = buttonTf.GetComponent<Image>();
        if (img != null)
        {
            img.color = bg;
            img.type = Image.Type.Sliced;
        }

        Button btn = buttonTf.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;
        }

        TextMeshProUGUI tmp = buttonTf.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = label;
            tmp.color = labelColor;
            tmp.fontSize = 26f;
            tmp.enableAutoSizing = false;
            tmp.fontStyle = FontStyles.Bold;
            tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            tmp.raycastTarget = false;
        }
    }

    static void EnsureTip(RectTransform topUi)
    {
        if (topUi == null) return;

        Transform existing = topUi.Find(TipObjectName);
        TextMeshProUGUI tip;
        RectTransform tipRt;

        if (existing == null)
        {
            GameObject go = new GameObject(TipObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(topUi, false);
            tipRt = go.GetComponent<RectTransform>();
            tip = go.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            tipRt = existing as RectTransform;
            tip = existing.GetComponent<TextMeshProUGUI>();
            if (tip == null) tip = existing.gameObject.AddComponent<TextMeshProUGUI>();
        }

        tipRt.anchorMin = new Vector2(0f, 0.5f);
        tipRt.anchorMax = new Vector2(0f, 0.5f);
        tipRt.pivot = new Vector2(0f, 0.5f);
        tipRt.anchoredPosition = new Vector2(28f, -22f);
        tipRt.sizeDelta = new Vector2(460f, 28f);

        // Prefer game font if title uses one
        Transform titleTf = FindChild(topUi, "Customize UI");
        TextMeshProUGUI titleTmp = titleTf != null ? titleTf.GetComponent<TextMeshProUGUI>() : null;
        if (titleTmp != null && titleTmp.font != null)
            tip.font = titleTmp.font;

        tip.text = "Drag to move  ·  Pinch to scale  ·  Save when done";
        tip.fontSize = 18f;
        tip.enableAutoSizing = false;
        tip.fontStyle = FontStyles.Normal;
        tip.color = TipText;
        tip.horizontalAlignment = HorizontalAlignmentOptions.Left;
        tip.verticalAlignment = VerticalAlignmentOptions.Middle;
        tip.raycastTarget = false;
    }

    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c.name == name) return c;
        }
        // Deep search (preview nests can vary)
        return parent.Find(name);
    }
}
