using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Decor that blocks a camera's view of its part of the line (walls, objects on them, presents...) is hidden for
/// that camera only (Renderer.forceRenderingOff while it renders): the other cameras still see it, so no hole appears
/// on the other screens. An object is an occluder of a camera when it cuts one of the rays from the camera to the
/// gameplay points of its screen (belt, machines, elves, sleighs).</summary>
public class ElfOccluders
{
    readonly Dictionary<Camera, HashSet<Renderer>> _hidden = new Dictionary<Camera, HashSet<Renderer>>();
    readonly HashSet<Renderer> _all = new HashSet<Renderer>();
    bool _hooked;

    /// <summary>Finds the occluders of cam among candidates (decor renderers) for the given gameplay points.</summary>
    public int Build(Camera cam, List<Renderer> candidates, List<Vector3> targets)
    {
        HashSet<Renderer> set = new HashSet<Renderer>();
        Vector3 eye = cam.transform.position;
        foreach (Renderer r in candidates)
        {
            if (r == null) continue;
            Bounds b = r.bounds;
            foreach (Vector3 target in targets)
            {
                Vector3 to = target - eye;
                float distance = to.magnitude;
                // Cuts the ray in the first 70% of the way (what stands on or near the line stays).
                if (b.IntersectRay(new Ray(eye, to / distance), out float hit) && hit < distance * 0.7f) { set.Add(r); break; }
            }
        }
        _hidden[cam] = set;
        foreach (Renderer r in set) _all.Add(r);
        if (!_hooked)
        {
            _hooked = true;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }
        return set.Count;
    }

    /// <summary>Every camera now shows the same view as source (podium): same hidden set.</summary>
    public void CopyTo(Camera source, IEnumerable<Camera> cameras)
    {
        if (!_hidden.TryGetValue(source, out HashSet<Renderer> set)) return;
        foreach (Camera cam in cameras) _hidden[cam] = set;
    }

    public void Dispose()
    {
        if (_hooked) RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        _hooked = false;
        foreach (Renderer r in _all) if (r != null) r.forceRenderingOff = false;
    }

    void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        _hidden.TryGetValue(cam, out HashSet<Renderer> set); // other cameras (Scene view...): everything shown
        foreach (Renderer r in _all)
            if (r != null) r.forceRenderingOff = set != null && set.Contains(r);
    }
}
