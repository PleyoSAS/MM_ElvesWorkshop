using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>One station of the line = one team. Carve / Paint / Wrap: a gantry machine across the belt (prefab
/// ElfMachine: posts in front of / behind the belt, the toys pass under its head and piston; one sync light per player on
/// the front post). Load (alone or merged with Wrap): the claw loader (prefab ElfClaw) moves the toy from the belt into the
/// sleigh standing behind it on its right. Its elves stand behind the belt on jump buttons. Every active player of the
/// station must land within SYNC_WINDOW for the machine to fire (alone = no sync needed). With no active player, a bot
/// runs the station (robot elf, or the claw on its own).</summary>
public class ElfStation
{
    public const float SYNC_WINDOW = 0.5f;
    public const float AFK_SECONDS = 10f; // no jump for 10 s = inactive (the elf fades out)
    public const float ELF_Z = 1.75f;
    const float CLAW_Z = 1.9f, CLAW_SLEIGH_YAW = -180f; // claw column in front of the belt; drops straight toward the camera
    const int SLEIGH_CAPACITY = 16;
    int _rowCount = 1;
    readonly List<Transform> _leaving = new List<Transform>();

    /// <summary>Sleigh row (loader): path distance of slot i (0 = under the claw, then upstream), and where a score
    /// board hangs on the front of the sleigh at slot i.</summary>
    public float SleighSlotS(int i) { return _sleighBaseS - i * _sleighSpacing; }
    public float SleighWidth { get; private set; }
    public Pose SleighBoardPose(int i)
    {
        Pose p = ElfPath.At(SleighSlotS(i));
        return new Pose(p.position + p.rotation * new Vector3(0f, _sleighTopLocal, _sleighFrontLocal - 0.03f), p.rotation);
    }


    public Transform Root { get; private set; }
    public int Index;
    public int Steps;
    public float MachineX;
    public readonly List<int> Members = new List<int>();
    public readonly List<ElfAvatar> Avatars = new List<ElfAvatar>();
    public int Processed;
    public int[] MemberScore;
    public string Name;
    public Transform Sleigh { get; private set; }
    public bool IsLoader => (Steps & ElfToy.LOAD) != 0;
    public ElfTeam Team;
    /// <summary>Toy lane this machine owns (toy Kind) when its team has 2 machines; -1: every toy.</summary>
    public int Lane = -1;

    ElfGame _game;
    bool[] _active;
    float[] _lastLand, _lastSeen;
    Transform _piston;
    float _pistonAnim = 1f, _pistonRestY;
    TextMeshPro _sign;
    float _signTimer;
    GameObject[] _lights;
    Color[] _memberColors;
    bool _pending;
    float _pendingSince, _syncFlash, _beltY;

    // Claw loader
    Transform _arm, _claw, _cable, _pile;
    ElfToy _held;
    float _clawTime = -1f;
    Vector3 _heldFrom;
    readonly List<Transform> _sleighs = new List<Transform>(); // [0] = the one being filled, the full ones on its left
    Transform _sleighTemplate;
    GameObject _sleighClean;
    Vector3 _sleighScale, _sleighLocal;
    Quaternion _sleighLocalRot;
    float _sleighBaseS, _sleighFrontLocal, _sleighTopLocal;
    readonly List<float> _sleighS = new List<float>(); // current path distance of each sleigh of the row
    float _sleighSpacing;
    int _sleighFill;
    bool _onMachine;
    float _standY, _standZ;
    public TextMeshPro CounterLabel;
    ElfSquash _squash; // the machine / claw squashes a little when an elf lands on it

    public void Build(ElfGame game, int index, int steps, List<int> members, float machineX, Transform root, float beltY, float[] offsets, Transform scene)
    {
        _game = game;
        Index = index;
        Steps = steps;
        MachineX = machineX;
        _beltY = beltY;
        Members.AddRange(members);
        int n = Members.Count;
        MemberScore = new int[n];
        _active = new bool[n];
        _lastLand = new float[n];
        _lastSeen = new float[n];
        _memberColors = new Color[n];
        _lights = new GameObject[n];
        for (int i = 0; i < n; i++)
            _lastLand[i] = -99f;

        Transform station = new GameObject($"Station{index}").transform;
        station.SetParent(root, false);
        Root = station;
        Pose pose = ElfPath.At(machineX); // straight, ring or terraces
        station.SetPositionAndRotation(pose.position, pose.rotation);
        ElfAssets a = ElfArt.Assets;
        Color stepColor = ElfArt.StepColors[FirstStep(steps)];
        Name = BuildName(steps);
        Transform lights, label;

        // Machine placed in the scene (GameScene/---- Machines ----/<Step>): moved to this station's x, everything else
        // (local offsets, rotation, scale, added objects) kept as edited. Missing: instantiated from the prefabs.
        if (scene != null)
        {
            scene.gameObject.SetActive(true);
            scene.SetParent(station, false);
            Vector3 p = scene.localPosition;
            scene.localPosition = new Vector3(0f, p.y, p.z);
        }

        if (IsLoader)
        {
            // Claw on the camera side of the belt (turned 180°: its arm reaches back over the belt); it drops the gifts
            // into a row of sleighs standing in front of the belt, lower, on its left.
            Transform claw = scene != null ? scene.Find("ClawMachine") : null;
            if (claw == null)
            {
                claw = Object.Instantiate(a.ClawPrefab, station, false).transform;
                claw.localPosition = new Vector3(0f, 0f, -CLAW_Z);
                claw.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            // Ring / terraces: the claw stands behind the belt (tree side), its sleigh behind it, so the camera sees the
            // belt; its players stand on their buttons on top of it (on the arm joint), like on the other machines.
            bool behind = ElfPath.Current != ElfPath.Layout.Straight;
            if (behind)
            {
                claw.localPosition = new Vector3(claw.localPosition.x, claw.localPosition.y, Mathf.Abs(claw.localPosition.z));
                claw.localRotation = Quaternion.identity;
            }
            _squash = new ElfSquash(claw, 0.04f);
            _arm = claw.Find("Arm");
            _claw = _arm.Find("Claw");
            _cable = _claw.Find("Cable");
            if (behind)
            {
                Transform joint = _arm.Find("Joint");
                Transform column = claw.Find("Column");
                Bounds jb = ElfArt.GetBounds(joint != null ? joint.gameObject : _arm.gameObject);
                Vector3 axis = column != null ? column.position : claw.position;
                Vector3 top = station.InverseTransformPoint(new Vector3(axis.x, jb.max.y, axis.z));
                _onMachine = true;
                _standY = top.y;
                _standZ = top.z;
            }
            _arm.localRotation = Quaternion.Euler(0f, CLAW_SLEIGH_YAW, 0f);
            Vector3 drop = _claw.position;
            _arm.localRotation = Quaternion.identity;
            _sleighTemplate = scene != null ? scene.Find("Sleigh") : null;
            if (_sleighTemplate == null)
            {
                _sleighTemplate = Object.Instantiate(a.SleighPrefab, station, false).transform;
                _sleighTemplate.localScale = Vector3.one * 0.8f;
            }
            // Template placed so its pile is under the drop point (in the station frame); the row then follows the
            // path (ElfPath: straight, ring or terraces), slot i = i sleighs upstream.
            Transform pile = _sleighTemplate.Find("Pile");
            Vector3 dropLocal = station.InverseTransformPoint(drop);
            Vector3 offsetLocal = pile != null ? station.InverseTransformVector(pile.position - _sleighTemplate.position) : Vector3.zero;
            Vector3 templateLocal = station.InverseTransformPoint(_sleighTemplate.position);
            // Curved maps have a flat floor: the height set for the straight map (sleighs below its brick wall) is ignored.
            _sleighLocal = new Vector3(0f, ElfPath.Current == ElfPath.Layout.Straight ? templateLocal.y : 0f, dropLocal.z - offsetLocal.z);
            _sleighLocalRot = Quaternion.Inverse(station.rotation) * _sleighTemplate.rotation;
            _sleighBaseS = MachineX + dropLocal.x - offsetLocal.x;
            Quaternion keep = _sleighTemplate.rotation;
            _sleighTemplate.rotation = _sleighLocalRot; // measured upright, as in the station frame
            Bounds sb = ElfArt.GetBounds(_sleighTemplate.gameObject);
            _sleighTemplate.rotation = keep;
            SleighWidth = sb.size.x;
            _sleighSpacing = SleighWidth + 0.1f;
            _sleighFrontLocal = _sleighLocal.z + (sb.min.z - _sleighTemplate.position.z);
            _sleighTopLocal = (pile != null ? pile.position.y : sb.center.y) - _sleighTemplate.position.y + _sleighLocal.y - 0.05f;
            _sleighScale = _sleighTemplate.localScale;
            SetSleighPose(_sleighTemplate, _sleighBaseS);
            _sleighClean = Object.Instantiate(_sleighTemplate.gameObject, _sleighTemplate.parent);
            _sleighClean.name = "SleighSpare";
            _sleighClean.SetActive(false);
            Sleigh = _sleighTemplate;
            _sleighs.Add(Sleigh);
            _sleighS.Add(_sleighBaseS);
            _pile = pile != null ? pile : Sleigh;
            lights = new GameObject("Lights").transform;
            lights.SetParent(claw, false);
            lights.localPosition = new Vector3(0f, 1.6f, 0.34f);
            label = scene != null ? scene.Find("Label") : null;
            if (label == null)
            {
                label = new GameObject("Label").transform;
                label.SetParent(station, false);
                label.localPosition = new Vector3(0f, 6.4f, 0.6f);
            }
        }
        else
        {
            Transform machine = scene != null ? scene.Find("Machine") : null;
            if (machine == null) machine = Object.Instantiate(a.MachinePrefab, station, false).transform;
            _piston = machine.Find("Piston");
            _pistonRestY = _piston != null ? _piston.localPosition.y : 0f;
            _squash = new ElfSquash(machine, 0.06f);
            Transform stripe = machine.Find("Stripe");
            if (stripe != null) ElfArt.SetColor(stripe.gameObject, stepColor);
            // One player on the station: the piston takes the player color.
            if (_piston != null) ElfArt.SetColor(_piston.gameObject, n == 1 ? game.GetPlayerColor(Members[0]) : stepColor);
            lights = machine.Find("Lights");
            // The elves stand on their buttons on top of the machine: on the bottom of its "Head" (hidden) if it has
            // one, otherwise on its top, above the piston.
            Transform head = machine.Find("Head");
            Bounds hb = ElfArt.GetBounds(head != null ? head.gameObject : machine.gameObject);
            Vector3 stand = head != null ? new Vector3(hb.center.x, hb.min.y, hb.center.z)
                : _piston != null ? new Vector3(_piston.position.x, hb.max.y, _piston.position.z) : new Vector3(hb.center.x, hb.max.y, hb.center.z);
            Vector3 top = station.InverseTransformPoint(stand); // station frame
            _onMachine = true;
            _standY = top.y;
            _standZ = top.z;
            if (head != null) head.gameObject.SetActive(false);
            label = machine.Find("Label");
        }
        _sign = ElfArt.Label(label, new Vector3(0f, 1f, 0f), "", 7f, ElfArt.SyncOk);

        for (int i = 0; i < n; i++)
        {
            float x = _onMachine ? (i - (n - 1) * 0.5f) * 1.15f : offsets[i];
            _memberColors[i] = game.GetPlayerColor(Members[i]);
            ElfAvatar avatar = new ElfAvatar();
            Transform cap = AddButton(station, x, _memberColors[i]);
            avatar.Build(station, StandPosition(x), _memberColors[i], game.GetPlayerLetter(Members[i]), false, cap);
            Avatars.Add(avatar);
            _lights[i] = ElfArt.Prim(PrimitiveType.Sphere, lights, new Vector3(0f, i * 0.55f, 0f), Vector3.one * 0.45f, ElfArt.Grey(_memberColors[i]), "Light");
        }
    }

    /// <summary>Where an elf stands: behind the belt on its button, or on top of the machine (test).</summary>
    Vector3 StandPosition(float x) { return _onMachine ? new Vector3(x, _standY + 0.29f, _standZ) : new Vector3(x, _beltY, ELF_Z); }

    Transform AddButton(Transform station, float x, Color color)
    {
        ElfAssets a = ElfArt.Assets;
        if (a == null || a.ButtonPrefab == null) return null;
        Transform b = Object.Instantiate(a.ButtonPrefab, station, false).transform;
        b.localPosition = _onMachine ? new Vector3(x, _standY + 0.24f, _standZ) : new Vector3(x, _beltY - 0.05f, ELF_Z);
        // Socket stretched down to the floor.
        Transform socket = b.Find("Socket");
        float h = b.position.y;
        if (socket != null && h > 0.1f && !_onMachine) { socket.localPosition = new Vector3(0f, -h * 0.5f, 0f); socket.localScale = new Vector3(socket.localScale.x, h * 0.5f, socket.localScale.z); }
        Transform cap = b.Find("Cap");
        if (cap != null) ElfArt.SetColor(cap.gameObject, color);
        return cap;
    }

    public bool HasActiveHuman()
    {
        for (int i = 0; i < _active.Length; i++)
            if (_active[i]) return true;
        return false;
    }

    public bool IsMemberActive(int member) { return _active[member]; }

    public void OnJump(int member, float time, float airSeconds)
    {
        Activate(member, time);
        Avatars[member].Jump(airSeconds);
    }

    /// <summary>A landing = the "validation" jump. Fires the machine when every active player of the station landed
    /// within SYNC_WINDOW.</summary>
    public void OnLand(int member, float time, List<ElfToy> toys)
    {
        Activate(member, time);
        _squash?.Punch();
        Avatars[member].Land();
        _lastLand[member] = time;

        int activeCount = 0;
        bool all = true;
        for (int i = 0; i < _active.Length; i++)
        {
            if (!_active[i]) continue;
            activeCount++;
            if (time - _lastLand[i] > SYNC_WINDOW) all = false;
        }
        if (!all)
        {
            _pending = true;
            _pendingSince = time;
            return;
        }
        _pending = false;
        for (int i = 0; i < _active.Length; i++)
        {
            if (!_active[i]) continue;
            _lastLand[i] = -99f;
        }
        bool processed = Fire(toys, false);
        if (processed)
        {
            for (int i = 0; i < _active.Length; i++)
                if (_active[i]) MemberScore[i]++;
        }
        if (activeCount > 1)
        {
            _syncFlash = 0.5f;
            ShowSign(ElfLoc.Get("SYNC_OK"), ElfArt.SyncOk);
        }
    }

    /// <summary>Processes the toy under the machine, if any. Returns true when a toy was processed.</summary>
    public bool Fire(List<ElfToy> toys, bool byBot)
    {
        _pistonAnim = 0f;
        ElfToy best = null;
        float bestDistance = _game.WindowX;
        int bit = 1 << Index;
        for (int i = 0; i < toys.Count; i++)
        {
            ElfToy toy = toys[i];
            if (!CanTake(toy, byBot))
                continue;
            float d = Mathf.Abs(toy.X - MachineX);
            if (d <= bestDistance)
            {
                bestDistance = d;
                best = toy;
            }
        }
        if (best == null)
            return false;

        best.HandledStations |= bit;
        // The bot leaves unfinished toys alone (they drop into the bin at the end of the belt).
        if (byBot && best.Unfinished && (Steps & ~ElfToy.LOAD) != 0)
            return false;
        // A player alone on the station paints / wraps in his color.
        best.Apply(Steps, Members.Count == 1 ? _memberColors[0] : (Color?)null);
        _game.RefreshOwner(best);
        // The buttons and the transformed toy light up together (visual link).
        for (int i = 0; i < Avatars.Count; i++) Avatars[i].Glow();
        best.Glow(Members.Count > 0 ? _memberColors[0] : Color.white);
        ElfSoundSet sounds = _game.Sounds;
        _game.PlaySfx(IsLoader ? sounds.ClawGrab : FirstStep(Steps) == 0 ? sounds.Carve : FirstStep(Steps) == 1 ? sounds.Paint : sounds.Wrap);
        if (IsLoader)
        {
            // The bot never loads a toy that was not wrapped.
            if (!byBot || (best.Done & ElfToy.WRAP) != 0)
            {
                toys.Remove(best);
                Grab(best);
            }
        }
        Processed++;
        return true;
    }

    /// <summary>A toy this machine may process: on the belt, not passed yet, its step not already done (by the other
    /// machine of the step). The other lane's toy (colored ring of the other player): only when that player is not
    /// active (it then counts for this one); never for the automatic machine.</summary>
    bool CanTake(ElfToy toy, bool byBot)
    {
        if (!toy.IsMoving || toy.Ghost || (toy.HandledStations & (1 << Index)) != 0) return false;
        int needed = Steps & ~ElfToy.LOAD;
        if (needed != 0 && (toy.Done & needed) == needed) return false;
        if (Lane < 0 || toy.Kind == Lane) return true;
        if (byBot || Team == null) return false;
        foreach (ElfStation other in Team.Machines)
            if (other != this && other.Lane == toy.Kind && other.HasActiveHuman()) return false;
        return true;
    }

    /// <summary>Start of the play: the players who jumped during the tutorial get a fresh AFK delay.</summary>
    public void ResetSeen(float time)
    {
        for (int i = 0; i < _lastSeen.Length; i++) _lastSeen[i] = time;
    }

    public void ShowMissed() { ShowSign(ElfLoc.Get("MISSED"), new Color(1f, 0.3f, 0.25f)); _game.PlaySfx(_game.Sounds.Missed); }

    public void SetCrown(bool on)
    {
        for (int i = 0; i < Avatars.Count; i++)
            Avatars[i].SetCrown(on);
    }

    public void Celebrate()
    {
        for (int i = 0; i < Avatars.Count; i++)
            if (!Avatars[i].IsRobot) Avatars[i].Celebrate();
    }

    public void Tick(float deltaTime, float time, bool playing, List<ElfToy> toys)
    {
        _squash?.Tick(deltaTime);
        // No active player: the machine runs on its own, no character shown (absent elves are hidden, their "Jump to
        // play" pill stays). With 2 machines on the step it only takes its own lane.
        if (playing && !HasActiveHuman())
        {
            int bit = 1 << Index;
            for (int i = 0; i < toys.Count; i++)
            {
                ElfToy toy = toys[i];
                if (CanTake(toy, true) && toy.X >= MachineX - 0.05f && toy.X <= MachineX + _game.WindowX)
                {
                    Fire(toys, true);
                    break;
                }
            }
        }

        for (int i = 0; i < _active.Length; i++)
        {
            if (playing && _active[i] && time - _lastSeen[i] > AFK_SECONDS)
            {
                _active[i] = false;
                Avatars[i].SetActive(false);
            }
        }

        if (_pending && time - _pendingSince > SYNC_WINDOW)
        {
            _pending = false;
            ShowSign(ElfLoc.Get("SYNC_KO"), ElfArt.SyncKo);
        }

        _syncFlash -= deltaTime;
        for (int i = 0; i < _lights.Length; i++)
        {
            Color c;
            if (!_active[i]) c = ElfArt.Grey(_memberColors[i]) * 0.5f;
            else if (_syncFlash > 0f) c = ElfArt.SyncOk;
            else if (time - _lastLand[i] <= SYNC_WINDOW) c = Color.white;
            else c = _memberColors[i];
            ElfArt.SetColor(_lights[i], c);
        }

        if (_piston != null)
        {
            _pistonAnim = Mathf.Min(1f, _pistonAnim + deltaTime * 3.5f);
            float down = Mathf.Sin(Mathf.Clamp01(_pistonAnim) * Mathf.PI);
            _piston.localPosition = new Vector3(0f, _pistonRestY - down * 1.1f, 0f);
        }
        if (_arm != null) { TickClaw(deltaTime); TickSleighs(deltaTime); }

        _signTimer -= deltaTime;
        if (_signTimer <= 0f && _sign.text.Length > 0)
            _sign.text = "";

        for (int i = 0; i < Avatars.Count; i++)
            Avatars[i].Tick(deltaTime, time);
    }

    #region Claw
    void Grab(ElfToy toy)
    {
        if (_held != null) Drop();
        _held = toy;
        toy.Hold();
        _heldFrom = toy.Root.position;
        _clawTime = 0f;
    }

    /// <summary>0-0.18 s down onto the toy, 0.18-0.4 up, 0.4-0.8 turn to the sleigh, drop, 0.8-1.2 back.</summary>
    void TickClaw(float deltaTime)
    {
        float restY = -1.2f, grabY = _beltY + ElfToy.SIZE + 0.35f - _arm.localPosition.y;
        float yaw = 0f, y = restY;
        if (_clawTime >= 0f)
        {
            _clawTime += deltaTime;
            float t = _clawTime;
            if (t < 0.18f) y = Mathf.Lerp(restY, grabY, t / 0.18f);
            else if (t < 0.4f) y = Mathf.Lerp(grabY, restY, (t - 0.18f) / 0.22f);
            else if (t < 0.8f) { yaw = Mathf.SmoothStep(0f, CLAW_SLEIGH_YAW, (t - 0.4f) / 0.4f); y = restY; }
            else if (t < 1.2f) yaw = Mathf.SmoothStep(CLAW_SLEIGH_YAW, 0f, (t - 0.8f) / 0.4f);
            else _clawTime = -1f;
            if (_held != null)
            {
                Vector3 hook = _claw.position + Vector3.down * (ElfToy.SIZE + 0.25f);
                _held.Root.position = t < 0.18f ? Vector3.Lerp(_heldFrom, hook, t / 0.18f) : hook;
                if (t >= 0.8f) Drop();
            }
        }
        _arm.localRotation = Quaternion.Euler(0f, yaw, 0f);
        _claw.localPosition = new Vector3(0f, y, _claw.localPosition.z);
        if (_cable != null)
        {
            _cable.localPosition = new Vector3(0f, -y * 0.5f, 0f);
            _cable.localScale = new Vector3(_cable.localScale.x, -y * 0.5f, _cable.localScale.z);
        }
    }

    /// <summary>The gifts go to the central pile (ElfGame.ThrowToPile): no sleigh.</summary>
    public void HideSleighs()
    {
        foreach (Transform s in _sleighs) s.gameObject.SetActive(false);
    }

    void Drop()
    {
        ElfToy toy = _held;
        _held = null;
        if (_game.ThrowToPile(toy)) return; // several screens: thrown onto the gift pile in the middle of the hall
        int k = _sleighFill++;
        toy.Park(_pile, new Vector3(((k % 4) - 1.5f) * 0.7f, (k / 8) * 0.55f, ((k / 4) % 2 - 0.5f) * 0.7f));
        _game.LoadToy(toy);
        if (_sleighFill >= SLEIGH_CAPACITY) NextSleigh();
    }

    /// <summary>Fills the row in front of the belt with count sleighs (empty at start; the boards hang on them).</summary>
    public void BuildSleighRow(int count)
    {
        _rowCount = Mathf.Max(1, count);
        for (int i = _sleighs.Count; i < _rowCount; i++)
        {
            Transform s = Object.Instantiate(_sleighClean, _sleighClean.transform.parent).transform;
            s.name = "Sleigh";
            s.gameObject.SetActive(true);
            SetSleighPose(s, SleighSlotS(i));
            _sleighS.Add(SleighSlotS(i));
            _sleighs.Add(s);
        }
    }

    /// <summary>The full sleigh slides left with the others (players watch the row fill up); an empty one pops in
    /// under the claw. The oldest (leftmost) shrinks away: the row keeps its length.</summary>
    void NextSleigh()
    {
        Transform s = Object.Instantiate(_sleighClean, _sleighClean.transform.parent).transform;
        s.name = "Sleigh";
        s.gameObject.SetActive(true);
        SetSleighPose(s, _sleighBaseS);
        _sleighS.Insert(0, _sleighBaseS);
        s.localScale = Vector3.zero;
        _sleighs.Insert(0, s);
        Sleigh = s;
        Transform pile = s.Find("Pile");
        _pile = pile != null ? pile : s;
        _sleighFill = 0;
        if (CounterLabel != null)
        {
            Transform counter = s.Find("Counter");
            CounterLabel.transform.SetParent(counter != null ? counter : s, false);
        }
        while (_sleighs.Count > _rowCount)
        {
            _leaving.Add(_sleighs[_sleighs.Count - 1]); // shrinks away (TickSleighs)
            _sleighs.RemoveAt(_sleighs.Count - 1);
            _sleighS.RemoveAt(_sleighS.Count - 1);
        }
    }

    /// <summary>Sleigh at path distance s: on the camera side of the belt, oriented like the belt there.</summary>
    void SetSleighPose(Transform sleigh, float s)
    {
        Pose p = ElfPath.At(s);
        sleigh.SetPositionAndRotation(p.position + p.rotation * _sleighLocal, p.rotation * _sleighLocalRot);
    }

    void TickSleighs(float deltaTime)
    {
        for (int i = _leaving.Count - 1; i >= 0; i--)
        {
            Transform s = _leaving[i];
            s.localScale = Vector3.MoveTowards(s.localScale, Vector3.zero, deltaTime * 2f);
            if (s.localScale.x <= 0f) { Object.Destroy(s.gameObject); _leaving.RemoveAt(i); }
        }
        for (int i = 0; i < _sleighs.Count; i++)
        {
            Transform s = _sleighs[i];
            _sleighS[i] = Mathf.MoveTowards(_sleighS[i], SleighSlotS(i), _sleighSpacing * 1.5f * deltaTime);
            SetSleighPose(s, _sleighS[i]);
            if (i == 0) s.localScale = Vector3.MoveTowards(s.localScale, _sleighScale, deltaTime * 3f);
        }
    }
    #endregion

    void Activate(int member, float time)
    {
        _lastSeen[member] = time;
        if (_active[member])
            return;
        _active[member] = true;
        Avatars[member].SetActive(true);
    }

    void ShowSign(string text, Color color)
    {
        _sign.text = text;
        _sign.color = color;
        _signTimer = 0.8f;
    }

    public static int FirstStep(int steps)
    {
        for (int s = 0; s < 4; s++) if ((steps & (1 << s)) != 0) return s;
        return 0;
    }

    static string BuildName(int steps)
    {
        string numbers = "", names = "";
        for (int s = 0; s < 4; s++)
        {
            if ((steps & (1 << s)) == 0) continue;
            numbers += (numbers.Length > 0 ? "+" : "") + (s + 1);
            names += (names.Length > 0 ? " + " : "") + ElfLoc.Get("STEP_" + s);
        }
        return numbers + ". " + names;
    }
}
