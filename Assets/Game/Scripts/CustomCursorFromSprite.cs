using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Vector2 hotspot = Vector2.zero;
    [SerializeField] private CursorMode cursorMode = CursorMode.ForceSoftware;

    private void Awake()
    {
        ApplyCursor();
    }

    private void Start()
    {
        ApplyCursor();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ApplyCursor();
    }

    private void ApplyCursor()
    {
        if (cursorTexture == null)
        {
            Debug.LogWarning("Cursor texture atanmadý.");
            return;
        }

        Cursor.visible = true;
        Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
    }
}