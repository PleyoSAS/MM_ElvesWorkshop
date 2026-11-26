using UnityEngine;

/// <summary>One sound effect of the game: the clip, its own volume and a small random pitch change.</summary>
[System.Serializable]
public class ElfSound
{
    public AudioClip Clip;
    [Range(0f, 1f)] public float Volume = 0.8f;
    [Tooltip("Random pitch change at each play (0 = always the same).")]
    [Range(0f, 0.3f)] public float PitchVariation = 0.05f;
}

/// <summary>Every sound effect of the game, by action (ElfGame > Sounds).</summary>
[System.Serializable]
public class ElfSoundSet
{
    [Header("Players")]
    public ElfSound Jump = new ElfSound();
    public ElfSound Land = new ElfSound();
    [Tooltip("A player jumps for the first time (joins the line).")]
    public ElfSound Join = new ElfSound();
    [Header("Machines")]
    public ElfSound Carve = new ElfSound();
    public ElfSound Paint = new ElfSound();
    public ElfSound Wrap = new ElfSound();
    public ElfSound ClawGrab = new ElfSound();
    [Tooltip("Ring / terraces: the claw throws the gift onto the socle.")]
    public ElfSound ClawThrow = new ElfSound();
    [Tooltip("A box comes out of the spawner machine.")]
    public ElfSound Spawn = new ElfSound();
    [Tooltip("A toy passed a machine without being transformed.")]
    public ElfSound Missed = new ElfSound();
    [Header("Gifts")]
    public ElfSound GiftDelivered = new ElfSound();
    public ElfSound UnfinishedLoaded = new ElfSound();
    [Header("Game flow")]
    public ElfSound Splash = new ElfSound();
    [Tooltip("Each number of the countdowns (start and last 3 seconds).")]
    public ElfSound Countdown = new ElfSound();
    public ElfSound Go = new ElfSound();
    public ElfSound TimeUp = new ElfSound();
    [Tooltip("The ceremony / podium appears.")]
    public ElfSound Ceremony = new ElfSound();
    [Tooltip("The circle closing the game.")]
    public ElfSound Iris = new ElfSound();
    [Header("Spawner malfunction event")]
    public ElfSound MalfunctionAlarm = new ElfSound();
    [Tooltip("Looped during the whole malfunction.")]
    public ElfSound MalfunctionLoop = new ElfSound();
    public ElfSound Spark = new ElfSound();
}

/// <summary>Sound effects player: a small pool of 2D sources (several sounds at once, each with its own pitch) and
/// one looping source.</summary>
public partial class ElfGame
{
    [Header("Sounds (SFX)")]
    [SerializeField, Range(0f, 1f)] float _sfxVolume = 1f;
    [SerializeField] ElfSoundSet _sounds = new ElfSoundSet();

    public ElfSoundSet Sounds => _sounds;
    bool _splashSound;
    int _lastTick = -1;

    /// <summary>Countdown sound, once per number.</summary>
    void CountdownTick(int number)
    {
        if (number == _lastTick) return;
        _lastTick = number;
        PlaySfx(_sounds.Countdown);
    }
    AudioSource[] _sfxPool;
    AudioSource _sfxLoop;
    int _sfxNext;

    public void PlaySfx(ElfSound sound)
    {
        if (sound == null || sound.Clip == null || _sfxVolume <= 0f) return;
        if (_sfxPool == null)
        {
            _sfxPool = new AudioSource[12];
            for (int i = 0; i < _sfxPool.Length; i++) _sfxPool[i] = NewSource(false);
        }
        AudioSource source = _sfxPool[_sfxNext];
        _sfxNext = (_sfxNext + 1) % _sfxPool.Length;
        source.pitch = 1f + Random.Range(-sound.PitchVariation, sound.PitchVariation);
        source.PlayOneShot(sound.Clip, sound.Volume * _sfxVolume);
    }

    /// <summary>Starts / stops a looping sound (one at a time).</summary>
    void SetSfxLoop(ElfSound sound, bool on)
    {
        if (!on) { if (_sfxLoop != null) _sfxLoop.Stop(); return; }
        if (sound == null || sound.Clip == null) return;
        if (_sfxLoop == null) _sfxLoop = NewSource(true);
        _sfxLoop.clip = sound.Clip;
        _sfxLoop.volume = sound.Volume * _sfxVolume;
        if (!_sfxLoop.isPlaying) _sfxLoop.Play();
    }

    AudioSource NewSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }
}
