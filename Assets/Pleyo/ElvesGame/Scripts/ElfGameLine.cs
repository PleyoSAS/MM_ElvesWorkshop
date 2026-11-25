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

    const int PILE_LAYER = 28;
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
        foreach (ElfStation station in _stations) if (station.IsLoader) station.HideSleighs();
    }

    /// <summary>Called by a claw when it drops a gift: false = no pile (the claw parks it in its sleigh).</summary>
    public bool ThrowToPile(ElfToy toy)
    {
        if (_giftPile == null) return false;
        // Next spot of the pile: a sunflower spiral per layer, layers stacked.
        int k = _pileCount++, layer = k / PILE_LAYER, j = k % PILE_LAYER;
        float r = _pileRadius * Mathf.Sqrt((j + 0.5f) / PILE_LAYER), angle = j * 2.39996f + layer * 0.7f;
        Vector3 local = new Vector3(Mathf.Cos(angle) * r, layer * 0.62f, Mathf.Sin(angle) * r);
        _flights.Add(new Flight { Toy = toy, From = toy.Root.position, Local = local });
        return true;
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
            f.Toy.Park(_giftPile, f.Local);
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
