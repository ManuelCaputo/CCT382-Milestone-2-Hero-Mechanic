using UnityEngine;

// Listens to the events and plays a random clip with random pitch/volume.
[RequireComponent(typeof(AudioSource))]
public class RevolverAudio : MonoBehaviour
{
    public RevolverData data;
    public VoidEventChannel shotFired;
    public VoidEventChannel emptyClick;
    public VoidEventChannel reloadStarted;
    public HitEventChannel bulletHit;

    AudioSource src;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
    }

    void OnEnable()
    {
        if (shotFired != null) shotFired.OnRaised += OnShot;
        if (emptyClick != null) emptyClick.OnRaised += OnEmpty;
        if (reloadStarted != null) reloadStarted.OnRaised += OnReload;
        if (bulletHit != null) bulletHit.OnRaised += OnHit;
    }

    void OnDisable()
    {
        if (shotFired != null) shotFired.OnRaised -= OnShot;
        if (emptyClick != null) emptyClick.OnRaised -= OnEmpty;
        if (reloadStarted != null) reloadStarted.OnRaised -= OnReload;
        if (bulletHit != null) bulletHit.OnRaised -= OnHit;
    }

    void OnShot() => Play(data.shotClips);
    void OnEmpty() => Play(data.emptyClickClips);
    void OnReload() => Play(data.reloadClips);
    void OnHit(Vector3 point, Vector3 normal) => Play(data.hitClips);

    void Play(AudioClip[] clips)
    {
        if (data == null || clips == null || clips.Length == 0) return;

        src.pitch = Random.Range(data.pitchRange.x, data.pitchRange.y);
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        src.PlayOneShot(clip, Random.Range(data.volumeRange.x, data.volumeRange.y));
    }
}