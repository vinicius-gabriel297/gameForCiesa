using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Warana.EditorTools
{
    /// <summary>
    /// Cena de conferência do autotile: pinta, com cada RuleTile, as oito formas que quebram
    /// um nine-slice ingênuo, lado a lado e na mesma tela.
    ///
    /// Existe porque olhar a Palette não prova nada — lá cada tile aparece sozinho, e o que
    /// erra são as combinações: pilar de uma célula de largura, plataforma de uma célula de
    /// altura, célula solta, buraco no meio da massa. Se as oito saem certas aqui, as 16
    /// regras ortogonais de <see cref="TerrainRuleTileBuilder"/> estão completas.
    /// </summary>
    public static class AutoTileSandboxBuilder
    {
        private const string ScenePath = ScratchScene.Folder + "/AutoTile_Sandbox.unity";

        private const string RootName = "Scene";
        private const string GroundLayerName = "Ground";

        /// <summary>16 px / PPU 64 = 0.25. Uma unidade tem 4 células.</summary>
        private const float Cell = TilePaletteBuilder.CellSize;

        /// <summary>Altura de uma faixa em células: 5 de sonda mais 5 de respiro.</summary>
        private const int BandHeight = 10;

        /// <summary>Largura ocupada pelas oito sondas, em células.</summary>
        private const int BandWidth = 52;

        private static readonly Color SkyColor = new Color(0.086f, 0.106f, 0.129f);

        [MenuItem("Waraná/Tilemap/Rascunho/Sandbox de Autotile")]
        public static void Build()
        {
            var terrain = new List<TileBase>();
            var names = new List<string>();

            foreach (string set in new[] { TerrainBlocks.DebugMapSet, TerrainBlocks.HighForestSet })
            {
                foreach (TerrainBlock block in TerrainBlocks.For(set))
                {
                    string path = TilePaletteBuilder.TilePath(set, block.AssetName);
                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile == null) continue;

                    terrain.Add(tile);
                    names.Add(block.AssetName);
                }
            }

            if (terrain.Count == 0)
            {
                Debug.LogError("[Sandbox] Nenhum autotile encontrado. Rode " +
                               "'Waraná/Pixel Art/Configurar Legacy Fantasy' e depois " +
                               "'Waraná/Tilemap/Gerar Tiles e Palettes'.");
                return;
            }

            if (!ScratchScene.CanWrite(ScenePath)) return;

            // NewScene troca a cena aberta sem avisar; pergunta antes para não perder trabalho.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int groundLayer = TestLevelBuilder.EnsureLayer(GroundLayerName);
            ScratchScene.EnsureFolder();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera(terrain.Count);
            BuildGlobalLight();

            var root = new GameObject(RootName);
            var gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(root.transform);
            gridObject.AddComponent<Grid>().cellSize = new Vector3(Cell, Cell, 0f);

            for (int i = 0; i < terrain.Count; i++)
            {
                Tilemap map = AddTilemap(
                    gridObject.transform, $"Sondas_{names[i]}", groundLayer, -10 + i, solid: true);

                PaintProbes(map, terrain[i], new Vector3Int(0, i * BandHeight, 0));
                Finalize(map);
            }

            BuildWater(gridObject.transform, terrain.Count);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[Sandbox] {terrain.Count} autotile(s) em {ScenePath}. " +
                      "Confira, de baixo para cima em cada faixa: bloco, pilar, plataforma, " +
                      "célula solta, buraco, L, T e escada.");
        }

        // -------------------------------------------------------------------------- sondas

        /// <summary>
        /// As oito formas, da esquerda para a direita. Cada uma isola um grupo de regras:
        /// o bloco cobre nine-slice; o pilar exige PillarTop/Mid/Bottom; a plataforma exige
        /// StripLeft/Mid/Right; a célula solta exige Single; o buraco mostraria canto côncavo
        /// se a folha tivesse; L, T e escada misturam tudo.
        /// </summary>
        private static void PaintProbes(Tilemap map, TileBase tile, Vector3Int origin)
        {
            var cells = new HashSet<Vector2Int>();

            void Fill(int x0, int y0, int width, int height)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++) cells.Add(new Vector2Int(x0 + x, y0 + y));
                }
            }

            Fill(0, 0, 5, 5);    // bloco
            Fill(7, 0, 1, 5);    // pilar de uma célula de largura
            Fill(10, 0, 5, 1);   // plataforma de uma célula de altura
            Fill(17, 0, 1, 1);   // célula solta

            Fill(20, 0, 5, 5);   // bloco com buraco de uma célula
            cells.Remove(new Vector2Int(22, 2));

            Fill(27, 0, 2, 5);   // L
            Fill(29, 0, 3, 2);

            Fill(34, 3, 7, 2);   // T
            Fill(37, 0, 1, 3);

            for (int step = 0; step < 5; step++) Fill(43 + step, 0, 1, step + 1); // escada

            Paint(map, tile, cells, origin);
        }

        private static void BuildWater(Transform parent, int bands)
        {
            var water = AssetDatabase.LoadAssetAtPath<TileBase>(
                TilePaletteBuilder.TilePath(TerrainBlocks.HighForestSet, "RT_Forest_Water"));
            var foam = AssetDatabase.LoadAssetAtPath<TileBase>(
                TilePaletteBuilder.TilePath(TerrainBlocks.HighForestSet, "AT_Water_Foam"));

            if (water == null) return;

            var origin = new Vector3Int(0, bands * BandHeight, 0);

            // Sem colisão nas duas: água é para atravessar.
            Tilemap pool = AddTilemap(parent, "Sondas_Agua", 0, 5, solid: false);

            var cells = new HashSet<Vector2Int>();
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 4; y++) cells.Add(new Vector2Int(x, y));
            }

            Paint(pool, water, cells, origin);
            pool.CompressBounds();

            if (foam == null) return;

            // A espuma mora na célula ACIMA da poça — na folha ela é 3/4 transparente.
            Tilemap foamMap = AddTilemap(parent, "Sondas_Agua_Espuma", 0, 6, solid: false);

            var foamCells = new HashSet<Vector2Int>();
            for (int x = 0; x < 8; x++) foamCells.Add(new Vector2Int(x, 4));

            Paint(foamMap, foam, foamCells, origin);
            foamMap.CompressBounds();
        }

        // ------------------------------------------------------------------------ tilemaps

        private static Tilemap AddTilemap(
            Transform parent, string name, int layer, int sortingOrder, bool solid)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;

            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;

            if (!solid) return tilemap;

            // O Rigidbody2D vem primeiro de propósito: AddComponent<CompositeCollider2D>
            // satisfaz o [RequireComponent] criando um corpo Dynamic sozinho.
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;
            composite.edgeRadius = 0f;
            composite.offsetDistance = 0f;
            composite.useDelaunayMesh = true;

            var collider = go.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
            collider.extrusionFactor = 0.0001f;
            collider.useDelaunayMesh = true;

            return tilemap;
        }

        /// <summary>
        /// Pinta num único SetTilesBlock. O array é x-major com origem em bounds.min, e o
        /// size.z tem que ser 1 — com 0 a chamada é silenciosamente ignorada.
        /// </summary>
        private static void Paint(
            Tilemap tilemap, TileBase tile, HashSet<Vector2Int> cells, Vector3Int origin)
        {
            var bounds = new BoundsInt(origin.x, origin.y, 0, BandWidth, BandHeight, 1);
            var tiles = new TileBase[bounds.size.x * bounds.size.y];

            foreach (Vector2Int cell in cells)
            {
                int x = cell.x;
                int y = cell.y;
                if (x < 0 || y < 0 || x >= bounds.size.x || y >= bounds.size.y) continue;

                tiles[x + y * bounds.size.x] = tile;
            }

            tilemap.SetTilesBlock(bounds, tiles);
        }

        private static void Finalize(Tilemap tilemap)
        {
            tilemap.CompressBounds();

            var collider = tilemap.GetComponent<TilemapCollider2D>();
            var composite = tilemap.GetComponent<CompositeCollider2D>();

            collider.ProcessTilemapChanges(); // força o collider a consumir as mudanças pendentes
            composite.GenerateGeometry();
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        }

        // -------------------------------------------------------------------------- cenário

        private static void BuildCamera(int bands)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;

            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<AudioListener>();

            // Enquadra as faixas todas de uma vez: é para isso que a cena existe.
            float height = (bands + 1) * BandHeight * Cell;
            camera.orthographicSize = height * 0.6f;
            go.transform.position = new Vector3(BandWidth * Cell * 0.5f, height * 0.5f, -10f);
        }

        private static void BuildGlobalLight()
        {
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }
    }
}
