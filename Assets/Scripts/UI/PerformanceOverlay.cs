using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// IMGUI overlay: FPS, 1% low, chunks, tris, vertices.
/// </summary>
public class PerformanceOverlay : MonoBehaviour
{
    [SerializeField] private World _world;
    [SerializeField] private bool _showOverlay = true;
    [SerializeField] private Key _toggleKey = Key.F3;
    [SerializeField] private int _frameSampleCount = 1000;
    [SerializeField] private float _meshStatsInterval = 0.25f;

    private float[] _frameTimes;
    private int _frameTimeIndex;
    private int _frameTimeCount;
    private float[] _sortedFrameTimes;

    private float _displayedFps;
    private float _displayedOnePercentLow;
    private float _fpsSmooth;

    private int _chunks;
    private int _sections;
    private int _vertices;
    private int _triangles;
    private float _meshStatsTimer;

    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private bool _stylesReady;

    private void Awake()
    {
        if (_world == null)
            _world = FindFirstObjectByType<World>();

        int samples = Mathf.Max(100, _frameSampleCount);
        _frameTimes = new float[samples];
        _sortedFrameTimes = new float[samples];
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
            _showOverlay = !_showOverlay;

        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
        {
            _frameTimes[_frameTimeIndex] = dt;
            _frameTimeIndex = (_frameTimeIndex + 1) % _frameTimes.Length;
            if (_frameTimeCount < _frameTimes.Length)
                _frameTimeCount++;

            float instantFps = 1f / dt;
            _fpsSmooth = Mathf.Lerp(_fpsSmooth <= 0f ? instantFps : _fpsSmooth, instantFps, 0.1f);
            _displayedFps = _fpsSmooth;
            _displayedOnePercentLow = CalculateOnePercentLowFps();
        }

        _meshStatsTimer += Time.unscaledDeltaTime;
        if (_meshStatsTimer >= _meshStatsInterval)
        {
            _meshStatsTimer = 0f;
            RefreshMeshStats();
        }
    }

    private void OnGUI()
    {
        if (!_showOverlay)
            return;

        EnsureStyles();

        const float width = 220f;
        const float height = 130f;
        Rect rect = new Rect(12f, 12f, width, height);

        GUI.Box(rect, GUIContent.none, _boxStyle);

        Rect labelRect = new Rect(rect.x + 10f, rect.y + 8f, width - 20f, height - 16f);
        GUI.Label(
            labelRect,
            $"FPS: {_displayedFps:0.0}\n" +
            $"1% Low: {_displayedOnePercentLow:0.0}\n" +
            $"Chunks: {_chunks}\n" +
            $"Sections: {_sections}\n" +
            $"Tris: {FormatCount(_triangles)}\n" +
            $"Verts: {FormatCount(_vertices)}\n" +
            $"[{_toggleKey}] toggle",
            _labelStyle);
    }

    private void RefreshMeshStats()
    {
        if (_world == null)
        {
            _world = FindFirstObjectByType<World>();
            if (_world == null)
            {
                _chunks = 0;
                _sections = 0;
                _vertices = 0;
                _triangles = 0;
                return;
            }
        }

        _world.GetRenderStats(out _chunks, out _sections, out _vertices, out _triangles);
    }

    private float CalculateOnePercentLowFps()
    {
        if (_frameTimeCount <= 0)
            return 0f;

        for (int i = 0; i < _frameTimeCount; i++)
            _sortedFrameTimes[i] = _frameTimes[i];

        System.Array.Sort(_sortedFrameTimes, 0, _frameTimeCount);

        // 99th percentile frame time => 1% low FPS
        int index = Mathf.Clamp(Mathf.CeilToInt(_frameTimeCount * 0.99f) - 1, 0, _frameTimeCount - 1);
        float worstFrameTime = _sortedFrameTimes[index];
        if (worstFrameTime <= 0f)
            return 0f;

        return 1f / worstFrameTime;
    }

    private void EnsureStyles()
    {
        if (_stylesReady)
            return;

        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.65f));

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            richText = false
        };
        _labelStyle.normal.textColor = Color.white;

        _stylesReady = true;
    }

    private static Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        Texture2D texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static string FormatCount(int value)
    {
        if (value >= 1_000_000)
            return $"{value / 1_000_000f:0.00}M";
        if (value >= 1_000)
            return $"{value / 1_000f:0.0}K";
        return value.ToString();
    }
}
