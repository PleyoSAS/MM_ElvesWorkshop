using System.Collections.Generic;

/// <summary>A team = one step of the line (Carve, Paint, Wrap, Load, or merged steps on 1 screen) and its players. Each
/// Carve / Paint / Wrap player has his own machine (2 players: lanes 0 / 1, each owning every other toy); the Load team
/// shares the claw. Team score = toys processed by its machines.</summary>
public class ElfTeam
{
    public int Steps;
    public readonly List<ElfStation> Machines = new List<ElfStation>();

    public string Name => Machines.Count > 0 ? Machines[0].Name : "";

    public int Processed
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < Machines.Count; i++) sum += Machines[i].Processed;
            return sum;
        }
    }

    public bool HasPlayers
    {
        get
        {
            for (int i = 0; i < Machines.Count; i++) if (Machines[i].Members.Count > 0) return true;
            return false;
        }
    }

    /// <summary>Whether a toy at x has passed every machine of this team (its last chance at this step is gone).</summary>
    public float LastMachineX
    {
        get
        {
            float x = float.MinValue;
            for (int i = 0; i < Machines.Count; i++) if (Machines[i].MachineX > x) x = Machines[i].MachineX;
            return x;
        }
    }
}
