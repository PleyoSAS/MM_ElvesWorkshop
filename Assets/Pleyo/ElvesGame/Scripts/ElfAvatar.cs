using TMPro;
using UnityEngine;

/// <summary>
/// A player's elf (ithappy Cute Characters, like the MM Skipping Rope kids: modular parts swapped by mesh, one look per
/// letter, elf hat / jacket in the player color, pointy ears) or the robot of a station with no player. Stands on its
/// jump button behind the belt. Jumps with the trampoline (Mixamo jump clips fitted to the air time); while inactive:
/// grey with a "Jump to play" pill; golden crown for the leading team.
/// </summary>
public class ElfAvatar
{
    public Transform Root;
    public bool IsRobot { get; private set; }

    Transform _body, _cap;
    Animator _anim;
    Renderer[] _renderers;
    GameObject _crown, _sign, _badge;
    bool _active = true;
    float _airTime = -1f, _airDuration = 0.5f, _squash, _press, _height, _badgeY, _glow;
    Material _capMaterial;
    Light _capLight;
    Color _color;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    int _jumpPick = -1;
    float _alpha = 1f;
    Material[][] _opaque, _fade;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>Shown (player active, or robot running the station); fades in / out.</summary>
    public bool Visible => _active;

    // Mixamo jump states of ElfKid.controller (copied from MM Skipping Rope): takeoff / landing normalized times, length.
    static readonly string[] JUMPS = { "JoyfulJump", "JumpingUp" };
    static readonly Vector2[] JUMP_AIR = { new Vector2(0.333f, 0.583f), new Vector2(0.633f, 1f) };
    static readonly float[] JUMP_LENGTH = { 1.867f, 0.833f };
    static readonly string[] IDLES = { "Happy", "HappyIdle", "Excited" };
    static readonly int IdleHash = Animator.StringToHash("Idle");
    static readonly int JumpSpeedHash = Animator.StringToHash("JumpSpeed");
    static readonly int CelebrateHash = Animator.StringToHash("Celebrate");
    static readonly Color Skin = new Color(1f, 0.8f, 0.66f);
    static readonly Color GreyTint = new Color(0.55f, 0.55f, 0.58f);
    const float JUMP_HEIGHT = 3.2f; // x2.5 (was 1.3)

