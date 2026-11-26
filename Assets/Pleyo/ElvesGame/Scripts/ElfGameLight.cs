using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Mood lighting, applied at start on the map in use (runtime only: the scene keeps its own settings):
/// 1. a narrow warm spot above each player (a wide one above a team with no player);
/// 2. a darker background: three-colour ambient, cool and dim up high, warm on the floor;
/// 3. glowing garlands (brighter emission + bloom);
/// 4. bluish moonlight from the windows (glowing glass and sky, small blue lights inside);
/// 5. vignette and a light haze in the distance.</summary>
public partial class ElfGame
{
    [Header("Lighting (mood, at runtime)")]
    [SerializeField] bool _moodLighting = true;
    [SerializeField] float _teamSpotIntensity = 10f;

    [SerializeField] float _bloomIntensity = 1.1f;
    [SerializeField] float _vignetteIntensity = 0.32f;
    [SerializeField] float _hazeEnd = 90f;

    static readonly Color Moon = new Color(0.5f, 0.62f, 1f);
    const int MAX_MOON_LIGHTS = 12;


    /// <summary>Light of a player's elf (ElfGame > Player light): spot or point light, carried by the elf (it follows
    /// him to the ceremony). Offset in the elf's frame: x right, y up, z backward (negative = toward the camera).</summary>
    [System.Serializable]
    public class ElfLightSettings
    {
        [Tooltip("Per-player entry: use it instead of the common settings.")]
        public bool Use;
        public LightType Type = LightType.Spot;
        public Color Color = new Color(1f, 0.93f, 0.8f);
        public float Intensity = 32f;
        public float Range = 16f;
        [Range(1f, 179f)] public float SpotAngle = 38f;
        [Range(1f, 179f)] public float InnerSpotAngle = 24f;
        public Vector3 Offset = new Vector3(0f, 9f, -3f);
        [Tooltip("Spot: aimed at this height on the elf.")]
        public float AimHeight = 1.6f;
        public bool Shadows;
    }

    [Header("Player light (one per elf, applied live)")]
    [SerializeField] ElfLightSettings _playerLight = new ElfLightSettings { Use = true };
    [Tooltip("Optional per player, by letter order (A, B, C...): tick Use to replace the common settings for that player.")]
    [SerializeField] ElfLightSettings[] _playerLightOverrides = new ElfLightSettings[0];

    readonly System.Collections.Generic.List<KeyValuePair<ElfAvatar, Light>> _playerLights = new System.Collections.Generic.List<KeyValuePair<ElfAvatar, Light>>();
    readonly System.Collections.Generic.List<int> _playerLightIds = new System.Collections.Generic.List<int>();

    void BuildPlayerLights()
    {
        foreach (ElfStation station in _stations)
            for (int m = 0; m < station.Avatars.Count; m++)
            {
                Light light = new GameObject("PlayerLight").AddComponent<Light>();
                light.transform.SetParent(station.Avatars[m].Root, false);
                _playerLights.Add(new KeyValuePair<ElfAvatar, Light>(station.Avatars[m], light));
                _playerLightIds.Add(station.Members[m]);
            }
        UpdatePlayerLights();
    }

