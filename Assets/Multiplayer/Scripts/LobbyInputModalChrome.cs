using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lobby modals for Join Code + Change Username.
/// Full-size card (1000x700 at reference), responsive clamp on smaller screens,
/// even margins, no accent line.
/// </summary>
public static class LobbyInputModalChrome
{
    static readonly Color DimOverlay    = new Color(0.01f, 0.02f, 0.04f, 0.72f);
    static readonly Color CardBg        = new Color(0.07f, 0.08f, 0.1f, 0.98f);
    static readonly Color CardShadow    = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color TitleColor    = Color.white; // yellow via game font material
    static readonly Color SubtitleColor = new Color(0.82f, 0.84f, 0.88f, 1f);
    static readonly Color TipColor      = new Color(0.65f, 0.67f, 0.72f, 1f);
    static readonly Color FieldBg       = new Color(0.95f, 0.95f, 0.97f, 1f);
    static readonly Color FieldText     = new Color(0.12f, 0.12f, 0.16f, 1f);
    static readonly Color Placeholder   = new Color(0.5f, 0.5f, 0.58f, 0.8f);
    static readonly Color CancelBg      = new Color(0.86f, 0.18f, 0.2f, 1f);
    static readonly Color OkayBg        = new Color(0.12f, 0.55f, 0.95f, 1f);
    static readonly Color ButtonText    = Color.white;

    const string DefaultJoinSubtitle = "Play with your friends!";
    const string AccentBarName = "ModalAccentBar";

    // Design size at 1920x1080 reference (Canvas Scaler)
    const float RefCardWidth = 1000f;
    const float RefCardHeight = 700f;
    const float ScreenMargin = 48f;

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

    public static void ApplyJoinCode(GameObject panelRoot)
    {
        ApplyDimAndModal(panelRoot);

        Transform cardTf = FindChild(panelRoot.transform, "Background_Image");
        if (cardTf == null && panelRoot.transform.childCount > 0)
            cardTf = panelRoot.transform.GetChild(0);

        RectTransform card = cardTf as RectTransform;
        Vector2 size = StyleCard(card);

        StyleJoinTitle(card, size);
        StyleJoinSubtitle(card, size);
        StyleInput(card, "JoinCodeInputField", "Enter Code", 12, size, hasTip: false);
        StyleActionButtons(card, size,
            FindChild(card, "Cancel_Button"), "Cancel", CancelBg,
            FindChild(card, "Okay_Button"), "Join", OkayBg);
    }

    public static void ApplyChangeUsername(GameObject panelRoot)
    {
        ApplyDimAndModal(panelRoot);

        Transform cardTf = FindChild(panelRoot.transform, "Background_Image");
        if (cardTf == null && panelRoot.transform.childCount > 0)
            cardTf = panelRoot.transform.GetChild(0);

        RectTransform card = cardTf as RectTransform;
        Vector2 size = StyleCard(card);

        StyleTitle(card, "Change Username", size);
        StyleInput(card, "ChangeUsernameInputfield", "Enter username", 10, size, hasTip: true);
        EnsureChangeUsernameTip(card, size);
        StyleActionButtons(card, size,
            FindChild(card, "Cancel_Button"), "Cancel", CancelBg,
            FindChild(card, "Okay_Button"), "Confirm", OkayBg);
    }

    public static void ResetJoinSubtitle(TMP_Text statusText)
    {
        if (statusText == null) return;
        statusText.text = DefaultJoinSubtitle;
        statusText.gameObject.SetActive(true);
        statusText.color = SubtitleColor;
    }

    static void ApplyDimAndModal(GameObject panelRoot)
    {
        if (panelRoot == null) return;

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

        Canvas modalCanvas = panelRoot.GetComponent<Canvas>();
        if (modalCanvas == null) modalCanvas = panelRoot.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 520;
        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
            panelRoot.AddComponent<GraphicRaycaster>();

        Image dim = panelRoot.GetComponent<Image>();
        if (dim != null)
        {
            dim.sprite = SolidSprite;
            dim.type = Image.Type.Simple;
            dim.color = DimOverlay;
            dim.raycastTarget = true;
        }
    }

    static Vector2 StyleCard(RectTransform card)
    {
        Vector2 size = new Vector2(RefCardWidth, RefCardHeight);
        if (card == null) return size;

        size = ResolveCardSize(card);

        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = size;

        Image img = card.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = SolidSprite;
            img.type = Image.Type.Simple;
            img.color = CardBg;
            img.raycastTarget = true;
        }

        Outline outline = card.GetComponent<Outline>();
        if (outline != null)
            Object.Destroy(outline);

        Shadow shadow = card.GetComponent<Shadow>();
        if (shadow == null) shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = CardShadow;
        shadow.effectDistance = new Vector2(0f, -8f);
        shadow.useGraphicAlpha = true;

