using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Score boards hung on the sleighs in front of the belt (world-space canvases, wood + paper like
/// ElfHud.WoodCard): gifts delivered, unfinished gifts loaded and one board per team (step, letters, score; yellow
/// paper = leading team), one per sleigh of the claw row.</summary>
public class ElfBoards
{
    static float PX = 0.0063f; // meters per canvas pixel: set so a stat board is 85% of a sleigh wide

    readonly List<TextMeshProUGUI> _teams = new List<TextMeshProUGUI>();
    readonly List<Image> _teamPapers = new List<Image>();
    readonly List<TextMeshProUGUI> _gifts = new List<TextMeshProUGUI>(), _unfinished = new List<TextMeshProUGUI>();

    const float BELT_PX = 0.009f; // gifts / unfinished boards on the belt front


    /// <summary>Team boards on the front of the loader's sleigh row (line order left to right, Load on the sleigh under
    /// the claw); gifts / unfinished boards hung on the belt front, in the widest free spaces of each screen.</summary>
    public void Build(Transform root, int teams, ElfStation loader, int screens, float screenWidth, float endX, float beltTop, float beltFrontZ)
    {
        if (loader != null)
        {
            PX = loader.SleighWidth * 0.85f / ElfHud.CARD_W;
            for (int i = 0; i < teams; i++)
            {
                Transform paper = Board(root, "TeamBoard" + i, loader.SleighBoardPose(teams - 1 - i), ElfHud.SQUARE_W, i, out Transform canvas);
                canvas.localScale = Vector3.one * PX * 1.2f;
                TextMeshProUGUI text = ElfHud.Text(paper, "Text", 30f, TextAlignmentOptions.Center, ElfHud.WoodInk);
                text.rectTransform.offsetMin = new Vector2(8f, 6f);
                text.rectTransform.offsetMax = new Vector2(-8f, -6f);
                text.lineSpacing = -12f;
                _teams.Add(text);
                _teamPapers.Add(paper.GetComponent<Image>());
            }
        }

        // Belt front: free except at the spawner (screen 1, skipped by from) and in front of the claw column.
        List<Vector2> used = new List<Vector2>();
        if (loader != null) used.Add(new Vector2(loader.MachineX - 1.2f, loader.MachineX + 1.2f));
        float statW = ElfHud.CARD_W * BELT_PX;
        ElfAssets a = ElfArt.Assets;
        for (int s = 0; s < screens; s++)
        {
            float from = s * screenWidth + (s == 0 ? 3.4f : 0.3f);
            float to = Mathf.Min((s + 1) * screenWidth - 0.3f, endX - 0.2f);
            // Two boards per screen, spread: each in its half of the screen.
            for (int k = 0; k < 2; k++)
            {
                float mid = (from + to) * 0.5f;
                if (!WidestGap(used, k == 0 ? from : mid, k == 0 ? mid : to, out float gapFrom, out float gapTo)) continue;
                float scale = Mathf.Min(1f, (gapTo - gapFrom - 0.2f) / statW);
                if (scale < 0.5f) continue;
                float cx = (gapFrom + gapTo) * 0.5f;
                Pose belt = ElfPath.At(cx);
                Board(root, k == 0 ? "GiftsBoard" : "UnfinishedBoard", new Pose(belt.position + belt.rotation * new Vector3(0f, beltTop, beltFrontZ), belt.rotation), ElfHud.CARD_W, k, out Transform canvas);
                canvas.localScale = Vector3.one * BELT_PX * scale;
                Object.Destroy(canvas.GetChild(0).gameObject); // StatCard draws its own wood card
                float x = -ElfHud.CARD_W * 0.5f;
                TextMeshProUGUI text = k == 0
                    ? ElfHud.StatCard(canvas, "Gifts", ref x, a != null ? a.GiftIcon : null, Color.white, false, new Color32(0xB4, 0x53, 0x09, 255))
                    : ElfHud.StatCard(canvas, "Unfinished", ref x, a != null ? a.GiftIcon : null, ElfArt.Unfinished, true, ElfHud.WoodInk);
                (k == 0 ? _gifts : _unfinished).Add(text);
                used.Add(new Vector2(cx - statW * scale * 0.5f, cx + statW * scale * 0.5f));
            }
        }
    }

    static bool WidestGap(List<Vector2> used, float from, float to, out float bestFrom, out float bestTo)
    {
        List<Vector2> blocks = new List<Vector2>(used);
        blocks.Sort((p, q) => p.x.CompareTo(q.x));
        bestFrom = bestTo = 0f;
        float cursor = from, best = 0f;
        foreach (Vector2 b in blocks)
        {
            if (b.y < from || b.x > to) continue;
            if (b.x - cursor > best) { best = b.x - cursor; bestFrom = cursor; bestTo = b.x; }
            cursor = Mathf.Max(cursor, b.y);
        }
        if (to - cursor > best) { best = to - cursor; bestFrom = cursor; bestTo = to; }
        return best > 0f;
    }

    public void Set(string gifts, string unfinished, string[] teamTexts, int best)
    {
        for (int i = 0; i < _gifts.Count; i++) if (_gifts[i].text != gifts) _gifts[i].text = gifts;
        for (int i = 0; i < _unfinished.Count; i++) if (_unfinished[i].text != unfinished) _unfinished[i].text = unfinished;
        for (int i = 0; i < _teams.Count && i < teamTexts.Length; i++)
        {
            if (_teams[i].text != teamTexts[i]) _teams[i].text = teamTexts[i];
            Color c = i == best ? ElfHud.BestCard : ElfHud.PaperCol;
            if (_teamPapers[i].color != c) _teamPapers[i].color = c;
        }
    }

    /// <summary>World-space canvas hung by its top center at position, holding a wood + paper card; returns the paper.</summary>
    static Transform Board(Transform root, string name, Pose pose, float widthPx, int seed, out Transform canvasTransform)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rect = (RectTransform)go.transform;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(widthPx, ElfHud.ROW_H);
        rect.localScale = Vector3.one * PX;
        rect.SetPositionAndRotation(pose.position, pose.rotation);
        canvasTransform = go.transform;
        return ElfHud.WoodCard(go.transform, "Card", new Vector2(-widthPx * 0.5f, 0f), new Vector2(widthPx, ElfHud.ROW_H), seed);
    }
}