    /// <summary>Every frame: the settings can be tuned in Play mode.</summary>
    void UpdatePlayerLights()
    {
        for (int i = 0; i < _playerLights.Count; i++)
        {
            Light light = _playerLights[i].Value;
            if (light == null) continue;
            int id = _playerLightIds[i];
            ElfLightSettings s = id >= 0 && id < _playerLightOverrides.Length && _playerLightOverrides[id] != null && _playerLightOverrides[id].Use ? _playerLightOverrides[id] : _playerLight;
            Transform elf = _playerLights[i].Key.Root;
            light.type = s.Type;
            light.color = s.Color;
            light.intensity = s.Intensity;
            light.range = s.Range;
            light.spotAngle = s.SpotAngle;
            light.innerSpotAngle = Mathf.Min(s.InnerSpotAngle, s.SpotAngle);
            light.shadows = s.Shadows ? LightShadows.Soft : LightShadows.None;
            light.transform.position = elf.position + elf.rotation * s.Offset;
            if (s.Type == LightType.Spot) light.transform.LookAt(elf.position + Vector3.up * s.AimHeight);
        }
    }
    void ApplyLighting()
    {
        if (!_moodLighting || _workshop == null) return;
        Transform root = new GameObject("MoodLighting").transform;
        root.SetParent(_world, false);

        // 2. Darker, cooler background: three-colour ambient instead of a flat one.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.24f, 0.26f, 0.38f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.38f, 0.34f);
        RenderSettings.ambientGroundColor = new Color(0.55f, 0.4f, 0.3f);

        // 1. A light on each player (ElfGame > Player light, live), and a wide spot above each team with no player.
        BuildPlayerLights();
        foreach (ElfTeam team in _teams)
        {
            if (team.Machines.Count == 0 || team.HasPlayers) continue;
            float cx = 0f;
            foreach (ElfStation machine in team.Machines) cx += machine.MachineX;
            cx /= team.Machines.Count;
            Pose pose = ElfPath.At(cx);
            Light spot = new GameObject("TeamSpot").AddComponent<Light>();
            spot.transform.SetParent(root, true);
            spot.transform.position = pose.position + pose.rotation * new Vector3(0f, 12f, -5f);
            spot.transform.LookAt(pose.position + pose.rotation * new Vector3(0f, _beltY + 1.5f, 0.5f));
            spot.type = LightType.Spot;
            spot.spotAngle = 60f;
            spot.innerSpotAngle = 35f;
            spot.range = 25f;
            spot.intensity = _teamSpotIntensity;
            spot.color = new Color(1f, 0.88f, 0.7f);
            spot.shadows = LightShadows.None;
        }

        // 3 / 4. Glowing garlands, moonlit windows and sky (material copies on the map's renderers).
        Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        Vector3 hall = Vector3.zero;
        foreach (ElfStation station in _stations) hall += station.Root.position;
        hall /= Mathf.Max(1, _stations.Count);
        int moonLights = 0;
        foreach (Renderer r in _workshop.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false, window = false;
            for (int m = 0; m < mats.Length; m++)
            {
                Material source = mats[m];
                if (source == null) continue;
                Color? emission = source.name.StartsWith("Garland") ? source.GetColor("_EmissionColor") * 4f
                    : source.name.StartsWith("Glass") || source.name.StartsWith("Outside") ? Moon * 1.6f : (Color?)null;
                if (emission == null) continue;
                window |= source.name.StartsWith("Glass");
                if (!copies.TryGetValue(source, out Material copy))
                {
                    copy = new Material(source) { name = source.name + " (mood)" };
                    copy.EnableKeyword("_EMISSION");
                    copy.SetColor("_EmissionColor", emission.Value);
                    if (!source.name.StartsWith("Garland") && copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", Color.Lerp(copy.GetColor("_BaseColor"), Moon, 0.5f));
                    copies[source] = copy;
                }
                mats[m] = copy;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
            // 4. A small blue light inside, in front of each window (a dozen at most).
            if (window && moonLights < MAX_MOON_LIGHTS)
            {
                moonLights++;
                Vector3 c = r.bounds.center;
                Vector3 inward = hall - c;
                inward.y = 0f;
                Light moon = new GameObject("MoonLight").AddComponent<Light>();
                moon.transform.SetParent(root, true);
                moon.transform.position = c + inward.normalized * 1.5f;
                moon.type = LightType.Point;
                moon.color = Moon;
                moon.range = 7f;
                moon.intensity = 2.5f;
                moon.shadows = LightShadows.None;
            }
        }

        // 3 / 5. Bloom on what glows, vignette (over the scene's own global volume).
        Volume volume = root.gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1f); // the white HUD cards (1.0) do not bloom
        bloom.intensity.Override(_bloomIntensity);
        bloom.scatter.Override(0.65f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(_vignetteIntensity);
        vignette.smoothness.Override(0.45f);
        volume.profile = profile;

        // 5. Light haze in the distance (the far side of the hall).
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.22f, 0.1f, 0.09f);
        RenderSettings.fogStartDistance = 35f;
        RenderSettings.fogEndDistance = _hazeEnd;
    }
}
