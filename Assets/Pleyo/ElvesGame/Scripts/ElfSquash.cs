using UnityEngine;

/// <summary>Cartoon squash / stretch of a transform with a springy rebound, its bottom kept in place: a machine when an
/// elf lands on it (amount &gt; 0: squashed first), the toy spawner when a box comes out (amount &lt; 0: stretched first).</summary>
public class ElfSquash
{
    readonly Transform _target;
    readonly Vector3 _scale, _position;
    readonly Quaternion _rotation;
    float _shake;
    bool _moved;
    readonly float _bottom, _amount;
    float _k;

    public ElfSquash(Transform target, float amount)
    {
        _target = target;
        _amount = amount;
        _scale = target.localScale;
        _position = target.localPosition;
        _rotation = target.localRotation;
        Bounds b = ElfArt.GetBounds(target.gameObject);
        Vector3 bottom = new Vector3(b.center.x, b.min.y, b.center.z);
        _bottom = target.parent != null ? target.parent.InverseTransformPoint(bottom).y : bottom.y;
    }

    public void Punch() { _k = 1f; }

    /// <summary>0 to 1: strong shaking and wobbling (machine malfunction); 0 stops it.</summary>
    public void SetShake(float amount) { _shake = amount; }

    public void Tick(float deltaTime)
    {
        if (_target == null || (_k <= 0f && _shake <= 0f && !_moved)) return;
        _k = Mathf.MoveTowards(_k, 0f, deltaTime * 3.5f);
        float s = _amount * _k * Mathf.Cos((1f - _k) * Mathf.PI * 2.5f);
        float sy = 1f - s, wide = 1f + s * 0.6f;
        _target.localScale = new Vector3(_scale.x * wide, _scale.y * sy, _scale.z * wide);
        Vector3 position = new Vector3(_position.x, _position.y + (_bottom - _position.y) * (1f - sy), _position.z);
        Quaternion rotation = _rotation;
        if (_shake > 0f)
        {
            // Jitter and wobble around the rest pose.
            position += new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 0.6f), Random.Range(-1f, 1f)) * (0.08f * _shake);
            rotation = _rotation * Quaternion.Euler(Mathf.Sin(Time.time * 41f) * 4f * _shake, Mathf.Sin(Time.time * 29f) * 5f * _shake, Mathf.Sin(Time.time * 37f) * 4f * _shake);
        }
        _target.localPosition = position;
        _target.localRotation = rotation;
        _moved = _k > 0f || _shake > 0f;
    }
}
