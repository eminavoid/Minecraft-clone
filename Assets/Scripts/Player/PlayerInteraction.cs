// File: PlayerInteraction.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
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

    [Header("Highlight")]
    [SerializeField]
    private Shader _highlightShader;

    private int _currentHotbarSlot = 0;
    private int _previousHotbarSlot = 0;

    // --- Campos Privados ---
    private Vector3 _highlightBlockPos;
    private Vector3 _placeBlockPos;
    private bool _hasHitBlock;
    private CharacterController _controller;

    private const float HighlightExtent = 1.01f;
    private const float HighlightBarThickness = 0.02f;
    private GameObject _highlightObject;
    private Mesh _highlightMesh;
    private Material _highlightMaterial;

    // El 'heldBlockID' hardcodeado se ha eliminado

    private void Awake()
    {
        CreateBlockHighlight();
    }

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

        UpdateBlockHighlight();
    }

    private void OnDisable()
    {
        if (_highlightObject != null)
            _highlightObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_highlightObject != null)
            Destroy(_highlightObject);
        if (_highlightMesh != null)
            Destroy(_highlightMesh);
        if (_highlightMaterial != null)
            Destroy(_highlightMaterial);
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

            // No coloques "Aire", ni un bloque que atraviese la cápsula:
            // el CharacterController se queda atascado y el step offset no lo saca.
            if (blockToPlace != 0 && !TargetCellIntersectsPlayer(_placeBlockPos))
            {
                _world.SetBlock(_placeBlockPos, blockToPlace);
            }
        }
    }

    /// <summary>
    /// True when the 1 m cell overlaps the CharacterController capsule.
    /// The body only yaws, so the capsule axis stays world-up.
    /// </summary>
    private bool TargetCellIntersectsPlayer(Vector3 cellMin)
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();
        if (_controller == null)
            return false;

        Vector3 scale = transform.lossyScale;
        float radius = _controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = _controller.height * Mathf.Abs(scale.y);
        if (height < radius * 2f)
            height = radius * 2f;

        Vector3 center = transform.TransformPoint(_controller.center);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        float segmentMinY = center.y - halfSegment;
        float segmentMaxY = center.y + halfSegment;

        float closestX = Mathf.Clamp(center.x, cellMin.x, cellMin.x + 1f);
        float closestZ = Mathf.Clamp(center.z, cellMin.z, cellMin.z + 1f);
        float dx = center.x - closestX;
        float dz = center.z - closestZ;

        float cellMaxY = cellMin.y + 1f;
        float dy = 0f;
        if (cellMaxY < segmentMinY)
            dy = segmentMinY - cellMaxY;
        else if (cellMin.y > segmentMaxY)
            dy = cellMin.y - segmentMaxY;

        return dx * dx + dy * dy + dz * dz <= radius * radius;
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

    // Keys 1-9 are SelectSlot1..SelectSlot9. Slot 0 is the first hotbar cell.
    public void OnSelectSlot1(InputValue value) { SelectHotbarSlot(value, 0); }
    public void OnSelectSlot2(InputValue value) { SelectHotbarSlot(value, 1); }
    public void OnSelectSlot3(InputValue value) { SelectHotbarSlot(value, 2); }
    public void OnSelectSlot4(InputValue value) { SelectHotbarSlot(value, 3); }
    public void OnSelectSlot5(InputValue value) { SelectHotbarSlot(value, 4); }
    public void OnSelectSlot6(InputValue value) { SelectHotbarSlot(value, 5); }
    public void OnSelectSlot7(InputValue value) { SelectHotbarSlot(value, 6); }
    public void OnSelectSlot8(InputValue value) { SelectHotbarSlot(value, 7); }
    public void OnSelectSlot9(InputValue value) { SelectHotbarSlot(value, 8); }

    private void SelectHotbarSlot(InputValue value, int slot)
    {
        if (!value.isPressed || slot < 0 || slot > 8 || slot == _currentHotbarSlot)
            return;

        _previousHotbarSlot = _currentHotbarSlot;
        _currentHotbarSlot = slot;

        if (_hotbarUI != null)
            _hotbarUI.UpdateSelectedSlot(_currentHotbarSlot, _previousHotbarSlot);

        Debug.Log($"Slot seleccionado: {_currentHotbarSlot}");
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

    private void CreateBlockHighlight()
    {
        Shader shader = _highlightShader != null
            ? _highlightShader
            : Shader.Find("MinecraftClone/BlockHighlight");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("PlayerInteraction: block highlight shader is missing.");
            return;
        }

        _highlightMesh = BuildWireCubeMesh(HighlightExtent, HighlightBarThickness);
        _highlightMaterial = new Material(shader) { name = "BlockHighlight" };
        _highlightMaterial.SetColor("_BaseColor", Color.black);
        _highlightMaterial.SetColor("_Color", Color.black);

        _highlightObject = new GameObject("Block Highlight");
        // The player transform is non-uniformly scaled, so the cube stays in world space.
        _highlightObject.SetActive(false);

        MeshFilter filter = _highlightObject.AddComponent<MeshFilter>();
        filter.sharedMesh = _highlightMesh;

        MeshRenderer renderer = _highlightObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _highlightMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void UpdateBlockHighlight()
    {
        if (_highlightObject == null)
            return;

        if (!_hasHitBlock)
        {
            if (_highlightObject.activeSelf)
                _highlightObject.SetActive(false);
            return;
        }

        if (!_highlightObject.activeSelf)
            _highlightObject.SetActive(true);

        _highlightObject.transform.position = _highlightBlockPos + new Vector3(0.5f, 0.5f, 0.5f);
    }

    private static Mesh BuildWireCubeMesh(float extent, float thickness)
    {
        float h = extent * 0.5f;
        float t = thickness * 0.5f;
        var vertices = new List<Vector3>(96);
        var triangles = new List<int>(432);

        AddBox(vertices, triangles, new Vector3(0f, -h, -h), new Vector3(h, t, t));
        AddBox(vertices, triangles, new Vector3(0f, -h, h), new Vector3(h, t, t));
        AddBox(vertices, triangles, new Vector3(0f, h, -h), new Vector3(h, t, t));
        AddBox(vertices, triangles, new Vector3(0f, h, h), new Vector3(h, t, t));

        AddBox(vertices, triangles, new Vector3(-h, -h, 0f), new Vector3(t, t, h));
        AddBox(vertices, triangles, new Vector3(h, -h, 0f), new Vector3(t, t, h));
        AddBox(vertices, triangles, new Vector3(-h, h, 0f), new Vector3(t, t, h));
        AddBox(vertices, triangles, new Vector3(h, h, 0f), new Vector3(t, t, h));

        AddBox(vertices, triangles, new Vector3(-h, 0f, -h), new Vector3(t, h, t));
        AddBox(vertices, triangles, new Vector3(h, 0f, -h), new Vector3(t, h, t));
        AddBox(vertices, triangles, new Vector3(-h, 0f, h), new Vector3(t, h, t));
        AddBox(vertices, triangles, new Vector3(h, 0f, h), new Vector3(t, h, t));

        var mesh = new Mesh { name = "BlockHighlight" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddBox(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 halfExtents)
    {
        Vector3 hx = new Vector3(halfExtents.x, 0f, 0f);
        Vector3 hy = new Vector3(0f, halfExtents.y, 0f);
        Vector3 hz = new Vector3(0f, 0f, halfExtents.z);
        int start = vertices.Count;

        vertices.Add(center - hx - hy - hz);
        vertices.Add(center + hx - hy - hz);
        vertices.Add(center + hx - hy + hz);
        vertices.Add(center - hx - hy + hz);
        vertices.Add(center - hx + hy - hz);
        vertices.Add(center + hx + hy - hz);
        vertices.Add(center + hx + hy + hz);
        vertices.Add(center - hx + hy + hz);

        int[] faces =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            1, 2, 6, 1, 6, 5,
            2, 3, 7, 2, 7, 6,
            3, 0, 4, 3, 4, 7
        };
        for (int i = 0; i < faces.Length; i++)
            triangles.Add(start + faces[i]);
    }
}
