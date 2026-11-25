using System.Collections.Generic;
using UnityEngine;

/// <summary>Pictures of the tutorial, rendered at start in a small studio far below the workshop (own camera and point
/// light, nothing else in range): the machine of the screen with an elf on its button and a toy under it, and the toy
/// stages (raw box, carved, painted, gift). Background = the tutorial panel color.</summary>
public static class ElfIcons
{
    public static readonly Color Panel = new Color32(0xE8, 0xEE, 0xF6, 255);
    static readonly Vector3 STUDIO = new Vector3(0f, -1000f, 0f);

    /// <summary>Raw box, carved (unpainted wood), painted, gift.</summary>
    public static Sprite[] ToySteps()
    {
        ElfAssets a = ElfArt.Assets;
        Sprite[] sprites = new Sprite[4];
        if (a == null) return sprites;
        GameObject carved = a.CarvedToys != null && a.CarvedToys.Length > 0 ? a.CarvedToys[0] : null;
        GameObject gift = a.Presents != null && a.Presents.Length > 0 ? a.Presents[0] : null;
        sprites[0] = Shot(a.RawBlock, null, 256);
        sprites[1] = Shot(carved, a.UnpaintedWood, 256);
        sprites[2] = Shot(carved, null, 256);
        sprites[3] = Shot(gift, null, 256);
        return sprites;
    }

    static Sprite Shot(GameObject prefab, Material replace, int size)
    {
        if (prefab == null) return null;
        Transform root = new GameObject("IconStudio").transform;
        root.position = STUDIO;
        GameObject model = ElfArt.Fit(prefab, root, 1f);
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        if (replace != null)
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int m = 0; m < mats.Length; m++) mats[m] = replace;
                r.sharedMaterials = mats;
            }
        Sprite sprite = Render(root, size, size, 18f);
        Object.DestroyImmediate(root.gameObject);
        return sprite;
    }

    /// <summary>Copy of the machine template (scene "---- Machines ----/name", hidden), an elf in the player color on its
    /// button on top, a strip of belt and a raw box under the piston.</summary>
    public static Sprite Machine(Transform template, float beltY, Color color, string letter)
    {
        ElfAssets a = ElfArt.Assets;
        Transform root = new GameObject("IconStudio").transform;
        root.position = STUDIO;
        if (template != null)
        {
            Transform group = Object.Instantiate(template.gameObject, root).transform;
            group.gameObject.SetActive(true);
            group.localPosition = new Vector3(0f, template.localPosition.y, template.localPosition.z);
            group.localRotation = template.localRotation;
            Transform machine = group.Find("Machine");
            Transform head = machine != null ? machine.Find("Head") : null;
            if (head != null) head.gameObject.SetActive(false);
            Transform piston = machine != null ? machine.Find("Piston") : null;
            if (piston != null) ElfArt.SetColor(piston.gameObject, color);
            Bounds hb = ElfArt.GetBounds(head != null ? head.gameObject : machine != null ? machine.gameObject : group.gameObject);
            Vector3 stand = piston != null && head == null ? new Vector3(piston.position.x, hb.max.y, piston.position.z) : new Vector3(hb.center.x, head != null ? hb.min.y : hb.max.y, hb.center.z);
            Transform button = null;
            if (a != null && a.ButtonPrefab != null)
            {
                button = Object.Instantiate(a.ButtonPrefab, root, false).transform;
                button.position = stand + Vector3.up * 0.24f;
                Transform cap = button.Find("Cap");
                if (cap != null) ElfArt.SetColor(cap.gameObject, color);
            }
            ElfAvatar elf = new ElfAvatar();
            elf.Build(root, root.InverseTransformPoint(stand + Vector3.up * 0.29f), color, letter, false, null);
            elf.ShowNow();
        }
        ElfArt.Prim(PrimitiveType.Cube, root, new Vector3(0f, beltY - 0.12f, 0f), new Vector3(3.4f, 0.24f, 1.7f), new Color(0.2f, 0.21f, 0.24f), "Belt");
        if (a != null && a.RawBlock != null)
        {
            Transform box = new GameObject("Box").transform;
            box.SetParent(root, false);
            box.localPosition = new Vector3(0f, beltY, 0f);
            ElfArt.Fit(a.RawBlock, box, ElfToy.SIZE * 0.8f);
        }
        foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        Sprite sprite = Render(root, 512, 400, 28f);
        Object.DestroyImmediate(root.gameObject);
        return sprite;
    }

    /// <summary>Tall structure of a machine (posts, feet, arches): drawn but left out of the framing, so the picture
    /// focuses on the elf, its button, the machine head and the toy.</summary>
    static bool IsFrame(Transform t, Transform root)
    {
        for (; t != null && t != root; t = t.parent)
            if (t.name.StartsWith("Post") || t.name.StartsWith("Foot") || t.name.StartsWith("Arch")) return true;
        return false;
    }

    /// <summary>Frames everything under root from the front (slightly above), renders it, returns a sprite.</summary>
    static Sprite Render(Transform root, int width, int height, float fov)
    {
        Bounds b = new Bounds(root.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r.GetComponent<TMPro.TMP_Text>() != null || IsFrame(r.transform, root)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        float aspect = (float)width / height;
        float halfV = Mathf.Max(b.extents.y, b.extents.x / aspect) * 1.12f;
        float distance = halfV / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) + b.extents.z;
        Vector3 dir = Quaternion.Euler(12f, 0f, 0f) * Vector3.forward;

        Camera cam = new GameObject("IconCamera").AddComponent<Camera>();
        cam.transform.SetParent(root, false);
        cam.transform.position = b.center - dir * distance;
        cam.transform.rotation = Quaternion.LookRotation(dir);
        cam.fieldOfView = fov;
        cam.aspect = aspect;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = distance * 3f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Panel;
        cam.enabled = false;
        Light light = new GameObject("IconLight").AddComponent<Light>();
        light.transform.SetParent(root, false);
        light.transform.position = cam.transform.position + Vector3.up * 3f;
        light.type = LightType.Point;
        light.range = distance * 3f;
        light.intensity = distance * 2.5f;
        light.shadows = LightShadows.None;

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
    }
}
