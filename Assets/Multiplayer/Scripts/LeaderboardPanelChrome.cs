using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// End-of-match scoreboard chrome. Centered panel layout:
/// Title → column headers → scrollable player rows → footer (countdown + Continue).
/// </summary>
public static class LeaderboardPanelChrome
{
    static readonly Color DimOverlay     = new Color(0.01f, 0.02f, 0.04f, 0.82f);
    static readonly Color CardBg         = new Color(0.07f, 0.08f, 0.1f, 0.98f);
    static readonly Color CardShadow     = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color AccentYellow   = new Color(1f, 0.84f, 0.16f, 1f);
    static readonly Color HeaderBg       = new Color(0.16f, 0.18f, 0.22f, 1f);
    static readonly Color HeaderText     = new Color(0.85f, 0.88f, 0.92f, 1f);
    static readonly Color RowEven        = new Color(0.1f, 0.11f, 0.14f, 0.95f);
    static readonly Color RowOdd         = new Color(0.08f, 0.09f, 0.12f, 0.85f);
    static readonly Color RowLocal       = new Color(0.22f, 0.18f, 0.06f, 0.95f);
    static readonly Color RowLocalBorder = new Color(1f, 0.84f, 0.16f, 0.9f);
    static readonly Color NameText       = new Color(0.96f, 0.97f, 0.99f, 1f);
    static readonly Color StatText       = new Color(0.9f, 0.92f, 0.96f, 1f);
    static readonly Color TipText        = new Color(0.75f, 0.78f, 0.84f, 1f);
    static readonly Color Gold           = new Color(1f, 0.84f, 0.2f, 1f);
    static readonly Color Silver         = new Color(0.78f, 0.82f, 0.88f, 1f);
    static readonly Color Bronze         = new Color(0.9f, 0.58f, 0.28f, 1f);
    static readonly Color HeroTint       = new Color(0.45f, 0.68f, 1f, 1f);
    static readonly Color AlienTint      = new Color(1f, 0.42f, 0.42f, 1f);

    const string CardName = "ScoreboardCard";
    const string TitleHostName = "ScoreboardTitleHost";
    const string HeaderName = "ScoreboardColumnHeader";
    const string RowsHostName = "ScoreboardRowsHost";
    const string ScrollName = "ScoreboardScroll";
    const string ViewportName = "ScoreboardViewport";
    const string FooterName = "ScoreboardFooter";

    const float RefCardWidth = 1100f;
    const float RefCardHeight = 900f;
    const float ScreenMarginX = 24f;
    const float ScreenMarginY = 36f;
    const float MaxScreenHeightFrac = 0.92f;
    const float TitleHeight = 74f;
    const float HeaderHeight = 50f;
    const float FooterHeight = 84f;
    const float RowHeight = 58f;
    const float RankWidth = 64f;
    const float StatWidth = 110f;
    const float SidePad = 28f;

    static Sprite _solidSprite;
    static TextMeshProUGUI _footerCountdown;
    static TMP_FontAsset _sharedFont;
    static Material _sharedFontMaterial;
    static System.Action _onContinue;

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

