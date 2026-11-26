using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// - Gift pile (ring / terraces): the claws throw their gifts in an arc onto the socle in the middle of the hall
///   (_giftSocle), where every screen sees the pile grow; no sleighs then. 1 screen: the sleighs as before.
/// - Ghost line (countdown): the belt is filled with toys 75% transparent that the machines transform on their own, so
///   every machine has toys coming at the start; they become normal, playable toys at "Go!".
/// </summary>
public partial class ElfGame
{
    [Tooltip("Ring / terraces: socle in the middle of the hall where the claws throw the gifts (empty: sleighs).")]
    [SerializeField] Transform _giftSocle;

    const float PILE_CELL = 0.74f, PILE_LAYER_H = 0.66f; // a parked gift: 75% of the raw box size
    readonly List<Vector3> _pileSpots = new List<Vector3>();
    const float FLIGHT_SECONDS = 1f, FLIGHT_ARC = 5f;
    Transform _giftPile;
    float _pileRadius;
    int _pileCount;
    bool _prefilled, _lineLive;

    class Flight { public ElfToy Toy; public Vector3 From, Local; public float T; }
    readonly List<Flight> _flights = new List<Flight>();

    void SetupGiftPile()
    {
        if (ElfPath.Current == ElfPath.Layout.Straight || _giftSocle == null || !_giftSocle.gameObject.activeInHierarchy) return;
        Bounds b = ElfArt.GetBounds(_giftSocle.gameObject);
        _giftPile = new GameObject("GiftPile").transform;
        _giftPile.position = new Vector3(b.center.x, b.max.y, b.center.z);
        _pileRadius = Mathf.Max(0.5f, Mathf.Min(b.extents.x, b.extents.z) - 0.6f);
        BuildPileSpots();
        foreach (ElfStation station in _stations) if (station.IsLoader) station.HideSleighs();
    }

    /// <summary>Called by a claw when it drops a gift: false = no pile (the claw parks it in its sleigh).</summary>
    public bool ThrowToPile(ElfToy toy)
    {
        if (_giftPile == null) return false;
        // Next spot of the pile: a sunflower spiral per layer, layers stacked.
        // Next spot of the stack (grid cells, filled from the centre so the pile grows as a stepped pyramid).
        Vector3 local = _pileSpots.Count > 0 ? _pileSpots[Mathf.Min(_pileCount++, _pileSpots.Count - 1)] : Vector3.zero;
        _flights.Add(new Flight { Toy = toy, From = toy.Root.position, Local = local });
        PlaySfx(_sounds.ClawThrow);
        return true;
    }


    /// <summary>Stack spots: square cells on each layer, every other layer shifted by half a cell (each gift sits on
    /// four below it) and narrower; ordered so a spot comes after the cells that hold it.</summary>
    void BuildPileSpots()
    {
        List<KeyValuePair<float, Vector3>> spots = new List<KeyValuePair<float, Vector3>>();
        for (int layer = 0; layer < 12; layer++)
        {
            float r = _pileRadius - layer * PILE_CELL * 0.5f;
            if (r < PILE_CELL * 0.5f) break;
            float offset = (layer % 2) * PILE_CELL * 0.5f;
            int n = Mathf.CeilToInt(r / PILE_CELL) + 1;
            for (int gx = -n; gx <= n; gx++)
                for (int gz = -n; gz <= n; gz++)
                {
                    Vector3 p = new Vector3(gx * PILE_CELL + offset, layer * PILE_LAYER_H, gz * PILE_CELL + offset);
                    float d = new Vector2(p.x, p.z).magnitude;
                    if (d <= r) spots.Add(new KeyValuePair<float, Vector3>(d / PILE_CELL + layer * 1.5f, p));
                }
        }
        spots.Sort((a, b) => a.Key.CompareTo(b.Key));
        foreach (KeyValuePair<float, Vector3> s in spots) _pileSpots.Add(s.Value);
    }
    void TickFlights(float dt)
    {
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            Flight f = _flights[i];
            if (f.Toy.Root == null) { _flights.RemoveAt(i); continue; }
            f.T += dt / FLIGHT_SECONDS;
            float t = Mathf.Clamp01(f.T);
            Vector3 to = _giftPile.TransformPoint(f.Local);
            f.Toy.Root.position = Vector3.Lerp(f.From, to, t) + Vector3.up * (FLIGHT_ARC * 4f * t * (1f - t));
            f.Toy.Root.Rotate(0f, 540f * dt, 0f, Space.World);
            if (t < 1f) continue;
            _flights.RemoveAt(i);
            f.Toy.Park(_giftPile, f.Local, true);
            // Same height for every gift (a clean layer), never wider than its cell.
            Bounds gb = ElfArt.GetBounds(f.Toy.Root.gameObject);
            if (gb.size.y > 0.01f) f.Toy.Root.localScale *= Mathf.Min(PILE_LAYER_H / gb.size.y, (PILE_CELL - 0.04f) / Mathf.Max(0.01f, Mathf.Max(gb.size.x, gb.size.z)));
            LoadToy(f.Toy);
            ElfArt.Sparkle(to + Vector3.up * 0.5f, ElfArt.Gold);
        }
    }

    /// <summary>Countdown: fills the belt with ghost toys, each already transformed by the machines it has passed.</summary>
    void Prefill()
    {
        _prefilled = true;
        float spacing = 4f * GetEffectivePeriod() * SpawnFactor() * _beltSpeed;
        for (float x = _spawnX + spacing * 0.5f; x < _endX - 0.5f; x += spacing)
        {
            ElfToy toy = new ElfToy();
            toy.Init(_world, x, _beltY, _spawned++ % 2);
            toy.SetGhost(true);
            foreach (ElfStation station in _stations)
            {
                if (station.MachineX + WindowX >= x) continue;
                toy.HandledStations |= 1 << station.Index;
                int needed = station.Steps & ~ElfToy.LOAD;
                if (needed != 0) toy.Apply(needed, station.Members.Count == 1 ? GetPlayerColor(station.Members[0]) : (Color?)null);
            }
            _toys.Add(toy);
            RefreshOwner(toy);
        }
    }

    /// <summary>"Go!": the ghost toys become normal, playable toys.</summary>
    void GoLive()
    {
        _lineLive = true;
        foreach (ElfToy toy in _toys) toy.SetGhost(false);
    }
}