    public void Build(Transform parent, Vector3 localPosition, Color color, string letter, bool robot, Transform buttonCap, bool robotLabel = true)
    {
        IsRobot = robot;
        _cap = buttonCap;
        _color = color;
        if (_cap != null && _cap.GetComponent<Renderer>() != null)
        {
            // Own material so the cap can glow alone (emission + small light) when the machine transforms a toy.
            _capMaterial = _cap.GetComponent<Renderer>().material;
            _capMaterial.EnableKeyword("_EMISSION");
            _capMaterial.SetColor(EmissionId, Color.black);
            _capLight = new GameObject("Glow").AddComponent<Light>();
            _capLight.transform.SetParent(_cap.parent, false);
            _capLight.transform.localPosition = new Vector3(0f, 0.6f, -0.4f);
            _capLight.type = LightType.Point;
            _capLight.color = color;
            _capLight.range = 3.5f;
            _capLight.shadows = LightShadows.None;
            _capLight.enabled = false;
        }
        Root = new GameObject(robot ? "Robot" : "Elf_" + letter).transform;
        Root.SetParent(parent, false);
        Root.localPosition = localPosition;
        _body = new GameObject("Body").transform;
        _body.SetParent(Root, false);
        ElfAssets a = ElfArt.Assets;
        _height = a != null ? a.ElfHeight : 3.2f;

        if (robot)
        {
            BuildRobot();
            if (robotLabel) ElfArt.Label(Root, new Vector3(0f, 4.2f, 0f), ElfLoc.Get("ROBOT"), 6f, Color.white);
        }
        else
        {
            if (a != null && a.ElfModel != null) BuildElf(a, color, letter);
            else ElfArt.Prim(PrimitiveType.Capsule, _body, new Vector3(0f, _height * 0.5f, 0f), new Vector3(1.2f, _height * 0.5f, 1.2f), color);
            _crown = ElfArt.Crown(_body);
            _crown.transform.localPosition = new Vector3(0f, _height + 0.05f, 0f);
            _crown.transform.localScale = Vector3.one * 1.1f;
            _crown.SetActive(false);
            _badgeY = _height + 0.75f;
            _badge = ElfArt.Badge(Root, new Vector3(0f, _badgeY, -0.3f), letter, color);
            // Absent player: only this pill stays (letter in the player color).
            _sign = ElfArt.Pill(Root, new Vector3(0f, _height + 1.65f, -0.3f), $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{letter}</color>  {ElfLoc.Get("JUMP_TO_PLAY")}", 5.4f);
        }
        _renderers = _body.GetComponentsInChildren<Renderer>(true);
        if (!robot)
        {
            _active = true;
            SetActive(false);
            _alpha = 0f; // absent from the start: hidden at once
            _body.gameObject.SetActive(false);
            if (_badge != null) _badge.SetActive(false);
        }
    }

    void BuildElf(ElfAssets a, Color color, string letter)
    {
        GameObject m = ElfArt.Fit(a.ElfModel, _body, _height);
        m.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // faces the camera (-z)
        int v = string.IsNullOrEmpty(letter) ? 0 : letter[0];
        Material clothes = ElfArt.Mat(Color.Lerp(color, Color.white, 0.1f));
        Material pants = ElfArt.Mat(new Color(0.12f, 0.35f, 0.18f));
        foreach (SkinnedMeshRenderer r in m.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            switch (r.name)
            {
                case "Body": SetPart(r, a.ElfBodies, v, null); break;
                case "Faces": SetPart(r, a.ElfFaces, v, null); break;
                case "Hairstyle": SetPart(r, a.ElfHairs, v / 3, null); break;
                case "Hat": if (a.ElfHat != null) { r.sharedMesh = a.ElfHat; r.sharedMaterial = clothes; } else r.gameObject.SetActive(false); break;
                case "Outwear": SetPart(r, a.ElfOutwear, v, clothes); break;
                case "Pants": r.sharedMaterial = pants; break;
                case "Shoes": break;
                default: r.gameObject.SetActive(false); break;
            }
        }
        _anim = m.GetComponentInChildren<Animator>();
        if (_anim != null)
        {
            _anim.applyRootMotion = false;
            _anim.runtimeAnimatorController = a.ElfController;
            if (_anim.isHuman)
            {
                // Pointy elf ears on the head bone.
                Transform head = _anim.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    float s = _height / 3.2f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        GameObject ear = ElfArt.Prim(PrimitiveType.Sphere, _body, Vector3.zero, new Vector3(0.16f, 0.62f, 0.28f) * s, Skin, "Ear");
                        ear.transform.position = head.position + new Vector3(side * 0.66f * s, 0.32f * s, 0f);
                        ear.transform.rotation = Quaternion.Euler(0f, 0f, -side * 55f);
                        ear.transform.SetParent(head, true);
                    }
                }
            }
        }
    }

    void BuildRobot()
    {
        Color metal = new Color(0.62f, 0.66f, 0.72f);
        ElfArt.Prim(PrimitiveType.Cube, _body, new Vector3(0f, 1.2f, 0f), new Vector3(1.6f, 1.6f, 1.2f), metal);
        ElfArt.Prim(PrimitiveType.Cube, _body, new Vector3(0f, 2.55f, 0f), new Vector3(1.2f, 1f, 1f), metal);
        ElfArt.Prim(PrimitiveType.Cube, _body, new Vector3(0f, 2.6f, -0.5f), new Vector3(0.85f, 0.25f, 0.06f), new Color(0.2f, 0.95f, 1f));
        ElfArt.Prim(PrimitiveType.Cylinder, _body, new Vector3(0f, 3.3f, 0f), new Vector3(0.08f, 0.3f, 0.08f), metal);
        ElfArt.Prim(PrimitiveType.Sphere, _body, new Vector3(0f, 3.65f, 0f), Vector3.one * 0.25f, new Color(1f, 0.3f, 0.2f));
        foreach (float x in new[] { -0.45f, 0.45f })
            ElfArt.Prim(PrimitiveType.Cylinder, _body, new Vector3(x, 0.2f, 0f), new Vector3(0.45f, 0.2f, 0.45f), new Color(0.25f, 0.27f, 0.3f));
    }

    static void SetPart(SkinnedMeshRenderer r, Mesh[] list, int i, Material m)
    {
        if (list == null || list.Length == 0) { r.gameObject.SetActive(false); return; }
        r.sharedMesh = list[Mathf.Abs(i) % list.Length];
        if (m != null) r.sharedMaterial = m;
    }

    public void SetActive(bool active)
    {
        // Inactive elf (absent / no jump for ElfStation.AFK_SECONDS): fades out, only its "Jump to play" pill stays.
        // Robot: shown while no player of its station is active.
        if (_active == active)
            return;
        _active = active;
        if (_sign != null) _sign.SetActive(!active);
        if (active && _anim != null && _anim.runtimeAnimatorController != null) _anim.CrossFadeInFixedTime(IdleHash, 0.2f);
    }

    /// <summary>Lights the jump button up (the station machine just transformed a toy).</summary>
    public void Glow() { if (_active) _glow = 1f; }

    public void SetCrown(bool on) { if (_crown != null && _crown.activeSelf != on) _crown.SetActive(on); }

    public void Celebrate()
    {
        if (_active && _anim != null && _anim.runtimeAnimatorController != null) _anim.SetBool(CelebrateHash, true);
    }

    /// <summary>Fade in / out: transparent copies of the materials while fading, the opaque ones back once done.</summary>
    void UpdateFade(float deltaTime)
    {
        float target = _active ? 1f : 0f;
        if (_alpha == target) return;
        _alpha = Mathf.MoveTowards(_alpha, target, deltaTime * 2.5f);
        if (_alpha > 0f && !_body.gameObject.activeSelf) _body.gameObject.SetActive(true);
        if (_badge != null)
        {
            _badge.SetActive(_alpha > 0f);
            _badge.transform.localScale = Vector3.one * _alpha;
        }
        bool fading = _alpha > 0f && _alpha < 1f;
        if (_opaque == null)
        {
            _opaque = new Material[_renderers.Length][];
            _fade = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _opaque[i] = _renderers[i].sharedMaterials;
                _fade[i] = new Material[_opaque[i].Length];
                for (int m = 0; m < _opaque[i].Length; m++) _fade[i][m] = ElfArt.Transparent(_opaque[i][m]);
            }
        }
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].sharedMaterials = fading ? _fade[i] : _opaque[i];
            if (!fading) continue;
            for (int m = 0; m < _fade[i].Length; m++)
            {
                if (_fade[i][m] == null || !_fade[i][m].HasProperty(BaseColorId)) continue;
                Color c = _opaque[i][m].HasProperty(BaseColorId) ? _opaque[i][m].GetColor(BaseColorId) : Color.white;
                c.a *= _alpha;
                _fade[i][m].SetColor(BaseColorId, c);
            }
        }
        if (_alpha <= 0f) _body.gameObject.SetActive(false);
    }

    public void Jump(float airDuration)
    {
        _airTime = 0f;
        _airDuration = Mathf.Max(0.25f, airDuration);
        if (_anim == null || _anim.runtimeAnimatorController == null) return;
        int j = Random.Range(0, JUMPS.Length);
        _jumpPick = j;
        Vector2 air = JUMP_AIR[j];
        _anim.SetFloat(JumpSpeedHash, Mathf.Clamp((air.y - air.x) * JUMP_LENGTH[j] / _airDuration, 0.6f, 2.5f));
        _anim.CrossFadeInFixedTime(JUMPS[j], 0.06f, 0, air.x * JUMP_LENGTH[j]);
    }

    public void Land()
    {
        _airTime = -1f;
        _squash = 1f;
        _press = 1f;
        if (_anim == null || _anim.runtimeAnimatorController == null || _jumpPick < 0) return;
        Vector2 air = JUMP_AIR[_jumpPick];
        _anim.SetFloat(JumpSpeedHash, 1f);
        if (air.y > 0.95f) _anim.CrossFadeInFixedTime(IdleHash, 0.12f);
        else _anim.CrossFadeInFixedTime(JUMPS[_jumpPick], 0.08f, 0, air.y * JUMP_LENGTH[_jumpPick]);
        _jumpPick = -1;
    }

    public void Tick(float deltaTime, float time)
    {
        UpdateFade(deltaTime);
        TickMotion(deltaTime, time);
    }

    void TickMotion(float deltaTime, float time)
    {
        float y = 0f;
        if (_airTime >= 0f)
        {
            _airTime += deltaTime;
            float t = Mathf.Clamp01(_airTime / _airDuration);
            y = 4f * JUMP_HEIGHT * t * (1f - t);
            if (_airTime > _airDuration * 1.6f)
                _airTime = -1f; // landing never came: stop floating
        }
        _squash = Mathf.MoveTowards(_squash, 0f, deltaTime * 5f);
        _press = Mathf.MoveTowards(_press, 0f, deltaTime * 4f);
        _body.localPosition = new Vector3(0f, y - _press * 0.12f, 0f);
        float sq = IsRobot ? _squash * 0.2f : _squash * 0.08f;
        _body.localScale = new Vector3(1f + sq, 1f - sq, 1f + sq);
        if (_cap != null && _active) _cap.localPosition = new Vector3(0f, 0.06f - _press * 0.14f, 0f); // the shown avatar drives the shared button
        if (_badge != null) _badge.transform.localPosition = new Vector3(0f, _badgeY + y, -0.3f); // follows the jump
        if (_capMaterial != null && (_glow > 0f || _capLight.enabled))
        {
            _glow = Mathf.MoveTowards(_glow, 0f, deltaTime * 2f);
            _capMaterial.SetColor(EmissionId, _color * (_glow * 3f));
            _capLight.enabled = _glow > 0f;
            _capLight.intensity = _glow * 8f;
        }
        if (_sign != null && _sign.activeSelf)
            _sign.transform.localPosition = new Vector3(0f, _height + 1.65f + Mathf.Sin(time * 3f) * 0.12f, -0.3f);
    }
}
