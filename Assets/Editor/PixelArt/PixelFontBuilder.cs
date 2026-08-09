using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Warana.EditorTools
{
    /// <summary>
    /// Gera o asset TMP da fonte do jogo a partir de <c>BoldPixels.ttf</c>.
    ///
    /// O projeto inteiro é pixel art e a interface vinha em LiberationSans — uma fonte
    /// vetorial suavizada, que trai a apresentação assim que aparece por cima do jogo.
    ///
    /// <para>O ponto do gerador é o <see cref="GlyphRenderMode.RASTER"/>: o modo SDF
    /// padrão do TMP guarda a distância até a borda e reconstrói o contorno suavizado em
    /// qualquer tamanho — exatamente o que uma fonte de pixel não quer. Em RASTER o atlas
    /// guarda o desenho como está, e com o filtro do atlas em Point o pixel continua
    /// quadrado.</para>
    ///
    /// <para>O tamanho de amostragem é múltiplo dos 16 px de corpo da fonte para que cada
    /// pixel do desenho vire um bloco inteiro no atlas; um valor quebrado faria o
    /// gerador arredondar linhas de pixels de formas diferentes em cada letra.</para>
    /// </summary>
    public static class PixelFontBuilder
    {
        private const string SourceFontPath = "Assets/BoldPixels/Assets/font/BoldPixels.ttf";
        private const string OutputFolder = "Assets/Fonts";
        private const string OutputPath = OutputFolder + "/BoldPixels Bitmap.asset";

        /// <summary>Corpo nativo da BoldPixels, em pixels.</summary>
        private const int NativePointSize = 16;

        /// <summary>
        /// 4× o corpo nativo. Grande o bastante para o título do menu não ser ampliado a
        /// partir de um atlas menor, e ainda dentro de um atlas de 1024².
        /// </summary>
        private const int SamplingPointSize = NativePointSize * 4;

        private const int AtlasSize = 1024;

        /// <summary>
        /// O que o jogo escreve. ASCII imprimível mais os acentos do português e os
        /// poucos sinais que os textos de controle usam (<see cref="Warana.UI.GameControls"/>).
        /// </summary>
        private static string BuildCharacterSet()
        {
            var sb = new StringBuilder();

            for (char c = ' '; c <= '~'; c++) sb.Append(c);

            sb.Append("ÁÂÃÀÄÇÉÊÈËÍÎÌÏÓÔÕÒÖÚÛÙÜÝÑ");
            sb.Append("áâãàäçéêèëíîìïóôõòöúûùüýñ");
            sb.Append("ºª°·—–…«»→←↑↓“”‘’");

            return sb.ToString();
        }

        [MenuItem("Waraná/Pixel Art/Gerar Fonte Bitmap")]
        public static void Generate()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[PixelFont] Fonte de origem não encontrada em {SourceFontPath}.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets", "Fonts");

            // padding 1: em RASTER não há campo de distância para preservar, mas um pixel
            // de folga evita que o filtro puxe a letra vizinha na borda do glifo.
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
                source, SamplingPointSize, 1, GlyphRenderMode.RASTER,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic);

            if (font == null)
            {
                Debug.LogError("[PixelFont] TMP não conseguiu criar o asset da fonte.");
                return;
            }

            // Antes de gerar atlas e material: os dois herdam este nome, e sem ele os
            // sub-assets aparecem sem identificação dentro do arquivo.
            font.name = System.IO.Path.GetFileNameWithoutExtension(OutputPath);

            font.TryAddCharacters(BuildCharacterSet(), out string missing);

            // Estático depois de preencher: com o atlas fechado, uma letra fora da lista
            // aparece como caixa vazia em vez de ser gerada em runtime — falha visível no
            // editor, e não uma surpresa no build, onde a fonte de origem não existe mais.
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            Material material = BuildMaterial(font);

            WriteAsset(font, material);

            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[PixelFont] A BoldPixels não tem estes caracteres: '{missing}'. " +
                                  "Eles vão sair como caixa vazia — troque o texto ou a fonte.");

            Debug.Log($"[PixelFont] {OutputPath} gerado: {font.characterTable.Count} caracteres, " +
                      $"corpo {SamplingPointSize} px.");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
        }

        /// <summary>
        /// O shader Bitmap do TMP apenas amostra o atlas — sem o cálculo de contorno do
        /// shader SDF, que num atlas RASTER só arredondaria as quinas do pixel.
        /// </summary>
        private static Material BuildMaterial(TMP_FontAsset font)
        {
            Shader shader = Shader.Find("TextMeshPro/Bitmap");
            if (shader == null)
            {
                Debug.LogWarning("[PixelFont] Shader 'TextMeshPro/Bitmap' não encontrado; " +
                                 "o material fica com o shader padrão do TMP.");
                return font.material;
            }

            var material = new Material(shader) { name = font.name + " Material" };
            material.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);

            font.material = material;
            return material;
        }

        /// <summary>
        /// Grava fonte, atlas e material como um asset só. O atlas vai com filtro Point:
        /// é ele que decide se a letra chega quadrada ou borrada na tela.
        /// </summary>
        private static void WriteAsset(TMP_FontAsset font, Material material)
        {
            Texture2D atlas = font.atlasTexture;
            if (atlas != null)
            {
                atlas.filterMode = FilterMode.Point;
                atlas.name = font.name + " Atlas";
            }

            AssetDatabase.DeleteAsset(OutputPath);
            AssetDatabase.CreateAsset(font, OutputPath);

            if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, font);
            if (material != null) AssetDatabase.AddObjectToAsset(material, font);

            // O atlas precisa apontar para a instância já salva, senão a referência
            // aponta para o objeto em memória e o asset abre sem textura.
            font.atlasTextures = new[] { atlas };
            material.SetTexture(ShaderUtilities.ID_MainTex, atlas);

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
