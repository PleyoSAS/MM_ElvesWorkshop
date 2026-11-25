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
    }

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
