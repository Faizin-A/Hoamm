using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    [Header("Flashlight Settings")]
    public Light flashlight;

    [Header("Input Settings")]
    public KeyCode toggleKey = KeyCode.F;

    private bool isOn = true;

    void Start()
    {
        if (flashlight == null)
        {
            Debug.LogError(
                "Flashlight belum dihubungkan!"
            );

            enabled = false;
            return;
        }

        flashlight.enabled = isOn;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }
    }

    void ToggleFlashlight()
    {
        isOn = !isOn;

        flashlight.enabled = isOn;

        Debug.Log(
            isOn
            ? "[FLASHLIGHT] Senter dinyalakan."
            : "[FLASHLIGHT] Senter dimatikan."
        );
    }
}