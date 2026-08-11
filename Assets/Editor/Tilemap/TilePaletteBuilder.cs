using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Warana.EditorTools
{
    /// <summary>
    /// Transforma as folhas de tile já fatiadas pelo <see cref="LegacyFantasyImporter"/> em
    /// assets de <see cref="Tile"/>, monta os autotiles de <see cref="TerrainRuleTileBuilder"/>
    /// e <see cref="WaterTileBuilder"/>, e pinta as Tile Palettes.
    ///
    /// Idempotente e cuidadoso com GUID: assets existentes são gravados por cima, nunca
    /// apagados e recriados — <c>Mapa_01</c> e <c>Prologo</c> referenciam quase 24 mil células
    /// destes tiles, e um GUID novo esvaziaria os dois mapas.
    /// </summary>
    public static class TilePaletteBuilder
    {
        /// <summary>16 px / PPU 64. Bate exato com o assetsPPU da Pixel Perfect Camera.</summary>
        public const float CellSize = (float)LegacyFantasyImporter.TileSize / PixelArtTextureDefaults.PixelsPerUnit;

        private const string TilesRoot = "Assets/Tiles";
        private const string PalettesRoot = "Assets/Palettes";

        /// <summary>Palette só com os autotiles — sem ela eles se perdem no meio de 888 tiles.</summary>
        private const string AutotileSetName = "Autotile";

        private const string DebugSheet =
            LegacyFantasyImporter.RootFolder +
            "/Legacy Fantasy - Debug Map/Legacy Fantasy - Debug Map/Assets/Tiles.png";

        private const string ForestSheet =
            LegacyFantasyImporter.RootFolder +
            "/Legacy-Fantasy - High Forest 2.0/Legacy-Fantasy - High Forest 2.3/Assets/Tiles.png";

        /// <summary>row 1, col 9 do Debug Map: miolo chapado do bloco marrom (índice = row * 26 + col).</summary>
        private const int BrownFillIndex = 35;

        public const string GroundRuleTilePath = TilesRoot + "/DebugMap/RT_Ground.asset";

        /// <summary>Bloco marrom chapado — silhueta de fundo, sem colisão.</summary>
        public static string BrownTilePath => $"{TilesRoot}/DebugMap/Tiles_{BrownFillIndex}.asset";

        /// <summary>Caminho de um asset gerado, para quem precisa carregá-lo de volta.</summary>
        public static string TilePath(string setName, string assetName) =>
            $"{TilesRoot}/{setName}/{assetName}.asset";

        [MenuItem("Waraná/Tilemap/Gerar Tiles e Palettes")]
        public static void BuildAll()
        {
            EnsureFolder(TilesRoot);
            EnsureFolder(PalettesRoot);

            var autotiles = new List<TileBase>();

            int debug = BuildSet(DebugSheet, TerrainBlocks.DebugMapSet, autotiles);
            int forest = BuildSet(ForestSheet, TerrainBlocks.HighForestSet, autotiles);

            BuildPalette(AutotileSetName, null, 0, autotiles);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TilePalette] DebugMap: {debug} tile(s). HighForest: {forest} tile(s). " +
                      $"{autotiles.Count} autotile(s) na palette '{AutotileSetName}'. " +
                      $"Palettes em {PalettesRoot}, célula {CellSize} un.");
        }

        // -------------------------------------------------------------------------- tiles

        /// <summary>
        /// Gera um Tile por sprite da folha, mais os autotiles do set. Devolve quantos Tiles
        /// saíram e acumula os autotiles em <paramref name="autotiles"/>.
        /// </summary>
        private static int BuildSet(string sheetPath, string setName, List<TileBase> autotiles)
        {
            var sprites = LoadSprites(sheetPath);
            if (sprites.Count == 0)
            {
                Debug.LogError($"[TilePalette] '{sheetPath}' não tem sprites fatiados. " +
                               "Rode 'Waraná/Pixel Art/Configurar Legacy Fantasy' antes.");
                return 0;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            int columns = texture.width / LegacyFantasyImporter.TileSize;

            string folder = $"{TilesRoot}/{setName}";
            EnsureFolder(folder);

            var tiles = new Dictionary<int, Tile>(sprites.Count);
            var keep = new HashSet<string>();

            // StartAssetEditing: sem isso cada CreateAsset dispara um import e a coisa leva minutos.
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var pair in sprites)
                {
                    string name = $"Tiles_{pair.Key}";
                    string path = $"{folder}/{name}.asset";
                    keep.Add(path);

                    // Reaproveitar o asset existente é obrigatório, não otimização: apagar e
                    // recriar troca o GUID, e toda cena montada com estes tiles perderia as
                    // referências e apareceria vazia.
                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                    bool isNew = tile == null;
                    if (isNew) tile = ScriptableObject.CreateInstance<Tile>();

                    tile.name = name;
                    tile.sprite = pair.Value;

                    // O default é ColliderType.Sprite, que traça a physics shape do sprite e
                    // gera geometria serrilhada. Grid dá a caixa limpa de 16 px alinhada à célula.
                    tile.colliderType = Tile.ColliderType.Grid;

                    // LockColor (default) impediria tingir o tilemap inteiro por Tilemap.color.
                    tile.flags = TileFlags.None;
                    tile.color = Color.white;

                    if (isNew) AssetDatabase.CreateAsset(tile, path);
                    else EditorUtility.SetDirty(tile);

                    tiles.Add(pair.Key, tile);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            List<TileBase> generated = BuildAutotiles(setName, folder, sprites, columns);

            // O keep sai da MESMA lista que gerou os assets. Manter duas listas em paralelo é
            // como as RuleTiles quase foram apagadas: esquecer um registro aqui faz
            // RemoveStaleAssets levar o asset e o GUID junto.
            foreach (TileBase tile in generated) keep.Add(AssetDatabase.GetAssetPath(tile));
            autotiles.AddRange(generated);

            RemoveStaleAssets(folder, keep);
            BuildPalette(setName, tiles, columns, generated);
            return tiles.Count;
        }

        /// <summary>Mapeia índice de grade -> sprite, lendo o sufixo numérico do nome.</summary>
        private static SortedDictionary<int, Sprite> LoadSprites(string sheetPath)
        {
            var result = new SortedDictionary<int, Sprite>();

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                if (asset is not Sprite sprite) continue;

                int underscore = sprite.name.LastIndexOf('_');
                if (underscore < 0) continue;
                if (!int.TryParse(sprite.name[(underscore + 1)..], out int index)) continue;

                result[index] = sprite;
            }

            return result;
        }

        // ----------------------------------------------------------------------- autotiles

        /// <summary>
        /// Terrenos do catálogo mais, no High Forest, a água. Devolve na ordem em que vão
        /// para a palette.
        /// </summary>
        private static List<TileBase> BuildAutotiles(
            string setName, string folder, SortedDictionary<int, Sprite> sprites, int columns)
        {
            var result = new List<TileBase>();

            foreach (TerrainBlock block in TerrainBlocks.For(setName))
            {
                RuleTile ruleTile = TerrainRuleTileBuilder.Build(
                    block, sprites, columns, $"{folder}/{block.AssetName}.asset");

                if (ruleTile != null) result.Add(ruleTile);
            }

            if (setName == TerrainBlocks.HighForestSet)
            {
                result.AddRange(WaterTileBuilder.Build(folder, sprites));
            }

            return result;
        }

        // ------------------------------------------------------------------------ palette

        /// <summary>
        /// Pinta a palette do set. <paramref name="tiles"/> nulo monta só a faixa de
        /// <paramref name="special"/> — é assim que a palette 'Autotile' é montada.
        /// </summary>
        private static void BuildPalette(
            string setName, Dictionary<int, Tile> tiles, int columns, List<TileBase> special)
        {
            string path = $"{PalettesRoot}/{setName}.prefab";

            // Recriar do zero trocaria o GUID do prefab a cada execução, e a janela Tile
            // Palette perderia a seleção. Só chama CreateNewPalette quando não existe nada.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                GridPaletteUtility.CreateNewPalette(
                    PalettesRoot,
                    setName,
                    GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Manual,
                    new Vector3(CellSize, CellSize, 0f),
                    GridLayout.CellSwizzle.XYZ);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var tilemap = root.GetComponentInChildren<Tilemap>();
                tilemap.ClearAllTiles();

                if (tiles != null)
                {
                    // Mesma disposição da folha: coluna cresce para a direita, linha para baixo.
                    foreach (var pair in tiles)
                    {
                        int col = pair.Key % columns;
                        int row = pair.Key / columns;
                        tilemap.SetTile(new Vector3Int(col, -row, 0), pair.Value);
                    }
                }

                // Os autotiles ficam numa faixa própria acima da folha, senão não haveria
                // como pintar com eles sem arrastar o asset para a janela na mão.
                for (int i = 0; i < special.Count; i++)
                {
                    tilemap.SetTile(new Vector3Int(i, 2, 0), special[i]);
                }

                tilemap.CompressBounds();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // -------------------------------------------------------------------------- pastas

        /// <summary>
        /// Apaga assets que sobraram de uma execução anterior (folha remontada, regra
        /// renomeada). Varre o disco em vez de usar FindAssets("t:TileBase"): o filtro por
        /// tipo depende de como a Unity indexa subclasse de ScriptableObject, e aqui um falso
        /// negativo apaga um asset e o GUID dele.
        /// </summary>
        private static void RemoveStaleAssets(string folder, HashSet<string> keep)
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            string fullFolder = Path.Combine(root, folder);
            if (!Directory.Exists(fullFolder)) return;

            foreach (string file in Directory.GetFiles(fullFolder, "*.asset"))
            {
                string path = $"{folder}/{Path.GetFileName(file)}";
                if (keep.Contains(path)) continue;
                if (AssetDatabase.LoadAssetAtPath<TileBase>(path) == null) continue;

                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
        }
    }
}
