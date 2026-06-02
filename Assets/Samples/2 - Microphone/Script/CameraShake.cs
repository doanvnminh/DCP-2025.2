using System.Collections;
using UnityEngine;

// Attach this to your Main Camera.
// SpawnedObjectImpact calls CameraShake.Instance.Shake() automatically.
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    private Vector3 _originalLocalPos;
    private Coroutine _activeShake;

    void Awake()
    {
        Instance = this;
        _originalLocalPos = transform.localPosition;
    }

    // strength : max positional offset in Unity units
    // duration : seconds until shake fully decays to zero
    public void Shake(float strength, float duration)
    {
        if (_activeShake != null) StopCoroutine(_activeShake);
        _activeShake = StartCoroutine(ShakeRoutine(strength, duration));
    }

    private IEnumerator ShakeRoutine(float strength, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Ease-out: strongest at start, fully fades by end
            float damped = Mathf.Lerp(strength, 0f, t);
            transform.localPosition = _originalLocalPos + Random.insideUnitSphere * damped;
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = _originalLocalPos;
        _activeShake = null;
    }
}
