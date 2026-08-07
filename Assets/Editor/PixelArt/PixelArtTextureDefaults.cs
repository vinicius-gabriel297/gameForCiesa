using System;
using UnityEditor;
using UnityEngine;

namespace Warana.EditorTools
{
    /// <summary>
    /// Regras de importação de textura do projeto — pixel art, 2D, sem filtro.
    /// Um único lugar decide o que "otimizado" significa aqui.
    /// </summary>
    public static class PixelArtTextureDefaults
    {
        /// <summary>
        /// Pixels por unidade do projeto. O Waraná tem ~62 px de altura útil,
        /// então 64 deixa o personagem com ~1 unidade — a mesma escala do collider
        /// e do level de teste (1 unidade = 1 metro).
        /// </summary>
        public const int PixelsPerUnit = 64;

        /// <summary>Resolução lógica de referência para a Pixel Perfect Camera (16:9, x3 = 1920x1080).</summary>
        public const int ReferenceWidth = 640;

        public const int ReferenceHeight = 360;

        /// <summary>Pastas onde as regras valem. Fora delas ninguém mexe no import.</summary>
        public static readonly string[] ManagedFolders =
        {
            "Assets/Art",
            "Assets/Animations",
        };

        public static bool IsManaged(string assetPath)
        {
            foreach (string folder in ManagedFolders)
            {
                if (assetPath.StartsWith(folder + "/", StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>
        /// Aplica qualidade/otimização. Não toca em PPU, pivô nem modo de sprite:
        /// isso é decisão de cada asset e seria destrutivo sobrescrever.
        /// </summary>
        public static void ApplyQuality(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;      // bilinear borra a borda do pixel
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;              // mipmap em 2D só embaça e gasta memória
            importer.textureCompression = TextureImporterCompression.Uncompressed; // DXT suja alpha e cor chapada
            importer.crunchedCompression = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // Tight desalinha o sprite da grade de pixels
            settings.spriteExtrude = 0;                        // extrude inventa pixels na borda
            importer.SetTextureSettings(settings);
        }

        [MenuItem("Waraná/Pixel Art/Reaplicar Import Settings")]
        public static void ReapplyToManagedFolders()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", ManagedFolders);
            int changed = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                ApplyQuality(importer);
                importer.SaveAndReimport();
                changed++;
            }

            Debug.Log($"[PixelArt] Import settings reaplicadas em {changed} textura(s).");
        }
    }

    /// <summary>
    /// Aplica os defaults de pixel art em texturas novas. Só na primeira importação:
    /// reimportar não pode desfazer um PPU ou pivô ajustado à mão depois.
    /// </summary>
    public class PixelArtTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!PixelArtTextureDefaults.IsManaged(assetPath)) return;
            if (!assetImporter.importSettingsMissing) return;

            var importer = (TextureImporter)assetImporter;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelArtTextureDefaults.PixelsPerUnit;
            PixelArtTextureDefaults.ApplyQuality(importer);
        }
    }
}
