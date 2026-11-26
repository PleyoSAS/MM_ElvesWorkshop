using UnityEngine;

/// <summary>Event "machine malfunction": twice per game the toy spawner goes haywire for 10-15 s: it shakes and
/// wobbles, throws sparks, flashes red, and the boxes come out about twice as fast. "Machine malfunction!" is shown on
/// every screen when it starts.</summary>
public partial class ElfGame
{
    [Header("Event: spawner malfunction")]
    [SerializeField] bool _malfunctions = true;
    [SerializeField] Vector2 _malfunctionSeconds = new Vector2(10f, 15f);
    [SerializeField] float _malfunctionSpawnFactor = 0.45f;

    float[] _malfunctionStarts;
    float _malfunctionEnd = -1f, _malfunctionBegin, _nextSpark;
    Light _alarm;
    Transform _spawner;
    /// <summary>The malfunction title is up (the HUD shakes it).</summary>
    public bool AlarmText { get; private set; }

    bool MalfunctionActive => _malfunctionEnd > 0f;

    /// <summary>Once per frame while playing (elapsed = play seconds). Sets a big message when it starts.</summary>
    void UpdateMalfunction(float elapsed, ref string message, ref bool big)
    {
        if (!_malfunctions || _spawner == null) return;
        if (_malfunctionStarts == null)
            _malfunctionStarts = new[] { Random.Range(_playSeconds * 0.28f, _playSeconds * 0.4f), Random.Range(_playSeconds * 0.65f, _playSeconds * 0.76f) };
        for (int i = 0; i < _malfunctionStarts.Length; i++)
        {
            if (_malfunctionStarts[i] < 0f || elapsed < _malfunctionStarts[i]) continue;
            _malfunctionStarts[i] = -1f;
            _malfunctionBegin = elapsed;
            _malfunctionEnd = elapsed + Random.Range(_malfunctionSeconds.x, _malfunctionSeconds.y);
            _spawnTimer = Mathf.Min(_spawnTimer, 0.3f);
            if (_alarm == null)
            {
                _alarm = new GameObject("MalfunctionAlarm").AddComponent<Light>();
                _alarm.transform.SetParent(_spawner, false);
                _alarm.transform.position = ElfArt.GetBounds(_spawner.gameObject).center + Vector3.up * 2f;
                _alarm.type = LightType.Point;
                _alarm.color = new Color(1f, 0.25f, 0.1f);
                _alarm.range = 9f;
                _alarm.shadows = LightShadows.None;
            }
            _alarm.enabled = true;
            StartTremor();
            PlaySfx(_sounds.MalfunctionAlarm);
            SetSfxLoop(_sounds.MalfunctionLoop, true);
        }
        if (!MalfunctionActive) return;
        float left = _malfunctionEnd - elapsed;
        AlarmText = elapsed - _malfunctionBegin < 2.6f;
        if (AlarmText) { message = ElfLoc.Get("MALFUNCTION"); big = true; }
        Tremor(left > 0f ? Mathf.Clamp01(left) : 0f);
        _spawnerPop?.SetShake(left > 0f ? Mathf.Clamp01(left) : 0f);
        _alarm.intensity = Mathf.PingPong(Time.time * 16f, 1f) > 0.5f ? 14f : 2f; // flashing
        if (Time.time >= _nextSpark)
        {
            _nextSpark = Time.time + Random.Range(0.12f, 0.25f);
            Bounds b = ElfArt.GetBounds(_spawner.gameObject);
            Vector3 p = new Vector3(Random.Range(b.min.x, b.max.x), b.max.y - Random.Range(0f, b.size.y * 0.4f), Random.Range(b.min.z, b.max.z));
            ElfArt.Sparkle(p, new Color(1f, 0.55f, 0.1f));
            PlaySfx(_sounds.Spark);
        }
        if (left <= 0f)
        {
            _malfunctionEnd = -1f;
            _spawnerPop?.SetShake(0f);
            _alarm.enabled = false;
            StopTremor();
            SetSfxLoop(null, false);
        }
    }

