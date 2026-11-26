using UnityEngine;

/// <summary>Scene marker (editor only): an elf-sized box on a ceremony spot, so the spots can be seen and moved.</summary>
public class ElfSpotGizmo : MonoBehaviour
{
    public Color Color = new Color(1f, 0.8f, 0.2f);
    [Tooltip("Team object: no box, a label line only.")]
    public bool Team;

    void OnDrawGizmos()
    {
        Gizmos.color = Color;
        if (Team)
        {
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 5.2f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 5.2f, 0.35f);
        }
        else
        {
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.6f, new Vector3(0.9f, 3.2f, 0.9f));
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * -0.8f); // faces the camera (-z)
        }
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * (Team ? 5.8f : 3.4f), name);
#endif
    }
}
