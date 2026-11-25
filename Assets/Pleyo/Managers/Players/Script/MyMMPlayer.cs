using UnityEngine;

/// <summary>
/// Game player, one per trampoline. Forwards take-off and landing to ElfInput (same path for sonic sensors,
/// recordings and the Kinect).
/// </summary>
public class MyMMPlayer : PleyoMMPlayer
{
    public override void OnPlayerJumpThisFrame()
    {
        base.OnPlayerJumpThisFrame();
        ElfInput.TriggerJump(PleyoMMSensors.PlayerId_to_TrampolineId(PlayerId));
    }

    public override void OnPlayerLandThisFrame(float currentPlayerXPosition, float jumpStrength01)
    {
        base.OnPlayerLandThisFrame(currentPlayerXPosition, jumpStrength01);
        ElfInput.TriggerLand(PleyoMMSensors.PlayerId_to_TrampolineId(PlayerId), jumpStrength01);
    }
}