    public static void Apply(GameObject panelRoot, System.Action onContinue = null)
    {
        if (panelRoot == null) return;

        _onContinue = onContinue;

        StyleRootDim(panelRoot);

        // Steal font from any existing TMP so new labels are never blank
        CacheFont(panelRoot);

        RectTransform card = EnsureCard(panelRoot.transform);
        Vector2 cardSize = StyleCard(card);
        EnsureCardLayout(card);

        // Hide legacy clutter (old title / content wrapper / corner timer)
        HideLegacy(panelRoot.transform, card);

        Transform playerRows = FindDeep(panelRoot.transform, "PlayerRows");
        RectTransform titleHost = EnsureTitleHost(card, cardSize);
        RectTransform header = EnsureColumnHeader(card, cardSize);
        RectTransform rowsHost = EnsureRowsHost(card, playerRows as RectTransform);
        RectTransform footer = EnsureFooter(card, cardSize);

        // Strict visual order in the VerticalLayoutGroup
        titleHost.SetSiblingIndex(0);
        header.SetSiblingIndex(1);
        rowsHost.SetSiblingIndex(2);
        footer.SetSiblingIndex(3);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);
    }

    public static void SetCountdownSeconds(int seconds)
    {
        if (_footerCountdown != null)
            _footerCountdown.text = seconds + "s";
    }

    public static void StyleFilledRow(GameObject row, int rankIndex, bool isLocal, int teamId)
    {
        if (row == null) return;

        EnsureRowBackground(row, rankIndex, isLocal);
        StyleRowShell(row.transform as RectTransform);

        Transform rankTf = row.transform.Find("RankBadge");
        if (rankTf != null)
        {
            TextMeshProUGUI rankTmp = rankTf.GetComponent<TextMeshProUGUI>();
            if (rankTmp != null)
            {
                rankTmp.text = (rankIndex + 1).ToString();
                rankTmp.color = RankColor(rankIndex);
                rankTmp.fontStyle = rankIndex < 3 ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        foreach (TextMeshProUGUI tmp in row.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp == null) continue;
            string n = tmp.gameObject.name;
            if (n == "RankBadge") continue;

            if (n.IndexOf("Name", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (isLocal) tmp.color = AccentYellow;
                else if (teamId == 0) tmp.color = Color.Lerp(NameText, HeroTint, 0.4f);
                else if (teamId == 1) tmp.color = Color.Lerp(NameText, AlienTint, 0.4f);
                else tmp.color = NameText;
            }
            else if (n.IndexOf("Kill", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.IndexOf("Death", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                tmp.color = isLocal ? AccentYellow : StatText;
            }
        }
    }

    // ---------------------------------------------------------------
    // Root / card
    // ---------------------------------------------------------------

    static void StyleRootDim(GameObject panelRoot)
    {
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
        modalCanvas.sortingOrder = 540;
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

    static RectTransform EnsureCard(Transform panel)
    {
        Transform existing = panel.Find(CardName);
        if (existing != null)
            return existing as RectTransform;

        GameObject go = new GameObject(CardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(panel, false);
        return rt;
    }

    static Vector2 StyleCard(RectTransform card)
    {
        Vector2 size = ResolveCardSize(card);

        // Centered panel (not full-screen)
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = size;
        card.localScale = Vector3.one;
        card.SetAsFirstSibling();

        Image img = card.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = SolidSprite;
            img.type = Image.Type.Simple;
            img.color = CardBg;
            img.raycastTarget = true;
        }

        Shadow shadow = card.GetComponent<Shadow>();
        if (shadow == null) shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = CardShadow;
        shadow.effectDistance = new Vector2(0f, -10f);
        shadow.useGraphicAlpha = true;

        return size;
    }

    static void EnsureCardLayout(RectTransform card)
    {
        VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(
            Mathf.RoundToInt(SidePad),
            Mathf.RoundToInt(SidePad),
            20,
            16);
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        // Rows host takes remaining height; title/header/footer stay fixed
        vlg.childForceExpandHeight = false;
    }

    static Vector2 ResolveCardSize(RectTransform card)
    {
        Canvas canvas = card.GetComponentInParent<Canvas>();
        RectTransform canvasRt = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRt == null || canvasRt.rect.width < 10f)
            return new Vector2(RefCardWidth, RefCardHeight);

        float maxW = Mathf.Max(320f, canvasRt.rect.width - ScreenMarginX * 2f);
        float maxH = Mathf.Max(420f, canvasRt.rect.height - ScreenMarginY * 2f);
        maxH = Mathf.Min(maxH, canvasRt.rect.height * MaxScreenHeightFrac);

        float scale = Mathf.Min(1f, maxW / RefCardWidth, maxH / RefCardHeight);
        return new Vector2(RefCardWidth * scale, RefCardHeight * scale);
    }

    static void HideLegacy(Transform panel, RectTransform card)
    {
        string[] hideNames = { "Title", "LeaderBoardContent", "Leavingtimer", "RowHeader" };
        foreach (string n in hideNames)
        {
            foreach (Transform t in panel.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == card || t.IsChildOf(card) && t.name != "LeaderBoardContent" && t.name != "RowHeader")
                {
                    // only hide exact legacy roots / header, not our new hosts
                }
                if (t != null && t.name == n && !IsOurHost(t))
                {
                    // Keep PlayerRows alive — it's under LeaderBoardContent
                    if (n == "LeaderBoardContent")
                    {
                        // Don't hide the whole content until rows are moved out
                        continue;
                    }
                    if (n == "Title" || n == "Leavingtimer" || n == "RowHeader")
                        t.gameObject.SetActive(false);
                }
            }
        }
    }

    static bool IsOurHost(Transform t)
    {
        return t.name == TitleHostName || t.name == HeaderName ||
               t.name == RowsHostName || t.name == FooterName || t.name == CardName ||
               t.name == ScrollName || t.name == ViewportName;
    }

    /// <summary>
    /// Call after filling rows so the scroll content height matches visible players.
    /// </summary>
    public static void RefreshScrollLayout(GameObject panelRoot)
    {
        if (panelRoot == null) return;
        Transform rows = FindDeep(panelRoot.transform, "PlayerRows");
        if (rows == null) return;

        RectTransform content = rows as RectTransform;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        ScrollRect scroll = panelRoot.GetComponentInChildren<ScrollRect>(true);
        if (scroll != null)
        {
            scroll.content = content;
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    // ---------------------------------------------------------------
    // Title
    // ---------------------------------------------------------------

    static RectTransform EnsureTitleHost(RectTransform card, Vector2 cardSize)
    {
        RectTransform host = EnsureHost(card, TitleHostName);
        SetFixedHeight(host, TitleHeight);

        Image bg = host.GetComponent<Image>();
        if (bg != null) Object.Destroy(bg);

        TextMeshProUGUI tmp = host.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null)
        {
            GameObject go = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(host, false);
            Stretch(rt);
            tmp = go.GetComponent<TextMeshProUGUI>();
            ApplyFont(tmp);
        }

        RectTransform textRt = tmp.rectTransform;
        Stretch(textRt);

        tmp.text = "SCOREBOARD";
        tmp.enableAutoSizing = false;
        tmp.fontSize = 36f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = AccentYellow;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.margin = Vector4.zero;
        tmp.characterSpacing = 2f;
        tmp.raycastTarget = false;
        ApplyFont(tmp);

        return host;
    }

    // ---------------------------------------------------------------
    // Column header
    // ---------------------------------------------------------------

    static RectTransform EnsureColumnHeader(RectTransform card, Vector2 cardSize)
    {
        RectTransform header = EnsureHost(card, HeaderName);
        SetFixedHeight(header, HeaderHeight);

        Image bg = header.GetComponent<Image>();
        if (bg == null) bg = header.gameObject.AddComponent<Image>();
        bg.sprite = SolidSprite;
        bg.type = Image.Type.Simple;
        bg.color = HeaderBg;
        bg.raycastTarget = false;

        HorizontalLayoutGroup hlg = header.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 0, 0);
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        EnsureHeaderCol(header, "H_Rank", "", RankWidth, TextAlignmentOptions.Center);
        EnsureHeaderCol(header, "H_Player", "", -1f, TextAlignmentOptions.Left);
        EnsureHeaderCol(header, "H_Kills", "KILLS", StatWidth, TextAlignmentOptions.Center);
        EnsureHeaderCol(header, "H_Deaths", "DEATHS", StatWidth, TextAlignmentOptions.Center);

        FindChild(header, "H_Rank")?.SetSiblingIndex(0);
        FindChild(header, "H_Player")?.SetSiblingIndex(1);
        FindChild(header, "H_Kills")?.SetSiblingIndex(2);
        FindChild(header, "H_Deaths")?.SetSiblingIndex(3);

        header.gameObject.SetActive(true);
        return header;
    }

    static void EnsureHeaderCol(RectTransform header, string name, string text, float width, TextAlignmentOptions align)
    {
        Transform existing = FindChild(header, name);
        RectTransform rt;
        TextMeshProUGUI tmp;
        if (existing == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(header, false);
            tmp = go.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            rt = existing as RectTransform;
            tmp = existing.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = existing.gameObject.AddComponent<TextMeshProUGUI>();
        }

        rt.localScale = Vector3.one;
        LayoutElement le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        if (width > 0f)
        {
            le.preferredWidth = width;
            le.minWidth = width;
            le.flexibleWidth = 0f;
        }
        else
        {
            le.minWidth = 140f;
            le.preferredWidth = -1f;
            le.flexibleWidth = 1f;
        }
        le.flexibleHeight = 1f;

        ApplyFont(tmp);
        tmp.text = text;
        tmp.enableAutoSizing = false;
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = HeaderText;
        tmp.alignment = align;
        tmp.margin = Vector4.zero;
        tmp.raycastTarget = false;
        tmp.gameObject.SetActive(true);
    }

    // ---------------------------------------------------------------
    // Rows
    // ---------------------------------------------------------------

    static RectTransform EnsureRowsHost(RectTransform card, RectTransform playerRows)
    {
        RectTransform host = EnsureHost(card, RowsHostName);
        LayoutElement le = host.GetComponent<LayoutElement>();
        if (le == null) le = host.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.flexibleHeight = 1f;
        le.minHeight = 160f;
        le.preferredHeight = -1f;

        RectTransform viewport = EnsureScrollViewport(host);

        if (playerRows != null)
        {
            Transform oldParent = playerRows.parent;
            if (playerRows.parent != viewport)
                playerRows.SetParent(viewport, false);

            if (oldParent != null && oldParent.name == "LeaderBoardContent")
                oldParent.gameObject.SetActive(false);

            SetupScrollContent(playerRows);
            playerRows.gameObject.SetActive(true);

            ScrollRect scroll = host.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null)
                scroll.content = playerRows;

            for (int i = 0; i < playerRows.childCount; i++)
                StyleRowShell(playerRows.GetChild(i) as RectTransform);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(playerRows);
        }

        return host;
    }

    static RectTransform EnsureScrollViewport(RectTransform rowsHost)
    {
        RectTransform scrollRt = FindChild(rowsHost, ScrollName) as RectTransform;
        if (scrollRt == null)
        {
            GameObject scrollGo = new GameObject(ScrollName, typeof(RectTransform), typeof(ScrollRect));
            scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(rowsHost, false);
        }

        Stretch(scrollRt);
        scrollRt.localScale = Vector3.one;

        ScrollRect scroll = scrollRt.GetComponent<ScrollRect>();
        if (scroll == null) scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 35f;
        scroll.verticalScrollbar = null;
        scroll.horizontalScrollbar = null;

        RectTransform viewport = FindChild(scrollRt, ViewportName) as RectTransform;
        if (viewport == null)
        {
            GameObject vpGo = new GameObject(ViewportName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport = vpGo.GetComponent<RectTransform>();
            viewport.SetParent(scrollRt, false);
        }

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        Image vpImg = viewport.GetComponent<Image>();
        if (vpImg == null) vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.sprite = SolidSprite;
        vpImg.type = Image.Type.Simple;
        vpImg.color = new Color(1f, 1f, 1f, 0.01f); // needed so ScrollRect receives drags
        vpImg.raycastTarget = true;

        Stretch(viewport);
        viewport.localScale = Vector3.one;

        scroll.viewport = viewport;
        return viewport;
    }

    static void SetupScrollContent(RectTransform playerRows)
    {
        // Top-stretched content that grows downward with row count
        playerRows.anchorMin = new Vector2(0f, 1f);
        playerRows.anchorMax = new Vector2(1f, 1f);
        playerRows.pivot = new Vector2(0.5f, 1f);
        playerRows.anchoredPosition = Vector2.zero;
        playerRows.sizeDelta = new Vector2(0f, 0f);
        playerRows.localScale = Vector3.one;

        VerticalLayoutGroup vlg = playerRows.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = playerRows.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 4, 8);
        vlg.spacing = 6f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = playerRows.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = playerRows.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static void StyleRowShell(RectTransform row)
    {
        if (row == null) return;

        row.localScale = Vector3.one;

        LayoutElement le = row.GetComponent<LayoutElement>();
        if (le == null) le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = RowHeight;
        le.minHeight = RowHeight;
        le.flexibleHeight = 0f;
        le.flexibleWidth = 1f;

        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 0, 0);
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        EnsureRankBadge(row);

        Transform rank = FindChild(row, "RankBadge");
        Transform name = FindChild(row, "Name") ?? FindDeep(row, "Name");
        Transform kills = FindChild(row, "Kills") ?? FindDeep(row, "Kills");
        Transform deaths = FindChild(row, "Deaths") ?? FindDeep(row, "Deaths");

        if (rank != null) rank.SetSiblingIndex(0);
        if (name != null) name.SetSiblingIndex(1);
        if (kills != null) kills.SetSiblingIndex(2);
        if (deaths != null) deaths.SetSiblingIndex(3);

        ApplyCol(rank, RankWidth, false);
        ApplyCol(name, -1f, true);
        ApplyCol(kills, StatWidth, false);
        ApplyCol(deaths, StatWidth, false);

        StyleRowText(name, NameText, 22f, TextAlignmentOptions.Left);
        StyleRowText(kills, StatText, 22f, TextAlignmentOptions.Center);
        StyleRowText(deaths, StatText, 22f, TextAlignmentOptions.Center);
    }

    static void EnsureRankBadge(RectTransform row)
    {
        if (FindChild(row, "RankBadge") != null) return;

        GameObject go = new GameObject("RankBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(row, false);
        rt.SetAsFirstSibling();

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        ApplyFont(tmp);
        tmp.text = "-";
        tmp.enableAutoSizing = false;
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = HeaderText;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.margin = Vector4.zero;
        tmp.raycastTarget = false;
    }

    static void ApplyCol(Transform col, float width, bool flexible)
    {
        if (col == null) return;
        RectTransform rt = col as RectTransform;
        if (rt == null) return;
        rt.localScale = Vector3.one;

        LayoutElement le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        if (flexible)
        {
            le.minWidth = 120f;
            le.preferredWidth = -1f;
            le.flexibleWidth = 1f;
        }
        else
        {
            le.minWidth = width;
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
        }
        le.flexibleHeight = 1f;
    }

    static void StyleRowText(Transform col, Color color, float size, TextAlignmentOptions align)
    {
        if (col == null) return;
        TextMeshProUGUI tmp = col.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = col.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null) return;

        tmp.enableAutoSizing = false;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.margin = Vector4.zero;
        tmp.raycastTarget = false;
    }

    static void EnsureRowBackground(GameObject row, int index, bool isLocal)
    {
        Image bg = row.GetComponent<Image>();
        if (bg == null) bg = row.AddComponent<Image>();
        bg.sprite = SolidSprite;
        bg.type = Image.Type.Simple;
        bg.raycastTarget = false;
        bg.color = isLocal ? RowLocal : (index % 2 == 0 ? RowEven : RowOdd);

        Outline outline = row.GetComponent<Outline>();
        if (isLocal)
        {
            if (outline == null) outline = row.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = RowLocalBorder;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }
        else if (outline != null)
        {
            outline.enabled = false;
        }
    }

    // ---------------------------------------------------------------
    // Footer
    // ---------------------------------------------------------------

    static RectTransform EnsureFooter(RectTransform card, Vector2 cardSize)
    {
        RectTransform footer = EnsureHost(card, FooterName);
        SetFixedHeight(footer, FooterHeight);

        Image bg = footer.GetComponent<Image>();
        if (bg == null) bg = footer.gameObject.AddComponent<Image>();
        bg.sprite = SolidSprite;
        bg.type = Image.Type.Simple;
        bg.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);
        bg.raycastTarget = false;

        HorizontalLayoutGroup hlg = footer.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 6, 6);
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        RectTransform messageHost = EnsureFooterMessageHost(footer);
        EnsureContinueButton(footer);

        messageHost.SetSiblingIndex(0);
        FindChild(footer, "ContinueButton")?.SetSiblingIndex(1);

        return footer;
    }

    static RectTransform EnsureFooterMessageHost(RectTransform footer)
    {
        const string hostName = "FooterMessageHost";
        RectTransform host = FindChild(footer, hostName) as RectTransform;
        if (host == null)
        {
            GameObject go = new GameObject(hostName, typeof(RectTransform));
            host = go.GetComponent<RectTransform>();
            host.SetParent(footer, false);
        }

        // Flexible spacer keeps Continue on the right; inner stack centers the timer under the label
        LayoutElement le = host.GetComponent<LayoutElement>();
        if (le == null) le = host.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 200f;
        le.preferredWidth = -1f;
        le.flexibleHeight = 1f;

        VerticalLayoutGroup vlg = host.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = host.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.spacing = 1f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Move any old absolute-positioned footer labels under the message host
        ReparentIfPresent(footer, host, "FooterLabel");
        ReparentIfPresent(footer, host, "FooterCountdown");

        TextMeshProUGUI label = EnsureFooterLine(host, "FooterLabel", "Returning to Lobby", TipText, 16f, TextAlignmentOptions.Center);
        _footerCountdown = EnsureFooterLine(host, "FooterCountdown", "10s", AccentYellow, 22f, TextAlignmentOptions.Center);
        if (_footerCountdown != null)
            _footerCountdown.fontStyle = FontStyles.Bold;

        label?.transform.SetSiblingIndex(0);
        _footerCountdown?.transform.SetSiblingIndex(1);

        return host;
    }

    static void ReparentIfPresent(Transform from, Transform to, string childName)
    {
        Transform child = FindChild(from, childName);
        if (child != null && child.parent != to)
            child.SetParent(to, false);
    }

    static void EnsureContinueButton(RectTransform footer)
    {
        const string btnName = "ContinueButton";
        Transform existing = FindChild(footer, btnName);
        RectTransform rt;
        Image img;
        Button btn;
        TextMeshProUGUI label;

        if (existing == null)
        {
            GameObject go = new GameObject(btnName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(footer, false);
            img = go.GetComponent<Image>();
            btn = go.GetComponent<Button>();

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            StretchFull(labelRt);
            label = labelGo.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            rt = existing as RectTransform;
            img = existing.GetComponent<Image>();
            if (img == null) img = existing.gameObject.AddComponent<Image>();
            btn = existing.GetComponent<Button>();
            if (btn == null) btn = existing.gameObject.AddComponent<Button>();
            label = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                RectTransform labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.SetParent(rt, false);
                StretchFull(labelRt);
                label = labelGo.GetComponent<TextMeshProUGUI>();
            }
        }

        // Clear old absolute anchors from previous chrome version
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(140f, 42f);
        rt.localScale = Vector3.one;

        LayoutElement le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 140f;
        le.minWidth = 120f;
        le.preferredHeight = 42f;
        le.minHeight = 38f;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;

        img.sprite = SolidSprite;
        img.type = Image.Type.Simple;
        img.color = AccentYellow;
        img.raycastTarget = true;

        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        btn.colors = colors;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => _onContinue?.Invoke());

        ApplyFont(label);
        label.text = "CONTINUE";
        label.enableAutoSizing = false;
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.08f, 0.08f, 0.1f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.margin = Vector4.zero;
        label.raycastTarget = false;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static TextMeshProUGUI EnsureFooterLine(RectTransform parent, string name, string text, Color color, float size, TextAlignmentOptions align)
    {
        Transform existing = FindChild(parent, name);
        RectTransform rt;
        TextMeshProUGUI tmp;
        if (existing == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            tmp = go.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            rt = existing as RectTransform;
            tmp = existing.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = existing.gameObject.AddComponent<TextMeshProUGUI>();
        }

        rt.localScale = Vector3.one;
        LayoutElement le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = size + 8f;
        le.minHeight = size + 4f;
        le.flexibleWidth = 1f;

        ApplyFont(tmp);
        tmp.text = text;
        tmp.enableAutoSizing = false;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.horizontalAlignment = align == TextAlignmentOptions.Center
            ? HorizontalAlignmentOptions.Center
            : HorizontalAlignmentOptions.Left;
        tmp.margin = Vector4.zero;
        tmp.raycastTarget = false;
        return tmp;
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    static void CacheFont(GameObject root)
    {
        if (_sharedFont != null) return;
        TextMeshProUGUI any = root.GetComponentInChildren<TextMeshProUGUI>(true);
        if (any == null) return;
        _sharedFont = any.font;
        _sharedFontMaterial = any.fontSharedMaterial;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        if (_sharedFont != null) tmp.font = _sharedFont;
        if (_sharedFontMaterial != null) tmp.fontSharedMaterial = _sharedFontMaterial;
    }

    static RectTransform EnsureHost(RectTransform card, string name)
    {
        Transform existing = FindChild(card, name);
        if (existing != null)
            return existing as RectTransform;

        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(card, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    static void SetFixedHeight(RectTransform rt, float height)
    {
        LayoutElement le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        le.flexibleHeight = 0f;
        le.flexibleWidth = 1f;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static Color RankColor(int index)
    {
        if (index == 0) return Gold;
        if (index == 1) return Silver;
        if (index == 2) return Bronze;
        return HeaderText;
    }

    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
        }
        return null;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
