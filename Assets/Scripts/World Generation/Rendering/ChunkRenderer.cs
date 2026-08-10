using UnityEngine;

/// <summary>
/// Owns per-section renderers for a column chunk (Minecraft-style 16^3 sections).
/// </summary>
public class ChunkRenderer : MonoBehaviour
{
    private Chunk _chunkData;
    private World _world;
    private Material _material;
    private int _worldLayer;

    private ChunkSectionRenderer[] _sections;

    public Chunk ChunkData => _chunkData;

    public void Initialize(Chunk chunk, World world, Material material)
    {
        _chunkData = chunk;
        _world = world;
        _material = material;
        _worldLayer = gameObject.layer;

        EnsureSections();
    }

    public ChunkSectionRenderer GetSection(int sectionIndex)
    {
        if (!ChunkSection.IsValidIndex(sectionIndex))
            return null;

        EnsureSections();
        return _sections[sectionIndex];
    }

    public bool HasAnyMesh()
    {
        if (_sections == null)
            return false;

        for (int i = 0; i < _sections.Length; i++)
        {
            if (_sections[i] != null && _sections[i].HasMesh)
                return true;
        }

        return false;
    }

    public void ApplyRenderData(ChunkRenderData renderData)
    {
        if (renderData == null)
            return;

        EnsureSections();

        for (int i = 0; i < renderData.Sections.Length; i++)
        {
            SectionRenderData sectionData = renderData.Sections[i];
            if (sectionData == null || !ChunkSection.IsValidIndex(sectionData.SectionIndex))
                continue;

            _sections[sectionData.SectionIndex].ApplySectionData(sectionData);
        }
    }

    public void ClearAllColliders()
    {
        if (_sections == null)
            return;

        for (int i = 0; i < _sections.Length; i++)
        {
            if (_sections[i] != null)
                _sections[i].ClearCollider();
        }
    }

    public void BakeSectionCollider(int sectionIndex)
    {
        ChunkSectionRenderer section = GetSection(sectionIndex);
        if (section != null)
            section.BakeCollider();
    }

    private void EnsureSections()
    {
        if (_sections != null && _sections.Length == ChunkSection.CountY)
            return;

        _sections = new ChunkSectionRenderer[ChunkSection.CountY];

        for (int i = 0; i < ChunkSection.CountY; i++)
        {
            Transform existing = transform.Find($"Section_{i}");
            GameObject sectionObject;
            if (existing != null)
            {
                sectionObject = existing.gameObject;
            }
            else
            {
                sectionObject = new GameObject($"Section_{i}");
                sectionObject.transform.SetParent(transform, false);
                sectionObject.transform.localPosition = Vector3.zero;
                sectionObject.AddComponent<MeshFilter>();
                sectionObject.AddComponent<MeshRenderer>();
                sectionObject.AddComponent<MeshCollider>();
                sectionObject.AddComponent<ChunkSectionRenderer>();
            }

            ChunkSectionRenderer sectionRenderer = sectionObject.GetComponent<ChunkSectionRenderer>();
            sectionRenderer.Initialize(i, _material, _worldLayer);
            _sections[i] = sectionRenderer;
        }
    }
}
