using UnityEngine;

/// <summary>
/// Shape of the assembly line. The gameplay is one-dimensional: positions are distances s along the belt (toy X, machine
/// X, screen k = s in [k x 20, (k + 1) x 20)). At(s) turns a distance into the belt frame at floor level: position, and a
/// rotation whose local -z faces the camera of that screen (elves behind the belt at +z, boards in front at -z).
/// - Straight: the 1-screen map, along +x (identity rotation).
/// - Ring: 2-4 screens around a round hall (big tree in the middle); each screen = a quarter of the ring, cameras
///   outside looking in, so every team sees the others across the hall.
/// - Terraces: one row per screen, each row higher and further back, alternately left-to-right and right-to-left; a
///   ramp at the end of a row lifts the toys to the next one; each camera looks over the rows in front of it.
/// </summary>
public static class ElfPath
{
    public enum Layout { Straight, Ring, Terraces }

    public const float SCREEN = 20f;
    /// <summary>Ring: 4 screens + 6 m for the bin and the spawner between the end and the start of the line.</summary>
    public static readonly float RingRadius = (4f * SCREEN + 6f) / (2f * Mathf.PI);
    public static readonly Vector3 RingCenter = new Vector3(0f, 0f, (4f * SCREEN + 6f) / (2f * Mathf.PI));
    public const float TERRACE_RISE = 6f, TERRACE_DEPTH = 16f; // deep enough for the camera of a row to stand in front of it, behind the previous row

    public static Layout Current = Layout.Straight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Current = Layout.Straight; }

    public static Pose At(float s) { return At(s, Current); }

    public static Pose At(float s, Layout layout)
    {
        switch (layout)
        {
            case Layout.Ring:
            {
                float a = (s - SCREEN * 0.5f) / RingRadius; // screen 1 centre faces -z (south)
                Vector3 p = RingCenter + RingRadius * new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
                Vector3 inward = (RingCenter - p).normalized;
                return new Pose(p, Quaternion.LookRotation(inward, Vector3.up));
            }
            case Layout.Terraces:
            {
                int k = Mathf.Clamp(Mathf.FloorToInt(s / SCREEN), 0, 3);
                float u = s - k * SCREEN;
                // Ramp between row k and k + 1: the last metre of a row and the first of the next.
                if (u >= SCREEN - 1f && k < 3) return Ramp(k, (u - (SCREEN - 1f)) / 2f);
                if (u < 1f && k > 0) return Ramp(k - 1, (u + 1f) / 2f);
                return new Pose(RowPoint(k, u), Quaternion.identity);
            }
            default:
                return new Pose(new Vector3(s, 0f, 0f), Quaternion.identity);
        }
    }

    /// <summary>Direction the toys travel at s (world).</summary>
    public static Vector3 Tangent(float s)
    {
        Vector3 d = At(s + 0.05f).position - At(s - 0.05f).position;
        return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right;
    }

    /// <summary>World point of a local offset in the belt frame at s (x along the belt is ignored: use s).</summary>
    public static Vector3 Point(float s, Vector3 local)
    {
        Pose pose = At(s);
        return pose.position + pose.rotation * local;
    }

    static Vector3 RowPoint(int k, float u)
    {
        float x = k % 2 == 0 ? u : SCREEN - u;
        return new Vector3(x, k * TERRACE_RISE, k * TERRACE_DEPTH);
    }

    static Pose Ramp(int k, float t)
    {
        Vector3 a = RowPoint(k, SCREEN - 1f), b = RowPoint(k + 1, 1f);
        Vector3 p = Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1f;
        return new Pose(p, Quaternion.identity);
    }
}
