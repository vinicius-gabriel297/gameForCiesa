using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Warana.Companion;

namespace Warana.EditorTools
{
    /// <summary>
    /// Monta um mapa greybox de 90x30 unidades em Tilemap, inspirado na abertura de Ori:
    /// campo aberto e largo, poucas plataformas, uma bacia no meio para descer e subir.
    ///
    /// <b>Rascunho.</b> A cena que este builder gerava virou o <c>Prologo</c> e foi repintada
    /// à mão desde então; a saída fica em <see cref="ScratchScene.Folder"/> e
    /// <see cref="ScratchScene.CanWrite"/> recusa qualquer cena do Build Settings. Para
    /// conferir autotile, prefira <see cref="AutoTileSandboxBuilder"/>, que isola as formas
    /// difíceis em vez de espalhá-las por um mapa inteiro.
    /// </summary>
    public static class TilemapTestBuilder
    {
        public const string ScenePath = ScratchScene.Folder + "/TilemapTest.unity";

        private const string RootName = "Scene";
        private const string GroundLayerName = "Ground";
        private const string EyeFramesFolder = "Assets/Animations/eyes";

        /// <summary>16 px / PPU 64 = 0.25. Uma unidade tem 4 tiles.</summary>
        private const float Cell = TilePaletteBuilder.CellSize;

        private const int TilesPerUnit = (int)(1f / Cell);

        // Extensão do mapa em tiles. 360 x 120 = 90 x 30 unidades.
        private const int MinX = -45 * TilesPerUnit;
        private const int MaxX = 45 * TilesPerUnit;
        private const int MinY = -6 * TilesPerUnit;
        private const int MaxY = 24 * TilesPerUnit;

        private static readonly Vector3 SpawnPosition = new Vector3(-40f, 1f, 0f);
        private static readonly Vector3 EyePosition = new Vector3(-38.5f, 2.5f, 0f);
        private static readonly Color SkyColor = new Color(0.086f, 0.106f, 0.129f);

        /// <summary>Marrom chapado atrás do campo: só silhueta, mais escuro que o chão cinza.</summary>
        private static readonly Color DecoTint = new Color(0.55f, 0.55f, 0.6f);

        /// <summary>
        /// Perfil do terreno: (x inicial, x final, topo), em unidades de mundo, da esquerda
        /// para a direita. O chão é preenchido daí para baixo até o fundo do mapa.
        /// </summary>
        private static readonly (float from, float to, float top)[] GroundProfile =
        {
            (-45f, -30f,  0.00f), // clareira inicial: plana e vazia, o spawn fica aqui
            (-30f, -27f,  0.75f),
            (-27f, -24f,  1.50f),
            (-24f, -16f,  2.25f), // platô de abertura
            (-16f, -14f,  1.00f),
            (-14f,  -4f,  0.00f), // campo aberto
            ( -4f,  -3f, -1.00f),
            ( -3f,   3f, -2.00f), // bacia: desce e sobe
            (  3f,   4f, -1.00f),
            (  4f,  34f,  0.00f), // corrida longa, ~3 telas
            ( 34f,  45f,  4.50f), // platô final
        };

        /// <summary>
        /// Plataformas flutuantes: (centro x, topo y, largura). Poucas e espaçadas.
        /// Toda subida cabe no jumpHeight 2.0 do PlayerController2D e todo vão horizontal
        /// fica abaixo do alcance de ~3 unidades do pulo.
        /// </summary>
        private static readonly (float x, float top, float width)[] Platforms =
        {
            (-20.5f,  4.00f, 3.0f), // +1.75 a partir do platô
            ( -8.0f,  1.75f, 4.0f), // única no campo aberto
            ( -1.0f, -0.25f, 4.0f), // saída da bacia
            ( 10.0f,  1.75f, 3.0f),
            ( 16.0f,  1.50f, 3.0f),
            ( 24.0f,  1.75f, 3.5f), // início da escada para o platô final
            ( 28.0f,  3.25f, 3.0f),
            ( 31.5f,  4.50f, 3.0f), // encosta no platô de x = 34
        };

        private const float PlatformThickness = 0.5f;

        /// <summary>Pilares de fundo: (centro x, largura, altura acima do chão).</summary>
        private static readonly (float x, float width, float height)[] Pillars =
        {
            (-36f, 1.50f,  9f),
            (-26f, 1.25f,  6f),
            ( -6f, 1.75f, 11f),
            (  8f, 1.25f,  7f),
            ( 19f, 1.50f, 10f),
            ( 29f, 1.25f,  6f),
            ( 41f, 1.75f,  8f),
        };