    // Malfunction effects: belt tiles swaying sideways (each out of phase), toys bouncing on them, red alarm lights,
    // screen shake (EventShake, used by the camera loop).
    readonly System.Collections.Generic.List<Light> _flicker = new System.Collections.Generic.List<Light>();
    readonly System.Collections.Generic.List<float> _flickerBase = new System.Collections.Generic.List<float>();
    readonly System.Collections.Generic.List<Color> _flickerColor = new System.Collections.Generic.List<Color>();
    readonly System.Collections.Generic.List<Transform> _tiles = new System.Collections.Generic.List<Transform>();
    readonly System.Collections.Generic.List<Vector3> _tileRest = new System.Collections.Generic.List<Vector3>(), _tileAxis = new System.Collections.Generic.List<Vector3>();
    static readonly Color AlarmRed = new Color(1f, 0.08f, 0.05f);

    /// <summary>0 to 1 while the spawner malfunctions: screen shake.</summary>
    public float EventShake { get; private set; }

    void StartTremor()
    {
        _tiles.Clear();
        _tileRest.Clear();
        _tileAxis.Clear();
        if (_workshop != null && _workshop.Belt != null)
            foreach (Transform tile in _workshop.Belt)
            {
                if (!tile.gameObject.activeSelf) continue;
                // Sideways = along the belt as seen by the camera (the path direction at the tile).
                int index = tile.GetSiblingIndex();
                float s = ElfPath.Current != ElfPath.Layout.Straight && _workshop.TileS != null && index < _workshop.TileS.Length ? _workshop.TileS[index] + 1f : tile.position.x;
                _tiles.Add(tile);
                _tileRest.Add(tile.localPosition);
                _tileAxis.Add(_workshop.Belt.InverseTransformDirection(ElfPath.Tangent(s)));
            }
        _flicker.Clear();
        _flickerBase.Clear();
        _flickerColor.Clear();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (!light.enabled || light == _alarm || light.type == LightType.Directional || light.name == "Glow" || light.name == "Flash") continue;
            _flicker.Add(light);
            _flickerBase.Add(light.intensity);
            _flickerColor.Add(light.color);
        }
    }

    void Tremor(float amount)
    {
        float t = Time.time;
        EventShake = amount;
        ElfToy.Jolt = amount;
        for (int i = 0; i < _tiles.Count; i++)
            if (_tiles[i] != null) _tiles[i].localPosition = _tileRest[i] + _tileAxis[i] * (Mathf.Sin(t * 7f + i * 0.9f) * 0.14f * amount);
        for (int i = 0; i < _flicker.Count; i++)
        {
            if (_flicker[i] == null) continue;
            // Alarm: the lights turn red in waves and flicker, with short black-outs now and then.
            float red = Mathf.Clamp01(Mathf.Sin(t * 6f + i * 0.8f) * 2f + 0.5f) * amount;
            float n = Mathf.PerlinNoise(t * 9f, i * 7.3f);
            float k = n < 0.2f ? 0.1f : Mathf.Lerp(0.55f, 1.25f, n);
            _flicker[i].color = Color.Lerp(_flickerColor[i], AlarmRed, red);
            _flicker[i].intensity = _flickerBase[i] * Mathf.Lerp(1f, k, amount);
        }
    }

    void StopTremor()
    {
        AlarmText = false;
        EventShake = 0f;
        ElfToy.Jolt = 0f;
        for (int i = 0; i < _tiles.Count; i++) if (_tiles[i] != null) _tiles[i].localPosition = _tileRest[i];
        for (int i = 0; i < _flicker.Count; i++)
            if (_flicker[i] != null) { _flicker[i].intensity = _flickerBase[i]; _flicker[i].color = _flickerColor[i]; }
        _tiles.Clear();
        _flicker.Clear();
        _flickerBase.Clear();
        _flickerColor.Clear();
    }
}
