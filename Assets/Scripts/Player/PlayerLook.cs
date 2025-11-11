// File: PlayerLook.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles mouse look for the player.
/// This script rotates the player body (Y-axis) and the camera (X-axis).
/// It follows SRP by only managing look, not movement.
/// </summary>
public class PlayerLook : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField]
    private float _mouseSensitivity = 100f;

    [Tooltip("The transform of the child camera object.")]
    [SerializeField]
    private Transform _playerCamera;

    // --- Private Fields ---
    private Vector2 _lookInput;
    private float _xRotation = 0f; // This is our up/down tilt

    void Start()
    {
        // Lock and hide the cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // 1. Get input values
        float mouseX = _lookInput.x * _mouseSensitivity * Time.deltaTime;
        float mouseY = _lookInput.y * _mouseSensitivity * Time.deltaTime;

        // 2. Calculate and clamp the X-axis (up/down) rotation
        // We subtract mouseY because 'up' is a positive Y input,
        // but it corresponds to a negative X rotation.
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f); // Prevents flipping

        // 3. Apply rotations
        // Rotate the camera up/down
        _playerCamera.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // Rotate the entire player body left/right
        transform.Rotate(Vector3.up * mouseX);
    }

    /// <summary>
    // This is called by the 'PlayerInput' component's "Send Messages" behavior
    /// </summary>
    public void OnLook(InputValue value)
    {
        _lookInput = value.Get<Vector2>();
    }
}