        [MenuItem("Waraná/Tilemap/Rascunho/Build Mapa de Teste")]
        public static void Build()
        {
            var ground = AssetDatabase.LoadAssetAtPath<TileBase>(TilePaletteBuilder.GroundRuleTilePath);
            var deco = AssetDatabase.LoadAssetAtPath<TileBase>(TilePaletteBuilder.BrownTilePath);

            if (ground == null || deco == null)
            {
                Debug.LogError("[TilemapTest] Tiles não encontrados. Rode " +
                               "'Waraná/Pixel Art/Configurar Legacy Fantasy' e depois " +
                               "'Waraná/Tilemap/Gerar Tiles e Palettes'.");
                return;
            }

            if (!ScratchScene.CanWrite(ScenePath)) return;

            // NewScene troca a cena aberta sem avisar; pergunta antes para não perder trabalho.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ScratchScene.EnsureFolder();

            int groundLayer = TestLevelBuilder.EnsureLayer(GroundLayerName);
            PhysicsMaterial2D noFriction = TestLevelBuilder.EnsureNoFrictionMaterial();
            AnimatorController animatorController = WaranaAnimationBuilder.Build();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildGlobalLight();

            var root = new GameObject(RootName);

            BuildGrid(root.transform, groundLayer, ground, deco);

            GameObject player = TestLevelBuilder.BuildPlayer(
                root.transform, noFriction, groundLayer, animatorController);
            player.transform.position = SpawnPosition;

            BuildGuaranaEye(root.transform, player.transform);
            TestLevelBuilder.SetupCamera(player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeGameObject = player;
            Debug.Log($"[TilemapTest] Mapa de {(MaxX - MinX) / TilesPerUnit}x{(MaxY - MinY) / TilesPerUnit} " +
                      $"unidades montado em {ScenePath}. Aperte Play.");
        }

        // ----------------------------------------------------------------------- tilemaps

        private static void BuildGrid(Transform parent, int groundLayer, TileBase ground, TileBase deco)
        {
            var gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(parent);

            var grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(Cell, Cell, 0f);

            // Deco primeiro e sem colisão: é só silhueta atrás do campo.
            Tilemap decoMap = AddTilemap(gridObject.transform, "Tilemap_Deco", 0, -20, solid: false);
            decoMap.color = DecoTint;
            Paint(decoMap, deco, DecoCells());
            decoMap.CompressBounds(); // sem isso os chunks de render ficam do tamanho do mapa todo

            Tilemap groundMap = AddTilemap(gridObject.transform, "Tilemap_Ground", groundLayer, -10, solid: true);
            Paint(groundMap, ground, GroundCells());

            Tilemap platformMap = AddTilemap(gridObject.transform, "Tilemap_Platforms", groundLayer, -9, solid: true);
            Paint(platformMap, ground, PlatformCells());

            Finalize(groundMap);
            Finalize(platformMap);
        }

        private static Tilemap AddTilemap(Transform parent, string name, int layer, int sortingOrder, bool solid)
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
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons; // Outlines é oco: a cápsula tunela
            composite.generationType = CompositeCollider2D.GenerationType.Manual; // um rebuild só, no fim
            composite.edgeRadius = 0f;      // qualquer raio tira a superfície da grade de pixels
            composite.offsetDistance = 0f;
            composite.useDelaunayMesh = true;

            var collider = go.AddComponent<TilemapCollider2D>();

            // Merge dissolve as arestas verticais entre os boxes de cada tile. Sem isso a
            // CapsuleCollider2D do Player engancha nas emendas ao correr em linha reta.
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
            collider.extrusionFactor = 0.0001f; // fecha as fendas capilares que sobram do merge
            collider.useDelaunayMesh = true;
            collider.maximumTileChangeCount = 4000;

            return tilemap;
        }

