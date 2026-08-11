using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Warana.EditorTools
{
    /// <summary>
    /// Grava asset de tile preservando o GUID.
    ///
    /// Apagar e recriar seria mais curto e é justamente o que não pode: o GUID muda e toda
    /// cena pintada com aquele tile abre vazia. Daí a gravação por cima do asset existente.
    /// </summary>
    public static class TileAssetWriter
    {
        public static T Save<T>(T tile, string path) where T : TileBase
        {
            // CopySerialized traz o m_Name junto, e o da instância recém-criada é vazio: sem
            // acertar o nome depois, o tile aparece sem rótulo no Inspector e na Palette. Foi
            // esse detalhe que deixou as três RuleTiles antigas anônimas.
            string assetName = Path.GetFileNameWithoutExtension(path);

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                tile.name = assetName;
                AssetDatabase.CreateAsset(tile, path);
                return tile;
            }

            EditorUtility.CopySerialized(tile, existing);
            existing.name = assetName;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(tile);
            return existing;
        }
    }
}
