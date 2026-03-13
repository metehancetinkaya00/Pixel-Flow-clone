using UnityEngine;
using UnityEngine.InputSystem;

public class InputRaycaster : MonoBehaviour
{
    [SerializeField] private Camera cam;

    private void Reset()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        if (LevelUIManager.Instance != null && LevelUIManager.Instance.inputLocked)
        {
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            RaycastFromScreen(Mouse.current.position.ReadValue());
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            RaycastFromScreen(Touchscreen.current.primaryTouch.position.ReadValue());
        }
    }

    private void RaycastFromScreen(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 300f))
        {
            Shooter shooter = hit.collider.GetComponentInParent<Shooter>();
            if (shooter != null)
            {
                if (ShooterQueueManager.Instance != null)
                {
                    ShooterQueueManager.Instance.TryActivateShooter(shooter);
                }
            }
        }
    }
}