        // Remove old yellow accent bar if a previous build created it
        Transform accent = card.Find(AccentBarName);
        if (accent != null)
            accent.gameObject.SetActive(false);

        return size;
    }

    /// <summary>
    /// Prefer 1000x700; on smaller canvases shrink to keep ScreenMargin clear on all sides.
    /// </summary>
    static Vector2 ResolveCardSize(RectTransform card)
    {
        float w = RefCardWidth;
        float h = RefCardHeight;

        Canvas canvas = card.GetComponentInParent<Canvas>();
        RectTransform canvasRt = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRt == null)
            return new Vector2(w, h);

        float maxW = Mathf.Max(280f, canvasRt.rect.width - ScreenMargin * 2f);
        float maxH = Mathf.Max(240f, canvasRt.rect.height - ScreenMargin * 2f);

        float scale = Mathf.Min(1f, maxW / RefCardWidth, maxH / RefCardHeight);
        return new Vector2(RefCardWidth * scale, RefCardHeight * scale);
    }

    static float Scale(Vector2 cardSize, float valueAtRef)
    {
        float s = cardSize.y / RefCardHeight;
        return valueAtRef * s;
    }

    static void StyleJoinTitle(Transform card, Vector2 cardSize)
    {
        TextMeshProUGUI title = FindTitleLabel(card);
        Transform named = FindChild(card, "JoinCode_Text");
        if (named != null)
        {
            TextMeshProUGUI namedTmp = named.GetComponent<TextMeshProUGUI>();
            if (namedTmp != null) title = namedTmp;
        }
        ApplyTitle(title, "Join Code", cardSize);
    }

    static void StyleTitle(Transform card, string text, Vector2 cardSize)
    {
        ApplyTitle(FindTitleLabel(card), text, cardSize);
    }

    static TextMeshProUGUI FindTitleLabel(Transform card)
    {
        if (card == null) return null;

        for (int i = 0; i < card.childCount; i++)
        {
            Transform child = card.GetChild(i);
            if (child.name == "Status_Text" || child.name == "ChangeUsernameTip" || child.name == AccentBarName)
                continue;
            if (child.GetComponent<Button>() != null) continue;
            if (child.GetComponent<TMP_InputField>() != null) continue;

            TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp != null) return tmp;
        }
        return null;
    }

    static void ApplyTitle(TextMeshProUGUI title, string text, Vector2 cardSize)
    {
        if (title == null) return;

        float topPad = Scale(cardSize, 56f);
        float titleH = Scale(cardSize, 70f);
        float sidePad = Scale(cardSize, 80f);

        RectTransform rt = title.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -topPad);
        rt.sizeDelta = new Vector2(cardSize.x - sidePad * 2f, titleH);

        title.text = text;
        title.fontSize = Scale(cardSize, 55f);
        title.enableAutoSizing = false;
        title.fontStyle = FontStyles.Bold;
        title.color = TitleColor;
        title.horizontalAlignment = HorizontalAlignmentOptions.Center;
        title.verticalAlignment = VerticalAlignmentOptions.Middle;
        title.raycastTarget = false;
    }

    static void StyleJoinSubtitle(Transform card, Vector2 cardSize)
    {
        if (card == null) return;

        Transform statusTf = FindChild(card, "Status_Text");
        if (statusTf == null) return;

        float topPad = Scale(cardSize, 140f);
        float sidePad = Scale(cardSize, 80f);

        RectTransform rt = statusTf as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -topPad);
            rt.sizeDelta = new Vector2(cardSize.x - sidePad * 2f, Scale(cardSize, 40f));
        }

        TextMeshProUGUI tmp = statusTf.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return;

        string current = tmp.text != null ? tmp.text.Trim() : "";
        if (string.IsNullOrEmpty(current) ||
            current.StartsWith("Play with", System.StringComparison.OrdinalIgnoreCase) ||
            current == "Join Code -" ||
            current == "Join Code")
        {
            tmp.text = DefaultJoinSubtitle;
        }

        tmp.fontSize = Scale(cardSize, 28f);
        tmp.enableAutoSizing = false;
        tmp.fontStyle = FontStyles.Normal;
        tmp.color = SubtitleColor;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.raycastTarget = false;
        tmp.gameObject.SetActive(true);
    }

    static void EnsureChangeUsernameTip(RectTransform card, Vector2 cardSize)
    {
        if (card == null) return;

        const string tipName = "ChangeUsernameTip";
        Transform existing = card.Find(tipName);
        TextMeshProUGUI tip;
        RectTransform tipRt;

        if (existing == null)
        {
            GameObject go = new GameObject(tipName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(card, false);
            tipRt = go.GetComponent<RectTransform>();
            tip = go.GetComponent<TextMeshProUGUI>();

            TextMeshProUGUI title = FindTitleLabel(card);
            if (title != null && title.font != null)
                tip.font = title.font;
        }
        else
        {
            tipRt = existing as RectTransform;
            tip = existing.GetComponent<TextMeshProUGUI>();
            if (tip == null) tip = existing.gameObject.AddComponent<TextMeshProUGUI>();
            existing.gameObject.SetActive(true);
        }

        // Sit just under the centered input with even spacing
        float tipY = -Scale(cardSize, 70f);

        tipRt.anchorMin = new Vector2(0.5f, 0.5f);
        tipRt.anchorMax = new Vector2(0.5f, 0.5f);
        tipRt.pivot = new Vector2(0.5f, 0.5f);
        tipRt.anchoredPosition = new Vector2(0f, tipY);
        tipRt.sizeDelta = new Vector2(Scale(cardSize, 420f), Scale(cardSize, 28f));

        tip.text = "Max 10 characters";
        tip.fontSize = Scale(cardSize, 22f);
        tip.enableAutoSizing = false;
        tip.fontStyle = FontStyles.Normal;
        tip.color = TipColor;
        tip.horizontalAlignment = HorizontalAlignmentOptions.Center;
        tip.verticalAlignment = VerticalAlignmentOptions.Middle;
        tip.raycastTarget = false;
    }

    static void StyleInput(
        Transform card,
        string preferredName,
        string placeholderText,
        int characterLimit,
        Vector2 cardSize,
        bool hasTip)
    {
        if (card == null) return;

        Transform fieldTf = FindChild(card, preferredName);
        if (fieldTf == null)
        {
            TMP_InputField any = card.GetComponentInChildren<TMP_InputField>(true);
            if (any != null) fieldTf = any.transform;
        }
        if (fieldTf == null) return;

        // Centered field with balanced vertical room for title above and buttons below
        float fieldY = hasTip ? Scale(cardSize, 20f) : Scale(cardSize, 10f);
        float fieldW = Mathf.Min(Scale(cardSize, 480f), cardSize.x - Scale(cardSize, 160f));
        float fieldH = Scale(cardSize, 100f);

        RectTransform rt = fieldTf as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, fieldY);
            rt.sizeDelta = new Vector2(fieldW, fieldH);
        }

        Image img = fieldTf.GetComponent<Image>();
        if (img != null)
        {
            if (img.sprite == null)
                img.sprite = SolidSprite;
            img.color = FieldBg;
        }

        TMP_InputField field = fieldTf.GetComponent<TMP_InputField>();
        if (field == null) return;

        field.characterLimit = characterLimit;
        if (preferredName.IndexOf("Join", System.StringComparison.OrdinalIgnoreCase) >= 0)
            field.contentType = TMP_InputField.ContentType.Alphanumeric;

        if (field.textComponent != null)
        {
            field.textComponent.fontSize = Scale(cardSize, 36f);
            field.textComponent.color = FieldText;
            field.textComponent.horizontalAlignment = HorizontalAlignmentOptions.Center;
            field.textComponent.verticalAlignment = VerticalAlignmentOptions.Middle;
        }

        if (field.placeholder is TextMeshProUGUI placeholder)
        {
            placeholder.text = placeholderText;
            placeholder.fontSize = Scale(cardSize, 32f);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = Placeholder;
            placeholder.horizontalAlignment = HorizontalAlignmentOptions.Center;
            placeholder.verticalAlignment = VerticalAlignmentOptions.Middle;
        }
    }

    static void StyleActionButtons(
        Transform card,
        Vector2 cardSize,
        Transform cancelTf,
        string cancelLabel,
        Color cancelBg,
        Transform okayTf,
        string okayLabel,
        Color okayBg)
    {
        float bottomPad = Scale(cardSize, 90f);
        float btnW = Scale(cardSize, 200f);
        float btnH = Scale(cardSize, 80f);
        float gap = Scale(cardSize, 40f);
        float x = (btnW + gap) * 0.5f;

        StyleActionButton(cancelTf, cancelBg, cancelLabel, -x, bottomPad, btnW, btnH, cardSize);
        StyleActionButton(okayTf, okayBg, okayLabel, x, bottomPad, btnW, btnH, cardSize);
    }

    static void StyleActionButton(
        Transform buttonTf,
        Color bg,
        string label,
        float x,
        float y,
        float width,
        float height,
        Vector2 cardSize)
    {
        if (buttonTf == null) return;

        RectTransform rt = buttonTf as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        Image img = buttonTf.GetComponent<Image>();
        if (img != null)
        {
            if (img.sprite == null)
                img.sprite = SolidSprite;
            img.color = bg;
        }

        Button btn = buttonTf.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;
        }

        TextMeshProUGUI tmp = buttonTf.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = label;
            tmp.color = ButtonText;
            tmp.fontSize = Scale(cardSize, 36f);
            tmp.enableAutoSizing = false;
            tmp.fontStyle = FontStyles.Bold;
            tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            tmp.raycastTarget = false;
        }
    }

    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c.name == name) return c;
        }
        return parent.Find(name);
    }
}
