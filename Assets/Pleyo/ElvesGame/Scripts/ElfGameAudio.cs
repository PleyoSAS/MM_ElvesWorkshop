using UnityEngine;

/// <summary>Music: the main music loops from the splash to the end of the play (faster during a spawner
/// malfunction), then crossfades to the end music at the white fade (no end music: the main one goes on).</summary>
public partial class ElfGame
{
    [Header("Music")]
    [SerializeField] AudioClip _mainMusic;
    [SerializeField] AudioClip _endMusic;
    [SerializeField, Range(0f, 1f)] float _musicVolume = 0.6f;
    [Tooltip("Main music speed during a spawner malfunction.")]
    [SerializeField] float _malfunctionMusicPitch = 1.2f;

    AudioSource _music, _musicEnd;

    void StartMusic()
    {
        _music = gameObject.AddComponent<AudioSource>();
        _music.clip = _mainMusic;
        _music.loop = true;
        _music.playOnAwake = false;
        _music.spatialBlend = 0f;
        _music.volume = _musicVolume;
        if (_mainMusic != null) _music.Play();
        _musicEnd = gameObject.AddComponent<AudioSource>();
        _musicEnd.clip = _endMusic;
        _musicEnd.loop = true;
        _musicEnd.playOnAwake = false;
        _musicEnd.spatialBlend = 0f;
        _musicEnd.volume = 0f;
    }

    /// <summary>ending: from the white fade of the end of the game.</summary>
    void TickMusic(float dt, bool ending)
    {
        if (_music == null) return;
        _music.pitch = Mathf.MoveTowards(_music.pitch, MalfunctionActive ? _malfunctionMusicPitch : 1f, dt * 0.6f);
        if (!ending || _endMusic == null) return;
        if (!_musicEnd.isPlaying) _musicEnd.Play();
        _music.volume = Mathf.MoveTowards(_music.volume, 0f, dt * _musicVolume);
        _musicEnd.volume = Mathf.MoveTowards(_musicEnd.volume, _musicVolume, dt * _musicVolume);
    }
}
