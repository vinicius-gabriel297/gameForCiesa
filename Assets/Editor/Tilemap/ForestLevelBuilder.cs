using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Warana.Environment;
using Warana.Player;

namespace Warana.EditorTools
{
    /// <summary>
    /// Monta a Fase 01 — a floresta de abertura. Reskin do mapa de teste com os tiles do
    /// High Forest, relevo suave e caminhável (nada no caminho principal exige pular, mas o
    /// pulo continua ligado), Player em modo caminhada, SEM ataque e SEM o Guaraná Eye.
    /// Fundo em duas camadas de parallax (céu + linha de árvores).
    ///
    /// <b>Isto é rascunho, não a fase de verdade.</b> A cena que este builder gerava foi
    /// renomeada para <c>Mapa_01</c> e desde então recebeu mais de 15 mil células pintadas à
    /// mão, camadas novas e todo o encontro com a Abomination. Reconstruir do zero por cima
    /// dela apagaria esse trabalho, então a saída fica em <see cref="ScratchScene.Folder"/> e
    /// <see cref="ScratchScene.CanWrite"/> recusa qualquer cena do Build Settings. O que serve
    /// aqui é o esqueleto: relevo de partida e parallax para começar uma fase nova.
    /// </summary>
    public static class ForestLevelBuilder
    {
        public const string ScenePath = ScratchScene.Folder + "/Fase01_Floresta.unity";

        private const string RootName = "Scene";
        private const string GroundLayerName = "Ground";
        private const string ForestGroundTilePath = "Assets/Tiles/HighForest/RT_Forest_Grass.asset";

        private const string HighForestRoot =
            "Assets/Legacy_Fantasy Assets/Legacy-Fantasy - High Forest 2.0/Legacy-Fantasy - High Forest 2.3";
        private const string SkySpritePath = HighForestRoot + "/Background/Background.png";
        private const string TreeLineSpritePath = HighForestRoot + "/Trees/Background.png";

        /// <summary>16 px / PPU 64 = 0.25. Uma unidade tem 4 tiles.</summary>
        private const float Cell = TilePaletteBuilder.CellSize;

        private const int TilesPerUnit = (int)(1f / Cell);

        // Extensão do mapa em tiles. 256 x 96 = 64 x 24 unidades.
        private const int MinX = -32 * TilesPerUnit;
        private const int MaxX = 32 * TilesPerUnit;
        private const int MinY = -6 * TilesPerUnit;
        private const int MaxY = 18 * TilesPerUnit;

        /// <summary>A câmera lógica (640x360 @ PPU 64) mostra 10 x 5,625 unidades.</summary>
        private const float ViewHeight = (float)PixelArtTextureDefaults.ReferenceHeight
                                         / PixelArtTextureDefaults.PixelsPerUnit;

        private static readonly Vector3 SpawnPosition = new Vector3(-28f, 1f, 0f);

        /// <summary>Verde-azulado suave da floresta, atrás do sprite de céu (fallback).</summary>
        private static readonly Color SkyColor = new Color(0.44f, 0.62f, 0.58f);

        /// <summary>
        /// Perfil do terreno, da esquerda para a direita: (x inicial, x final, topo) em
        /// unidades. Só descidas livres e subidas em degraus de 0,25 — a cápsula do Player
        /// sobe andando, sem depender do pulo.
        /// </summary>
        private static readonly (float from, float to, float top)[] GroundProfile =
        {
            (-32f, -18f,  0.00f), // clareira do spawn: plana e larga
            (-18f,  -8f,  0.00f), // campo aberto
            ( -8f,  -6f, -0.25f), // desce em degrau raso
            ( -6f,   4f, -0.50f), // depressão suave: desce e sobe andando
            (  4f,   6f, -0.25f),
            (  6f,  22f,  0.00f), // volta ao nível, reta longa
            ( 22f,  24f,  0.25f), // escadinha de subida
            ( 24f,  26f,  0.50f),
            ( 26f,  32f,  0.75f), // platô final, um pouco mais alto
        };

        /// <summary>
        /// Plataformas baixas e opcionais: (centro x, topo y, largura). Ficam sobre o chão
        /// plano e ao alcance do pulo (jumpHeight 2.0) — decoram e convidam a explorar, mas o
        /// caminho continua embaixo, então nada obriga a pular.
        /// </summary>
        private static readonly (float x, float top, float width)[] Platforms =
        {
            (-12f, 1.50f, 3.0f),
            (  0f, 1.25f, 3.0f),
            ( 12f, 1.50f, 3.0f),
        };

        private const float PlatformThickness = 0.5f;

