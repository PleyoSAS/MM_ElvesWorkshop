using System.Collections.Generic;
using UnityEngine;

/// <summary>End of the game (after the 3-second countdown of the play): "Time's up!" pause, fade to white, the line
/// (belt, machines, sleighs, boards, toys) is swapped for a podium of the teams in the middle of screen 1, which every
/// screen then shows; a round gauge counts PODIUM_SECONDS down, then an iris closes the screens and the game quits.</summary>
public partial class ElfGame
{
    const float PODIUM_SCALE = 1.5f, END_PAUSE = 1.6f, END_WHITE_IN = 0.5f, END_WHITE_OUT = 0.6f, PODIUM_SECONDS = 8f, IRIS_SECONDS = 1.3f;
    static readonly float[] PODIUM_HEIGHTS = { 2f, 1.4f, 0.9f, 0.5f };
    static readonly float[] PODIUM_X = { 0f, -3.3f, 3.3f, 6.6f }; // 1st in the middle, 2nd left, 3rd right, 4th
    static readonly Color[] PODIUM_COLORS = { new Color(1f, 0.78f, 0.15f), new Color(0.78f, 0.8f, 0.85f), new Color(0.8f, 0.5f, 0.25f), new Color(0.55f, 0.36f, 0.2f) };

    float _endTime, _nextConfetti;
    bool _podiumBuilt;
    readonly List<Vector3> _podiumSpots = new List<Vector3>();
    readonly List<Color> _podiumColors = new List<Color>();

    void UpdateEnding(float dt, out string message, out bool big)
    {
        message = null;
        big = false;
        if (!_celebrated)
        {
            _celebrated = true;
            for (int i = 0; i < _stations.Count; i++) _stations[i].Celebrate();
        }
        _endTime += dt;
        float t = _endTime, podiumStart = END_PAUSE + END_WHITE_IN;
        float white = 0f, iris = 1f, gauge = -1f;
        if (t < END_PAUSE) { message = ElfLoc.Get("TIME_UP"); big = true; }
        else if (t < podiumStart) white = (t - END_PAUSE) / END_WHITE_IN;
        else
        {
            if (!_podiumBuilt) BuildPodium();
            float p = t - podiumStart;
            white = 1f - p / END_WHITE_OUT;
            gauge = Mathf.Clamp01(1f - p / PODIUM_SECONDS);
            if (p >= PODIUM_SECONDS) iris = 1f - (p - PODIUM_SECONDS) / IRIS_SECONDS;
            else if (Time.time >= _nextConfetti && _podiumSpots.Count > 0)
            {
                // Star bursts over the podium, in the teams' colors.
                _nextConfetti = Time.time + 0.3f;
                int k = Random.Range(0, _podiumSpots.Count);
                ElfArt.Sparkle(_podiumSpots[k] + Random.insideUnitSphere * 1.2f, _podiumColors[k]);
            }
            if (p >= PODIUM_SECONDS + IRIS_SECONDS + 0.4f) QuitGame();
        }
        for (int i = 0; i < _huds.Count; i++)
        {
            _huds[i].SetWhite(Mathf.Clamp01(white));
            _huds[i].SetIris(Mathf.Clamp01(iris));
            _huds[i].SetPodiumGauge(gauge, Mathf.CeilToInt(gauge * PODIUM_SECONDS));
        }
    }

