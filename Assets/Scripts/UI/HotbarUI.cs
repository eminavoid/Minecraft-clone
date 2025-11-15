// File: HotbarUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static BlockType;

/// <summary>
/// Gestiona la lógica visual de la hotbar.
/// Lee los datos de PlayerInteraction para mostrar los iconos y el selector.
/// </summary>
public class HotbarUI : MonoBehaviour
{
    [Header("Assets de Sprites")]
    [SerializeField] private Sprite _defaultSlotSprite;
    [SerializeField] private Sprite _selectorSlotSprite;

    [Header("Referencias de Slots (Asignar en orden 0-8)")]
    [SerializeField] private Image[] _slotBackgrounds;
    [SerializeField] private RawImage[] _slotIcons;

    private Texture _worldAtlasTexture; // Cache para la textura del atlas

    /// <summary>
    /// Llamado por PlayerInteraction al inicio para configurar la UI.
    /// </summary>
    public void Initialize(List<byte> hotbarIDs, Texture2D worldAtlas)
    {
        _worldAtlasTexture = worldAtlas;

        if (hotbarIDs.Count != _slotIcons.Length || hotbarIDs.Count != _slotBackgrounds.Length)
        {
            Debug.LogError("HotbarUI: ¡El número de slots no coincide! (Se esperan 9)");
            return;
        }

        // Iterar sobre todos los slots para configurarlos
        for (int i = 0; i < 9; i++)
        {
            byte blockID = hotbarIDs[i];

            if (blockID == 0) // 0 es Aire
            {
                // Desactivar el GameObject del icono
                _slotIcons[i].gameObject.SetActive(false);
            }
            else
            {
                // Activar y configurar el icono
                _slotIcons[i].gameObject.SetActive(true);

                BlockType type = BlockDatabase.GetBlockType(blockID);
                TextureAtlasCoord coord = type.GetTextureCoords(BlockFace.Top); // Usar la cara superior como icono

                if (coord != null)
                {
                    _slotIcons[i].texture = _worldAtlasTexture;
                    _slotIcons[i].uvRect = TextureAtlasManager.GetUVRect(coord);
                }
            }
        }
    }

    /// <summary>
    /// Llamado por PlayerInteraction cada vez que el slot cambia.
    /// </summary>
    public void UpdateSelectedSlot(int newSlot, int oldSlot)
    {
        // Des-seleccionar el slot antiguo
        if (oldSlot >= 0 && oldSlot < _slotBackgrounds.Length)
        {
            _slotBackgrounds[oldSlot].sprite = _defaultSlotSprite;
        }

        // Seleccionar el nuevo slot
        if (newSlot >= 0 && newSlot < _slotBackgrounds.Length)
        {
            _slotBackgrounds[newSlot].sprite = _selectorSlotSprite;
        }
    }
}