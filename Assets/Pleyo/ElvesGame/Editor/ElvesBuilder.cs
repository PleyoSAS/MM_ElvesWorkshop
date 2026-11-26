using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor tools of Elves' Workshop:
/// - "Rebuild Assets and Prefabs": fills Resources-free ElfAssets.asset (store-pack references) and creates the game
///   prefabs (machine, claw loader, sleigh, bin, button) in Assets/Pleyo/ElvesGame/Prefabs. Existing prefabs are kept
///   (your edits stay); delete one to have it rebuilt.
/// - "Build Workshop In Open Scene": puts the 1-screen workshop "---- Workshop ----" from Prefabs/Workshop.prefab (the
///   developer's edited version; "Save Workshop As Prefab" updates it); without the prefab, generates the original decor.
/// </summary>
public static class ElvesBuilder
{
    const string Dir = "Assets/Pleyo/ElvesGame/";
    const string PrefabDir = Dir + "Prefabs/";
    const string MatDir = Dir + "Materials/";
    const string Cute = "Assets/ithappy/Cute_Characters/";
    const string Furniture = "Assets/ithappy/Furniture_Cute/";
    const string Xmas = "Assets/ithappy/Christmas_Pack_FREE/Prefabs/";
    const string Factory = "Assets/EKstudio/LowPoly Factory Machine Pack Demo/";
    public const float ScreenWidth = 20f;

    #region Assets
    [MenuItem("Elves Workshop/Rebuild Assets and Prefabs")]
    public static string RebuildAssets()
    {
        Folder(Dir, "Prefabs"); Folder(Dir, "Materials");
        string path = Dir + "ElfAssets.asset";
        ElfAssets a = AssetDatabase.LoadAssetAtPath<ElfAssets>(path);
        if (a == null) { a = ScriptableObject.CreateInstance<ElfAssets>(); AssetDatabase.CreateAsset(a, path); }

        a.ElfModel = Load<GameObject>(Cute + "Prefabs/Base_Model.prefab");
        a.ElfController = Load<RuntimeAnimatorController>(Dir + "Animation/ElfKid.controller");
        a.ElfBodies = Meshes("Body", "Body_09", "Body_10", "Body_11", "Body_12");
        a.ElfFaces = Meshes("Faces", "Male_Emotion_Happy_01", "Female_Emotion_Happy_01", "Male_Emotion_Happy_02", "Female_Emotion_Happy_02");
        // Only hairstyles that stay under the elf hat (checked in a render; the long / big ones go through it).
        a.ElfHairs = Meshes("Hairstyle", "Hairstyle_Male_13", "Hairstyle_Female_05", "Hairstyle_Male_16", "Hairstyle_Female_03", "Hairstyle_Female_09");
        a.ElfOutwear = Meshes("Outwear", "Outwear_38", "Outwear_36", "Outwear_01");
        Mesh[] hat = Meshes("Hat", "Hat_41");
        a.ElfHat = hat.Length > 0 ? hat[0] : null;

        a.RawBlock = Load<GameObject>(Factory + "Prefabs/Props/BoxWooden.prefab");
        a.CarvedToys = new[] { Load<GameObject>(Furniture + "Prefabs/Props/Toy_05.prefab"), Load<GameObject>(Furniture + "Prefabs/Props/Toy_03.prefab") };
        a.Presents = Enumerable.Range(1, 6).Select(i => Load<GameObject>(Xmas + $"Present_00{i}.prefab")).Where(g => g != null).ToArray();
        a.UnpaintedWood = Mat("ToyWood", new Color(0.86f, 0.68f, 0.46f));
        a.CarvedMainColors = a.CarvedToys.Select(t => t != null ? ElfArt.MainColor(t) : Color.white).ToArray();
        a.PresentMainColors = a.Presents.Select(p => ElfArt.MainColor(p, true)).ToArray();

        a.MachinePrefab = Prefab("ElfMachine", BuildMachine);
        a.ClawPrefab = Prefab("ElfClaw", BuildClaw);
        a.SleighPrefab = Prefab("ElfSleigh", BuildSleigh);
        a.BinPrefab = Prefab("ElfBin", BuildBin);
        a.ButtonPrefab = Prefab("ElfButton", BuildButton);
        a.SparklePrefab = Prefab("ElfSparkle", BuildSparkle);

        a.Font = Load<TMP_FontAsset>(Dir + "Fonts/Fredoka-Bold SDF.asset");
        a.FontOutline = Load<Material>(Dir + "Fonts/Fredoka Outline.mat");
        a.FontBlackOutline = Load<Material>(Dir + "Fonts/Fredoka Black Outline.mat");
        a.Rounded = Load<Sprite>(Dir + "UI/RoundedRect.png");
        a.Disc = Load<Sprite>(Dir + "UI/DiscHD.png");
        a.GiftIcon = RenderIcon(Load<GameObject>(Xmas + "Present_001.prefab"), Dir + "UI/GiftIcon.png");
        a.WoodPanel = BoardSprite(Dir + "UI/WoodPanel.png", true);
        a.Paper = BoardSprite(Dir + "UI/Paper.png", false);
        EditorUtility.SetDirty(a);
        AssetDatabase.SaveAssets();

        ElfGame game = Object.FindAnyObjectByType<ElfGame>(FindObjectsInactive.Include);
        if (game != null)
        {
            SerializedObject so = new SerializedObject(game);
            so.FindProperty("_assets").objectReferenceValue = a;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }
        return $"assets ok, model={a.ElfModel != null} hat={a.ElfHat != null} toys={a.CarvedToys.Count(t => t != null)} presents={a.Presents.Length} font={a.Font != null} rounded={a.Rounded != null} game={game != null}";
    }

