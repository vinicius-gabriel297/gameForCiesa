using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Warana.EditorTools
{
    /// <summary>
    /// Deixa o projeto em modo pixel art: nada de MSAA, anisotropia ou upscale
    /// borrado, e a câmera travada na grade de pixels.
    /// </summary>
    public static class PixelArtProjectSetup
    {
        [MenuItem("Waraná/Pixel Art/Configurar Projeto")]
        public static void ConfigureProject()
        {
            ConfigureQualityLevels();
            ConfigureRenderPipelineAssets();

            Camera main = Camera.main;
            if (main != null) ConfigureCamera(main);

            AssetDatabase.SaveAssets();
            Debug.Log("[PixelArt] Projeto configurado para pixel art.");
        }

        /// <summary>MSAA e filtro anisotrópico borram a borda do pixel — desligados em todos os níveis.</summary>
        private static void ConfigureQualityLevels()
        {
            Object settingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var serialized = new SerializedObject(settingsAsset);
            SerializedProperty levels = serialized.FindProperty("m_QualitySettings");

            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("antiAliasing").intValue = 0;
                level.FindPropertyRelative("anisotropicTextures").intValue = 0; // AnisotropicFiltering.Disable
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRenderPipelineAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;

                asset.msaaSampleCount = 1;   // 1 = desligado
                asset.supportsHDR = false;   // sem HDR a cor chapada não muda de tom
                asset.renderScale = 1f;      // qualquer escala != 1 reamostra a imagem

                EditorUtility.SetDirty(asset);
            }
        }

        /// <summary>
        /// Pixel Perfect Camera com Pixel Snapping: os sprites caem na grade, mas a
        /// câmera ainda se move suavemente (Upscale RT travaria o movimento em degraus).
        /// </summary>
        public static PixelPerfectCamera ConfigureCamera(Camera camera)
        {
            camera.orthographic = true;

            var pixelPerfect = camera.GetComponent<PixelPerfectCamera>()
                               ?? camera.gameObject.AddComponent<PixelPerfectCamera>();

            pixelPerfect.assetsPPU = PixelArtTextureDefaults.PixelsPerUnit;
            pixelPerfect.refResolutionX = PixelArtTextureDefaults.ReferenceWidth;
            pixelPerfect.refResolutionY = PixelArtTextureDefaults.ReferenceHeight;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;

            EditorUtility.SetDirty(camera.gameObject);
            return pixelPerfect;
        }
    }
}
