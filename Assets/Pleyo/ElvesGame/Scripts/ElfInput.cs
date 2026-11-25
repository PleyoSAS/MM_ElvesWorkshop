using System;
using UnityEngine;

/// <summary>Single entry point for jumps / landings whatever the input method (MM sensors, recordings, Kinect via
/// MyMMPlayer, touch and development keyboard via ElfGame). Argument: trampolineId (0..11 = letters A..L).</summary>
public static class ElfInput
{
    public static event Action<int> OnPlayerJump;
    public static event Action<int, float> OnPlayerLand;

    public static void TriggerJump(int trampolineId) { OnPlayerJump?.Invoke(trampolineId); }
    public static void TriggerLand(int trampolineId, float jumpStrength01) { OnPlayerLand?.Invoke(trampolineId, jumpStrength01); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { OnPlayerJump = null; OnPlayerLand = null; }
}
