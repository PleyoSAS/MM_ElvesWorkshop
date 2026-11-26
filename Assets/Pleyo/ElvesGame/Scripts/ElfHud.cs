using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD of one screen in the MM Skipping Rope style (white soft-3D rounded cards, Fredoka font, dark ink text):
/// timer card, side panel (gifts, unfinished, teams), tutorial / results card, big outlined countdown, invisible operator
/// Quit button (top right). Canvas bound to the screen camera, scaled from 1920x1080 (1080p / 4K).</summary>
public class ElfHud : MonoBehaviour
{
    static readonly Color Card = new Color(1f, 1f, 1f, 0.95f);
    static readonly Color ShadowCol = new Color32(0xCB, 0xD5, 0xE1, 204);

    TextMeshProUGUI _timer, _message, _big;



    GameObject _timerCard, _messageCard;

    public void Build(Camera cam, Action onQuit)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        _timerCard = CardBox(transform, "TimerCard", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(260f, 110f));
        _timer = Text(_timerCard.transform, "Timer", 76f, TextAlignmentOptions.Center, ElfArt.Ink);


        _messageCard = CardBox(transform, "MessageCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1400f, 380f));
        _message = Text(_messageCard.transform, "Message", 58f, TextAlignmentOptions.Center, ElfArt.Ink);
        _message.rectTransform.offsetMin = new Vector2(40f, 24f);
        _message.rectTransform.offsetMax = new Vector2(-40f, -24f);
        _message.textWrappingMode = TextWrappingModes.Normal;
        _messageCard.SetActive(false);

        _big = Text(transform, "Big", 230f, TextAlignmentOptions.Center, Color.white);
        RectTransform bigRect = _big.rectTransform;
        bigRect.anchorMin = bigRect.anchorMax = new Vector2(0.5f, 0.5f);
        bigRect.sizeDelta = new Vector2(1400f, 400f);
        bigRect.anchoredPosition = new Vector2(0f, 150f);
        if (ElfArt.Assets != null && ElfArt.Assets.FontBlackOutline != null) _big.fontSharedMaterial = ElfArt.Assets.FontBlackOutline;
        _big.gameObject.SetActive(false);

        // Invisible operator Quit button, top right corner.
        GameObject quit = new GameObject("QuitButton", typeof(RectTransform));
        quit.transform.SetParent(transform, false);
        RectTransform q = (RectTransform)quit.transform;
        q.anchorMin = q.anchorMax = q.pivot = new Vector2(1f, 1f);
        q.sizeDelta = new Vector2(140f, 140f);
        quit.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        quit.AddComponent<Button>().onClick.AddListener(() => onQuit());

        BuildStats();
        BuildSplash();
        BuildBoard();
        BuildEnding();
    }

    #region Tutorial
    GameObject _tutorial;
    Image _tutorialGauge;
    Image[] _chips;
    Color[] _chipColors;
    TextMeshProUGUI[] _chipLabels;
    float[] _pulse;

    /// <summary>The player's tile bounces (he just jumped).</summary>
    public void PulseTile(int i) { if (_pulse != null && i < _pulse.Length) _pulse[i] = 1f; }

    /// <summary>Tutorial card (one page, MM style): "Jump once to join!" with a time gauge, one tile per player of
    /// this screen (grey "Jump!" until the player jumped, then his color "Ready!"), and three illustrated panels: the
    /// machine with an elf on its button over a toy, the toy stages (box > carved > painted > gift), the unfinished
    /// gift. Pictures: ElfIcons (null: panel without picture).</summary>
    public void BuildTutorial(string[] letters, Color[] colors, Sprite machine, Sprite[] steps)
    {
        ElfAssets a = ElfArt.Assets;
        _tutorial = CardBox(transform, "Tutorial", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1560f, 800f));
        _tutorial.transform.SetSiblingIndex(_white.transform.GetSiblingIndex());
        Transform card = _tutorial.transform;
        TextMeshProUGUI title = Text(card, "Title", 70f, TextAlignmentOptions.Center, ElfArt.Ink);
        Place(title.rectTransform, new Vector2(0f, 345f), new Vector2(1400f, 90f));
        title.text = ElfLoc.Get("TUTO_JOIN");