    /// <summary>Podium (built under the white screen): one step per team with players, most toys first (ties share the
    /// rank), its elves on it celebrating, team name and score above them; the line is hidden.</summary>
    void BuildPodium()
    {
        _podiumBuilt = true;
        List<ElfTeam> ranked = _teams.FindAll(team => team.HasPlayers);
        if (ranked.Count == 0) ranked = new List<ElfTeam>(_teams);
        ranked.Sort((a, b) => b.Processed.CompareTo(a.Processed));

        Pose centre = ElfPath.At(_viewS.Count > 0 ? _viewS[0] : _screenWidth * 0.5f); // where screen 1's camera aims
        int stepCount = Mathf.Min(ranked.Count, PODIUM_X.Length);
        float minX = 0f, maxX = 0f;
        for (int i = 0; i < stepCount; i++) { minX = Mathf.Min(minX, PODIUM_X[i]); maxX = Mathf.Max(maxX, PODIUM_X[i]); }
        float shift = -(minX + maxX) * 0.5f; // the steps centred in the view
        Transform podium = new GameObject("Podium").transform;
        podium.SetPositionAndRotation(centre.position, centre.rotation);
        podium.localScale = Vector3.one * PODIUM_SCALE; // seen from the game camera of screen 1
        float elfHeight = _assets != null ? _assets.ElfHeight : 3.2f;
        int rank = 0;
        for (int i = 0; i < ranked.Count && i < PODIUM_X.Length; i++)
        {
            ElfTeam team = ranked[i];
            if (i > 0 && team.Processed < ranked[i - 1].Processed) rank = i;
            float h = PODIUM_HEIGHTS[rank];
            Transform step = new GameObject("Step" + (i + 1)).transform;
            step.SetParent(podium, false);
            step.localPosition = new Vector3(PODIUM_X[i] + shift, 0f, 0f);
            ElfArt.Prim(PrimitiveType.Cube, step, new Vector3(0f, h * 0.5f, 0f), new Vector3(3.1f, h, 2.4f), PODIUM_COLORS[rank], "Block");
            ElfArt.Label(step, new Vector3(0f, h * 0.5f, -1.25f), (rank + 1).ToString(), 14f, Color.white);

            // The team's elves (the players who played; all of them when nobody did) move onto their step.
            List<ElfAvatar> elves = new List<ElfAvatar>();
            Color teamColor = PODIUM_COLORS[rank];
            foreach (ElfStation machine in team.Machines)
                for (int m = 0; m < machine.Avatars.Count; m++)
                    if (machine.Avatars[m].Visible) { elves.Add(machine.Avatars[m]); teamColor = GetPlayerColor(machine.Members[m]); }
            if (elves.Count == 0)
                foreach (ElfStation machine in team.Machines) elves.AddRange(machine.Avatars);
            float spacing = Mathf.Min(1f, 2.9f / Mathf.Max(1, elves.Count));
            for (int e = 0; e < elves.Count; e++)
            {
                ElfAvatar elf = elves[e];
                elf.SetActive(true);
                elf.Root.SetParent(step, false);
                elf.Root.localPosition = new Vector3((e - (elves.Count - 1) * 0.5f) * spacing, h, 0f);
                elf.Root.localRotation = Quaternion.identity;
                elf.Root.localScale = Vector3.one * 0.6f;
                elf.Celebrate();
            }
            ElfArt.Label(step, new Vector3(0f, h + elfHeight * 0.6f + 2.1f, 0f), $"{team.Name.Substring(team.Name.IndexOf(". ") + 1).Trim()}\n<size=150%>{team.Processed}</size>", 5f, Color.white);
            _podiumSpots.Add(step.position + centre.rotation * new Vector3(0f, (h + 2.5f) * PODIUM_SCALE, 0f));
            _podiumColors.Add(teamColor);
        }
        Light light = new GameObject("PodiumLight").AddComponent<Light>();
        light.transform.SetParent(podium, false);
        light.transform.localPosition = new Vector3(0f, 7f, -4f);
        light.type = LightType.Point;
        light.color = new Color(1f, 0.9f, 0.75f);
        light.range = 18f;
        light.intensity = 20f;

        // The line goes away: machines, claws and sleighs, boards, belt, spawner, bin, toys.
        foreach (ElfStation station in _stations) if (station.Root != null) station.Root.gameObject.SetActive(false);
        if (_boardsRoot != null) _boardsRoot.gameObject.SetActive(false);
        if (_bin != null) _bin.gameObject.SetActive(false);
        if (_workshop != null)
        {
            if (_workshop.Belt != null) _workshop.Belt.gameObject.SetActive(false);
            foreach (string name in new[] { "SpawnerMachine", "SpawnerPivot" })
            {
                Transform spawner = _workshop.transform.Find(name);
                if (spawner != null) spawner.gameObject.SetActive(false);
            }
        }
        foreach (ElfToy toy in _toys) toy.Destroy();
        foreach (ElfToy toy in _binned) toy.Destroy();
        _toys.Clear();
        _binned.Clear();

        // Every screen shows the podium, through the game camera of screen 1 (its view is clear of the decor).
        if (_cameras.Count == 0) return;
        Vector3 viewPosition = _cameras[0].transform.position;
        Quaternion viewRotation = _cameras[0].transform.rotation;
        foreach (Camera cam in _cameras) cam.transform.SetPositionAndRotation(viewPosition, viewRotation);
        _occluders.CopyTo(_cameras[0], _cameras);
    }
}
