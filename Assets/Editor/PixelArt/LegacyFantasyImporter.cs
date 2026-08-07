using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Warana.EditorTools
{
    /// <summary>
    /// Reimporta o pacote Legacy Fantasy nas regras do projeto.
    ///
    /// Os .meta que vieram no pacote usam PPU 100, filtro bilinear e auto-slice por alpha —
    /// o Tiles.png do Debug Map virava 6 sprites gigantes. O
    /// <see cref="PixelArtTexturePostprocessor"/> não resolve: ele só cobre Assets/Art e
    /// Assets/Animations, e só roda quando o .meta ainda não existe. Daí este comando.
    /// </summary>
    public static class LegacyFantasyImporter
    {
        public const string RootFolder = "Assets/Legacy_Fantasy Assets";

        /// <summary>Grade nativa dos dois tilesets: 416x464 = 26x29 e 400x400 = 25x25.</summary>
        public const int TileSize = 16;

        private enum SliceMode
        {
            /// <summary>Imagem inteira: fundos, árvores, HUD, mockups.</summary>
            Single,

            /// <summary>Folha de tiles: grade de 16x16, células vazias descartadas.</summary>
            Tileset,

            /// <summary>Folha de animação: grade do tamanho do frame, pivô no pé.</summary>
            Sheet,
        }

        [MenuItem("Waraná/Pixel Art/Configurar Legacy Fantasy")]
        public static void ConfigureAll()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
            {
                Debug.LogError($"[LegacyFantasy] Pasta '{RootFolder}' não encontrada.");
                return;
            }

            string[] paths = FindPngs();
            int sliced = 0;

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];

                    EditorUtility.DisplayProgressBar(
                        "Configurar Legacy Fantasy",
                        Path.GetFileName(path),
                        (float)i / paths.Length);

                    if (Configure(path)) sliced++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[LegacyFantasy] {paths.Length} textura(s) reimportadas em PPU " +
                      $"{PixelArtTextureDefaults.PixelsPerUnit}; {sliced} fatiada(s) em grade.");
        }

        /// <summary>
        /// Só .png: os .aseprite irmãos são importados em paralelo pelo com.unity.2d.aseprite
        /// e geram o próprio conjunto de sprites, que não é da nossa conta.
        /// </summary>
        private static string[] FindPngs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { RootFolder });
            var pngs = new List<string>(guids.Length);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) pngs.Add(path);
            }

            pngs.Sort(System.StringComparer.Ordinal);
            return pngs.ToArray();
        }

        /// <summary>Devolve true se a textura foi fatiada em grade.</summary>
        private static bool Configure(string path)
        {
            SliceMode mode = Classify(path);
            bool needsPixels = mode != SliceMode.Single;

            // Passo 1: qualidade + PPU. isReadable liga só quando precisamos varrer o alpha
            // para descartar célula vazia; desliga de novo no passo 3.
            ApplySettings(path, mode, readable: needsPixels);

            if (!needsPixels) return false;

            // Passo 2: o data provider captura o spriteImportMode no construtor, então ele só
            // pode ser criado depois do reimport acima — senão SetSpriteRects é no-op silencioso.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError($"[LegacyFantasy] Não consegui carregar a textura '{path}'.");
                return false;
            }

            Vector2Int cell = mode == SliceMode.Tileset
                ? new Vector2Int(TileSize, TileSize)
                : SheetCell(path, texture.height);

            if (texture.width % cell.x != 0 || texture.height % cell.y != 0)
            {
                Debug.LogWarning($"[LegacyFantasy] '{path}' ({texture.width}x{texture.height}) não fecha " +
                                 $"na grade {cell.x}x{cell.y}; deixei como imagem única.");
                ApplySettings(path, SliceMode.Single, readable: false);
                return false;
            }

            SpriteAlignment alignment = mode == SliceMode.Sheet
                ? SpriteAlignment.BottomCenter // personagem: o pé no chão da célula
                : SpriteAlignment.Center;

            int count = Slice(path, texture, cell, alignment);

            // Passo 3: os rects já estão no .meta, então desligar isReadable não os perde.
            ApplySettings(path, mode, readable: false);

            return count > 0;
        }

        // ------------------------------------------------------------------ classificação

        private static SliceMode Classify(string path)
        {
            string dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;

            // As folhas de tile de ambos os pacotes moram numa subpasta chamada "Assets".
            if (dir.EndsWith("/Assets")) return SliceMode.Tileset;

            if (path.Contains("/Character/") || path.Contains("/Mob/")) return SliceMode.Sheet;

            return SliceMode.Single;
        }

        /// <summary>
        /// Tamanho de frame por pasta. Detectar por coluna transparente não funciona aqui:
        /// em várias folhas os frames se encostam (Run-Sheet dá 8 "ilhas" para 10 frames) e
        /// em outras um frame se parte em duas. Os valores abaixo vêm de medir as folhas:
        /// toda largura é múltipla exata deles.
        /// </summary>
        private static Vector2Int SheetCell(string path, int textureHeight)
        {
            if (path.Contains("/Character/")) return new Vector2Int(64, textureHeight);
            if (path.Contains("/Mob/Boar/")) return new Vector2Int(48, 32);
            if (path.Contains("/Mob/Small Bee/")) return new Vector2Int(64, 64);
            if (path.Contains("/Mob/Snail/")) return new Vector2Int(48, 32);

            return new Vector2Int(textureHeight, textureHeight);
        }

        // ---------------------------------------------------------------------- importer

        private static void ApplySettings(string path, SliceMode mode, bool readable)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            // Estes existem só no TextureImporter — passá-los pelo TextureImporterSettings não compila.
            importer.spriteImportMode = mode == SliceMode.Single
                ? SpriteImportMode.Single
                : SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelArtTextureDefaults.PixelsPerUnit;
            importer.isReadable = readable;

            // Qualidade compartilhada com Assets/Art e Assets/Animations.
            PixelArtTextureDefaults.ApplyQuality(importer);

            // ApplyQuality zera isReadable via SetTextureSettings; reafirma depois.
            importer.isReadable = readable;

            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------------- slice

        /// <summary>
        /// Escreve os rects da grade. Devolve quantas células sobraram depois de jogar
        /// fora as totalmente transparentes.
        /// </summary>
        private static int Slice(string path, Texture2D texture, Vector2Int cell, SpriteAlignment alignment)
        {
            int cols = texture.width / cell.x;
            int rows = texture.height / cell.y;

            Color32[] pixels = texture.GetPixels32();
            string prefix = SanitizeName(Path.GetFileNameWithoutExtension(path));
            Vector2 pivot = PivotOf(alignment);

            var rects = new List<SpriteRect>(cols * rows);
            var pairs = new List<SpriteNameFileIdPair>(cols * rows);

            // row 0 = topo, igual ao slicer da janela do Sprite Editor. O índice segue a
            // posição na folha mesmo com células descartadas, então Tiles_27 é sempre a
            // mesma célula da grade.
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int x = col * cell.x;
                    int y = (rows - 1 - row) * cell.y; // Texture2D tem origem embaixo

                    if (IsEmpty(pixels, texture.width, x, y, cell)) continue;

                    var rect = new SpriteRect
                    {
                        name = $"{prefix}_{row * cols + col}",
                        rect = new Rect(x, y, cell.x, cell.y),
                        alignment = alignment,
                        pivot = pivot,
                        border = Vector4.zero,
                        spriteID = GUID.Generate(), // vazio ou repetido explode dentro do SetSpriteRects
                    };

                    rects.Add(rect);
                    pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
                }
            }

            if (rects.Count == 0)
            {
                Debug.LogWarning($"[LegacyFantasy] '{path}' está todo transparente na grade {cell.x}x{cell.y}.");
                return 0;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // SetSpriteRects descarta todo spriteID ausente do array novo: é isto, e não o
            // SaveAndReimport, que apaga os rects do auto-slice por alpha.
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
            provider.Apply();

            // Apply() só mexe no SerializedObject do importer; sem isto nada é reimportado.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return rects.Count;
        }

        private static bool IsEmpty(Color32[] pixels, int textureWidth, int x, int y, Vector2Int cell)
        {
            for (int j = 0; j < cell.y; j++)
            {
                int row = (y + j) * textureWidth;
                for (int i = 0; i < cell.x; i++)
                {
                    if (pixels[row + x + i].a > 8) return false;
                }
            }

            return true;
        }

        private static Vector2 PivotOf(SpriteAlignment alignment)
        {
            return alignment == SpriteAlignment.BottomCenter
                ? new Vector2(0.5f, 0f)
                : new Vector2(0.5f, 0.5f);
        }

        /// <summary>Nome de sprite não aceita '.' nem '/'; os arquivos do pacote têm hífen e espaço.</summary>
        private static string SanitizeName(string name)
        {
            return name.Replace(' ', '_').Replace('.', '_').Replace('/', '_');
        }
    }
}
