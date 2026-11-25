using System.Collections.Generic;
using System.Text;
using PleyoDLL.Interface;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Elves' Workshop orchestrator. One continuous 3D workshop (decor in GameScene: "---- Workshop ----", ElfWorkshop): a
/// conveyor belt crossing all the screens (one camera per screen, side by side). Raw blocks come out of the spawner
/// machine on screen 1, go through Carve / Paint / Wrap stations, and the claw loader puts them in the sleigh; toys
/// nobody loads fall into the bin at the end of the line.
///
/// Stations from the Player Template (config "TrampolineInfos": active trampolines, screen, color, letter):
/// - 4 screens: one step per screen, its players jump together.
/// - 3 screens: steps 1..3, the claw loads the sleigh on its own on the last screen.
/// - 2 screens: one station per player (screen 1: carve / paint, screen 2: wrap / load).
/// - 1 screen: 1-2 players = machines "1+2" and "3+4"; 3 players = one step each + the claw loads on its own.
/// A station without an active player is run by a bot (its elves stay grey with "Jump to play").
/// Difficulty: toy spacing in beats of the trampoline BPM (config), slowed when the players bounce slower (longer beats).
/// Displays: real displays in a build with enough monitors, else tiled on the main display (Editor).
/// </summary>
public class ElfGame : MonoBehaviour
{
    #region Variables
    public static ElfGame Instance { get; private set; }

    [SerializeField] ElfAssets _assets;
    [SerializeField] float _tutorialSeconds = 10f;
    [SerializeField] float _countdownSeconds = 3f;
    [SerializeField] float _playSeconds = 120f;
    [SerializeField] float _resultsSeconds = 10f;
    [SerializeField] float _beltSpeed = 2.4f;
    [SerializeField] bool _forceTiledDisplays;
    [Tooltip("Editor: screen k renders to Display k + 1 (open one Game view per display) instead of tiles in one Game view.")]
    [SerializeField] bool _editorDisplayPerScreen = true;
    [Tooltip("Map used with 2-4 screens (1 screen: the straight workshop).")]
    [SerializeField] ElfPath.Layout _multiLayout = ElfPath.Layout.Ring;
    [Tooltip("Ring map camera: height, z (from the belt, negative = outside the ring), pitch.")]
    [SerializeField] Vector3 _ringCamera = new Vector3(14.26f, -19.84f, 22f);
    [Tooltip("Terraces map camera (each row): height, z, pitch; it stands between its row and the previous one (z > -TERRACE_DEPTH).")]
    [SerializeField] Vector3 _terraceCamera = new Vector3(12f, -15f, 22f);
    [SerializeField] bool _logJumps;

    const float SCREEN_WIDTH = 20f;

    [Header("Camera of each screen (x = center of its screen)")]
    [SerializeField] float _cameraHeight = 12f;
    [SerializeField] float _cameraZ = -24f;
    [SerializeField] float _cameraPitch = 20f;
    // World width shown by each screen: 20 m, 25 m for 1 screen with 3 players (4 stations; no screen continuity to keep).
    float _screenWidth = SCREEN_WIDTH;
    const float WINDOW_SECONDS_MIN = 0.45f;
    const int MAX_BIN_TOYS = 40;
    const float ELF_SPACING = 2.6f;

    public float WindowX { get; private set; }
    public int Gifts { get; private set; }
    public int UnfinishedLoaded { get; private set; }

    PleyoDLL_ConfigurationFileExtended _config;
    int _inputMethod;
    bool _touchMode;
    float _configBpm = 80f;
    float _airSeconds = 0.4f;

    readonly List<KeyValuePair<int, List<int>>> _screens = new List<KeyValuePair<int, List<int>>>();
    readonly List<ElfStation> _stations = new List<ElfStation>(); // machines, in line order
    readonly List<ElfTeam> _teams = new List<ElfTeam>();
    readonly List<ElfToy> _toys = new List<ElfToy>();
    readonly List<ElfToy> _binned = new List<ElfToy>();
    readonly List<Camera> _cameras = new List<Camera>();
    readonly List<ElfHud> _huds = new List<ElfHud>();
    readonly Dictionary<int, KeyValuePair<ElfStation, int>> _byTrampoline = new Dictionary<int, KeyValuePair<ElfStation, int>>();
    readonly Dictionary<int, float> _lastLand = new Dictionary<int, float>();
    readonly Dictionary<int, float> _lastJump = new Dictionary<int, float>();
    readonly Dictionary<int, float> _periodEma = new Dictionary<int, float>();
    readonly List<KeyValuePair<float, int>> _scheduledLands = new List<KeyValuePair<float, int>>();

    Transform _world, _sceneMachines;
    ElfWorkshop _workshop;
    Material _beltMaterial;
    TMPro.TextMeshPro _sleighLabel;
    float _sleighPunch;
    float _beltY = 1.2f, _spawnX = 1.5f, _beltFrontZ = -1f;
    readonly ElfBoards _boards = new ElfBoards();
    float _endX, _playTimer, _spawnTimer, _resultsTimer;
    int _spawned;
    bool _ownGameOver, _quitRequested, _celebrated;
    readonly StringBuilder _sb = new StringBuilder();

