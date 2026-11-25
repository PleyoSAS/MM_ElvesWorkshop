using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>A toy on the conveyor: raw wooden block (from the spawner machine) -> carved toy (unpainted wood: Toy_05 or
/// Toy_03, alternately) -> painted (the toy's own colors; a player alone on Paint: its main color becomes the player
/// color) -> wrapped gift (Christmas present; same rule on Wrap) -> loaded on the sleigh by the claw. Glows with the jump
/// buttons when a machine transforms it. A toy that passes a station without being processed is "unfinished" for good
/// (grey + red "?"); toys nobody loads fall into the bin at the end of the line (physics, they pile up).</summary>
public class ElfToy
{
    public const int CARVE = 1, PAINT = 2, WRAP = 4, LOAD = 8;
    public const float SIZE = 1.1f;

    public Transform Root;
    public float X;
    public int Done;
    public int Kind;
    public bool Unfinished;
    public int HandledStations;
    public bool IsMoving => _state == 0;

    static readonly List<ElfToy> Glowing = new List<ElfToy>();
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static MaterialPropertyBlock _block;

    int _state; // 0 on the belt, 1 held / parked (moved by others), 2 in the bin (physics)
    GameObject _raw, _carved, _gift;
    Renderer[] _carvedRenderers, _giftRenderers, _all, _rawRenderers;
    Material[][] _rawOpaque;
    Material[][] _carvedSource, _giftSource;
    TextMeshPro _mark;
    Color? _paintColor, _wrapColor;
    Color _carvedMain = Color.white, _giftMain = Color.white, _glowColor;
    float _glow;
    Light _glowLight;
    GameObject _ownerRing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Glowing.Clear(); GhostMaterials.Clear(); _block = null; }

    public void Init(Transform parent, float x, float beltY, int kind)
    {
        X = x;
        Kind = kind;
        Root = new GameObject("Toy").transform;
        Root.SetParent(parent, false);
        PlaceOnBelt(beltY);
        ElfAssets a = ElfArt.Assets;

        Quaternion facing = Root.rotation;
        Root.rotation = Quaternion.identity; // models measured upright (the ring turns the belt)
        _raw = Model(a != null ? a.RawBlock : null, "Raw", SIZE * 0.8f, false, new Color(0.75f, 0.55f, 0.35f));
        int carvedIndex = a != null && a.CarvedToys != null && a.CarvedToys.Length > 0 ? kind % a.CarvedToys.Length : -1;
        _carved = Model(carvedIndex >= 0 ? a.CarvedToys[carvedIndex] : null, "Carved", SIZE, true, new Color(0.86f, 0.68f, 0.46f));
        int giftIndex = a != null && a.Presents != null && a.Presents.Length > 0 ? Random.Range(0, a.Presents.Length) : -1;
        _gift = Model(giftIndex >= 0 ? a.Presents[giftIndex] : null, "Gift", SIZE * 1.05f, false, new Color(0.85f, 0.1f, 0.2f));
        // Every stage as big as the raw wooden box (largest side).
        float boxSize = MaxSide(_raw);
        Match(_carved, boxSize);
        Match(_gift, boxSize);
        Root.rotation = facing;
        if (carvedIndex >= 0 && a.CarvedMainColors != null && carvedIndex < a.CarvedMainColors.Length) _carvedMain = a.CarvedMainColors[carvedIndex];
        if (giftIndex >= 0 && a.PresentMainColors != null && giftIndex < a.PresentMainColors.Length) _giftMain = a.PresentMainColors[giftIndex];

        _carvedRenderers = _carved.GetComponentsInChildren<Renderer>(true);
        _giftRenderers = _gift.GetComponentsInChildren<Renderer>(true);
        _carvedSource = Sources(_carvedRenderers);
        _giftSource = Sources(_giftRenderers);
        // Glow-capable copies of the raw block materials (the others are set by Refresh).
        _rawRenderers = _raw.GetComponentsInChildren<Renderer>(true);
        _rawOpaque = new Material[_rawRenderers.Length][];
        for (int i = 0; i < _rawRenderers.Length; i++) { SetMaterials(_rawRenderers[i], _rawRenderers[i].sharedMaterials, null, null, null); _rawOpaque[i] = _rawRenderers[i].sharedMaterials; }
        _mark = ElfArt.Label(Root, new Vector3(0f, SIZE + 0.7f, -0.3f), "?", 9f, new Color(1f, 0.2f, 0.2f));
        List<Renderer> all = new List<Renderer>();
        foreach (Renderer r in Root.GetComponentsInChildren<Renderer>(true))
            if (r.GetComponent<TMP_Text>() == null) all.Add(r);
        _all = all.ToArray();
        Refresh();
    }

    GameObject Model(GameObject prefab, string name, float size, bool byWidth, Color fallback)
    {
        Transform holder = new GameObject(name).transform;
        holder.SetParent(Root, false);
        if (prefab != null) ElfArt.Fit(prefab, holder, size, byWidth);
        else ElfArt.Prim(PrimitiveType.Cube, holder, new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, fallback);
        foreach (Collider c in holder.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        return holder.gameObject;
    }

    static float MaxSide(GameObject model)
    {
        Vector3 size = ElfArt.GetBounds(model).size;
        return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
    }

    /// <summary>Scales a model holder (pivot at its bottom centre) so its largest side is size.</summary>
    static void Match(GameObject model, float size)
    {
        float current = MaxSide(model);
        if (current > 0.001f && size > 0.001f) model.transform.localScale *= size / current;
    }

    static Material[][] Sources(Renderer[] renderers)
    {
        Material[][] sources = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++) sources[i] = renderers[i].sharedMaterials;
        return sources;
    }

    /// <summary>Glow-capable copies of the source materials; override replaces them all (unpainted wood); main + player
    /// recolor the model's main color.</summary>
    static void SetMaterials(Renderer r, Material[] source, Material replace, Color? main, Color? player)
    {
        Material[] mats = new Material[source.Length];
        for (int m = 0; m < mats.Length; m++) mats[m] = ElfArt.ToyMaterial(replace != null ? replace : source[m], main, player);
        r.sharedMaterials = mats;
    }

    /// <summary>Machine step(s) done; playerColor (a player alone on the station): the main color of the painted toy /
    /// gift becomes the player color.</summary>
    public void Apply(int steps, Color? playerColor = null)
    {
        int added = steps & ~LOAD & ~Done;
        Done |= steps & ~LOAD;
        if (playerColor != null)
        {
            if ((added & PAINT) != 0) _paintColor = playerColor;
            if ((added & WRAP) != 0) _wrapColor = playerColor;
        }
        Refresh();
        if (added != 0 && !Ghost) ElfArt.Sparkle(Root.position + Vector3.up * SIZE * 0.6f, ElfArt.StepColors[ElfStation.FirstStep(added)]);
    }

    /// <summary>Ghost toy (countdown): 75% transparent, transformed automatically by the machines it passes, not
    /// playable; becomes a normal toy at the start.</summary>
    public bool Ghost { get; private set; }

    public void SetGhost(bool ghost)
    {
        if (Ghost == ghost) return;
        Ghost = ghost;
        Refresh();
    }

    static readonly Dictionary<Material, Material> GhostMaterials = new Dictionary<Material, Material>();

    void ApplyGhostMaterials()
    {
        foreach (Renderer r in _all)
        {
            if (r == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;
                if (!GhostMaterials.TryGetValue(mats[m], out Material ghost) || ghost == null)
                {
                    GhostMaterials[mats[m]] = ghost = ElfArt.Transparent(mats[m]);
                    Color c = mats[m].HasProperty(BaseColorId) ? mats[m].GetColor(BaseColorId) : Color.white;
                    c.a *= 0.25f; // 75% transparent
                    ghost.SetColor(BaseColorId, c);
                }
                mats[m] = ghost;
            }
            r.sharedMaterials = mats;
        }
    }

    /// <summary>Lights the toy up like the jump buttons of the station that just transformed it (fades in 0.5 s).</summary>
    public void Glow(Color color)
    {
        _glowColor = color;
        _glow = 1f;
        if (_glowLight == null)
        {
            _glowLight = new GameObject("Glow").AddComponent<Light>();
            _glowLight.transform.SetParent(Root, false);
            _glowLight.transform.localPosition = new Vector3(0f, SIZE * 0.6f, -1f);
            _glowLight.type = LightType.Point;
            _glowLight.range = 3.5f;
            _glowLight.shadows = LightShadows.None;
        }
        _glowLight.color = color;
        if (!Glowing.Contains(this)) Glowing.Add(this);
        UpdateBlocks();
    }

    /// <summary>Fades the glowing toys (on the belt, in the claw or in the sleigh). Called once per frame by ElfGame.</summary>
    public static void TickGlows(float deltaTime)
    {
        for (int i = Glowing.Count - 1; i >= 0; i--)
        {
            ElfToy toy = Glowing[i];
            if (toy.Root == null) { Glowing.RemoveAt(i); continue; }
            toy._glow = Mathf.MoveTowards(toy._glow, 0f, deltaTime * 2f);
            toy.UpdateBlocks();
            if (toy._glow <= 0f) Glowing.RemoveAt(i);
        }
    }

    public void MarkUnfinished()
    {
        if (Unfinished)
            return;
        Unfinished = true;
        Refresh();
    }

    /// <summary>Colored ring under the toy: the player whose machine owns it at its next step (2 machines on that
    /// step); null hides it.</summary>
    public void SetOwner(Color? color)
    {
        if (color == null) { if (_ownerRing != null) _ownerRing.SetActive(false); return; }
        if (_ownerRing == null) _ownerRing = ElfArt.Prim(PrimitiveType.Cylinder, Root, new Vector3(0f, 0.03f, 0f), new Vector3(1.5f, 0.03f, 1.5f), color.Value, "OwnerRing");
        _ownerRing.SetActive(true);
        ElfArt.SetColor(_ownerRing, color.Value);
    }

    /// <summary>Taken off the belt (claw); its position is then driven by the claw.</summary>
    public void Hold() { _state = 1; SetOwner(null); }

    /// <summary>Drops in the sleigh pile (static from then on).</summary>
    public void Park(Transform pile, Vector3 localPosition)
    {
        _state = 1;
        Root.SetParent(pile, true);
        Root.localPosition = localPosition;
        Root.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Root.localScale = Vector3.one * 0.75f;
        _mark.gameObject.SetActive(false);
    }

    /// <summary>Leaves the end of the belt: physics body thrown forward into the bin.</summary>
    public void Fall(float beltSpeed)
    {
        _state = 2;
        SetOwner(null);
        _mark.transform.SetParent(null);
        Object.Destroy(_mark.gameObject);
        Bounds b = ElfArt.GetBounds(Root.gameObject);
        BoxCollider box = Root.gameObject.AddComponent<BoxCollider>();
        box.center = Root.InverseTransformPoint(b.center);
        box.size = b.size * 0.9f;
        Rigidbody body = Root.gameObject.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.linearVelocity = ElfPath.Tangent(X) * beltSpeed + new Vector3(0f, 0.6f, 0f) + Random.insideUnitSphere * 0.3f;
        body.angularVelocity = Random.insideUnitSphere * 3f;
    }

    public void Tick(float deltaTime, float beltSpeed, float beltY)
    {
        if (_state != 0) return;
        X += beltSpeed * deltaTime;
        PlaceOnBelt(beltY);
    }

    /// <summary>On the belt at distance X (ElfPath: straight, ring or terraces), facing like the belt.</summary>
    void PlaceOnBelt(float beltY)
    {
        Pose p = ElfPath.At(X);
        Root.SetPositionAndRotation(p.position + p.rotation * new Vector3(0f, beltY, 0f), p.rotation);
    }

    public void Destroy() { if (Root != null) Object.Destroy(Root.gameObject); }

    void Refresh()
    {
        bool wrapped = (Done & WRAP) != 0;
        bool carved = (Done & CARVE) != 0;
        _raw.SetActive(!wrapped && !carved);
        _carved.SetActive(!wrapped && carved);
        _gift.SetActive(wrapped);
        bool painted = (Done & PAINT) != 0;
        Material wood = ElfArt.Assets != null ? ElfArt.Assets.UnpaintedWood : null;
        for (int i = 0; i < _carvedRenderers.Length; i++)
            SetMaterials(_carvedRenderers[i], _carvedSource[i], painted ? null : wood, _carvedMain, painted ? _paintColor : null);
        for (int i = 0; i < _giftRenderers.Length; i++)
            SetMaterials(_giftRenderers[i], _giftSource[i], null, _giftMain, _wrapColor);
        for (int i = 0; i < _rawRenderers.Length; i++) _rawRenderers[i].sharedMaterials = _rawOpaque[i];
        if (Ghost) ApplyGhostMaterials();
        _mark.gameObject.SetActive(Unfinished);
        UpdateBlocks();
    }

    /// <summary>Property blocks: grey tint when unfinished, emission while glowing.</summary>
    void UpdateBlocks()
    {
        if (_block == null) _block = new MaterialPropertyBlock();
        bool any = Unfinished || _glow > 0f;
        for (int i = 0; i < _all.Length; i++)
        {
            if (_all[i] == null) continue;
            if (!any) { _all[i].SetPropertyBlock(null); continue; }
            _block.Clear();
            if (Unfinished) _block.SetColor(BaseColorId, ElfArt.Unfinished);
            _block.SetColor(EmissionId, _glowColor * (_glow * 1.5f));
            _all[i].SetPropertyBlock(_block);
        }
        if (_glowLight != null)
        {
            _glowLight.enabled = _glow > 0f;
            _glowLight.intensity = _glow * 8f;
        }
    }
}
