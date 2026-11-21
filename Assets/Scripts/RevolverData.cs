using UnityEngine;

[CreateAssetMenu(menuName = "Revolver/Revolver Data", fileName = "RevolverData")]
public class RevolverData : ScriptableObject
{
    [Header("Ammo")]
    public int magazineSize = 6;
    public float reloadTime = 2.5f;

    [Header("Shooting")]
    public float fireCooldown = 0.4f;
    public float damage = 25f;
    public float hitForce = 150f;
    public float range = 100f;

    [Header("Bullet")]
    public float bulletSpeed = 120f;
    public float bulletLifetime = 3f;

    [Header("Aim Zoom")]
    [Range(1f, 4f)] public float zoomAmount = 1.6f;
    public float zoomSpeed = 10f;

    [Header("Audio (random clip + random pitch/volume each play)")]
    public AudioClip[] shotClips;
    public AudioClip[] emptyClickClips;
    public AudioClip[] reloadClips;
    public AudioClip[] hitClips;
    public Vector2 pitchRange = new Vector2(0.92f, 1.08f);
    public Vector2 volumeRange = new Vector2(0.8f, 1f);
}