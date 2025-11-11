// File: PlayerController.cs

using UnityEngine;
using UnityEngine.InputSystem; // <-- Make 100% sure this line is here

/// <summary>
/// Handles player movement, jumping, and gravity.
/// This script receives input events from the PlayerInput component.
/// It follows SRP by only managing movement, not camera look.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField]
    private float _playerSpeed = 5.0f;

    [SerializeField]
    private float _jumpHeight = 1.8f;

    // --- Private Fields ---
    private CharacterController _controller;

    private float _verticalVelocity;
    [SerializeField]
    private float _gravityValue = -9.81f;
    private bool _isGrounded;

    // This stores the raw Vector2 from the Input System
    private Vector2 _moveInput;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // Check if the player is on the ground
        _isGrounded = _controller.isGrounded;

        // Reset vertical velocity if grounded
        if (_isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f; // A small negative force to keep them grounded
        }

        // --- Apply Movement ---
        // 1. Get world-space direction from local input
        // _moveInput.y is "forward" (W/S)
        // _moveInput.x is "right" (A/D)
        Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;

        // 2. Apply movement speed
        _controller.Move(move * _playerSpeed * Time.deltaTime);

        // --- Apply Gravity ---
        // 3. Apply gravity to vertical velocity
        _verticalVelocity += _gravityValue * Time.deltaTime;

        // 4. Apply vertical velocity to the controller
        _controller.Move(new Vector3(0, _verticalVelocity, 0) * Time.deltaTime);
    }

    // --- INPUT SYSTEM EVENT HANDLERS ---
    // These methods are CALLED by the 'PlayerInput' component

    /// <summary>
    /// Event handler for the "Move" action.
    /// Stores the Vector2 input.
    /// </summary>
    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
    }

    /// <summary>
    /// Event handler for the "Jump" action.
    /// </summary>
    public void OnJump(InputValue value)
    {
        // Only allow jumping if the input is pressed AND the player is grounded
        if (value.isPressed && _isGrounded)
        {
            // The physics formula to find the velocity needed to reach a height
            _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravityValue);
        }
    }
}