        /// <summary>
        /// Pinta num único SetTilesBlock. O array é x-major com origem em bounds.min, e o
        /// size.z tem que ser 1 — com 0 a chamada é silenciosamente ignorada.
        /// </summary>
        private static void Paint(Tilemap tilemap, TileBase tile, HashSet<Vector2Int> cells)
        {
            var bounds = new BoundsInt(MinX, MinY, 0, MaxX - MinX, MaxY - MinY, 1);
            var tiles = new TileBase[bounds.size.x * bounds.size.y];

            foreach (Vector2Int cell in cells)
            {
                int x = cell.x - MinX;
                int y = cell.y - MinY;
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

        // -------------------------------------------------------------------------- mapa

        private static HashSet<Vector2Int> GroundCells()
        {
            var cells = new HashSet<Vector2Int>();

            foreach ((float from, float to, float top) in GroundProfile)
            {
                FillRect(cells, from, to, MinY * Cell, top);
            }

            // Paredes das duas pontas, do fundo até o teto do mapa.
            FillRect(cells, -45f, -44f, MinY * Cell, MaxY * Cell);
            FillRect(cells, 44f, 45f, MinY * Cell, MaxY * Cell);

            return cells;
        }

        private static HashSet<Vector2Int> PlatformCells()
        {
            var cells = new HashSet<Vector2Int>();

            foreach ((float x, float top, float width) in Platforms)
            {
                FillRect(cells, x - width * 0.5f, x + width * 0.5f, top - PlatformThickness, top);
            }

            return cells;
        }

        private static HashSet<Vector2Int> DecoCells()
        {
            var cells = new HashSet<Vector2Int>();

            foreach ((float x, float width, float height) in Pillars)
            {
                float baseY = SurfaceAt(x);
                FillRect(cells, x - width * 0.5f, x + width * 0.5f, baseY - 1f, baseY + height);
            }

            return cells;
        }

        /// <summary>Topo do terreno em uma coordenada x, em unidades de mundo.</summary>
        private static float SurfaceAt(float x)
        {
            foreach ((float from, float to, float top) in GroundProfile)
            {
                if (x >= from && x < to) return top;
            }

            return 0f;
        }

        /// <summary>Retângulo em unidades de mundo -> células. O topo é exclusivo.</summary>
        private static void FillRect(HashSet<Vector2Int> cells, float left, float right, float bottom, float top)
        {
            int x0 = Mathf.RoundToInt(left * TilesPerUnit);
            int x1 = Mathf.RoundToInt(right * TilesPerUnit);
            int y0 = Mathf.RoundToInt(bottom * TilesPerUnit);
            int y1 = Mathf.RoundToInt(top * TilesPerUnit);

            for (int x = x0; x < x1; x++)
            {
                for (int y = y0; y < y1; y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
        }

        // ------------------------------------------------------------------------ cena

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };

            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>();

            go.transform.position = new Vector3(SpawnPosition.x, SpawnPosition.y, -10f);
        }

        private static void BuildGlobalLight()
        {
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        private static void BuildGuaranaEye(Transform parent, Transform player)
        {
            var go = new GameObject("Guarana Eye");
            go.transform.SetParent(parent);
            go.transform.position = EyePosition;

            var renderer = go.AddComponent<SpriteRenderer>();
            var eye = go.AddComponent<GuaranaEye>();

            var so = new SerializedObject(eye);
            so.FindProperty("target").objectReferenceValue = player;

            SerializedProperty frames = so.FindProperty("frames");
            Sprite[] eyeFrames = LoadEyeFrames();
            frames.arraySize = eyeFrames.Length;
            for (int i = 0; i < eyeFrames.Length; i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue = eyeFrames[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // O OnEnable do componente já rodou quando o AddComponent voltou, antes de existir
            // qualquer quadro — sem isto o orbe fica sem sprite até entrar em Play.
            if (eyeFrames.Length > 0) renderer.sprite = eyeFrames[0];
            renderer.sortingOrder = 10;
        }

        /// <summary>
        /// Quadros do olho, do TOTALMENTE ABERTO ao TOTALMENTE FECHADO — é a ordem que o
        /// <see cref="GuaranaEye"/> espera, e o índice 0 é o estado de repouso.
        ///
        /// Os arquivos são numerados ao contrário disso: eyes_04 é o olho aberto e eyes_01 o
        /// fechado. Ordenar pelo nome crescente deixaria o orbe de olho fechado o tempo todo,
        /// piscando para abrir.
        /// </summary>
        private static Sprite[] LoadEyeFrames()
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { EyeFramesFolder });
            var paths = new List<string>(guids.Length);

            foreach (string guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            paths.Reverse();

            var sprites = new List<Sprite>(paths.Count);
            foreach (string path in paths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) sprites.Add(sprite);
            }

            return sprites.ToArray();
        }
    }
}
