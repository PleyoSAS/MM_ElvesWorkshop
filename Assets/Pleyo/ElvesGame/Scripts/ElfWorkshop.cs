using UnityEngine;

/// <summary>
/// Root of the workshop decor in GameScene (floor, walls, decorations, conveyor belt, toy spawner machine), editable in
/// the scene. Built once by the menu "Elves Workshop/Build Workshop In Open Scene" (ElvesBuilder) for 4 screens of
/// 20 m (x 0..80); ElfGame hides the belt tiles past the end of the line when fewer screens are used.
/// </summary>
public class ElfWorkshop : MonoBehaviour
{
    [Tooltip("Height of the belt surface (toys ride on it).")]
    public float BeltTopY = 1.2f;
    [Tooltip("Parent of the belt tiles (one per few meters, along +x).")]
    public Transform Belt;
    [Tooltip("Where the raw blocks come out of the spawner machine (x used).")]
    public Transform SpawnPoint;
    [Tooltip("Belt material scrolled to show the belt moving (instance made at runtime). Optional.")]
    public Material BeltMaterial;
    public Vector2 BeltScroll = new Vector2(0f, -0.25f);
    [Tooltip("Map shape (ElfPath): Straight = 1 screen; Ring / Terraces = 2-4 screens. ElfGame shows the matching root.")]
    public ElfPath.Layout Layout = ElfPath.Layout.Straight;
    [Tooltip("Curved maps: path distance of the spawn point and of each belt tile (child order), set by the builder.")]
    public float SpawnS = 0.4f;
    public float[] TileS;
}
