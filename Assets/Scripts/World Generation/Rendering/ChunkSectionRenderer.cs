using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ChunkSectionRenderer : MonoBehaviour
{
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private MeshCollider _meshCollider;
    private Mesh _mesh;

    private int _sectionIndex;
    private bool _hasMesh;
    private bool _hasCollider;

    public int SectionIndex => _sectionIndex;
    public bool HasMesh => _hasMesh;
    public bool HasCollider => _hasCollider;

    public void GetMeshStats(out int vertexCount, out int triangleCount)
    {
        vertexCount = 0;
        triangleCount = 0;

        if (!_hasMesh || _mesh == null)
            return;

        vertexCount = _mesh.vertexCount;
        triangleCount = _mesh.triangles.Length / 3;
    }

    public void Initialize(int sectionIndex, Material material, int worldLayer)
    {
        _sectionIndex = sectionIndex;
        gameObject.layer = worldLayer;

        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _meshCollider = GetComponent<MeshCollider>();

        _mesh = new Mesh();
        _mesh.name = $"SectionMesh_{sectionIndex}";
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _meshFilter.sharedMesh = _mesh;
        _meshRenderer.sharedMaterial = material;
        _meshCollider.sharedMesh = null;

        gameObject.SetActive(false);
    }

    public void ApplySectionData(SectionRenderData data)
    {
        if (data == null)
            return;

        ClearCollider();

        if (data.IsEmpty)
        {
            _mesh.Clear();
            _hasMesh = false;
            gameObject.SetActive(false);
            return;
        }

        if (data.Vertices.Length != data.UVs.Length || data.Vertices.Length != data.UV1s.Length)
        {
            Debug.LogError(
                $"ChunkSectionRenderer: Vertex/UV mismatch (v={data.Vertices.Length}, uv0={data.UVs.Length}, uv1={data.UV1s.Length}).");
            return;
        }

        _mesh.Clear();
        _mesh.SetVertices(data.Vertices);
        _mesh.SetTriangles(data.Triangles, 0);
        _mesh.SetUVs(0, data.UVs);
        _mesh.SetUVs(1, data.UV1s);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
        _hasMesh = true;
        gameObject.SetActive(true);
    }

    public void BakeCollider()
    {
        if (!_hasMesh)
            return;

        _meshCollider.sharedMesh = null;
        _meshCollider.sharedMesh = _mesh;
        _hasCollider = true;
    }

    public void ClearCollider()
    {
        if (_meshCollider != null && _meshCollider.sharedMesh != null)
            _meshCollider.sharedMesh = null;

        _hasCollider = false;
    }
}
