using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>World-space score boards (canvases, wood + paper like ElfHud.WoodCard): one board per team on the sleighs
/// of the claw row (step, letters, score; yellow paper = leading team), and one board per player on the belt front in
/// front of his machine (his score, in his color). Gifts / unfinished gifts are on the HUD (ElfHud.SetStats).</summary>
public class ElfBoards
{
    static float PX = 0.0063f; // meters per canvas pixel: set so a stat board is 85% of a sleigh wide

    readonly List<TextMeshProUGUI> _teams = new List<TextMeshProUGUI>();
    readonly List<Image> _teamPapers = new List<Image>();

    const float BELT_PX = 0.0065f; // player boards on the belt front (short enough to clear the floor)


    /// <summary>Team boards on the front of the loader's sleigh row (line order left to right, Load on the sleigh under
    /// the claw); player boards on the belt front.</summary>
    public void Build(Transform root, int teams, ElfStation loader, List<ElfStation> stations, System.Func<int, Color> color, System.Func<int, string> letter, float beltTop, float beltFrontZ)
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

        // Belt front: one board per player, in front of his machine (left of the claw column for a claw).
        foreach (ElfStation station in stations)
            for (int m = 0; m < station.Members.Count; m++)
            {
                float x = station.MachineX + (station.IsLoader && ElfPath.Current == ElfPath.Layout.Straight ? -1.6f : 0f) + (m - (station.Members.Count - 1) * 0.5f) * 1.7f;
                Pose belt = ElfPath.At(x);
                Transform paper = Board(root, "PlayerBoard", new Pose(belt.position + belt.rotation * new Vector3(0f, beltTop, beltFrontZ), belt.rotation), ElfHud.SQUARE_W, m + station.Index, out Transform canvas);
                canvas.localScale = Vector3.one * BELT_PX;
                TextMeshProUGUI text = ElfHud.Text(paper, "Score", 96f, TextAlignmentOptions.Center, Color.white);
                text.rectTransform.offsetMin = new Vector2(6f, 4f);
                text.rectTransform.offsetMax = new Vector2(-6f, -4f);
                if (ElfArt.Assets != null && ElfArt.Assets.FontBlackOutline != null) text.fontSharedMaterial = ElfArt.Assets.FontBlackOutline;
                text.outlineWidth = 0.32f; // thick black outline
                text.outlineColor = Color.black;
                _players.Add(new PlayerBoard { Station = station, Member = m, Text = text, Color = color(station.Members[m]) });
            }
    }

    class PlayerBoard { public ElfStation Station; public int Member; public TextMeshProUGUI Text; public Color Color; }
    readonly List<PlayerBoard> _players = new List<PlayerBoard>();

    /// <summary>Player boards: the score of the player's machine, in his color (grey while inactive).</summary>
    public void SetPlayers()
    {
        foreach (PlayerBoard p in _players)
        {
            // Just the score, in the player color (grey while inactive), thick black outline.
            string text = p.Station.MemberScore[p.Member].ToString();
            Color c = p.Station.IsMemberActive(p.Member) ? p.Color : new Color32(0x94, 0xA3, 0xB8, 255);
            if (p.Text.color != c) p.Text.color = c;
            if (p.Text.text != text) p.Text.text = text;
        }
    }

    public void Set(string[] teamTexts, int best)
    {
        for (int i = 0; i < _teams.Count && i < teamTexts.Length; i++)
        {
            if (_teams[i].text != teamTexts[i]) _teams[i].text = teamTexts[i];
            Color c = i == best ? ElfHud.BestCard : ElfHud.PaperCol;
            if (_teamPapers[i].color != c) _teamPapers[i].color = c;
        }
    }


    /// <summary>Small wood + paper board (child of parent, at pose) with one big number (gifts in a sleigh).</summary>
    public static TextMeshProUGUI CountBoard(Transform parent, Pose pose, float metersPerPixel)
    {
        Transform paper = Board(parent, "CountBoard", pose, ElfHud.SQUARE_W, 1, out Transform canvas);
        canvas.localScale = Vector3.one * metersPerPixel;
        TextMeshProUGUI text = ElfHud.Text(paper, "Count", 110f, TextAlignmentOptions.Center, ElfHud.WoodInk);
        text.text = "0";
        return text;
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