        [MenuItem("Waraná/Tilemap/Rascunho/Build Fase 01 (Floresta)")]
        public static void Build()
        {
            var ground = AssetDatabase.LoadAssetAtPath<TileBase>(ForestGroundTilePath);
            if (ground == null)
            {
                Debug.LogError("[Fase01] RuleTile do High Forest não encontrada em " +
                               $"'{ForestGroundTilePath}'. Rode 'Waraná/Pixel Art/Configurar " +
                               "Legacy Fantasy' e depois 'Waraná/Tilemap/Gerar Tiles e Palettes'.");
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

            BuildParallax(root.transform);
            BuildGrid(root.transform, groundLayer, ground);

            GameObject player = BuildWalkingPlayer(root.transform, noFriction, groundLayer, animatorController);
            TestLevelBuilder.SetupCamera(player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeGameObject = player;
            Debug.Log($"[Fase01] Floresta de {(MaxX - MinX) / TilesPerUnit}x{(MaxY - MinY) / TilesPerUnit} " +
                      $"unidades montada em {ScenePath}. Aperte Play.");
        }

        // ------------------------------------------------------------------------- player

        /// <summary>
        /// Player de caminhada: o mesmo recheio do <see cref="TestLevelBuilder.BuildPlayer"/>,
        /// mas sem o <see cref="PlayerAttack"/> e com o <see cref="PlayerAnimator"/> em modo
        /// Walk (usa o clip de caminhada em vez do de corrida).
        /// </summary>
        private static GameObject BuildWalkingPlayer(
            Transform parent, PhysicsMaterial2D material, int groundLayer, AnimatorController animatorController)
        {
            GameObject player = TestLevelBuilder.BuildPlayer(parent, material, groundLayer, animatorController);
            player.transform.position = SpawnPosition;

            // Sem combate nesta fase: o controller descobre habilidades em runtime e o
            // PlayerAnimator faz null-guard do evento, então remover é limpo.
            var attack = player.GetComponent<PlayerAttack>();
            if (attack != null) Object.DestroyImmediate(attack);

            // Locomoção em modo caminhada: liga o bool 'walkMode' do PlayerAnimator.
            var animator = player.GetComponent<PlayerAnimator>();
            if (animator != null)
            {
                var so = new SerializedObject(animator);
                so.FindProperty("walkMode").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return player;
        }

        // ----------------------------------------------------------------------- tilemaps

        private static void BuildGrid(Transform parent, int groundLayer, TileBase ground)
        {
            var gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(parent);

            var grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(Cell, Cell, 0f);

            Tilemap groundMap = AddTilemap(gridObject.transform, "Tilemap_Ground", groundLayer, -10);
            Paint(groundMap, ground, GroundCells());

            Tilemap platformMap = AddTilemap(gridObject.transform, "Tilemap_Platforms", groundLayer, -9);
            Paint(platformMap, ground, PlatformCells());

            Finalize(groundMap);
            Finalize(platformMap);
        }

        private static Tilemap AddTilemap(Transform parent, string name, int layer, int sortingOrder)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;

            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;

            // Rigidbody2D primeiro: AddComponent<CompositeCollider2D> satisfaz o
            // [RequireComponent] criando um corpo Dynamic sozinho, e Static é o que queremos.
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
            collider.maximumTileChangeCount = 4000;

            return tilemap;
        }

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

            collider.ProcessTilemapChanges();
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
            FillRect(cells, -32f, -31f, MinY * Cell, MaxY * Cell);
            FillRect(cells, 31f, 32f, MinY * Cell, MaxY * Cell);

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

        // ---------------------------------------------------------------------- parallax

        private static void BuildParallax(Transform parent)
        {
            var root = new GameObject("Parallax");
            root.transform.SetParent(parent);

            var sky = AssetDatabase.LoadAssetAtPath<Sprite>(SkySpritePath);
            var treeLine = AssetDatabase.LoadAssetAtPath<Sprite>(TreeLineSpritePath);

            if (sky == null || treeLine == null)
            {
                Debug.LogWarning("[Fase01] Sprites de fundo do High Forest não encontrados; " +
                                 "pulei o parallax. Verifique o import do pacote Legacy Fantasy.");
                return;
            }

            // Céu: sobe de escala para cobrir a altura da viewport e acompanha a câmera na
            // vertical. Fator alto = quase parado, sensação de fundo distante.
            float skyScale = Mathf.Ceil(ViewHeight / sky.bounds.size.y * 10f) / 10f + 0.1f;
            BuildParallaxLayer(root.transform, "BG_Sky", sky, sortingOrder: -100,
                bandWidth: 30f, scale: skyScale, baseY: 0f,
                parallaxFactor: 0.9f, followVertical: true);

            // Linha de árvores: escala nativa, plantada no chão. A faixa (4 un) centrada em
            // 1,5 vai de -0,5 a 3,5 — base atrás do terreno, copas acima do horizonte.
            BuildParallaxLayer(root.transform, "BG_TreeLine", treeLine, sortingOrder: -50,
                bandWidth: 60f, scale: 1f, baseY: 1.5f,
                parallaxFactor: 0.6f, followVertical: false);
        }

        /// <summary>
        /// Monta uma camada de fundo: SpriteRenderer em Draw Mode Tiled + ParallaxLayer.
        /// Em Tiled a faixa é centrada no transform, então basta posicioná-la no X do spawn.
        /// <paramref name="baseY"/> é o centro vertical da faixa quando <c>followVertical</c>
        /// é falso, ou o deslocamento em relação à câmera quando é verdadeiro (0 = centrado).
        /// </summary>
        private static void BuildParallaxLayer(
            Transform parent, string name, Sprite sprite, int sortingOrder,
            float bandWidth, float scale, float baseY, float parallaxFactor, bool followVertical)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(bandWidth, sprite.bounds.size.y);
            renderer.sortingOrder = sortingOrder;

            go.transform.position = new Vector3(SpawnPosition.x, baseY, 0f);

            var layer = go.AddComponent<ParallaxLayer>();
            var so = new SerializedObject(layer);
            so.FindProperty("parallaxFactor").floatValue = parallaxFactor;
            so.FindProperty("infiniteHorizontal").boolValue = true;
            so.FindProperty("followVertical").boolValue = followVertical;
            so.ApplyModifiedPropertiesWithoutUndo();
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
    }
}
