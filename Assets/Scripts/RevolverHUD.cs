using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RevolverHUD : MonoBehaviour
{
    public Revolver revolver;
    public RevolverData data;
    public IntEventChannel ammoChanged;
    public VoidEventChannel reloadStarted;
    public VoidEventChannel reloadFinished;

    public TMP_Text ammoText;
    [Tooltip("Image with Image Type = Filled, Fill Method = Horizontal.")]
    public Image reloadFill;

    int lastAmmo;
    float reloadTimer = -1f;

    void OnEnable()
    {
        if (ammoChanged != null) ammoChanged.OnRaised += OnAmmoChanged;
        if (reloadStarted != null) reloadStarted.OnRaised += BeginReload;
        if (reloadFinished != null) reloadFinished.OnRaised += EndReload;
    }

    void OnDisable()
    {
        if (ammoChanged != null) ammoChanged.OnRaised -= OnAmmoChanged;
        if (reloadStarted != null) reloadStarted.OnRaised -= BeginReload;
        if (reloadFinished != null) reloadFinished.OnRaised -= EndReload;
    }

    void OnAmmoChanged(int ammo) => lastAmmo = ammo;

    void BeginReload() => reloadTimer = 0f;

    void EndReload()
    {
        reloadTimer = -1f;
        if (reloadFill != null) reloadFill.fillAmount = 0f;
    }

    void Update()
    {
        if (ammoText == null || revolver == null || data == null) return;

        if (revolver.Riding)
            ammoText.text = "Unlimited";
        else if (reloadTimer >= 0f)
            ammoText.text = "Reloading...";
        else
            ammoText.text = $"{lastAmmo} / {data.magazineSize}";

        if (reloadTimer >= 0f)
        {
            reloadTimer += Time.deltaTime;
            if (reloadFill != null)
                reloadFill.fillAmount = Mathf.Clamp01(reloadTimer / data.reloadTime);
        }
    }
}