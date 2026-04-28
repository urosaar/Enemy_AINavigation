using UnityEngine;

public class CursorLockOnStart : MonoBehaviour
{
    [SerializeField] private bool lockCursor = true;
    [SerializeField] private bool hideCursor = true;

    private void Start()
    {
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !hideCursor;
    }
}
