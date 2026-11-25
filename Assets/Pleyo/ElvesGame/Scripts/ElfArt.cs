using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Shared visuals: store-pack assets (ElfAssets), flat-color URP Lit materials, primitives, 3D labels (Fredoka).</summary>
public static class ElfArt
{
    public static readonly Color Gold = new Color(1f, 0.8f, 0.15f);
    public static readonly Color Unfinished = new Color(0.45f, 0.45f, 0.47f);
    public static readonly Color SyncOk = new Color(0.25f, 1f, 0.35f);
    public static readonly Color SyncKo = new Color(1f, 0.55f, 0.1f);
    public static readonly Color Ink = new Color32(0x0F, 0x17, 0x2A, 255);
    public static readonly Color HudGold = new Color32(0xFA, 0xCC, 0x15, 255);
    public static readonly Color[] StepColors = { new Color(0.6f, 0.38f, 0.2f), new Color(0.2f, 0.5f, 0.95f), new Color(0.9f, 0.15f, 0.25f), new Color(0.2f, 0.7f, 0.3f) };

    public static ElfAssets Assets;

    static readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
    static Material _baseMaterial;
    static MaterialPropertyBlock _block;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _materials.Clear(); _baseMaterial = null; Assets = null; _block = null; _toyMaterials.Clear(); _readable.Clear(); }

    public static Material Mat(Color color)
    {
        if (_materials.TryGetValue(color, out Material material) && material != null)
            return material;
        if (_baseMaterial == null)
        {
            // The default primitive material is URP Lit and is always in builds (Shader.Find may not be).
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _baseMaterial = probe.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(probe);
        }
        material = new Material(_baseMaterial) { color = color };
        _materials[color] = material;
        return material;
    }

    public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Color color, string name = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        if (name != null)
            go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;
        SetColor(go, color);
        return go;
    }

    public static void SetColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = Mat(color);
    }

    /// <summary>Tints renderers through a property block (null color = back to their materials).</summary>
    public static void Tint(Renderer[] renderers, Color? color)
    {
        if (_block == null) _block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i] is SpriteRenderer || renderers[i].GetComponent<TMP_Text>() != null) continue;
            if (color == null) { renderers[i].SetPropertyBlock(null); continue; }
            _block.Clear();
            _block.SetColor("_BaseColor", color.Value);
            _block.SetColor("_Color", color.Value);
            renderers[i].SetPropertyBlock(_block);
        }
    }

    public static TextMeshPro Label(Transform parent, Vector3 localPosition, string text, float fontSize, Color color)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (Assets != null && Assets.Font != null)
        {
            tmp.font = Assets.Font;
            if (Assets.FontBlackOutline != null) tmp.fontSharedMaterial = Assets.FontBlackOutline;
        }
        else
        {
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
        }
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(12f, 2f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    /// <summary>Instance of a prefab under parent, scaled to the given height (or largest horizontal size when byWidth),
    /// centered on the parent in x / z, its bottom on the parent.</summary>
    public static GameObject Fit(GameObject prefab, Transform parent, float size, bool byWidth = false)
    {
        GameObject g = Object.Instantiate(prefab, parent, false);
        g.transform.localPosition = Vector3.zero;
        Bounds b = GetBounds(g);
        float current = byWidth ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
        g.transform.localScale = g.transform.localScale * (size * parent.lossyScale.x / Mathf.Max(0.001f, current));
        b = GetBounds(g);
        g.transform.position += parent.position - new Vector3(b.center.x, b.min.y, b.center.z);
        return g;
    }

    public static Bounds GetBounds(GameObject g)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    public static Color Grey(Color color) { return Color.Lerp(color, new Color(0.45f, 0.45f, 0.45f), 0.8f); }

    /// <summary>Low-poly golden crown (band, spikes with balls, gems), ~1 m wide.</summary>
    public static GameObject Crown(Transform parent)
    {
        Transform c = new GameObject("Crown").transform;
        c.SetParent(parent, false);
        Color deep = new Color(0.85f, 0.55f, 0.05f);
        Prim(PrimitiveType.Cylinder, c, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.12f, 0.9f), Gold);
        Prim(PrimitiveType.Cylinder, c, new Vector3(0f, 0.02f, 0f), new Vector3(0.96f, 0.035f, 0.96f), deep);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            GameObject spike = Prim(PrimitiveType.Cube, c, dir * 0.41f + new Vector3(0f, 0.36f, 0f), new Vector3(0.12f, 0.2f, 0.05f), Gold);
            spike.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, 45f);
            Prim(PrimitiveType.Sphere, c, dir * 0.41f + new Vector3(0f, 0.52f, 0f), Vector3.one * 0.08f, Gold);
            Prim(PrimitiveType.Sphere, c, dir * 0.45f + new Vector3(0f, 0.12f, 0f), Vector3.one * 0.1f, i % 2 == 0 ? new Color(0.9f, 0.1f, 0.2f) : new Color(0.15f, 0.35f, 0.95f));
        }
        return c.gameObject;
    }

    /// <summary>HUD-style pill in the world (dark rounded sprite + yellow Fredoka text).</summary>
    public static GameObject Pill(Transform parent, Vector3 localPosition, string text, float width)
    {
        GameObject root = new GameObject("Pill");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        if (Assets != null && Assets.Rounded != null)
        {
            SpriteRenderer sr = new GameObject("Back").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root.transform, false);
            sr.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            sr.sprite = Assets.Rounded;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(width, 0.8f);
            sr.color = new Color32(0x0F, 0x17, 0x2A, 235);
        }
        TextMeshPro t = Label(root.transform, Vector3.zero, text, 4.4f, HudGold);
        if (Assets != null && Assets.Font != null) t.fontSharedMaterial = Assets.Font.material;
        return root;
    }

    /// <summary>Player letter on a colored disc (world space).</summary>
    public static GameObject Badge(Transform parent, Vector3 localPosition, string letter, Color color)
    {
        GameObject root = new GameObject("Badge");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        if (Assets != null && Assets.Disc != null)
        {
            SpriteRenderer sr = new GameObject("Disc").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root.transform, false);
            sr.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            sr.sprite = Assets.Disc;
            sr.color = color;
            float s = 0.9f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
            sr.transform.localScale = Vector3.one * s;
        }
        TextMeshPro t = Label(root.transform, new Vector3(0f, 0.02f, 0f), letter, 6f, Color.white);
        if (Assets != null && Assets.FontOutline != null) t.fontSharedMaterial = Assets.FontOutline;
        return root;
    }

    /// <summary>Star burst (Assets.SparklePrefab) at a toy just transformed by a machine, white to the step color.</summary>
    public static void Sparkle(Vector3 position, Color color)
    {
        if (Assets == null || Assets.SparklePrefab == null) return;
        GameObject fx = Object.Instantiate(Assets.SparklePrefab, position, Quaternion.identity);
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            // Bigger, faster, twice as many stars as the prefab.
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(color, Color.white, 0.6f), Color.Lerp(color, Gold, 0.3f));
            main.startSizeMultiplier *= 1.7f;
            main.startSpeedMultiplier *= 1.3f;
            main.startLifetimeMultiplier *= 1.25f;
            ParticleSystem.EmissionModule emission = ps.emission;
            for (int b = 0; b < emission.burstCount; b++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(b);
                if (burst.count.constant > 1f) burst.count = burst.count.constant * 2.2f;
                emission.SetBurst(b, burst);
            }
            ps.Play();
        }
        // Flash of light on the surroundings.
        Light flash = new GameObject("Flash").AddComponent<Light>();
        flash.transform.SetParent(fx.transform, false);
        flash.type = LightType.Point;
        flash.color = Color.Lerp(color, Color.white, 0.4f);
        flash.range = 7f;
        flash.intensity = 25f;
        flash.shadows = LightShadows.None;
        Object.Destroy(flash.gameObject, 0.15f);
        Object.Destroy(fx, 2.5f);
    }

    #region Toy materials (glow + player color)
    static readonly Dictionary<(Material, Color, Color), Material> _toyMaterials = new Dictionary<(Material, Color, Color), Material>();
    static readonly Dictionary<Texture, Texture2D> _readable = new Dictionary<Texture, Texture2D>();

    /// <summary>Copy of a toy / gift material that can glow (emission on, black until a property block sets it). With a
    /// player color, its texture (palette atlas) is recolored: the pixels of the model's main color (same hue) take the
    /// player color, keeping their shading; the other colors stay. Cached.</summary>
    public static Material ToyMaterial(Material source, Color? main = null, Color? player = null)
    {
        if (source == null) return null;
        bool recolor = main != null && player != null;
        var key = (source, recolor ? main.Value : Color.clear, recolor ? player.Value : Color.clear);
        if (_toyMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
        Material m = new Material(source) { name = source.name + (recolor ? " (player)" : " (glow)") };
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        if (recolor)
        {
            Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
            if (tex != null) m.SetTexture("_BaseMap", Recolor(tex, main.Value, player.Value));
            else m.SetColor("_BaseColor", player.Value);
        }
        _toyMaterials[key] = m;
        return m;
    }

    static Texture2D Recolor(Texture source, Color main, Color player)
    {
        Texture2D readable = Readable(source);
        Color[] px = readable.GetPixels();
        Color.RGBToHSV(main, out float hm, out float sm, out float vm);
        Color.RGBToHSV(player, out float hp, out float sp, out float vp);
        for (int i = 0; i < px.Length; i++)
        {
            Color.RGBToHSV(px[i], out float h, out float s, out float v);
            float dh = Mathf.Abs(h - hm);
            if (s < 0.2f || Mathf.Min(dh, 1f - dh) > 0.06f) continue;
            Color c = Color.HSVToRGB(hp, Mathf.Clamp01(sp * s / Mathf.Max(0.01f, sm)), Mathf.Clamp01(vp * v / Mathf.Max(0.01f, vm)));
            c.a = px[i].a;
            px[i] = c;
        }
        Texture2D t = new Texture2D(readable.width, readable.height, TextureFormat.RGBA32, true, false) { name = source.name + " (player)", filterMode = source.filterMode, wrapMode = source.wrapMode };
        t.SetPixels(px);
        t.Apply(true, true);
        return t;
    }

    /// <summary>CPU-readable copy of any texture (blit through a render texture, works when Read/Write is off).</summary>
    public static Texture2D Readable(Texture source)
    {
        if (_readable.TryGetValue(source, out Texture2D cached) && cached != null) return cached;
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D t = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
        t.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        t.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        _readable[source] = t;
        return t;
    }

    /// <summary>Main color of a model: the saturated hue covering the largest surface (triangle areas, color read in
    /// the texture at each triangle's UV). Used by the builder (meshes are readable in the Editor).</summary>
    public static Color MainColor(GameObject model) { return MainColor(model, false); }

    /// <summary>ignoreTop: skips the top quarter of each mesh (the bow of a present, bigger than its paper).</summary>
    public static Color MainColor(GameObject model, bool ignoreTop)
    {
        float[] weight = new float[24];
        Color[] sum = new Color[24];
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;
            if (mesh == null) continue;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            Material[] mats = r.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
            {
                if (mats[sub] == null) continue;
                Texture tex = mats[sub].HasProperty("_BaseMap") ? mats[sub].GetTexture("_BaseMap") : mats[sub].mainTexture;
                Texture2D readable = tex != null ? Readable(tex) : null;
                Color tint = mats[sub].HasProperty("_BaseColor") ? mats[sub].GetColor("_BaseColor") : Color.white;
                int[] tris = mesh.GetTriangles(sub);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    if (ignoreTop && (vertices[tris[t]].y + vertices[tris[t + 1]].y + vertices[tris[t + 2]].y) / 3f > mesh.bounds.min.y + mesh.bounds.size.y * 0.75f) continue;
                    float area = Vector3.Cross(vertices[tris[t + 1]] - vertices[tris[t]], vertices[tris[t + 2]] - vertices[tris[t]]).magnitude;
                    Color c = tint;
                    if (readable != null && uv.Length == vertices.Length)
                    {
                        Vector2 u = (uv[tris[t]] + uv[tris[t + 1]] + uv[tris[t + 2]]) / 3f;
                        c *= readable.GetPixelBilinear(u.x - Mathf.Floor(u.x), u.y - Mathf.Floor(u.y));
                    }
                    Color.RGBToHSV(c, out float h, out float s, out float v);
                    if (s < 0.25f || v < 0.15f) continue;
                    int bin = Mathf.Min(23, (int)(h * 24f));
                    weight[bin] += area;
                    sum[bin] += c * area;
                }
            }
        }
        int best = 0;
        // Neighbor hue bins merged: a gradient texture spreads one color over several bins.
        float bestScore = -1f;
        for (int i = 0; i < 24; i++)
        {
            float score = weight[(i + 23) % 24] + weight[i] + weight[(i + 1) % 24];
            if (score > bestScore) { bestScore = score; best = i; }
        }
        float w = 0f;
        Color total = Color.clear;
        for (int k = -1; k <= 1; k++) { w += weight[(best + k + 24) % 24]; total += sum[(best + k + 24) % 24]; }
        return w > 0f ? total / w : Color.white;
    }
    #endregion

    /// <summary>Transparent (alpha-blended) copy of a URP Lit material, for fading a model out / in.</summary>
    public static Material Transparent(Material source)
    {
        if (source == null) return null;
        Material m = new Material(source) { name = source.name + " (fade)" };
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        return m;
    }
}
