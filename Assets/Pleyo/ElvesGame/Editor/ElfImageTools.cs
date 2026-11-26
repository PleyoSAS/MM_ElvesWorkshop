using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor image helpers.</summary>
public static class ElfImageTools
{
    /// <summary>Cuts an illustration out of its white background: flood fill from the borders through near-white
    /// pixels (the drawing's outline stops it), soft edge on the pixels touching the background. Saves a PNG at
    /// dstAsset (project path) imported as a sprite.</summary>
    public static string CutOutWhite(string srcFile, string dstAsset) { return CutOutWhite(srcFile, dstAsset, new Vector2Int[0]); }

    /// <summary>Same, plus white areas enclosed by the drawing: one seed pixel in each (x, y from the top left).</summary>
    public static string CutOutWhite(string srcFile, string dstAsset, Vector2Int[] enclosed)
    {
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(srcFile));
        int w = tex.width, h = tex.height;
        Color32[] px = tex.GetPixels32();
        bool[] bg = new bool[w * h];
        Queue<int> queue = new Queue<int>();
        System.Func<int, bool> white = i => { Color32 c = px[i]; int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b)), max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)); return min >= 232 && max - min < 20; };
        for (int x = 0; x < w; x++) { Seed(x, queue, bg, white); Seed((h - 1) * w + x, queue, bg, white); }
        for (int y = 0; y < h; y++) { Seed(y * w, queue, bg, white); Seed(y * w + w - 1, queue, bg, white); }
        foreach (Vector2Int p in enclosed) Seed((h - 1 - p.y) * w + p.x, queue, bg, white);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % w, y = i / w;
            if (x > 0) Seed(i - 1, queue, bg, white);
            if (x < w - 1) Seed(i + 1, queue, bg, white);
            if (y > 0) Seed(i - w, queue, bg, white);
            if (y < h - 1) Seed(i + w, queue, bg, white);
        }
        for (int i = 0; i < px.Length; i++)
        {
            if (bg[i]) { px[i] = new Color32(255, 255, 255, 0); continue; }
            int x = i % w, y = i / w;
            bool edge = (x > 0 && bg[i - 1]) || (x < w - 1 && bg[i + 1]) || (y > 0 && bg[i - w]) || (y < h - 1 && bg[i + w]);
            if (!edge) continue;
            // Anti-aliased border: the whiter, the more transparent.
            Color32 c = px[i];
            int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            px[i].a = (byte)Mathf.Clamp((255 - min) * 255 / 60, 0, 255);
        }
        tex = new Texture2D(w, h, TextureFormat.RGBA32, false); // a JPG loads as RGB24: no alpha
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(dstAsset, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(dstAsset);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(dstAsset);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        return $"{dstAsset} {w}x{h}";
    }

    static void Seed(int i, Queue<int> queue, bool[] bg, System.Func<int, bool> white)
    {
        if (bg[i] || !white(i)) return;
        bg[i] = true;
        queue.Enqueue(i);
    }
}