    static GameObject Prefab(string name, System.Action<Transform> build)
    {
        string path = PrefabDir + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        GameObject root = new GameObject(name);
        build(root.transform);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    /// <summary>Gantry machine across the belt (posts in front of / behind the belt, toys pass under the head). Head =
    /// EKstudio machine facing the camera. Runtime anchors: Piston, Stripe (step color), Lights (one light per player is
    /// added there), Label (step name and sync sign above).</summary>
    static void BuildMachine(Transform r)
    {
        Material metal = Mat("MachineMetal", new Color(0.55f, 0.58f, 0.64f));
        Material gold = Mat("MachineGold", new Color(1f, 0.78f, 0.2f));
        foreach (float z in new[] { -1.75f, 1.75f })
        {
            Box(r, "Post", new Vector3(0f, 1.95f, z), new Vector3(0.45f, 3.9f, 0.45f), gold);
            Box(r, "Foot", new Vector3(0f, 0.08f, z), new Vector3(1.1f, 0.16f, 0.8f), metal);
        }
        GameObject head = Place(Load<GameObject>(Factory + "Prefabs/Machine/Machn_2.prefab"), r, new Vector3(0f, 3.75f, 0f), 3.1f, 0f);
        if (head != null) head.name = "Head";
        Box(r, "Stripe", new Vector3(0f, 3.95f, -1.72f), new Vector3(3.1f, 0.35f, 0.06f), Mat("MachineStripe", Color.white));
        GameObject piston = Prim(PrimitiveType.Cylinder, r, "Piston", new Vector3(0f, 3.45f, 0f), new Vector3(1.2f, 0.3f, 1.2f), Mat("MachinePiston", Color.white));
        Prim(PrimitiveType.Cylinder, r, "Rod", new Vector3(0f, 3.9f, 0f), new Vector3(0.4f, 0.3f, 0.4f), metal).transform.SetParent(piston.transform, true);
        Empty(r, "Lights", new Vector3(0f, 2.2f, -2.0f)); // on the front post, facing the camera
        Empty(r, "Label", new Vector3(0f, 7.5f, -0.8f));
    }

    /// <summary>Claw loader: column behind the belt, yawing arm (anchor "Arm", pointing -z toward the belt at rest),
    /// cable and claw at the arm tip (anchor "Claw", moved down / up at runtime).</summary>
    static void BuildClaw(Transform r)
    {
        Material metal = Mat("MachineMetal", new Color(0.55f, 0.58f, 0.64f));
        Material yellow = Mat("ClawYellow", new Color(1f, 0.72f, 0.1f));
        Material dark = Mat("ClawDark", new Color(0.18f, 0.2f, 0.24f));
        Prim(PrimitiveType.Cylinder, r, "Base", new Vector3(0f, 0.15f, 0f), new Vector3(1.4f, 0.15f, 1.4f), dark);
        Prim(PrimitiveType.Cylinder, r, "Column", new Vector3(0f, 2.3f, 0f), new Vector3(0.55f, 2.2f, 0.55f), yellow);
        Transform arm = Empty(r, "Arm", new Vector3(0f, 4.6f, 0f));
        Prim(PrimitiveType.Sphere, arm, "Joint", Vector3.zero, Vector3.one * 0.8f, dark);
        Box(arm, "Beam", new Vector3(0f, 0f, -1.05f), new Vector3(0.4f, 0.4f, 2.5f), yellow);
        Box(arm, "Counterweight", new Vector3(0f, 0f, 0.5f), new Vector3(0.7f, 0.6f, 0.6f), dark);
        Transform claw = Empty(arm, "Claw", new Vector3(0f, -1.2f, -2.1f));
        Prim(PrimitiveType.Cylinder, claw, "Cable", new Vector3(0f, 0.6f, 0f), new Vector3(0.08f, 0.6f, 0.08f), dark);
        Box(claw, "Hub", Vector3.zero, new Vector3(0.6f, 0.25f, 0.6f), metal);
        for (int i = 0; i < 3; i++)
        {
            Transform f = Empty(claw, "Finger" + i, Vector3.zero);
            f.localRotation = Quaternion.Euler(0f, i * 120f, 0f);
            Box(f, "Tip", new Vector3(0f, -0.35f, 0.32f), new Vector3(0.12f, 0.6f, 0.12f), metal).transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        }
    }

    /// <summary>Santa's sleigh; anchors "Pile" (loaded gifts) and "Counter" (gift count).</summary>
    static void BuildSleigh(Transform r)
    {
        Material red = Mat("SleighRed", new Color(0.82f, 0.1f, 0.12f));
        Material gold = Mat("MachineGold", new Color(1f, 0.78f, 0.2f));
        Box(r, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(3.2f, 1.1f, 1.8f), red);
        Box(r, "Back", new Vector3(1.45f, 1.8f, 0f), new Vector3(0.35f, 1.6f, 1.8f), red);
        Box(r, "Front", new Vector3(-1.5f, 1.5f, 0f), new Vector3(0.3f, 0.6f, 1.8f), red);
        foreach (float z in new[] { -0.8f, 0.8f })
        {
            Box(r, "Runner", new Vector3(0f, 0.12f, z), new Vector3(3.8f, 0.12f, 0.14f), gold);
            Prim(PrimitiveType.Cylinder, r, "Curl", new Vector3(-1.9f, 0.45f, z), new Vector3(0.5f, 0.07f, 0.5f), gold).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box(r, "Strut", new Vector3(-0.8f, 0.3f, z), new Vector3(0.12f, 0.4f, 0.12f), gold);
            Box(r, "Strut", new Vector3(0.9f, 0.3f, z), new Vector3(0.12f, 0.4f, 0.12f), gold);
        }
        Box(r, "Trim", new Vector3(0f, 1.48f, -0.91f), new Vector3(3.2f, 0.12f, 0.04f), gold);
        Empty(r, "Pile", new Vector3(0f, 1.5f, 0f));
        Empty(r, "Counter", new Vector3(0f, 3.3f, -0.6f));
    }

    /// <summary>Open wooden crate at the end of the line: missed toys fall in (physics) and pile up.</summary>
    static void BuildBin(Transform r)
    {
        Material wood = Mat("BinWood", new Color(0.55f, 0.36f, 0.2f));
        const float w = 2.2f, d = 2.4f, h = 1.0f, t = 0.15f;
        Box(r, "Bottom", new Vector3(0f, t * 0.5f, 0f), new Vector3(w, t, d), wood, true);
        Box(r, "WallFront", new Vector3(0f, h * 0.5f, -d * 0.5f), new Vector3(w, h, t), wood, true);
        Box(r, "WallBack", new Vector3(0f, h * 0.5f, d * 0.5f), new Vector3(w, h, t), wood, true);
        Box(r, "WallLeft", new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wood, true);
        Box(r, "WallRight", new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wood, true);
    }

    /// <summary>Jump button under an elf's feet, sticking out of the belt; "Cap" (player color) sinks at each landing.</summary>
    static void BuildButton(Transform r)
    {
        Prim(PrimitiveType.Cylinder, r, "Socket", new Vector3(0f, -0.12f, 0f), new Vector3(1.5f, 0.12f, 1.5f), Mat("ClawDark", new Color(0.18f, 0.2f, 0.24f)));
        Prim(PrimitiveType.Cylinder, r, "Cap", new Vector3(0f, 0.06f, 0f), new Vector3(1.2f, 0.1f, 1.2f), Mat("ButtonCap", Color.white));
    }

    /// <summary>Star burst when a machine transforms a toy: "Flash" (one big glow) + "Stars" (4-point sparkles thrown up
    /// and falling). Alpha-blended URP particles, star texture generated in UI/Sparkle.png. Tinted at runtime (ElfArt.Sparkle).</summary>
    static void BuildSparkle(Transform r)
    {
        string texPath = Dir + "UI/Sparkle.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
        {
            const int n = 128;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float rays = Mathf.Max(0f, 1f - Mathf.Abs(u) * 10f) * (1f - Mathf.Abs(v)) + Mathf.Max(0f, 1f - Mathf.Abs(v) * 10f) * (1f - Mathf.Abs(u));
                    float a = Mathf.Clamp01(Mathf.Exp(-d * d * 18f) + rays * rays) * Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            System.IO.File.WriteAllBytes(texPath, t.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        string matPath = MatDir + "Sparkle.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f); // alpha: solid stars, visible on the red wall
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            AssetDatabase.CreateAsset(mat, matPath);
        }
        Particles(r, "Flash", mat, 1, 0.3f, 0.35f, 0f, 0f, 3.5f, 4f, 0f, 0f);
        Particles(r, "Stars", mat, 30, 0.8f, 1.3f, 4.5f, 8f, 0.55f, 1.1f, 1.2f, 0.5f);
    }

    static void Particles(Transform parent, string name, Material mat, int count, float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax, float gravity, float radius)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // hemisphere opens upward
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = radius > 0f;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = Mathf.Max(0.01f, radius);
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, count == 1 ? AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1f) : AnimationCurve.Linear(0f, 1f, 1f, 0f));
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
        rot.enabled = count > 1;
        rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
    }
    #endregion

    #region Scene
    const string WorkshopPrefab = "Assets/Pleyo/ElvesGame/Prefabs/Workshop.prefab";

    /// <summary>Puts the 1-screen workshop (prefab WorkshopPrefab, the developer's edited version) in the open scene,
    /// replacing "---- Workshop ----". Scene edits of the instance reach this menu only once applied to the prefab
    /// (Overrides > Apply All). Without the prefab, generates the original decor (BuildWorkshopFromScratch).</summary>
    [MenuItem("Elves Workshop/Build Workshop In Open Scene")]
    public static string BuildWorkshop()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkshopPrefab);
        if (prefab == null) return BuildWorkshopFromScratch();
        GameObject old = SceneRoots("---- Workshop ----").Count > 0 ? SceneRoots("---- Workshop ----")[0] : null;
        int index = old != null ? old.transform.GetSiblingIndex() : -1;
        if (old != null) Undo.DestroyObjectImmediate(old);
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        root.name = "---- Workshop ----";
        if (index >= 0) root.transform.SetSiblingIndex(index);
        Undo.RegisterCreatedObjectUndo(root, "Build Workshop");
        EditorSceneManager.MarkSceneDirty(root.scene);
        return "workshop ok (prefab " + WorkshopPrefab + ")";
    }

    /// <summary>Saves the open scene's "---- Workshop ----" as WorkshopPrefab (the scene object becomes its instance).</summary>
    [MenuItem("Elves Workshop/Save Workshop As Prefab")]
    public static string SaveWorkshopPrefab()
    {
        List<GameObject> roots = SceneRoots("---- Workshop ----");
        if (roots.Count == 0) return "no ---- Workshop ---- in the open scene";
        PrefabUtility.SaveAsPrefabAssetAndConnect(roots[0], WorkshopPrefab, InteractionMode.UserAction);
        EditorSceneManager.MarkSceneDirty(roots[0].scene);
        return "saved " + WorkshopPrefab;
    }

    /// <summary>Original generated decor of the 1-screen workshop (used by BuildWorkshop when the prefab is missing).</summary>
    static string BuildWorkshopFromScratch()
    {
        GameObject old = GameObject.Find("---- Workshop ----");
        if (old != null) Object.DestroyImmediate(old);
        GameObject root = new GameObject("---- Workshop ----");
        ElfWorkshop ws = root.AddComponent<ElfWorkshop>();
        const float length = ScreenWidth * 4f;

        // Floor: wooden tiles (Furniture Cute, Wood_4).
        Transform floor = Empty(root.transform, "Floor", Vector3.zero);
        Material wood4 = Load<Material>(Furniture + "Materials/Wood_4.mat");
        GameObject floorTile = Load<GameObject>(Furniture + "Prefabs/Floor/Floor_01.prefab");
        for (float x = -10f; x < length + 10f; x += 4f)
            for (float z = -10f; z < 8f; z += 4f)
                Retint(PlaceAt(floorTile, floor, new Vector3(x + 2f, 0f, z + 2f), 0f, false), wood4, null);

        // Back wall: modular walls (plain / windows / door) in Gradient_Red, two rows.
        Transform walls = Empty(root.transform, "Walls", Vector3.zero);
        Material red = Load<Material>(Furniture + "Materials/Gradient_Red.mat");
        string[] pattern = { "Wall_05", "Wall_03", "Wall_05", "Wall_04", "Wall_05" };
        int k = 0;
        for (float x = -10f; x < length + 10f; x += 4f, k++)
        {
            Retint(PlaceAt(Load<GameObject>(Furniture + $"Prefabs/Walls/{pattern[k % pattern.Length]}.prefab"), walls, new Vector3(x + 2f, 0f, 6f), 0f, true), red, "Wall_Edge");
            Retint(PlaceAt(Load<GameObject>(Furniture + "Prefabs/Walls/Wall_05.prefab"), walls, new Vector3(x + 2f, 4f, 6f), 0f, true), red, "Wall_Edge");
            Retint(PlaceAt(Load<GameObject>(Furniture + "Prefabs/Walls/Wall_05.prefab"), walls, new Vector3(x + 2f, 8f, 6f), 0f, true), red, "Wall_Edge");
            Retint(PlaceAt(Load<GameObject>(Furniture + "Prefabs/Walls/Wall_05.prefab"), walls, new Vector3(x + 2f, 12f, 6f), 0f, true), red, "Wall_Edge");
        }

        // Snowy sky seen through the windows.
        Box(walls, "Outside", new Vector3(length * 0.5f, 6f, 7.2f), new Vector3(length + 40f, 14f, 0.1f), Mat("Outside", new Color(0.72f, 0.86f, 1f)));

        // Christmas decorations, same set on each screen.
        Transform decor = Empty(root.transform, "Decor", Vector3.zero);
        for (int s = 0; s < 4; s++)
        {
            float cx = s * ScreenWidth + ScreenWidth * 0.5f;
            Fit(Load<GameObject>(Xmas + "Christmass_tree_indoor_001_toys.prefab"), decor, new Vector3(cx - 9.2f, 0f, 4.6f), 4.2f, 0f);
            Fit(Load<GameObject>(Xmas + "Garland_001.prefab"), decor, new Vector3(cx - 5f, 7.4f, 5.75f), 0.55f, 0f);
            Fit(Load<GameObject>(Xmas + "Garland_002.prefab"), decor, new Vector3(cx + 5f, 7.4f, 5.75f), 0.55f, 0f);
            Fit(Load<GameObject>(Xmas + "Advent_Wreath_001.prefab"), decor, new Vector3(cx, 7.0f, 5.75f), 1.3f, 0f);
            Fit(Load<GameObject>(Xmas + "Christmass_sock_001.prefab"), decor, new Vector3(cx - 2.5f, 5.6f, 5.8f), 0.8f, 180f);
            Fit(Load<GameObject>(Xmas + "Christmass_sock_001.prefab"), decor, new Vector3(cx + 2.5f, 5.6f, 5.8f), 0.8f, 180f);
            Fit(Load<GameObject>(Xmas + "Shugar_cane_001.prefab"), decor, new Vector3(cx + 8.8f, 0f, 5.2f), 3.2f, 0f);
            for (int p = 0; p < 3; p++)
                Fit(Load<GameObject>(Xmas + $"Present_00{1 + (s + p) % 6}.prefab"), decor, new Vector3(cx - 8.2f + p * 0.9f, 0f, 3.9f - (p % 2) * 0.7f), 0.7f - p * 0.1f, p * 25f);
        }

        // Conveyor belt (EKstudio tiles, turned along +x).
        Transform belt = Empty(root.transform, "Belt", Vector3.zero);
        GameObject conveyor = Load<GameObject>(Factory + "Prefabs/Conveyor/Conveyor_Long.prefab");
        float top = 1.2f;
        // 5 m tiles from x = -2 (inside the spawner): a line of N screens ends on a tile edge at N x 20 - 2.
        for (float x = -2f; x < length; x += 5f)
        {
            GameObject tile = PlaceAt(conveyor, belt, new Vector3(x + 2.5f, 0f, 0f), 90f, false, 5f / 8.11f);
            top = Bounds(tile).max.y;
        }
        ws.Belt = belt;
        ws.BeltTopY = top;
        ws.BeltMaterial = Load<Material>(Factory + "Material/M_Machn_Conveyor.mat");

        // Toy spawner machine at the start of the line (screen 1): the raw blocks come out of its right side.
        GameObject spawner = Fit(Load<GameObject>(Factory + "Prefabs/Machine/Machn_2.prefab"), root.transform, new Vector3(0.4f, 0f, 0f), 3.4f, 0f);
        if (spawner != null) spawner.name = "SpawnerMachine";
        ws.SpawnPoint = Empty(root.transform, "SpawnPoint", new Vector3(0.4f + Bounds(spawner).extents.x * 0.6f, top, 0f));

        EditorSceneManager.MarkSceneDirty(root.scene);
        return $"workshop ok, belt top {top:0.00}, floor {floor.childCount}, walls {walls.childCount}, decor {decor.childCount}, belt tiles {belt.childCount}";
    }

    /// <summary>Places the game machines in the open scene as prefab instances (root "---- Machines ----": Carve, Paint,
    /// Wrap, Load = claw + sleigh + label anchor, Bin), at their x for 4 screens / 8 players. Only the missing ones are
    /// created: edits are kept. At runtime ElfGame moves each group to its station x (the rest stays as edited).</summary>
    [MenuItem("Elves Workshop/Add Machines To Open Scene")]
    public static string AddMachines()
    {
        ElfAssets a = AssetDatabase.LoadAssetAtPath<ElfAssets>(Dir + "ElfAssets.asset");
        if (a == null) return "missing ElfAssets.asset (menu Rebuild Assets and Prefabs)";
        GameObject rootGo = GameObject.Find("---- Machines ----");
        Transform root = rootGo != null ? rootGo.transform : new GameObject("---- Machines ----").transform;
        string[] names = { "Carve", "Paint", "Wrap" };
        float[] xs = { 10.8f, 29.5f, 49.5f };
        int created = 0;
        for (int i = 0; i < 3; i++)
        {
            if (root.Find(names[i]) != null) continue;
            Transform group = Empty(root, names[i], new Vector3(xs[i], 0f, 0f));
            Instance(a.MachinePrefab, group, "Machine", Vector3.zero);
            created++;
        }
        if (root.Find("Load") == null)
        {
            Transform group = Empty(root, "Load", new Vector3(71.15f, 0f, 0f));
            // Claw in front of the belt (camera side), turned to reach back over it; sleigh row built at runtime from "Sleigh".
            Instance(a.ClawPrefab, group, "ClawMachine", new Vector3(0f, 0f, -1.9f));
            group.Find("ClawMachine").localRotation = Quaternion.Euler(0f, 180f, 0f);

            Instance(a.SleighPrefab, group, "Sleigh", new Vector3(-1.8f, 0f, -3f));
            group.Find("Sleigh").localScale = Vector3.one;
            Empty(group, "Label", new Vector3(0f, 6.4f, 0.6f));
            created++;
        }
        if (root.Find("Bin") == null)
        {
            Instance(a.BinPrefab, root, "Bin", new Vector3(79.15f, 0f, 0f));
            created++;
        }
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return $"machines ok, {created} created";
    }

    /// <summary>Indoor lighting of the workshop (root "---- Lighting ----", created only if missing): per screen, two
    /// wall lamps (Furniture Cute Light_11) with a warm point light each, and two warm fill lights in front of the line
    /// lighting the elves. No shadows (cost); the scene's Directional Light stays as the main light.</summary>
    [MenuItem("Elves Workshop/Add Lighting To Open Scene")]
    public static string AddLighting()
    {
        if (GameObject.Find("---- Lighting ----") != null) return "lighting already there";
        Transform root = new GameObject("---- Lighting ----").transform;
        GameObject lamp = Load<GameObject>(Furniture + "Prefabs/Light/Light_11.prefab");
        Color warm = new Color(1f, 0.78f, 0.5f);
        for (int s = 0; s < 4; s++)
        {
            float cx = s * ScreenWidth + ScreenWidth * 0.5f;
            foreach (float dx in new[] { -6.5f, 6.5f })
            {
                Transform wall = Empty(root, $"WallLamp{s + 1}", new Vector3(cx + dx, 6.3f, 5.75f));
                if (lamp != null)
                {
                    GameObject l = (GameObject)PrefabUtility.InstantiatePrefab(lamp, wall);
                    l.transform.localScale = Vector3.one * 4f;
                    l.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
                PointLight(wall, new Vector3(0f, 0f, -1.2f), warm, 10f, 6f);
            }
            foreach (float dx in new[] { -5f, 5f })
                PointLight(Empty(root, $"FillLight{s + 1}", new Vector3(cx + dx, 8f, -5f)), Vector3.zero, warm, 18f, 5f);
        }
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "lighting ok";
    }

    /// <summary>Snow falling outside, behind the windows (root "---- Outside Snow ----", only if missing): one particle
    /// system between the back wall and the sky ("Walls/Outside") along the 4 screens. Material Materials/Snow.mat.</summary>
    [MenuItem("Elves Workshop/Add Snow To Open Scene")]
    public static string AddSnow()
    {
        if (GameObject.Find("---- Outside Snow ----") != null) return "snow already there";
        string texPath = Dir + "UI/Snowflake.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
        {
            const int n = 64;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * 2.5f)));
                }
            System.IO.File.WriteAllBytes(texPath, t.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        string matPath = MatDir + "Snow.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            AssetDatabase.CreateAsset(mat, matPath);
        }
        const float length = ScreenWidth * 4f;
        GameObject go = new GameObject("---- Outside Snow ----");
        GameObject sky = GameObject.Find("---- Workshop ----/Walls/Outside");
        float skyZ = sky != null ? sky.transform.position.z : 7.2f; // just in front of the sky, behind the back wall
        go.transform.position = new Vector3(length * 0.5f, 16f, skyZ - 0.6f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // emits downward
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 10f;
        main.startLifetime = 14f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.maxParticles = 8000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 450f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(length + 30f, 0.8f, 0.1f);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.separateAxes = true; // no drift in depth: the flakes stay behind the wall
        noise.strengthX = 0.4f;
        noise.strengthY = 0.4f;
        noise.strengthZ = 0f;
        noise.frequency = 0.3f;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        EditorSceneManager.MarkSceneDirty(go.scene);
        return "snow ok";
    }

    /// <summary>9-sliced HUD sprites drawn in code: wooden board (horizontal planks, grain, dark rim, rounded corners) or
    /// paper sheet (white, slightly darker edges, tinted by the Image color).</summary>
    static Sprite BoardSprite(string path, bool wood)
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
        {
            const int n = 128;
            int radius = wood ? 22 : 8, rim = wood ? 9 : 0;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Rounded-rectangle coverage (anti-aliased) and distance to the edge.
                    float cx = Mathf.Clamp(x + 0.5f, radius, n - radius), cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                    float d = radius - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float edge = Mathf.Min(Mathf.Min(x + 0.5f, n - x - 0.5f), Mathf.Min(y + 0.5f, n - y - 0.5f));
                    if (x + 0.5f >= radius && x + 0.5f <= n - radius || y + 0.5f >= radius && y + 0.5f <= n - radius) d = edge;
                    float alpha = Mathf.Clamp01(d);
                    Color c;
                    if (wood)
                    {
                        int plank = y / 32;
                        float grain = Mathf.Sin(x * 0.18f + plank * 2.1f + Mathf.Sin(y * 0.4f + x * 0.03f) * 1.5f) * 0.04f;
                        c = Color.Lerp(new Color32(0x9A, 0x5E, 0x2C, 255), new Color32(0xB0, 0x70, 0x38, 255), (plank % 2) * 0.6f);
                        c = new Color(c.r + grain, c.g + grain * 0.7f, c.b + grain * 0.4f, 1f);
                        if (y % 32 < 2) c *= 0.7f; // gap between planks
                        if (d < rim) c = new Color32(0x5A, 0x33, 0x16, 255);
                        else if (d < rim + 2) c *= 0.8f;
                    }
                    else c = Color.Lerp(new Color(0.85f, 0.85f, 0.85f), Color.white, Mathf.Clamp01(d / 6f));
                    c.a = alpha;
                    t.SetPixel(x, y, c);
                }
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            int border = wood ? 32 : 12;
            imp.spriteBorder = new Vector4(border, border, border, border);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Renders a prefab on a transparent background to a PNG sprite (HUD icons).</summary>
    static Sprite RenderIcon(GameObject prefab, string path)
    {
        if (prefab == null) return null;
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
        {
            ShaderUtil.allowAsyncCompilation = false;
            GameObject go = Object.Instantiate(prefab);
            go.transform.position = new Vector3(0f, -500f, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer rr in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(rr.bounds);
            GameObject camGo = new GameObject("IconCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.15f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(1f, 0f, 1f, 1f); // magenta, keyed out below
            camGo.transform.position = b.center + new Vector3(0f, b.extents.y * 0.8f, -10f);
            camGo.transform.LookAt(b.center);
            GameObject lightGo = new GameObject("IconLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            const int size = 256;
            RenderTexture rt = new RenderTexture(size, size, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            Color[] px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                if (c.r > 0.85f && c.b > 0.85f && c.g < 0.2f) px[i] = new Color(0f, 0f, 0f, 0f);
            }
            tex.SetPixels(px);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(rt);
            AssetDatabase.ImportAsset(path);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void PointLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
    {
        Light light = Empty(parent, "PointLight", localPos).gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    static void Instance(GameObject prefab, Transform parent, string name, Vector3 localPos)
    {
        if (prefab == null) return;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localPosition = localPos;
    }
    #endregion


    #region Multi-screen maps
    [MenuItem("Elves Workshop/Build Ring Map In Open Scene")]
    public static string BuildRingMap() { return BuildCurvedMap(ElfPath.Layout.Ring); }


    /// <summary>Map for 2-4 screens along ElfPath (ring around a big tree): floor, belt tiles (their path
    /// distance stored in ElfWorkshop.TileS), spawner, walls, decorations and lights. Root "---- Workshop Ring ----"
    /// (rebuilt: edits lost); ElfGame shows it when the config has 2-4 screens.</summary>
    static List<GameObject> SceneRoots(string name)
    {
        List<GameObject> found = new List<GameObject>();
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == name) found.Add(go);
        return found;
    }

    static string BuildCurvedMap(ElfPath.Layout layout)
    {
        ElfPath.Origin = Pose.identity; // built around the world origin (the game then follows the Belt object)
        const string rootName = "---- Workshop Ring ----";
        // Also finds the inactive root (ElfGame shows the maps at runtime only): no duplicates.
        foreach (GameObject old in SceneRoots(rootName)) Object.DestroyImmediate(old);
        GameObject root = new GameObject(rootName);
        ElfWorkshop ws = root.AddComponent<ElfWorkshop>();
        ws.Layout = layout;
        Material wood4 = Load<Material>(Furniture + "Materials/Wood_4.mat");
        Material red = Load<Material>(Furniture + "Materials/Gradient_Red.mat");
        GameObject floorTile = Load<GameObject>(Furniture + "Prefabs/Floor/Floor_01.prefab");
        GameObject wallPrefab = Load<GameObject>(Furniture + "Prefabs/Walls/Wall_05.prefab");
        GameObject tree = Load<GameObject>(Xmas + "Christmass_tree_indoor_001_toys.prefab");
        float R = ElfPath.RingRadius;

        // Floor.
        Transform floor = Empty(root.transform, "Floor", Vector3.zero);
        {
            float reach = R + 34f;
            for (float x = -reach; x < reach; x += 4f)
                for (float z = -reach; z < reach; z += 4f)
                    if (new Vector2(x + 2f, z + 2f).magnitude < reach)
                        Retint(PlaceAt(floorTile, floor, ElfPath.RingCenter + new Vector3(x + 2f, 0f, z + 2f), 0f, false), wood4, null);
        }

        // Belt: short conveyor tiles along the path.
        Transform belt = Empty(root.transform, "Belt", Vector3.zero);
        GameObject conveyor = Load<GameObject>(Factory + "Prefabs/Conveyor/Conveyor_Long.prefab");
        List<float> tileS = new List<float>();
        float top = 1.2f, piece = 2f, end = 4f * ElfPath.SCREEN + 4f;
        for (float s = -2f; s < end - 0.01f; s += piece)
        {
            Pose pose = ElfPath.At(s + piece * 0.5f, layout);
            Transform pivot = Empty(belt, "Tile", Vector3.zero);
            pivot.SetPositionAndRotation(pose.position, pose.rotation);
            GameObject tile = PlaceAt(conveyor, pivot, Vector3.zero, 90f, false, piece / 8.11f);
            top = Bounds(tile).max.y - pose.position.y;
            tileS.Add(s);
        }
        ws.Belt = belt;
        ws.TileS = tileS.ToArray();
        ws.BeltTopY = top;
        ws.BeltMaterial = Load<Material>(Factory + "Material/M_Machn_Conveyor.mat");

        // Spawner machine at the start of the line.
        Pose spawn = ElfPath.At(0.4f, layout);
        Transform spawnPivot = Empty(root.transform, "SpawnerPivot", Vector3.zero);
        spawnPivot.SetPositionAndRotation(spawn.position, spawn.rotation);
        GameObject spawner = Fit(Load<GameObject>(Factory + "Prefabs/Machine/Machn_2.prefab"), spawnPivot, Vector3.zero, 3.4f, 0f);
        if (spawner != null) spawner.name = "SpawnerMachine";
        ws.SpawnS = 1.4f;
        Transform spawnPoint = Empty(root.transform, "SpawnPoint", Vector3.zero);
        spawnPoint.position = ElfPath.At(ws.SpawnS, layout).position + Vector3.up * top;
        ws.SpawnPoint = spawnPoint;

        // Walls, decorations, lights.
        Transform walls = Empty(root.transform, "Walls", Vector3.zero);
        Transform decor = Empty(root.transform, "Decor", Vector3.zero);
        Transform lights = Empty(root.transform, "Lights", Vector3.zero);
        Color warm = new Color(1f, 0.78f, 0.5f);
        {
            // Round hall: wall circle facing the centre, giant tree in the middle, presents around it.
            float rw = R + 32f;
            int count = Mathf.CeilToInt(2f * Mathf.PI * rw / 4f);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2f * Mathf.PI / count;
                Vector3 outward = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle));
                for (int row = 0; row < 4; row++)
                {
                    Transform pivot = Empty(walls, "WallPivot", Vector3.zero);
                    pivot.SetPositionAndRotation(ElfPath.RingCenter + outward * rw + Vector3.up * row * 4f, Quaternion.LookRotation(outward, Vector3.up));
                    Retint(PlaceAt(wallPrefab, pivot, Vector3.zero, 0f, true), red, "Wall_Edge");
                }
            }
            Fit(tree, decor, ElfPath.RingCenter, 11f, 0f);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * 36f * Mathf.Deg2Rad;
                Vector3 p = ElfPath.RingCenter + new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * (5.5f + (i % 2) * 1.2f);
                Fit(Load<GameObject>(Xmas + $"Present_00{1 + i % 6}.prefab"), decor, p, 1.2f - (i % 3) * 0.2f, i * 40f);
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = (i * 90f + 45f) * Mathf.Deg2Rad;
                Fit(Load<GameObject>(Xmas + "Shugar_cane_001.prefab"), decor, ElfPath.RingCenter + new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * 8f, 3.5f, 0f);
            }
            PointLight(Empty(lights, "TreeLight", ElfPath.RingCenter + Vector3.up * 13f), Vector3.zero, warm, 30f, 12f);
            for (int i = 0; i < 4; i++)
            {
                Pose p = ElfPath.At(i * ElfPath.SCREEN + ElfPath.SCREEN * 0.5f, layout);
                PointLight(Empty(lights, "LineLight" + (i + 1), p.position + p.rotation * new Vector3(0f, 8f, -5f)), Vector3.zero, warm, 20f, 6f);
            }
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        return $"{rootName} ok: belt tiles {belt.childCount}, belt top {top:0.00}, walls {walls.childCount}";
    }
    #endregion
    #region Helpers
    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogWarning("ElvesBuilder: missing " + path);
        return asset;
    }

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + name)) AssetDatabase.CreateFolder(parent.TrimEnd('/'), name);
    }

    static Mesh[] Meshes(string folder, params string[] names)
    {
        List<Mesh> list = new List<Mesh>();
        foreach (string n in names)
        {
            Mesh m = AssetDatabase.LoadAllAssetsAtPath(Cute + $"Meshes/{folder}/{n}.fbx").OfType<Mesh>().FirstOrDefault();
            if (m != null) list.Add(m); else Debug.LogWarning("ElvesBuilder: missing mesh " + n);
        }
        return list.ToArray();
    }

    static Material Mat(string name, Color color)
    {
        string path = MatDir + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.35f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Transform Empty(Transform parent, string name, Vector3 pos)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider = false)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider = false)
    {
        return Prim(PrimitiveType.Cube, parent, name, pos, scale, m, collider);
    }

    static Bounds Bounds(GameObject g)
    {
        Renderer[] rs = g != null ? g.GetComponentsInChildren<Renderer>() : new Renderer[0];
        if (rs.Length == 0) return new Bounds(g != null ? g.transform.position : Vector3.zero, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// <summary>Prefab instance centered on pos in x / z, its bottom at pos.y (backToZ: its back face at pos.z instead).</summary>
    static GameObject PlaceAt(GameObject prefab, Transform parent, Vector3 pos, float yRot, bool backToZ, float lengthScale = 1f)
    {
        if (prefab == null) return null;
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        g.transform.localScale = Vector3.Scale(g.transform.localScale, new Vector3(1f, 1f, lengthScale));
        g.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        g.transform.localPosition = Vector3.zero;
        Bounds b = Bounds(g);
        Vector3 offset = g.transform.position - new Vector3(b.center.x, b.min.y, backToZ ? b.max.z : b.center.z);
        g.transform.position = parent.TransformPoint(pos) + offset;
        return g;
    }

    /// <summary>Like PlaceAt, scaled so its height is `height`.</summary>
    static GameObject Fit(GameObject prefab, Transform parent, Vector3 pos, float height, float yRot)
    {
        if (prefab == null) return null;
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        g.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        g.transform.localPosition = Vector3.zero;
        float h = Mathf.Max(0.001f, Bounds(g).size.y);
        g.transform.localScale *= height / h;
        Bounds b = Bounds(g);
        g.transform.position = parent.TransformPoint(pos) + (g.transform.position - new Vector3(b.center.x, b.min.y, b.center.z));
        return g;
    }

    /// <summary>Prefab instance under a prefab being built, scaled to `width` (x), bottom at pos.y.</summary>
    static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float width, float yRot)
    {
        if (prefab == null) return null;
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        g.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        g.transform.localPosition = Vector3.zero;
        g.transform.localScale *= width / Mathf.Max(0.001f, Bounds(g).size.x);
        Bounds b = Bounds(g);
        g.transform.position = parent.TransformPoint(pos) + (g.transform.position - new Vector3(b.center.x, b.min.y, b.center.z));
        return g;
    }

    /// <summary>Replaces every material of the instance by m, except those whose name starts with keep.</summary>
    static void Retint(GameObject g, Material m, string keep)
    {
        if (g == null || m == null) return;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (keep == null || mats[i] == null || !mats[i].name.StartsWith(keep)) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }
    #endregion
}