    static readonly KeyCode[] DEBUG_KEYS =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
        KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Equals
    };
    #endregion

    #region Unity Methods
    void Awake()
    {
        Instance = this;
        ElfArt.Assets = _assets;
        _config = PleyoDLL_ConfigurationFileExtended.LoadConfigurationData();
        if (_config != null)
        {
            _inputMethod = _config.InputMethod;
            _configBpm = _config.TrampolineAverageBPM > 1f ? _config.TrampolineAverageBPM : 80f;
            float airShare = _config.PercentageOfTheTimeSpentInTheAir > 0.1f ? _config.PercentageOfTheTimeSpentInTheAir : 0.58f;
            _airSeconds = airShare * 60f / _configBpm;
        }
        _touchMode = _inputMethod == 7 || _inputMethod == 8;
    }

    void Start()
    {
        EnsureEventSystem();
        ReadScreens();
        if (_screens.Count == 1 && _screens[0].Value.Count >= 3) _screenWidth = SCREEN_WIDTH + 5f;
        SetupWorkshop();
        BuildStations();
        // Sleigh row of the claw in front of the belt: one sleigh per score board, the boards hang on them.
        ElfStation loader = _stations.Find(st => st.IsLoader);
        if (loader != null) loader.BuildSleighRow(_teams.Count);
        _boards.Build(_world, _teams.Count, loader, _screens.Count, _screenWidth, _endX, _beltY - 0.05f, _beltFrontZ - 0.03f);
        BuildViews();
        ElfInput.OnPlayerJump += OnPlayerJump;
        ElfInput.OnPlayerLand += OnPlayerLand;
        Debug.Log($"ElfGame: {_screens.Count} screen(s), {_stations.Count} station(s), input {_inputMethod}, BPM {_configBpm}");
    }

    void OnDestroy()
    {
        ElfInput.OnPlayerJump -= OnPlayerJump;
        ElfInput.OnPlayerLand -= OnPlayerLand;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float time = Time.time;
        UpdateDebugInputs(time);
        // Invisible close cross: a mouse click in the top right corner (100 x 100 px) of the window quits.
        if (Input.GetMouseButtonDown(0) && Input.mousePosition.x > Screen.width - 100f && Input.mousePosition.y > Screen.height - 100f) QuitGame();

        EGameState state = GetGameState();
        bool gameOver = state == EGameState.GAME_OVER || _ownGameOver;
        bool playing = false, big = false;
        string message = null, timer = "";
        float introSeconds = _tutorialSeconds + _countdownSeconds;

        if (gameOver)
        {
            _resultsTimer += dt;
            message = GetResultsText();
            if (!_celebrated)
            {
                _celebrated = true;
                for (int i = 0; i < _stations.Count; i++) _stations[i].Celebrate();
            }
            if (_resultsTimer >= _resultsSeconds) QuitGame();
        }
        else if (state == EGameState.GAME_LOADING)
        {
            message = ElfLoc.Get("TUTO_1");
        }
        else
        {
            _playTimer += dt;
            if (_playTimer < _tutorialSeconds)
            {
                int page = Mathf.Min(2, (int)(_playTimer / (_tutorialSeconds / 3f)));
                message = $"<size=125%><color=#B45309>{ElfLoc.Get("TITLE")}</color></size>\n{ElfLoc.Get("TUTO_" + (page + 1))}";
            }
            else if (_playTimer < introSeconds)
            {
                message = Mathf.CeilToInt(introSeconds - _playTimer).ToString();
                big = true;
            }
            else
            {
                playing = true;
                float elapsed = _playTimer - introSeconds;
                float remaining = Mathf.Max(0f, _playSeconds - elapsed);
                if (elapsed < 1.2f) { message = ElfLoc.Get("GO"); big = true; }
                timer = $"{(int)remaining / 60}:{(int)remaining % 60:00}";
                UpdateSpawn(dt, elapsed);
                if (remaining <= 0f && PleyoGameManagerTemplate.Instance == null) _ownGameOver = true;
            }
        }

        WindowX = Mathf.Max(WINDOW_SECONDS_MIN, 0.6f * GetEffectivePeriod()) * _beltSpeed;
        UpdateToys(dt);
        ElfToy.TickGlows(dt);
        if (_beltMaterial != null && _workshop != null)
            _beltMaterial.mainTextureOffset += _workshop.BeltScroll * (_beltSpeed * dt);
        for (int i = 0; i < _stations.Count; i++)
            _stations[i].Tick(dt, time, playing, _toys);
        UpdateCrowns();
        if (_sleighLabel != null)
        {
            // Sleigh counter pops at each loaded gift.
            _sleighPunch = Mathf.MoveTowards(_sleighPunch, 0f, dt * 2.5f);
            float pop = 1f + Mathf.Sin(_sleighPunch * Mathf.PI) * 0.6f * _sleighPunch;
            _sleighLabel.transform.localScale = Vector3.one * pop;
        }

        UpdateScores();
        for (int i = 0; i < _huds.Count; i++)
        {
            _huds[i].SetTimer(timer);
            _huds[i].SetMessage(message, big);
        }
    }
    #endregion

    #region Public Methods
    public float GetTotalGameDuration() { return _tutorialSeconds + _countdownSeconds + _playSeconds; }

    /// <summary>Called by the claw when it drops a toy in the sleigh.</summary>
    public void LoadToy(ElfToy toy)
    {
        bool finished = !toy.Unfinished && (toy.Done & (ElfToy.CARVE | ElfToy.PAINT | ElfToy.WRAP)) == (ElfToy.CARVE | ElfToy.PAINT | ElfToy.WRAP);
        if (finished) Gifts++;
        else UnfinishedLoaded++;
        if (_sleighLabel != null) _sleighLabel.text = Gifts.ToString();
        _sleighPunch = 1f;
    }

    public Color GetPlayerColor(int trampolineId)
    {
        if (MMDefaultPlayersColor.Instance != null)
            return MMDefaultPlayersColor.Instance.GetPlayerColorFromTrampolineId(trampolineId);
        return Color.HSVToRGB(trampolineId / 8f % 1f, 0.8f, 1f);
    }

    public string GetPlayerLetter(int trampolineId) { return PleyoMMSensors.GetLetterFromTrampolineId(trampolineId); }

    public void QuitGame()
    {
        if (_quitRequested) return;
        _quitRequested = true;
        if (PleyoGameManagerTemplate.Instance != null) PleyoGameManagerTemplate.Instance.ApplicationQuit();
        else Application.Quit();
    }

    /// <summary>Called by MyGameManager at game over: each player's score = toys processed by his machine; ranking =
    /// his team's rank.</summary>
    public void ApplyScoresToTemplate(MMGameManager manager)
    {
        foreach (ElfTeam team in _teams)
        {
            int rank = 1;
            foreach (ElfTeam other in _teams)
                if (other.HasPlayers && other.Processed > team.Processed) rank++;
            foreach (ElfStation station in team.Machines)
                for (int m = 0; m < station.Members.Count; m++)
                {
                    int playerId = PleyoMMSensors.TrampolineId_to_PlayerId(station.Members[m]);
                    manager.SetPlayerScore(playerId, station.MemberScore[m]);
                    manager.SetPlayerRanking(playerId, rank);
                }
        }
    }
    #endregion

    #region Input
    void OnPlayerJump(int trampolineId)
    {
        if (!_byTrampoline.TryGetValue(trampolineId, out KeyValuePair<ElfStation, int> slot)) return;
        EGameState state = GetGameState();
        // Keyboard / touch don't go through the template's "jump to join": mark the player ready ourselves.
        if (state == EGameState.GAME_LOADING && MMGameManager.Instance != null && (_touchMode || _inputMethod <= 1 || Application.isEditor))
            MMGameManager.Instance.PlayerReady(PleyoMMSensors.TrampolineId_to_PlayerId(trampolineId));
        _lastJump[trampolineId] = Time.time;
        slot.Key.OnJump(slot.Value, Time.time, _airSeconds);
    }

    void OnPlayerLand(int trampolineId, float jumpStrength01)
    {
        if (!_byTrampoline.TryGetValue(trampolineId, out KeyValuePair<ElfStation, int> slot)) return;
        float time = Time.time;
        // Measured jump period of this player (slower bounces = longer period = slower line).
        float configPeriod = 60f / _configBpm;
        if (_lastLand.TryGetValue(trampolineId, out float last))
        {
            float interval = time - last;
            if (interval > 0.3f && interval < configPeriod * 2.5f)
                _periodEma[trampolineId] = _periodEma.TryGetValue(trampolineId, out float ema) ? Mathf.Lerp(ema, interval, 0.3f) : interval;
        }
        if (_logJumps && _lastJump.TryGetValue(trampolineId, out float jumpTime))
            Debug.Log($"ElfGame: land {GetPlayerLetter(trampolineId)} air {time - jumpTime:0.00}s strength {jumpStrength01:0.00}");
        _lastLand[trampolineId] = time;
        slot.Key.OnLand(slot.Value, time, _toys);
    }

    /// <summary>Development keys 1..9, 0, -, = : trampolines A..L jump (landing after the config air time); Space: all.
    /// Touch (input 7 / 8): the touched elf jumps.</summary>
    void UpdateDebugInputs(float time)
    {
        for (int i = _scheduledLands.Count - 1; i >= 0; i--)
        {
            if (time < _scheduledLands[i].Key) continue;
            int id = _scheduledLands[i].Value;
            _scheduledLands.RemoveAt(i);
            ElfInput.TriggerLand(id, 0.5f);
        }
        if (Input.anyKeyDown)
        {
            for (int i = 0; i < DEBUG_KEYS.Length; i++)
                if (Input.GetKeyDown(DEBUG_KEYS[i])) SimulateJump(i, time);
            if (Input.GetKeyDown(KeyCode.Space))
                foreach (int id in _byTrampoline.Keys) SimulateJump(id, time);
        }
        if (_touchMode)
        {
            for (int t = 0; t < Input.touchCount; t++)
                if (Input.GetTouch(t).phase == TouchPhase.Began) OnTouch(Input.GetTouch(t).position, time);
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0)) OnTouch(Input.mousePosition, time);
        }
    }

    void SimulateJump(int trampolineId, float time)
    {
        if (!_byTrampoline.ContainsKey(trampolineId)) return;
        ElfInput.TriggerJump(trampolineId);
        _scheduledLands.Add(new KeyValuePair<float, int>(time + _airSeconds, trampolineId));
    }

    void OnTouch(Vector2 screenPosition, float time)
    {
        float best = float.MaxValue;
        int bestId = -1;
        for (int c = 0; c < _cameras.Count; c++)
        {
            Camera cam = _cameras[c];
            if (!cam.pixelRect.Contains(screenPosition)) continue;
            foreach (KeyValuePair<int, KeyValuePair<ElfStation, int>> pair in _byTrampoline)
            {
                Vector3 p = cam.WorldToScreenPoint(pair.Value.Key.Avatars[pair.Value.Value].Root.position + Vector3.up * 1.5f);
                float d = ((Vector2)p - screenPosition).sqrMagnitude;
                if (p.z > 0f && d < best) { best = d; bestId = pair.Key; }
            }
        }
        if (bestId >= 0) SimulateJump(bestId, time);
    }
    #endregion

    #region Gameplay
    /// <summary>Toy spacing = 2 to 5 beats of the effective period (config BPM, slower when players bounce slower),
    /// tighter as the match goes on, and tighter when the steps have 2 machines (one per player). Toys alternate between
    /// the two carved models = the two lanes (one per machine of a 2-machine step).</summary>
    void UpdateSpawn(float dt, float elapsed)
    {
        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;
        int[] beats = elapsed < 30f ? new[] { 3, 4, 4, 5 } : elapsed < 80f ? new[] { 2, 3, 3, 4 } : new[] { 2, 2, 3, 3, 4 };
        _spawnTimer = beats[Random.Range(0, beats.Length)] * GetEffectivePeriod() * SpawnFactor();
        ElfToy toy = new ElfToy();
        toy.Init(_world, _spawnX, _beltY, _spawned++ % 2);
        _toys.Add(toy);
        RefreshOwner(toy);
    }

    /// <summary>0.6 when every Carve / Paint / Wrap step has 2 machines, 0.8 when some do, 1 otherwise.</summary>
    float SpawnFactor()
    {
        int steps = 0, doubled = 0;
        foreach (ElfTeam team in _teams)
        {
            if ((team.Steps & ~ElfToy.LOAD) == 0) continue;
            steps++;
            if (team.Machines.Count > 1) doubled++;
        }
        return doubled == 0 ? 1f : doubled == steps ? 0.6f : 0.8f;
    }

    /// <summary>Owner ring under a toy: color of the player whose machine should process it at its next step, when that
    /// step has 2 machines (each owns every other toy), shown only once the toy is on that player's screen. Called every
    /// frame per toy and after each machine step.</summary>
    int ScreenOf(float x) { return Mathf.Clamp(Mathf.FloorToInt(x / _screenWidth), 0, _screens.Count - 1); }

    public void RefreshOwner(ElfToy toy)
    {
        Color? owner = null;
        foreach (ElfTeam team in _teams)
        {
            int needed = team.Steps & ~ElfToy.LOAD;
            if (needed == 0 || (toy.Done & needed) == needed || toy.X > team.LastMachineX + WindowX) continue;
            if (team.Machines.Count > 1)
                foreach (ElfStation machine in team.Machines)
                    // Only on the owner's screen: players don't see the colors of the other screens' players.
                    if (machine.Lane == toy.Kind && machine.Members.Count > 0 && ScreenOf(toy.X) == ScreenOf(machine.MachineX))
                        owner = GetPlayerColor(machine.Members[0]);
            break;
        }
        toy.SetOwner(owner);
    }

    float GetEffectivePeriod()
    {
        float configPeriod = 60f / _configBpm;
        float sum = 0f;
        int count = 0;
        float now = Time.time;
        foreach (KeyValuePair<int, float> pair in _periodEma)
        {
            if (_lastLand.TryGetValue(pair.Key, out float last) && now - last < 5f) { sum += pair.Value; count++; }
        }
        float measured = count > 0 ? sum / count : configPeriod;
        return Mathf.Clamp(measured, configPeriod, configPeriod * 1.6f);
    }

    void UpdateToys(float dt)
    {
        for (int i = _toys.Count - 1; i >= 0; i--)
        {
            ElfToy toy = _toys[i];
            // Passed a machine: it can't process the toy any more. Passed the last machine of a step without that step
            // done: unfinished for good.
            for (int s = 0; s < _stations.Count; s++)
            {
                ElfStation station = _stations[s];
                int bit = 1 << station.Index;
                if ((toy.HandledStations & bit) != 0 || toy.X <= station.MachineX + WindowX) continue;
                toy.HandledStations |= bit;
                int needed = station.Steps & ~ElfToy.LOAD;
                if (needed == 0) continue;
                if (station.MachineX >= station.Team.LastMachineX && (toy.Done & needed) != needed)
                {
                    toy.MarkUnfinished();
                    station.ShowMissed();
                }
            }
            toy.Tick(dt, _beltSpeed, _beltY);
            RefreshOwner(toy); // also when the toy enters the next screen
            if (toy.X >= _endX)
            {
                toy.Fall(_beltSpeed);
                _toys.RemoveAt(i);
                _binned.Add(toy);
                if (_binned.Count > MAX_BIN_TOYS)
                {
                    _binned[0].Destroy();
                    _binned.RemoveAt(0);
                }
            }
        }
    }

    /// <summary>Crown on the elves of the team that processed the most toys (ties share it).</summary>
    void UpdateCrowns()
    {
        int best = 0;
        foreach (ElfTeam team in _teams) if (team.HasPlayers) best = Mathf.Max(best, team.Processed);
        foreach (ElfTeam team in _teams)
        {
            bool on = best > 0 && team.HasPlayers && team.Processed == best;
            foreach (ElfStation machine in team.Machines) machine.SetCrown(on);
        }
    }

    string[] _teamTexts;

    /// <summary>Score boards: gifts, unfinished gifts loaded, one board per team (step, letters, score).</summary>
    void UpdateScores()
    {
        if (_teamTexts == null) _teamTexts = new string[_teams.Count];
        int best = -1, bestScore = 0;
        for (int i = 0; i < _teams.Count; i++)
            if (_teams[i].HasPlayers && _teams[i].Processed > bestScore) { bestScore = _teams[i].Processed; best = i; }
        for (int i = 0; i < _teams.Count; i++)
        {
            ElfTeam team = _teams[i];
            _sb.Clear();
            _sb.Append("<size=70%><color=#475569>").Append(team.Name).Append("</color></size>\n");
            if (!team.HasPlayers) _sb.Append("<size=80%><color=#475569>").Append(ElfLoc.Get("ROBOT")).Append("</color></size>");
            foreach (ElfStation station in team.Machines)
                for (int m = 0; m < station.Members.Count; m++)
                {
                    Color c = station.IsMemberActive(m) ? GetPlayerColor(station.Members[m]) : Color.grey;
                    _sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c * 0.85f)).Append('>').Append(GetPlayerLetter(station.Members[m])).Append("</color> ");
                }
            _sb.Append("\n<size=150%>").Append(team.Processed).Append("</size>");
            _teamTexts[i] = _sb.ToString();
        }
        string gifts = $"{ElfLoc.Get("GIFTS")}\n<size=190%>{Gifts}</size>";
        string unfinished = $"<size=80%>{ElfLoc.Get("UNFINISHED")}</size>\n<size=190%>{UnfinishedLoaded}</size>";
        _boards.Set(gifts, unfinished, _teamTexts, best);
    }

    string GetResultsText()
    {
        ElfTeam bestTeam = null;
        foreach (ElfTeam team in _teams)
            if (team.HasPlayers && (bestTeam == null || team.Processed > bestTeam.Processed)) bestTeam = team;
        _sb.Clear();
        _sb.Append("<size=120%><color=#B45309>").Append(ElfLoc.Get("RESULTS")).Append("</color></size>\n");
        _sb.Append(ElfLoc.Get("GIFTS")).Append(" : <color=#B45309>").Append(Gifts).Append("</color>\n");
        _sb.Append("<size=70%><color=#475569>").Append(ElfLoc.Get("UNFINISHED")).Append(" : ").Append(UnfinishedLoaded);
        if (UnfinishedLoaded > 0) _sb.Append("  (").Append(ElfLoc.Get("FUN")).Append(')');
        _sb.Append("</color></size>\n");
        if (bestTeam != null && bestTeam.Processed > 0)
        {
            _sb.Append("<size=80%>").Append(ElfLoc.Get("BEST_TEAM")).Append(" : ");
            foreach (ElfStation station in bestTeam.Machines)
                for (int m = 0; m < station.Members.Count; m++)
                    _sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(GetPlayerColor(station.Members[m]) * 0.85f)).Append('>').Append(GetPlayerLetter(station.Members[m])).Append("</color> ");
            _sb.Append("(").Append(bestTeam.Name).Append(")</size>");
        }
        return _sb.ToString();
    }

    EGameState GetGameState()
    {
        if (PleyoGameManagerTemplate.Instance == null) return EGameState.GAME_PLAYING;
        return PleyoGameManagerTemplate.Instance.GetCurrentGameState();
    }
    #endregion

    #region Build
    /// <summary>Screens with players, left to right; trampolines of each screen sorted by real world position.</summary>
    void ReadScreens()
    {
        if (_config != null)
        {
            List<int> order = new List<int>();
            List<int> configOrder = _config.GetScreenIdOrderedByRealWorldPositionFromLeftToRight() ?? new List<int>();
            for (int i = 0; i < configOrder.Count; i++) if (!order.Contains(configOrder[i])) order.Add(configOrder[i]);
            for (int d = 0; d < 8; d++) if (!order.Contains(d)) order.Add(d);
            for (int i = 0; i < order.Count && _screens.Count < 4; i++)
            {
                if (!_config.IsAPlayerOnThisDisplayId(order[i])) continue;
                List<int> trampolines = _config.GetTrampolineIdSortByRealWorldPosition(order[i]);
                if (trampolines != null && trampolines.Count > 0)
                    _screens.Add(new KeyValuePair<int, List<int>>(order[i], new List<int>(trampolines)));
            }
        }
        if (_screens.Count == 0)
            _screens.Add(new KeyValuePair<int, List<int>>(0, new List<int> { 0 }));
    }

    void BuildStations()
    {
        int count = _screens.Count;
        const int C = ElfToy.CARVE, P = ElfToy.PAINT, W = ElfToy.WRAP, L = ElfToy.LOAD;
        // Per screen: list of (steps, members).
        List<List<KeyValuePair<int, List<int>>>> layout = new List<List<KeyValuePair<int, List<int>>>>();
        for (int s = 0; s < count; s++) layout.Add(new List<KeyValuePair<int, List<int>>>());
        int[] single = { C, P, W, L };

        if (count >= 3)
        {
            for (int s = 0; s < count; s++) layout[s].Add(Slot(single[s], _screens[s].Value, 0, 99));
            if (count == 3) layout[2].Add(Slot(L, null, 0, 0));
        }
        else if (count == 2)
        {
            for (int s = 0; s < 2; s++)
            {
                List<int> m = _screens[s].Value;
                layout[s].Add(Slot(single[s * 2], m, 0, 1));
                layout[s].Add(Slot(single[s * 2 + 1], m, 1, 99)); // 3rd player of the screen joins the 2nd station
            }
        }
        else
        {
            List<int> m = _screens[0].Value;
            if (m.Count >= 3)
            {
                layout[0].Add(Slot(C, m, 0, 1));
                layout[0].Add(Slot(P, m, 1, 1));
                layout[0].Add(Slot(W, m, 2, 99));
                layout[0].Add(Slot(L, null, 0, 0));
            }
            else
            {
                layout[0].Add(Slot(C | P, m, 0, 1));
                layout[0].Add(Slot(W | L, m, 1, 1));
            }
        }

        for (int s = 0; s < count; s++)
        {
            // One team per slot. A team of the Carve / Paint / Wrap steps gets one machine per player (lane 0, 1:
            // each owns every other toy); the claw (Load) stays one machine for its whole team (synchronized).
            List<KeyValuePair<int, List<int>>> slots = layout[s];
            List<KeyValuePair<ElfTeam, List<int>>> machines = new List<KeyValuePair<ElfTeam, List<int>>>();
            for (int i = 0; i < slots.Count; i++)
            {
                ElfTeam team = new ElfTeam { Steps = slots[i].Key };
                _teams.Add(team);
                bool loader = (team.Steps & L) != 0;
                List<int> members = slots[i].Value;
                if (loader || members.Count <= 1) machines.Add(new KeyValuePair<ElfTeam, List<int>>(team, members));
                else foreach (int member in members) machines.Add(new KeyValuePair<ElfTeam, List<int>>(team, new List<int> { member }));
            }

            // Machines packed left to right on their screen (after the spawner on screen 1, before the bin on the last).
            int n = machines.Count;
            float from = s == 0 ? 3.2f : 0.6f;
            float to = s == count - 1 ? _endX - s * _screenWidth - 0.3f : _screenWidth - 1.6f;
            float[] left = new float[n], right = new float[n];
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                bool loader = (machines[i].Key.Steps & L) != 0;
                int m = machines[i].Value.Count;
                float half = loader ? 1.3f : 1.6f;
                if (!loader) { left[i] = right[i] = half + 0.5f; }
                else
                {
                    int leftCount = loader ? m : Mathf.Max(m == 0 ? 1 : 0, (m + 1) / 2);
                    int rightCount = loader ? 0 : m / 2;
                    left[i] = half + leftCount * ELF_SPACING;
                    right[i] = loader ? 2.5f : half + rightCount * ELF_SPACING;
                }
                total += left[i] + right[i];
            }
            float scale = Mathf.Min(1f, (to - from) / total);
            float gap = Mathf.Max(0f, (to - from - total) / (n + 1));
            float cursor = s * _screenWidth + from + gap;
            for (int i = 0; i < n; i++)
            {
                ElfTeam team = machines[i].Key;
                bool loader = (team.Steps & L) != 0;
                float x = cursor + left[i] * scale;
                cursor += (left[i] + right[i]) * scale + gap;
                float half = loader ? 1.3f : 1.6f;
                List<int> members = machines[i].Value;
                float[] offsets = new float[members.Count];
                for (int m = 0; m < members.Count; m++)
                {
                    int k = loader ? m : m / 2;
                    float side = loader || m % 2 == 0 ? -1f : 1f;
                    offsets[m] = side * (half + ELF_SPACING * 0.5f + k * ELF_SPACING) * scale;
                }
                ElfStation station = new ElfStation();
                station.Team = team;
                station.Lane = team.Machines.Count; // set again below once the team is complete
                station.Build(this, _stations.Count, team.Steps, members, x, _world, _beltY, offsets, TakeSceneStation(team.Steps));
                for (int m = 0; m < station.Members.Count; m++)
                    _byTrampoline[station.Members[m]] = new KeyValuePair<ElfStation, int>(station, m);
                if (station.Sleigh != null) station.CounterLabel = _sleighLabel = ElfArt.Label(station.Sleigh.Find("Counter") ?? station.Sleigh, Vector3.zero, "0", 12f, ElfArt.Gold);
                team.Machines.Add(station);
                _stations.Add(station);
            }
        }
        // Lane = owner of every other toy, only when the team has 2 machines (-1: the machine takes every toy).
        foreach (ElfTeam team in _teams)
            for (int i = 0; i < team.Machines.Count; i++) team.Machines[i].Lane = team.Machines.Count > 1 ? i : -1;
        // Scene machine groups are templates (cloned per machine): hide them.
        if (_sceneMachines != null)
            foreach (string name in SCENE_STATIONS)
            {
                Transform template = _sceneMachines.Find(name);
                if (template != null) template.gameObject.SetActive(false);
            }
    }

    static readonly string[] SCENE_STATIONS = { "Carve", "Paint", "Wrap", "Load" };

    /// <summary>Copy of the machine group placed in the scene for these steps (---- Machines ----/Carve, Paint, Wrap,
    /// Load; the loader always uses Load): one copy per machine, so a step with 2 players gets 2 identical machines.
    /// Null to build it from the prefabs.</summary>
    Transform TakeSceneStation(int steps)
    {
        if (_sceneMachines == null) return null;
        string name = (steps & ElfToy.LOAD) != 0 ? "Load" : SCENE_STATIONS[ElfStation.FirstStep(steps)];
        Transform template = _sceneMachines.Find(name);
        if (template == null) return null;
        Transform copy = Instantiate(template.gameObject, template.parent).transform;
        copy.name = name;
        return copy;
    }

    static KeyValuePair<int, List<int>> Slot(int steps, List<int> players, int from, int max)
    {
        List<int> members = new List<int>();
        if (players != null)
            for (int i = from; i < players.Count && members.Count < max; i++) members.Add(players[i]);
        return new KeyValuePair<int, List<int>>(steps, members);
    }

    /// <summary>Uses the decor of the scene (ElfWorkshop): belt height, spawn point, belt tiles past the end of the line
    /// hidden; adds the bin at the end of the line.</summary>
    void SetupWorkshop()
    {
        // Map: the straight workshop for 1 screen, the ring or the terraces (_multiLayout) for 2-4 screens. Each map is a
        // root with ElfWorkshop in the scene (Layout field); the other maps are hidden.
        ElfPath.Layout layout = _screens.Count > 1 ? _multiLayout : ElfPath.Layout.Straight;
        _workshop = null;
        foreach (ElfWorkshop w in FindObjectsByType<ElfWorkshop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (w.Layout == layout || (_workshop == null && w.Layout == ElfPath.Layout.Straight)) _workshop = w;
        if (_workshop != null && _workshop.Layout != layout) layout = ElfPath.Layout.Straight; // map missing: straight
        ElfPath.Current = layout;
        // Ring / terraces: the path follows the map's Belt object (moved or turned in the scene with its tiles).
        ElfPath.Origin = layout != ElfPath.Layout.Straight && _workshop != null && _workshop.Belt != null
            ? new Pose(_workshop.Belt.position, Quaternion.Euler(0f, _workshop.Belt.eulerAngles.y, 0f)) : Pose.identity;
        foreach (ElfWorkshop w in FindObjectsByType<ElfWorkshop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            w.gameObject.SetActive(w == _workshop);
        // Lights and snow of the straight map belong to it.
        foreach (string name in new[] { "---- Lighting ----", "---- Outside Snow ----" })
        {
            GameObject straightOnly = GameObject.Find(name);
            if (straightOnly != null) straightOnly.SetActive(layout == ElfPath.Layout.Straight);
        }
        _endX = _screens.Count * _screenWidth - 2f;
        if (_workshop != null)
        {
            _world = _workshop.transform;
            bool straight = layout == ElfPath.Layout.Straight;
            _beltY = straight ? BeltTop(_workshop) : _workshop.BeltTopY;
            if (straight && _workshop.Belt != null && _workshop.Belt.childCount > 0) _beltFrontZ = ElfArt.GetBounds(_workshop.Belt.gameObject).min.z;
            if (_workshop.SpawnPoint != null) _spawnX = straight ? _workshop.SpawnPoint.position.x : _workshop.SpawnS;
            if (_workshop.Belt != null)
            {
                foreach (Transform tile in _workshop.Belt)
                {
                    // Tiles past the end of the line hidden (curved maps: by the path distance stored by the builder).
                    int index = tile.GetSiblingIndex();
                    bool keep = !straight && _workshop.TileS != null && index < _workshop.TileS.Length ? _workshop.TileS[index] < _endX - 0.5f : ElfArt.GetBounds(tile.gameObject).min.x < _endX - 0.5f;
                    tile.gameObject.SetActive(keep);
                    if (!keep || _workshop.BeltMaterial == null) continue;
                    foreach (Renderer r in tile.GetComponentsInChildren<Renderer>())
                    {
                        Material[] mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] != _workshop.BeltMaterial) continue;
                            if (_beltMaterial == null) _beltMaterial = new Material(_workshop.BeltMaterial);
                            mats[i] = _beltMaterial;
                        }
                        r.sharedMaterials = mats;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("ElfGame: no ElfWorkshop in the scene (menu Elves Workshop/Build Workshop In Open Scene).");
            _world = new GameObject("---- Workshop (missing) ----").transform;
            ElfArt.Prim(PrimitiveType.Cube, _world, new Vector3(_endX * 0.5f, _beltY * 0.5f, 0f), new Vector3(_endX + 2f, _beltY, 1.6f), new Color(0.2f, 0.2f, 0.22f), "Belt");
        }
        // Machines placed in the scene (menu Elves Workshop/Add Machines To Open Scene): their x is set by the layout.
        GameObject machines = GameObject.Find("---- Machines ----");
        _sceneMachines = machines != null ? machines.transform : null;
        Transform sceneBin = _sceneMachines != null ? _sceneMachines.Find("Bin") : null;
        if (sceneBin != null)
            PlaceOnPath(sceneBin, _endX + 1.15f, new Vector3(0f, sceneBin.position.y, sceneBin.position.z));
        else if (_assets != null && _assets.BinPrefab != null)
        {
            Transform bin = Instantiate(_assets.BinPrefab, _world, false).transform;
            PlaceOnPath(bin, _endX + 1.15f, Vector3.zero);
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.55f, 0.52f);
    }

    static void PlaceOnPath(Transform t, float s, Vector3 local)
    {
        Pose p = ElfPath.At(s);
        t.SetPositionAndRotation(p.position + p.rotation * local, p.rotation);
    }

    /// <summary>Top of the belt as placed in the scene (highest point of the belt tiles), so the belt can be moved freely;
    /// ElfWorkshop.BeltTopY when there are no tiles.</summary>
    static float BeltTop(ElfWorkshop workshop)
    {
        float top = float.MinValue;
        if (workshop.Belt != null)
            foreach (Renderer r in workshop.Belt.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
        return top > float.MinValue ? top : workshop.BeltTopY;
    }

    bool UseRealDisplays()
    {
        if (Application.isEditor) return _editorDisplayPerScreen;
        if (_forceTiledDisplays) return false;
        int maxDisplay = 0;
        for (int i = 0; i < _screens.Count; i++) maxDisplay = Mathf.Max(maxDisplay, _screens[i].Key);
        return maxDisplay > 0 && Display.displays.Length > maxDisplay;
    }

    /// <summary>One perspective camera + HUD per screen, each framing its 20 m of the workshop: side by side they
    /// look like one long workshop.</summary>
    void BuildViews()
    {
        MMCameraPlayer[] splitCameras = FindObjectsByType<MMCameraPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < splitCameras.Length; i++)
        {
            Camera splitCamera = splitCameras[i].GetComponent<Camera>();
            if (splitCamera != null) splitCamera.enabled = false;
        }

        bool real = UseRealDisplays();
        int count = _screens.Count;
        int columns = count <= 1 ? 1 : 2;
        int rows = count <= 2 ? 1 : 2;
        for (int s = 0; s < count; s++)
        {
            int displayId = Application.isEditor ? s : _screens[s].Key;
            if (real && displayId > 0 && displayId < Display.displays.Length && !Display.displays[displayId].active)
                Display.displays[displayId].Activate();

            Rect rect = new Rect(0f, 0f, 1f, 1f);
            if (!real && count > 1)
            {
                int column = s % columns, row = s / columns;
                float w = 1f / columns, h = 1f / rows;
                rect = rows == 1 ? new Rect(column * w, 0.25f, w, 0.5f) : new Rect(column * w, 1f - (row + 1) * h, w, h);
            }

            GameObject camObject = new GameObject($"Screen{s}_Camera");
            camObject.transform.SetParent(_world, false);
            Camera cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = 35f;
            cam.rect = rect;
            cam.targetDisplay = real ? displayId : 0;
            cam.depth = 10 + s;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.08f, 0.07f);
            float cx = s * _screenWidth + _screenWidth * 0.5f;
            // Same framing on every layout, in the belt frame at the screen centre (ring: outside looking in; terraces: in
            // front of and above its row).
            Pose view = ElfPath.At(cx);
            Vector3 cameraSet = ElfPath.Current == ElfPath.Layout.Ring ? _ringCamera : ElfPath.Current == ElfPath.Layout.Terraces ? _terraceCamera : new Vector3(_cameraHeight, _cameraZ, _cameraPitch);
            camObject.transform.SetPositionAndRotation(view.position + view.rotation * new Vector3(0f, cameraSet.x, cameraSet.y), view.rotation * Quaternion.Euler(cameraSet.z, 0f, 0f));
            UniversalAdditionalCameraData urp = camObject.AddComponent<UniversalAdditionalCameraData>();
            urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            urp.renderPostProcessing = true;
            _cameras.Add(cam);

            GameObject hudObject = new GameObject($"Screen{s}_Hud", typeof(RectTransform));
            hudObject.transform.SetParent(camObject.transform, false);
            ElfHud hud = hudObject.AddComponent<ElfHud>();
            hud.Build(cam, QuitGame);
            _huds.Add(hud);
        }
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
    #endregion
}
