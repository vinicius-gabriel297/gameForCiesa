using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Warana.EditorTools
{
    /// <summary>
    /// Transforma as folhas de tile já fatiadas pelo <see cref="LegacyFantasyImporter"/> em
    /// assets de <see cref="Tile"/> e monta as Tile Palettes.
    ///
    /// Idempotente: apaga e regera as pastas de saída.
    /// </summary>
    public static class TilePaletteBuilder
    {
        /// <summary>16 px / PPU 64. Bate exato com o assetsPPU da Pixel Perfect Camera.</summary>
        public const float CellSize = (float)LegacyFantasyImporter.TileSize / PixelArtTextureDefaults.PixelsPerUnit;

        private const string TilesRoot = "Assets/Tiles";
        private const string PalettesRoot = "Assets/Palettes";

        private const string DebugSheet =
            LegacyFantasyImporter.RootFolder +
            "/Legacy Fantasy - Debug Map/Legacy Fantasy - Debug Map/Assets/Tiles.png";

        private const string ForestSheet =
            LegacyFantasyImporter.RootFolder +
            "/Legacy-Fantasy - High Forest 2.0/Legacy-Fantasy - High Forest 2.3/Assets/Tiles.png";

        // Células escolhidas à mão na folha do Debug Map (índice = row * 26 + col, row 0 = topo).
        // O bloco cinza de 64x64 é chapado com um contorno escuro de 1 px só no topo, e a
        // célula (0,0) tem o texto "64" desenhado — daí pegarmos a coluna 1.
        private const int GreyEdgeIndex = 1;   // row 0, col 1: linha escura em cima + cinza embaixo
        private const int GreyFillIndex = 27;  // row 1, col 1: cinza chapado
        private const int BrownFillIndex = 35; // row 1, col 9: mesma coisa no bloco marrom

        public const string GroundRuleTilePath = TilesRoot + "/DebugMap/RT_Ground.asset";

        /// <summary>Bloco marrom chapado — silhueta de fundo, sem colisão.</summary>
        public static string BrownTilePath => $"{TilesRoot}/DebugMap/Tiles_{BrownFillIndex}.asset";

        [MenuItem("Waraná/Tilemap/Gerar Tiles e Palettes")]
        public static void BuildAll()
        {
            EnsureFolder(TilesRoot);
            EnsureFolder(PalettesRoot);

            int debug = BuildSet(DebugSheet, "DebugMap");
            int forest = BuildSet(ForestSheet, "HighForest");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TilePalette] DebugMap: {debug} tile(s). HighForest: {forest} tile(s). " +
                      $"Palettes em {PalettesRoot}, célula {CellSize} un.");
        }

        // -------------------------------------------------------------------------- tiles

        /// <summary>Gera um Tile por sprite da folha e pinta a palette. Devolve quantos saíram.</summary>
        private static int BuildSet(string sheetPath, string setName)
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

            List<TileBase> ruleTiles = BuildRuleTiles(setName, folder, sprites, columns);
            foreach (TileBase ruleTile in ruleTiles) keep.Add(AssetDatabase.GetAssetPath(ruleTile));

            RemoveStaleAssets(folder, keep);
            BuildPalette(setName, tiles, columns, ruleTiles);
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

        // ------------------------------------------------------------------------ palette

        private static void BuildPalette(
            string setName, Dictionary<int, Tile> tiles, int columns, List<TileBase> ruleTiles)
        {
            string path = $"{PalettesRoot}/{setName}.prefab";

            // CreateNewPalette usa GenerateUniqueAssetPath: sem apagar antes viraria "DebugMap 1".
            AssetDatabase.DeleteAsset(path);

            GridPaletteUtility.CreateNewPalette(
                PalettesRoot,
                setName,
                GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Manual,
                new Vector3(CellSize, CellSize, 0f),
                GridLayout.CellSwizzle.XYZ);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var tilemap = root.GetComponentInChildren<Tilemap>();

                // Mesma disposição da folha: coluna cresce para a direita, linha para baixo.
                foreach (var pair in tiles)
                {
                    int col = pair.Key % columns;
                    int row = pair.Key / columns;
                    tilemap.SetTile(new Vector3Int(col, -row, 0), pair.Value);
                }

                // As RuleTiles ficam numa faixa própria acima da folha, senão não haveria
                // como pintar com elas sem arrastar o asset para a janela na mão.
                for (int i = 0; i < ruleTiles.Count; i++)
                {
                    tilemap.SetTile(new Vector3Int(i, 2, 0), ruleTiles[i]);
                }

                tilemap.CompressBounds();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ----------------------------------------------------------------------- ruletile

        /// <summary>Gera as RuleTiles de cada folha. Devolve-as na ordem em que vão para a palette.</summary>
        private static List<TileBase> BuildRuleTiles(
            string setName, string folder, SortedDictionary<int, Sprite> sprites, int columns)
        {
            var result = new List<TileBase>();

            if (setName == "DebugMap")
            {
                result.Add(BuildGreyboxRuleTile(folder, sprites));
            }
            else if (setName == "HighForest")
            {
                // Os dois blocos de terreno da folha têm o mesmo desenho 5x4: uma coluna de
                // borda de cada lado, uma linha de topo e uma de base, e o miolo no meio.
                result.Add(BuildNineSliceRuleTile(
                    $"{folder}/RT_Forest_Grass.asset", sprites, columns,
                    colLeft: 0, colRight: 4, rowTop: 1, rowBottom: 4));

                result.Add(BuildNineSliceRuleTile(
                    $"{folder}/RT_Forest_Rock.asset", sprites, columns,
                    colLeft: 0, colRight: 4, rowTop: 6, rowBottom: 9));
            }

            result.RemoveAll(tile => tile == null);
            return result;
        }

        /// <summary>
        /// RuleTile do greybox: uma única regra — se não tem vizinho em cima, usa a célula com
        /// a linha escura; senão, cinza chapado. O pacote não traz blob de autotile (o bloco é
        /// chapado, com contorno só no topo e na esquerda), então dá para fazer só a superfície —
        /// que é justamente o que precisa ficar legível num teste de plataforma.
        /// </summary>
        private static TileBase BuildGreyboxRuleTile(string folder, SortedDictionary<int, Sprite> sprites)
        {
            if (!sprites.TryGetValue(GreyEdgeIndex, out Sprite edge) ||
                !sprites.TryGetValue(GreyFillIndex, out Sprite fill))
            {
                Debug.LogError("[TilePalette] Não achei as células cinza do Debug Map para a RuleTile.");
                return null;
            }

            var ruleTile = ScriptableObject.CreateInstance<RuleTile>();
            ruleTile.m_DefaultSprite = fill;
            ruleTile.m_DefaultColliderType = Tile.ColliderType.Grid;
            ruleTile.m_TilingRules = new List<RuleTile.TilingRule>
            {
                Rule(new[] { edge }, (Vector3Int.up, false)),
            };

            return Save(ruleTile, folder + "/RT_Ground.asset");
        }

        /// <summary>
        /// Nine-slice a partir de um bloco retangular da folha: as quatro quinas, as quatro
        /// bordas e o miolo. As variantes do meio de cada faixa entram como saída aleatória,
        /// então uma parede longa não fica com a mesma pedra repetida.
        ///
        /// A ordem das regras importa — a primeira que casar vence. As de topo vêm antes das
        /// de base de propósito: numa plataforma de um tile de espessura os dois lados estão
        /// expostos, e o que tem que aparecer é a grama.
        /// </summary>
        private static TileBase BuildNineSliceRuleTile(
            string path, SortedDictionary<int, Sprite> sprites, int columns,
            int colLeft, int colRight, int rowTop, int rowBottom)
        {
            Sprite[] Cells(int rowFrom, int rowTo, int colFrom, int colTo)
            {
                var found = new List<Sprite>();
                for (int row = rowFrom; row <= rowTo; row++)
                {
                    for (int col = colFrom; col <= colTo; col++)
                    {
                        if (sprites.TryGetValue(row * columns + col, out Sprite sprite)) found.Add(sprite);
                    }
                }

                return found.ToArray();
            }

            int midColFrom = colLeft + 1, midColTo = colRight - 1;
            int midRowFrom = rowTop + 1, midRowTo = rowBottom - 1;

            Sprite[] topLeft = Cells(rowTop, rowTop, colLeft, colLeft);
            Sprite[] top = Cells(rowTop, rowTop, midColFrom, midColTo);
            Sprite[] topRight = Cells(rowTop, rowTop, colRight, colRight);
            Sprite[] left = Cells(midRowFrom, midRowTo, colLeft, colLeft);
            Sprite[] center = Cells(midRowFrom, midRowTo, midColFrom, midColTo);
            Sprite[] right = Cells(midRowFrom, midRowTo, colRight, colRight);
            Sprite[] bottomLeft = Cells(rowBottom, rowBottom, colLeft, colLeft);
            Sprite[] bottom = Cells(rowBottom, rowBottom, midColFrom, midColTo);
            Sprite[] bottomRight = Cells(rowBottom, rowBottom, colRight, colRight);

            if (center.Length == 0)
            {
                Debug.LogError($"[TilePalette] Bloco de terreno vazio para '{path}'.");
                return null;
            }

            var ruleTile = ScriptableObject.CreateInstance<RuleTile>();
            ruleTile.m_DefaultSprite = center[0];
            ruleTile.m_DefaultColliderType = Tile.ColliderType.Grid;
            ruleTile.m_TilingRules = new List<RuleTile.TilingRule>
            {
                Rule(topLeft,     (Vector3Int.up, false),   (Vector3Int.left, false)),
                Rule(topRight,    (Vector3Int.up, false),   (Vector3Int.right, false)),
                Rule(top,         (Vector3Int.up, false)),
                Rule(bottomLeft,  (Vector3Int.down, false), (Vector3Int.left, false)),
                Rule(bottomRight, (Vector3Int.down, false), (Vector3Int.right, false)),
                Rule(bottom,      (Vector3Int.down, false)),
                Rule(left,        (Vector3Int.left, false)),
                Rule(right,       (Vector3Int.right, false)),
                Rule(center), // sem vizinho declarado: casa com tudo que sobrou
            };

            return Save(ruleTile, path);
        }

        /// <summary>
        /// Uma regra do RuleTile. Cada vizinho é (direção, precisa ser do mesmo tile).
        /// Com mais de um sprite a saída vira aleatória, sem rotacionar nem espelhar —
        /// terreno espelhado ficaria com a borda para o lado errado.
        /// </summary>
        private static RuleTile.TilingRule Rule(Sprite[] variants, params (Vector3Int dir, bool isThis)[] neighbors)
        {
            var rule = new RuleTile.TilingRule
            {
                m_Sprites = variants,
                m_ColliderType = Tile.ColliderType.Grid,
                m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_RandomTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_Output = variants.Length > 1
                    ? RuleTile.TilingRuleOutput.OutputSprite.Random
                    : RuleTile.TilingRuleOutput.OutputSprite.Single,
                m_NeighborPositions = new List<Vector3Int>(neighbors.Length),
                m_Neighbors = new List<int>(neighbors.Length),
            };

            foreach ((Vector3Int dir, bool isThis) in neighbors)
            {
                rule.m_NeighborPositions.Add(dir);
                rule.m_Neighbors.Add(isThis
                    ? RuleTile.TilingRule.Neighbor.This
                    : RuleTile.TilingRule.Neighbor.NotThis);
            }

            return rule;
        }

        /// <summary>
        /// Grava por cima do asset existente em vez de apagar e recriar — o GUID tem que
        /// sobreviver, senão as cenas pintadas com esta RuleTile ficam vazias.
        /// </summary>
        private static TileBase Save(RuleTile ruleTile, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(ruleTile, path);
                return ruleTile;
            }

            EditorUtility.CopySerialized(ruleTile, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(ruleTile);
            return existing;
        }

        /// <summary>Apaga assets que sobraram de uma execução anterior (folha remontada, regra renomeada).</summary>
        private static void RemoveStaleAssets(string folder, HashSet<string> keep)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TileBase", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!keep.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        // ------------------------------------------------------------------------- pastas

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
        }

    }
}