        // Time gauge under the title.
        Image back = Child(card, "GaugeBack", new Vector2(620f, 22f), WhiteSprite(), new Color32(0xE2, 0xE8, 0xF0, 255));
        Place((RectTransform)back.transform, new Vector2(0f, 285f), new Vector2(620f, 22f));
        _tutorialGauge = Child(back.transform, "Gauge", new Vector2(620f, 22f), WhiteSprite(), ElfArt.HudGold);
        _tutorialGauge.type = Image.Type.Filled;
        _tutorialGauge.fillMethod = Image.FillMethod.Horizontal;

        // Player tiles.
        int n = letters.Length;
        _chips = new Image[n];
        _chipColors = colors;
        _chipLabels = new TextMeshProUGUI[n];
        _pulse = new float[n];
        for (int i = 0; i < n; i++)
        {
            float x = (i - (n - 1) * 0.5f) * 190f;
            Image tile = Child(card, "Tile" + letters[i], new Vector2(128f, 128f), a != null ? a.Rounded : null, Color.grey);
            if (a != null && a.Rounded != null) { tile.type = Image.Type.Sliced; tile.pixelsPerUnitMultiplier = 1.6f; }
            Place((RectTransform)tile.transform, new Vector2(x, 175f), new Vector2(128f, 128f));
            TextMeshProUGUI letter = Text(tile.transform, "Letter", 78f, TextAlignmentOptions.Center, Color.white);
            if (a != null && a.FontBlackOutline != null) letter.fontSharedMaterial = a.FontBlackOutline;
            letter.text = letters[i];
            _chips[i] = tile;
            _chipLabels[i] = Text(card, "State" + letters[i], 30f, TextAlignmentOptions.Center, new Color32(0x94, 0xA3, 0xB8, 255));
            Place(_chipLabels[i].rectTransform, new Vector2(x, 88f), new Vector2(190f, 40f));
        }

