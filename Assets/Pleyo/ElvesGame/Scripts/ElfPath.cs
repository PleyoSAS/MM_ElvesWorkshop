using UnityEngine;

/// <summary>
/// Shape of the assembly line. The gameplay is one-dimensional: positions are distances s along the belt (toy X, machine
/// X, screen k = s in [k x 20, (k + 1) x 20)). At(s) turns a distance into the belt frame at floor level: position, and a
/// rotation whose local -z faces the camera of that screen (elves behind the belt at +z, boards in front at -z).
/// - Straight: the 1-screen map, along +x (identity rotation).
/// - Ring: 2-4 screens around a round hall (big tree in the middle); each screen = a quarter of the ring, cameras
///   outside looking in, so every team sees the others across the hall.
/// </summary>
public static class ElfPath
{
    public enum Layout { Straight, Ring }

    public const float SCREEN = 20f;
    /// <summary>Ring: 4 screens + 6 m for the bin and the spawner between the end and the start of the line.</summary>
    public static readonly float RingRadius = (4f * SCREEN + 6f) / (2f * Mathf.PI);
    public static readonly Vector3 RingCenter = new Vector3(0f, 0f, (4f * SCREEN + 6f) / (2f * Mathf.PI));

    public static Layout Current = Layout.Straight;
    /// <summary>Ring: where the path is in the world (ElfGame sets the map's Belt object pose, so moving the Belt in the
    /// scene moves the gameplay with it). Straight: ignored.</summary>
    public static Pose Origin = Pose.identity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Current = Layout.Straight; Origin = Pose.identity; }

    public static Pose At(float s) { return At(s, Current); }

    public static Pose At(float s, Layout layout)
    {
        if (layout == Layout.Straight) return new Pose(new Vector3(s, 0f, 0f), Quaternion.identity);
        float a = (s - SCREEN * 0.5f) / RingRadius; // screen 1 centre faces -z (south)
        Vector3 p = RingCenter + RingRadius * new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
        Quaternion rotation = Quaternion.LookRotation((RingCenter - p).normalized, Vector3.up);
        return new Pose(Origin.position + Origin.rotation * p, Origin.rotation * rotation);
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
}
