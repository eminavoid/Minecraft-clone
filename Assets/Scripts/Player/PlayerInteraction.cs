// File: PlayerInteraction.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // ¡Necesario para las Listas!

/// <summary>
/// Handles all player interaction with the world:
/// raycasting, highlighting, breaking, and placing blocks.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Raycasting")]
    [SerializeField]
    private float _interactionDistance = 5.0f;

    [SerializeField]
    private LayerMask _worldLayer;

    [Header("References")]
    [SerializeField]
    private Transform _playerCamera;

    [SerializeField]
    private World _world;

    [Header("Hotbar")]
    [Tooltip("La lista de IDs de bloques en la hotbar (9 slots)")]
    [SerializeField]
    private List<byte> _hotbarBlockIDs = new List<byte>(9);

    [Header("UI")]
    [SerializeField]
    private HotbarUI _hotbarUI;

    private int _currentHotbarSlot = 0;
    private int _previousHotbarSlot = 0;

    // --- Campos Privados ---
    private Vector3 _highlightBlockPos;
    private Vector3 _placeBlockPos;
    private bool _hasHitBlock;

    // El 'heldBlockID' hardcodeado se ha eliminado

    private void Start()
    {
        if (_world == null || _hotbarUI == null)
        {
            Debug.LogError("¡Faltan referencias en PlayerInteraction!");
            return;
        }

        // Inicializar la UI con los datos
        _hotbarUI.Initialize(_hotbarBlockIDs, _world.GetWorldAtlasTexture());
        
        // Seleccionar el primer slot (0)
        _hotbarUI.UpdateSelectedSlot(_currentHotbarSlot, -1); // -1 para no des-seleccionar nada
    }

    void Update()
    {
        // El Raycasting no cambia
        Ray ray = new Ray(_playerCamera.position, _playerCamera.forward);

        _hasHitBlock = Physics.Raycast(
            ray,
            out RaycastHit hit,
            _interactionDistance,
            _worldLayer
        );

        if (_hasHitBlock)
        {
            _highlightBlockPos = hit.point - (hit.normal * 0.5f);
            _placeBlockPos = hit.point + (hit.normal * 0.5f);

            _highlightBlockPos = new Vector3(
                Mathf.FloorToInt(_highlightBlockPos.x),
                Mathf.FloorToInt(_highlightBlockPos.y),
                Mathf.FloorToInt(_highlightBlockPos.z)
            );
            _placeBlockPos = new Vector3(
                Mathf.FloorToInt(_placeBlockPos.x),
                Mathf.FloorToInt(_placeBlockPos.y),
                Mathf.FloorToInt(_placeBlockPos.z)
            );
        }
    }

    // --- Input System Event Handlers ---

    /// <summary>
    /// Llamado por el Input Action "Break" (Click Izquierdo)
    /// </summary>
    public void OnBreak(InputValue value)
    {
        if (value.isPressed && _hasHitBlock)
        {
            _world.SetBlock(_highlightBlockPos, 0); // 0 = Air ID
        }
    }

    /// <summary>
    /// Llamado por el Input Action "Place" (Click Derecho)
    /// </summary>
    public void OnPlace(InputValue value)
    {
        if (value.isPressed && _hasHitBlock)
        {
            // ¡LÓGICA ACTUALIZADA!
            // Obtiene el bloque del slot actual de la hotbar
            byte blockToPlace = _hotbarBlockIDs[_currentHotbarSlot];

            // No coloques "Aire"
            if (blockToPlace != 0)
            {
                _world.SetBlock(_placeBlockPos, blockToPlace);
            }
        }
    }

    /// <summary>
    /// Llamado por el Input Action "ChangeSlot" (Rueda del Mouse)
    /// </summary>
    public void OnChangeSlot(InputValue value)
    {
        float scroll = value.Get<float>();

        // Almacenar el slot antiguo
        _previousHotbarSlot = _currentHotbarSlot;

        if (scroll > 0f) _currentHotbarSlot--;
        else if (scroll < 0f) _currentHotbarSlot++;

        // Lógica de "envoltura" (Wrap)
        if (_currentHotbarSlot > 8) _currentHotbarSlot = 0;
        if (_currentHotbarSlot < 0) _currentHotbarSlot = 8;

        // Si el slot realmente cambió, informar a la UI
        if (_previousHotbarSlot != _currentHotbarSlot)
        {
            if (_hotbarUI != null)
            {
                _hotbarUI.UpdateSelectedSlot(_currentHotbarSlot, _previousHotbarSlot);
            }
            Debug.Log($"Slot seleccionado: {_currentHotbarSlot}");
        }
    }

    // --- Métodos "Getter" (para que la UI los lea) ---
    public List<byte> GetHotbarIDs()
    {
        return _hotbarBlockIDs;
    }
    public int GetCurrentSlot()
    {
        return _currentHotbarSlot;
    }

    private void OnDrawGizmos()
    {
        if (_hasHitBlock)
        {
            Gizmos.color = new Color(1, 1, 1, 0.3f);
            Gizmos.DrawCube(
                _highlightBlockPos + new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(1.01f, 1.01f, 1.01f)
            );
        }
    }


}