        // Illustrated panels.
        string[] captions = { ElfLoc.Get("TUTO_2"), ElfLoc.Get("TUTO_STEPS"), ElfLoc.Get("TUTO_3") };
        for (int p = 0; p < 3; p++)
        {
            Image panel = Child(card, "Panel" + p, new Vector2(480f, 420f), a != null ? a.Rounded : null, ElfIcons.Panel);
            if (a != null && a.Rounded != null) { panel.type = Image.Type.Sliced; panel.pixelsPerUnitMultiplier = 0.6f; }
            Place((RectTransform)panel.transform, new Vector2((p - 1) * 505f, -165f), new Vector2(480f, 420f));
            Transform pt = panel.transform;
            if (p == 0 && machine != null)
            {
                Image picture = Child(pt, "Picture", Vector2.zero, machine, Color.white);
                picture.preserveAspect = true;
                Place((RectTransform)picture.transform, new Vector2(0f, 62f), new Vector2(440f, 290f));
            }
            else if (p == 1 && steps != null)
            {
                for (int s = 0; s < steps.Length; s++)
                {
                    if (steps[s] == null) continue;
                    Place((RectTransform)Child(pt, "Step" + s, Vector2.zero, steps[s], Color.white).transform, new Vector2((s - 1.5f) * 112f, 62f), new Vector2(96f, 96f));
                    if (s == steps.Length - 1) continue;
                    TextMeshProUGUI arrow = Text(pt, "Arrow", 44f, TextAlignmentOptions.Center, new Color32(0xB4, 0x53, 0x09, 255));
                    Place(arrow.rectTransform, new Vector2((s - 1f) * 112f, 62f), new Vector2(30f, 60f));
                    arrow.text = ">";
                }
            }
            else if (p == 2)
            {
                Image gift = Child(pt, "Unfinished", Vector2.zero, a != null ? a.GiftIcon : null, a != null && a.GiftIcon != null ? ElfArt.Unfinished : new Color(0f, 0f, 0f, 0f));
                gift.preserveAspect = true;
                Place((RectTransform)gift.transform, new Vector2(0f, 62f), new Vector2(220f, 220f));
                TextMeshProUGUI mark = Text(gift.transform, "Mark", 150f, TextAlignmentOptions.Center, new Color(1f, 0.2f, 0.2f));
                if (a != null && a.FontBlackOutline != null) mark.fontSharedMaterial = a.FontBlackOutline;
                mark.text = "?";
            }
            TextMeshProUGUI caption = Text(pt, "Caption", 32f, TextAlignmentOptions.Center, ElfArt.Ink);
            Place(caption.rectTransform, new Vector2(0f, -150f), new Vector2(450f, 110f));
            caption.textWrappingMode = TextWrappingModes.Normal;
            caption.text = captions[p];
        }
        _tutorial.SetActive(false);
    }

    /// <summary>Shows the tutorial card: remaining time (1 to 0) and which players of this screen jumped.</summary>
    public void SetTutorial(bool show, float remaining01, bool[] jumped)
    {
        if (_tutorial == null) return;
        if (_tutorial.activeSelf != show) _tutorial.SetActive(show);
        if (!show) return;
        _tutorialGauge.fillAmount = Mathf.Clamp01(remaining01);
        for (int i = 0; i < _chips.Length && i < jumped.Length; i++)
        {
            _chips[i].color = jumped[i] ? _chipColors[i] : new Color32(0x6B, 0x72, 0x80, 255);
            // Bounce at each jump: grows and hops up, then settles.
            _pulse[i] = Mathf.MoveTowards(_pulse[i], 0f, Time.deltaTime * 3f);
            float bounce = Mathf.Sin((1f - _pulse[i]) * Mathf.PI) * (_pulse[i] > 0f ? 1f : 0f);
            _chips[i].transform.localScale = Vector3.one * ((jumped[i] ? 1.08f : 1f) + bounce * 0.4f);
            RectTransform tr = (RectTransform)_chips[i].transform;
            tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, 175f + bounce * 30f);
            string state = ElfLoc.Get(jumped[i] ? "TUTO_READY" : "TUTO_JUMP");
            if (_chipLabels[i].text != state) _chipLabels[i].text = state;
        }
    }

    static void Place(RectTransform r, Vector2 position, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = position;
        r.sizeDelta = size;
    }
    #endregion

    #region Splash and stats
    GameObject _splash, _statsCard;
    TextMeshProUGUI _giftsText, _unfinishedText;

    /// <summary>Splash: the game logo (ElfAssets.Logo, else the title in big outlined letters) over the scene.</summary>
    void BuildSplash()
    {
        ElfAssets a = ElfArt.Assets;
        _splash = new GameObject("Splash", typeof(RectTransform));
        _splash.transform.SetParent(transform, false);
        Place((RectTransform)_splash.transform, new Vector2(0f, 60f), new Vector2(1400f, 700f));
        _splash.AddComponent<CanvasGroup>();
        if (a != null && a.Logo != null)
        {
            Image logo = _splash.AddComponent<Image>();
            logo.sprite = a.Logo;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
        }
        else
        {
            TextMeshProUGUI title = Text(_splash.transform, "Title", 190f, TextAlignmentOptions.Center, ElfArt.HudGold);
            if (a != null && a.FontBlackOutline != null) title.fontSharedMaterial = a.FontBlackOutline;
            title.textWrappingMode = TextWrappingModes.Normal;
            title.text = ElfLoc.Get("TITLE");
        }
        _splash.SetActive(false);
    }

    /// <summary>t: 0 to 1 over the splash (pops in, stays, fades out at the end); outside [0, 1) hides it.</summary>
    public void SetSplash(float t)
    {
        bool show = t >= 0f && t < 1f;
        if (_splash.activeSelf != show) _splash.SetActive(show);
        if (!show) return;
        float pop = Mathf.Clamp01(t / 0.15f);
        _splash.transform.localScale = Vector3.one * (0.6f + 0.4f * (1f - Mathf.Pow(1f - pop, 3f)) + Mathf.Sin(pop * Mathf.PI) * 0.08f);
        _splash.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01((1f - t) / 0.12f);
    }

    /// <summary>Top-left white card: gifts delivered, unfinished gifts loaded (shown with the timer).</summary>
    void BuildStats()
    {
        ElfAssets a = ElfArt.Assets;
        _statsCard = CardBox(transform, "Stats", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(470f, 190f));
        _giftsText = StatRow(_statsCard.transform, 45f, a != null ? a.GiftIcon : null, Color.white, false);
        _unfinishedText = StatRow(_statsCard.transform, -45f, a != null ? a.GiftIcon : null, ElfArt.Unfinished, true);
        _statsCard.SetActive(false);
    }

    static TextMeshProUGUI StatRow(Transform card, float y, Sprite icon, Color tint, bool mark)
    {
        Image image = Child(card, "Icon", Vector2.zero, icon, icon != null ? tint : new Color(0f, 0f, 0f, 0f));
        image.preserveAspect = true;
        Place((RectTransform)image.transform, new Vector2(-170f, y), new Vector2(78f, 78f));
        if (mark)
        {
            TextMeshProUGUI q = Text(image.transform, "Mark", 60f, TextAlignmentOptions.Center, new Color(1f, 0.2f, 0.2f));
            if (ElfArt.Assets != null && ElfArt.Assets.FontBlackOutline != null) q.fontSharedMaterial = ElfArt.Assets.FontBlackOutline;
            q.text = "?";
        }
        TextMeshProUGUI text = Text(card, "Text", 32f, TextAlignmentOptions.Left, ElfArt.Ink);
        Place(text.rectTransform, new Vector2(50f, y), new Vector2(340f, 80f));
        text.lineSpacing = -14f;
        return text;
    }

    public void ShowStats(bool show) { if (_statsCard.activeSelf != show) _statsCard.SetActive(show); }

    public void SetStats(int gifts, int unfinished)
    {
        string g = $"<size=75%>{ElfLoc.Get("GIFTS")}</size>\n<color=#B45309><size=140%>{gifts}</size></color>";
        string u = $"<size=75%>{ElfLoc.Get("UNFINISHED")}</size>\n<size=140%>{unfinished}</size>";
        if (_giftsText.text != g) _giftsText.text = g;
        if (_unfinishedText.text != u) _unfinishedText.text = u;
    }
    #endregion


    #region Ceremony board
    GameObject _board;
    TextMeshProUGUI _boardGifts, _boardUnfinished;

    /// <summary>End board (ElfAssets.CeremonyBoard, top centre): "Gifts delivered" and the score in big letters on the
    /// main board, unfinished gifts (grey gift icon + "?", score) on the hanging plank.</summary>
    void BuildBoard()
    {
        ElfAssets a = ElfArt.Assets;
        const float W = 640f, H = W * 1342f / 2000f;
        _board = new GameObject("CeremonyBoard", typeof(RectTransform));
        _board.transform.SetParent(transform, false);
        RectTransform r = (RectTransform)_board.transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, -10f);
        r.sizeDelta = new Vector2(W, H);
        Image image = _board.AddComponent<Image>();
        image.sprite = a != null ? a.CeremonyBoard : null;
        image.color = image.sprite != null ? Color.white : new Color32(0x6B, 0x3F, 0x22, 255);
        image.raycastTarget = false;
        Color cream = new Color32(0xFF, 0xEB, 0xC8, 255);
        // Main board (interior around the middle of the picture).
        _boardGifts = Text(_board.transform, "Gifts", 40f, TextAlignmentOptions.Center, cream);
        PlaceIn(_boardGifts.rectTransform, new Vector2(0f, -0.005f * H), new Vector2(0.66f * W, 0.36f * H));
        _boardGifts.lineSpacing = -20f;
        if (a != null && a.FontBlackOutline != null) _boardGifts.fontSharedMaterial = a.FontBlackOutline;
        // Hanging plank: icon + unfinished gifts.
        Image icon = Child(_board.transform, "UnfinishedIcon", Vector2.zero, a != null ? a.GiftIcon : null, a != null && a.GiftIcon != null ? ElfArt.Unfinished : new Color(0f, 0f, 0f, 0f));
        icon.preserveAspect = true;
        PlaceIn((RectTransform)icon.transform, new Vector2(-0.17f * W, -0.326f * H), new Vector2(0.075f * H * 1.6f, 0.075f * H * 1.6f));
        TextMeshProUGUI mark = Text(icon.transform, "Mark", 34f, TextAlignmentOptions.Center, new Color(1f, 0.2f, 0.2f));
        if (a != null && a.FontBlackOutline != null) mark.fontSharedMaterial = a.FontBlackOutline;
        mark.text = "?";
        _boardUnfinished = Text(_board.transform, "Unfinished", 19f, TextAlignmentOptions.Left, cream);
        _boardUnfinished.textWrappingMode = TextWrappingModes.NoWrap;
        PlaceIn(_boardUnfinished.rectTransform, new Vector2(0.045f * W, -0.326f * H), new Vector2(0.36f * W, 0.1f * H));
        _boardUnfinished.enableAutoSizing = true;
        _boardUnfinished.fontSizeMin = 12f;
        _boardUnfinished.fontSizeMax = 22f;
        _board.SetActive(false);
    }

    static void PlaceIn(RectTransform r, Vector2 fromCentre, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = fromCentre;
        r.sizeDelta = size;
    }

    /// <summary>Shows the end board with the final scores (pops in); hide with show = false.</summary>
    public void ShowBoard(bool show, int gifts, int unfinished)
    {
        if (_board.activeSelf != show) _board.SetActive(show);
        if (!show) return;
        _boardGifts.text = $"<size=70%>{ElfLoc.Get("GIFTS")}</size>\n<size=230%><color=#FACC15>{gifts}</color></size>";
        _boardUnfinished.text = $"{ElfLoc.Get("UNFINISHED")}  <size=140%>{unfinished}</size>";
        _boardPop = Mathf.MoveTowards(_boardPop, 1f, Time.deltaTime * 3f);
        float k = 1f - Mathf.Pow(1f - _boardPop, 3f);
        _board.transform.localScale = Vector3.one * (0.6f + 0.4f * k + Mathf.Sin(_boardPop * Mathf.PI) * 0.06f);
    }

    float _boardPop;
    #endregion
    #region Ending (white fade, podium gauge, iris)
    Image _white, _gauge;
    RectTransform _iris;
    GameObject _gaugeRoot;
    TextMeshProUGUI _gaugeText;

    void BuildEnding()
    {
        ElfAssets a = ElfArt.Assets;
        // Round gauge of the podium (top centre, in place of the timer).
        _gaugeRoot = new GameObject("PodiumGauge", typeof(RectTransform));
        _gaugeRoot.transform.SetParent(transform, false);
        RectTransform gr = (RectTransform)_gaugeRoot.transform;
        gr.anchorMin = gr.anchorMax = new Vector2(1f, 1f); // top right (the end board takes the top centre)
        gr.pivot = new Vector2(1f, 1f);
        gr.anchoredPosition = new Vector2(-40f, -30f);
        gr.sizeDelta = new Vector2(170f, 170f);
        Image back = _gaugeRoot.AddComponent<Image>();
        back.sprite = a != null ? a.Disc : null;
        back.color = Card;
        back.raycastTarget = false;
        Shadow shadow = _gaugeRoot.AddComponent<Shadow>();
        shadow.effectColor = ShadowCol;
        shadow.effectDistance = new Vector2(0f, -6f);
        _gauge = Child(_gaugeRoot.transform, "Fill", new Vector2(150f, 150f), a != null ? a.Disc : null, ElfArt.HudGold);
        _gauge.type = Image.Type.Filled;
        _gauge.fillMethod = Image.FillMethod.Radial360;
        _gauge.fillOrigin = (int)Image.Origin360.Top;
        _gauge.fillClockwise = false;
        Child(_gaugeRoot.transform, "Inner", new Vector2(112f, 112f), a != null ? a.Disc : null, Color.white);
        _gaugeText = Text(_gaugeRoot.transform, "Seconds", 64f, TextAlignmentOptions.Center, ElfArt.Ink);
        _gaugeRoot.SetActive(false);

        // White fade (full screen).
        GameObject white = new GameObject("White", typeof(RectTransform));
        white.transform.SetParent(transform, false);
        RectTransform wr = (RectTransform)white.transform;
        wr.anchorMin = Vector2.zero;
        wr.anchorMax = Vector2.one;
        wr.offsetMin = wr.offsetMax = Vector2.zero;
        _white = white.AddComponent<Image>();
        _white.color = new Color(1f, 1f, 1f, 0f);
        _white.raycastTarget = false;
        white.SetActive(false);

        // Iris: a black square with a round hole, surrounded by black bands; it shrinks to close the screen.
        GameObject iris = new GameObject("Iris", typeof(RectTransform));
        iris.transform.SetParent(transform, false);
        _iris = (RectTransform)iris.transform;
        _iris.anchorMin = _iris.anchorMax = new Vector2(0.5f, 0.5f);
        Image hole = iris.AddComponent<Image>();
        hole.sprite = HoleSprite();
        hole.color = Color.black;
        hole.raycastTarget = false;
        Band(_iris, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(8000f, 8000f));
        Band(_iris, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(8000f, 8000f));
        Band(_iris, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(8000f, 0f));
        Band(_iris, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(8000f, 0f));
        iris.SetActive(false);
    }

    public void SetWhite(float alpha)
    {
        bool show = alpha > 0f;
        if (_white.gameObject.activeSelf != show) _white.gameObject.SetActive(show);
        _white.color = new Color(1f, 1f, 1f, alpha);
    }

    /// <summary>open: 1 = fully open (hidden), 0 = closed (black screen).</summary>
    public void SetIris(float open)
    {
        bool show = open < 1f;
        if (_iris.gameObject.activeSelf != show) _iris.gameObject.SetActive(show);
        if (!show) return;
        Rect r = ((RectTransform)transform).rect;
        float size = Mathf.Sqrt(r.width * r.width + r.height * r.height) * 1.05f * open * open; // eased: slows at the end
        _iris.sizeDelta = new Vector2(size, size);
    }

    /// <summary>Podium round gauge: fill 1 to 0 and the seconds left; fill &lt; 0 hides it.</summary>
    public void SetPodiumGauge(float fill, int seconds)
    {
        bool show = fill >= 0f;
        if (_gaugeRoot.activeSelf != show) _gaugeRoot.SetActive(show);
        if (!show) return;
        _gauge.fillAmount = fill;
        _gaugeText.text = Mathf.Max(0, seconds).ToString();
    }

    static Image Child(Transform parent, string name, Vector2 size, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void Band(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        GameObject go = new GameObject("Band", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }

    static Sprite _whiteSprite, _holeSprite;

    static Sprite WhiteSprite()
    {
        if (_whiteSprite == null) _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        return _whiteSprite;
    }

    /// <summary>Black square with a transparent round hole (soft edge), for the iris.</summary>
    static Sprite HoleSprite()
    {
        if (_holeSprite != null) return _holeSprite;
        const int n = 256;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte alpha = (byte)(Mathf.Clamp01((d - (n * 0.5f - 2f)) / 2f) * 255f);
                pixels[y * n + x] = new Color32(255, 255, 255, alpha);
            }
        t.SetPixels32(pixels);
        t.Apply();
        _holeSprite = Sprite.Create(t, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f));
        return _holeSprite;
    }
    #endregion

    public void SetTimer(string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (_timerCard.activeSelf != show) _timerCard.SetActive(show);
        if (show) _timer.text = text;
    }

    public const float CARD_W = 380f, SQUARE_W = 170f, ROW_H = 150f, GAP = 14f;
    public static readonly Color BestCard = new Color32(0xFF, 0xF1, 0x7A, 255); // leading team: yellow paper

    /// <summary>Bottom row: delivered gifts (gift icon), unfinished gifts loaded (grey gift + "?"), and per team a square
    /// with its step, its players' letters and its score (yellow card = leading team).</summary>
    /// <summary>Gift stat board content: icon (tinted; "?" mark for unfinished) + text. x advanced by the card width.</summary>
    public static TextMeshProUGUI StatCard(Transform parent, string name, ref float x, Sprite icon, Color iconTint, bool questionMark, Color textColor)
    {
        Transform card = WoodCard(parent, name, new Vector2(x, 0f), new Vector2(CARD_W, ROW_H), name.Length);
        x += CARD_W + GAP;
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(card, false);
        RectTransform ir = (RectTransform)iconGo.transform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
        ir.anchoredPosition = new Vector2(8f, 0f);
        ir.sizeDelta = new Vector2(100f, 100f);
        Image image = iconGo.AddComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.color = icon != null ? iconTint : new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        if (questionMark)
        {
            TextMeshProUGUI q = Text(iconGo.transform, "Mark", 80f, TextAlignmentOptions.Center, new Color(1f, 0.2f, 0.2f));
            if (ElfArt.Assets != null && ElfArt.Assets.FontBlackOutline != null) q.fontSharedMaterial = ElfArt.Assets.FontBlackOutline;
            q.text = "?";
        }
        TextMeshProUGUI text = Text(card, "Text", 30f, TextAlignmentOptions.Left, textColor);
        text.rectTransform.offsetMin = new Vector2(116f, 4f);
        text.rectTransform.offsetMax = new Vector2(-8f, -4f);
        text.lineSpacing = -10f;
        return text;
    }

    public static readonly Color PaperCol = new Color32(0xFF, 0xD9, 0xA6, 255);
    public static readonly Color WoodInk = new Color32(0x2B, 0x17, 0x0B, 255);

    /// <summary>Wooden board (UI/WoodPanel.png) with a sheet of paper pinned on it, slightly tilted (like the paper
    /// notes of a workshop). Returns the paper (content parent; its Image color changes for the leading team).</summary>
    public static Transform WoodCard(Transform parent, string name, Vector2 position, Vector2 size, int seed)
    {
        ElfAssets a = ElfArt.Assets;
        GameObject wood = new GameObject(name, typeof(RectTransform));
        wood.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)wood.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image woodImage = wood.AddComponent<Image>();
        woodImage.raycastTarget = false;
        if (a != null && a.WoodPanel != null) { woodImage.sprite = a.WoodPanel; woodImage.type = Image.Type.Sliced; }
        else woodImage.color = new Color32(0x8B, 0x55, 0x2B, 255);
        Shadow shadow = wood.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(0f, -6f);

        GameObject paper = new GameObject("Paper", typeof(RectTransform));
        paper.transform.SetParent(wood.transform, false);
        RectTransform p = (RectTransform)paper.transform;
        p.anchorMin = Vector2.zero;
        p.anchorMax = Vector2.one;
        p.offsetMin = new Vector2(16f, 16f);
        p.offsetMax = new Vector2(-16f, -16f);
        p.localRotation = Quaternion.Euler(0f, 0f, (seed % 3 - 1) * 1.5f);
        Image paperImage = paper.AddComponent<Image>();
        paperImage.color = PaperCol;
        paperImage.raycastTarget = false;
        if (a != null && a.Paper != null) { paperImage.sprite = a.Paper; paperImage.type = Image.Type.Sliced; }
        Shadow paperShadow = paper.AddComponent<Shadow>();
        paperShadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        paperShadow.effectDistance = new Vector2(2f, -3f);
        return paper.transform;
    }

    /// <summary>Card message (tutorial, results), or a big outlined text (countdown, GO) when big is true.</summary>
    public void SetMessage(string text, bool big = false)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (_messageCard.activeSelf != (show && !big)) _messageCard.SetActive(show && !big);
        if (_big.gameObject.activeSelf != (show && big)) _big.gameObject.SetActive(show && big);
        TextMeshProUGUI target = big ? _big : _message;
        if (show && target.text != text) target.text = text;
    }

    float _bigShake;

    /// <summary>0 to 1: the big text shakes, wobbles, pulses and flashes red / orange (machine malfunction).</summary>
    public void SetBigShake(float amount)
    {
        if (amount <= 0f && _bigShake <= 0f) return;
        _bigShake = amount;
        RectTransform r = _big.rectTransform;
        float t = Time.time;
        r.anchoredPosition = new Vector2(0f, 150f) + (amount > 0f ? new Vector2(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(-10f, 10f)) * amount : Vector2.zero);
        r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 31f) * 7f * amount);
        r.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(t * 11f)) * 0.18f * amount);
        _big.color = amount > 0f ? (Mathf.PingPong(t * 8f, 1f) > 0.5f ? new Color(1f, 0.25f, 0.15f) : new Color(1f, 0.7f, 0.1f)) : Color.white;
    }

    static GameObject CardBox(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = Card;
        image.raycastTarget = false;
        if (ElfArt.Assets != null && ElfArt.Assets.Rounded != null)
        {
            image.sprite = ElfArt.Assets.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.6f;
        }
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = ShadowCol;
        shadow.effectDistance = new Vector2(0f, -6f);
        return go;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (ElfArt.Assets != null && ElfArt.Assets.Font != null) tmp.font = ElfArt.Assets.Font;
        else tmp.fontStyle = FontStyles.Bold;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }
}
