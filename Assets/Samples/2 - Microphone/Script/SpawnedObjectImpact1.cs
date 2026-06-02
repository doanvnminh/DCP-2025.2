using System.Collections.Generic;
using UnityEngine;

// Automatically added to every spawned object by ObjectSpawner.
// Maintains a global registry of every spawned object so impacts
// only ever affect OTHER spawned objects — never the ground or environment.
[RequireComponent(typeof(Rigidbody))]
public class SpawnedObjectImpact1 : MonoBehaviour
{
    // ── Global registry ───────────────────────────────────────────────────────
    // Every live SpawnedObjectImpact1 registers itself here on Awake and
    // removes itself on Destroy. Impact logic iterates this list directly,
    // so only spawned objects ever receive blast force.
    public static readonly List<SpawnedObjectImpact1> All = new List<SpawnedObjectImpact1>();

    // Set by ObjectSpawner right after instantiation.
    [HideInInspector] public float sizeInMeters = 1f;
    // Assigned by ObjectSpawner from the materialSounds array — may be null (silent).
    [HideInInspector] public AudioClip impactClip;

    private Rigidbody    _rb;
    private AudioSource  _audio;
    private bool _hasImpacted = false;

    void Awake()
    {
        _rb    = GetComponent<Rigidbody>();
        // 3-D positional audio so distant impacts sound further away.
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 1f;   // full 3D
        _audio.playOnAwake  = false;
        _audio.rolloffMode  = AudioRolloffMode.Linear;
        _audio.maxDistance  = 20f;
        All.Add(this);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    // Fires on the parent Rigidbody's GameObject even when child MeshColliders hit.
    void OnCollisionEnter(Collision collision)
    {
        if (_hasImpacted) return;

        float impulse = collision.impulse.magnitude;
        Debug.Log($"[IMPACT-HIT] '{gameObject.name}' collision impulse={impulse:F4}  mass={_rb.mass:F2}kg");

        // Scale threshold with mass so light objects (phone, cup) aren't silently ignored.
        float threshold = Mathf.Max(_rb.mass * 0.05f, 0.02f);
        if (impulse < threshold) return;

        _hasImpacted = true;

        float myMass = _rb.mass;
        // Fixed large radius — covers the whole ground plane (2.5 x 2.5 scale → ~3.6m diagonal).
        const float blastRadius = 6f;
        float blastForce = Mathf.Clamp(myMass * 0.1f, 1f, 40f);

        // ── Camera shake ──────────────────────────────────────────────────────
        float shakeStrength = Mathf.Clamp(myMass * 0.003f, 0.02f, 0.40f);
        float shakeDuration = Mathf.Clamp(myMass * 0.002f, 0.10f, 0.70f);
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(shakeStrength, shakeDuration);

        // ── Blast other spawned objects ───────────────────────────────────────
        // Same force for every object in range — no distance falloff.
        int blasted = 0;
        foreach (SpawnedObjectImpact1 other in All)
        {
            if (other == this) continue;
            if (other._rb == null) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist > blastRadius) continue;

            // Immunity: objects at least 80 % my mass don't move.
            if (myMass / other._rb.mass < 0.2f) continue;

            // Uniform force — every object in range gets the same push.
            other._rb.AddExplosionForce(
                blastForce,
                transform.position,
                blastRadius,
                upwardsModifier: 0.3f,
                ForceMode.Impulse
            );

            other._rb.linearVelocity = Vector3.ClampMagnitude(other._rb.linearVelocity, 7f);
            blasted++;
        }

        Debug.Log($"[IMPACT] '{gameObject.name}' FIRED | mass:{myMass:F1}kg " +
                  $"blastR:{blastRadius:F1}m force:{blastForce:F0}N " +
                  $"objectsInRegistry:{All.Count - 1} blasted:{blasted}");

        // ── Collision sound with acoustic pitch + volume modeling ─────────────
        // Pitch  : steeper curve (^0.6) so large objects rumble noticeably lower
        //          cup 0.1m→3.0×  cat 0.45m→1.6×  person 1.8m→0.70×  truck 3.5m→0.48×
        // Volume : larger objects hit harder — scales up with size (^0.4)
        //          cup 0.1m→0.40  cat 0.45m→0.73  person 1.8m→1.26  truck 3.5m→1.62
        if (impactClip != null && _audio != null)
        {
            float size   = Mathf.Max(sizeInMeters, 0.01f);
            float pitch  = Mathf.Clamp(Mathf.Pow(1f / size, 0.6f), 0.15f, 3.0f);
            float volume = Mathf.Clamp(Mathf.Pow(size, 0.4f), 0.3f, 2.0f);
            _audio.pitch = pitch;
            _audio.PlayOneShot(impactClip, volume);
            Debug.Log($"[SOUND] '{gameObject.name}' playing '{impactClip.name}' pitch={pitch:F2} vol={volume:F2}");
        }
    }
}
