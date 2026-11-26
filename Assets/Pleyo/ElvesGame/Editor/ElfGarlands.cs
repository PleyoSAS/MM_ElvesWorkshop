using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Blinking garlands: the bulbs of the garland meshes (material "Garland": wall garlands, tree lights) get
/// material LedBlink (shader Elves/LedBlink). A copy of each mesh stores in its vertex colour which half each bulb
/// belongs to (bulbs = separate pieces of the "Garland" sub-mesh, alternately 0 / 1 along the garland), so the two
/// halves light up in turn: one bulb out of two black, then the other.</summary>
public static class ElfGarlands
{
    const string Dir = "Assets/Pleyo/ElvesGame/";
    const string MaterialPath = Dir + "Materials/LedBlink.mat";
    const string MeshDir = Dir + "Generated/";

    [MenuItem("Elves Workshop/Make Blinking Garlands In Open Scene")]
    public static string MakeBlinkingGarlands()
    {
        Material led = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (led == null)
        {
            led = new Material(Shader.Find("Elves/LedBlink")) { name = "LedBlink" };
            AssetDatabase.CreateAsset(led, MaterialPath);
        }
        if (!AssetDatabase.IsValidFolder(MeshDir.TrimEnd('/'))) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Generated");
        Dictionary<Mesh, Mesh> blinking = new Dictionary<Mesh, Mesh>();
        int renderers = 0;
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Material[] mats = r.sharedMaterials;
            int sub = System.Array.FindIndex(mats, m => m != null && m.name == "Garland");
            MeshFilter filter = r.GetComponent<MeshFilter>();
            // (A material slot past the last sub-mesh redraws the whole mesh: not bulbs.)
            if (sub < 0 || filter == null || filter.sharedMesh == null || sub >= filter.sharedMesh.subMeshCount) continue;
            Mesh source = filter.sharedMesh;
            if (!blinking.TryGetValue(source, out Mesh copy)) blinking[source] = copy = BlinkingCopy(source, sub);
            Undo.RecordObject(filter, "Blinking garlands");
            Undo.RecordObject(r, "Blinking garlands");
            filter.sharedMesh = copy;
            mats[sub] = led;
            r.sharedMaterials = mats;
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            renderers++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return $"blinking garlands: {renderers} renderers, {blinking.Count} meshes";
    }

    /// <summary>Copy of the mesh (saved in Generated/) whose vertex colour red = 0 / 1 alternately per bulb.</summary>
    static Mesh BlinkingCopy(Mesh source, int sub)
    {
        string path = MeshDir + source.name + "_Blink.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        Mesh copy = Object.Instantiate(source);
        copy.name = source.name + "_Blink";
        Vector3[] v = copy.vertices;
        int[] tris = copy.GetTriangles(sub);
        int[] parent = new int[v.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }
        // A bulb = triangles sharing vertices, or vertices at the same place (UV seams).
        Dictionary<Vector3Int, int> byPosition = new Dictionary<Vector3Int, int>();
        HashSet<int> used = new HashSet<int>();
        for (int t = 0; t < tris.Length; t += 3)
        {
            Union(tris[t], tris[t + 1]);
            Union(tris[t + 1], tris[t + 2]);
            for (int k = 0; k < 3; k++)
            {
                int i = tris[t + k];
                used.Add(i);
                Vector3Int key = Vector3Int.RoundToInt(v[i] * 10000f);
                if (byPosition.TryGetValue(key, out int other)) Union(other, i); else byPosition[key] = i;
            }
        }
        // Bulbs in order along the garland (its longest axis), every other one in the second half.
        Bounds b = new Bounds(v.Length > 0 ? v[tris.Length > 0 ? tris[0] : 0] : Vector3.zero, Vector3.zero);
        foreach (int i in used) b.Encapsulate(v[i]);
        Vector3 axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right : b.size.y >= b.size.z ? Vector3.up : Vector3.forward;
        Dictionary<int, List<int>> bulbs = new Dictionary<int, List<int>>();
        foreach (int i in used)
        {
            int root = Find(i);
            if (!bulbs.TryGetValue(root, out List<int> list)) bulbs[root] = list = new List<int>();
            list.Add(i);
        }
        List<KeyValuePair<float, List<int>>> ordered = new List<KeyValuePair<float, List<int>>>();
        foreach (List<int> bulb in bulbs.Values)
        {
            Vector3 c = Vector3.zero;
            foreach (int i in bulb) c += v[i];
            ordered.Add(new KeyValuePair<float, List<int>>(Vector3.Dot(c / bulb.Count, axis), bulb));
        }
        ordered.Sort((p, q) => p.Key.CompareTo(q.Key));
        Color[] colors = new Color[v.Length];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        for (int n = 0; n < ordered.Count; n++)
            foreach (int i in ordered[n].Value) colors[i] = new Color(n % 2, 1f, 1f, 1f);
        copy.colors = colors;
        AssetDatabase.CreateAsset(copy, path);
        Debug.Log($"ElfGarlands: {source.name}: {ordered.Count} bulbs");
        return copy;
    }
}
