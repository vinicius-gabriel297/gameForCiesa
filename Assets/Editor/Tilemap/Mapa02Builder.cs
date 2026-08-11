using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Warana.CameraRig;
using Warana.Gameplay;

namespace Warana.EditorTools
{
    /// <summary>
    /// Gera o Mapa 02 (<i>Raízes de Waraná</i>) a partir da planta em <see cref="Mapa02Layout"/>.
    ///
    /// Não monta a cena do zero como os outros builders: <b>copia o Mapa_01</b> e troca só a
    /// geometria. O Mapa_01 carrega HUD, sete camadas de parallax, áudio, volume global, menu
    /// de pausa, intro e a sequência final — tudo amarrado por referências de cena que um
    /// <c>NewScene</c> perderia e que eu teria que reconstruir componente a componente. Copiar
    /// e repintar preserva a fiação e deixa o builder cuidar do que ele sabe fazer: tilemap,
    /// posições e limites.
    ///
    /// O arquivo de origem nunca é escrito — <see cref="AssetDatabase.CopyAsset"/> lê do disco
    /// e <see cref="EditorSceneManager.SaveScene"/> grava sempre no caminho do Mapa 02. E
    /// <see cref="ScratchScene.CanWrite"/> guarda o destino: no dia em que o Mapa 02 entrar no
    /// Build Settings e começar a ser pintado à mão, este builder passa a recusar, como já
    /// acontece com o Mapa_01 e o Prólogo.
    /// </summary>
    public static class Mapa02Builder
    {
        public const string SourceScenePath = "Assets/Scenes/Mapa_01.unity";
        public const string ScenePath = "Assets/Scenes/Mapa_02.unity";

        private const string GroundLayerName = "Ground";
        private const string GrassTilePath = "Assets/Tiles/HighForest/RT_Forest_Grass.asset";
        private const string RockTilePath = "Assets/Tiles/DebugMap/RT_Ground_Brown.asset";
        private const string WoodTilePath = "Assets/Tiles/HighForest/RT_Forest_Wood.asset";
        private const string WaterTilePath = "Assets/Tiles/HighForest/RT_Forest_Water.asset";
        private const string FoamTilePath = "Assets/Tiles/HighForest/AT_Water_Foam.asset";
        private const string MadGhostPrefabPath = "Assets/Prefabs/Enemies/MadGhost.prefab";
        private const string HeartPrefabPath = "Assets/Prefabs/Pickups/HeartPickup.prefab";

        /// <summary>16 px / PPU 64 = 0,25. Uma unidade tem 4 células.</summary>
        private const float Cell = TilePaletteBuilder.CellSize;

        private const int TilesPerUnit = (int)(1f / Cell);

        private static readonly int MinCellX = Mathf.RoundToInt(Mapa02Layout.MinX * TilesPerUnit);
        private static readonly int MaxCellX = Mathf.RoundToInt(Mapa02Layout.MaxX * TilesPerUnit);
        private static readonly int MinCellY = Mathf.RoundToInt(Mapa02Layout.MinY * TilesPerUnit);
        private static readonly int MaxCellY = Mathf.RoundToInt(Mapa02Layout.MaxY * TilesPerUnit);

        /// <summary>Faixas ao ar livre onde a vegetação do Mapa_01 é redistribuída.</summary>
        private static readonly (float from, float to)[] OutdoorBands =
        {
            (-135f, -70f),
            (-29f, 103f),
        };

        [MenuItem("Waraná/Tilemap/Gerar Mapa 02 (Metroidvania)")]
        public static void Build()
        {
            var grass = AssetDatabase.LoadAssetAtPath<TileBase>(GrassTilePath);
            var wood = AssetDatabase.LoadAssetAtPath<TileBase>(WoodTilePath);
            if (grass == null || wood == null)
            {
                Debug.LogError("[Mapa02] Autotiles do High Forest não encontrados. Rode " +
                               "'Waraná/Tilemap/Gerar Tiles e Palettes' antes.");
                return;
            }

            if (!ScratchScene.CanWrite(ScenePath)) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
            {
                Debug.LogError($"[Mapa02] Cena de origem '{SourceScenePath}' não encontrada.");
                return;
            }

            // NewScene/OpenScene trocam a cena aberta sem avisar; pergunta antes.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                AssetDatabase.DeleteAsset(ScenePath);
            }

            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
            {
                Debug.LogError($"[Mapa02] Não consegui copiar '{SourceScenePath}' para '{ScenePath}'.");
                return;
            }

            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform grid = FindGrid(scene);
            if (grid == null)
            {
                Debug.LogError("[Mapa02] 'Scene/Grid' não existe na cena copiada.");
                return;
            }

            var oldGround = grid.Find("Tilemap_Ground").GetComponent<Tilemap>();

            // A vegetação precisa ser medida contra o terreno ANTIGO antes de ele sumir: o que
            // importa é a altura de cada árvore em relação ao chão, não a coordenada absoluta.
            List<(Transform actor, float offset)> scenery = MeasureScenery(scene, oldGround);

            int groundLayer = TestLevelBuilder.EnsureLayer(GroundLayerName);
            HashSet<Vector2Int> solid = SolidCells();
            HashSet<Vector2Int> woodCells = WoodCells();

            PaintTilemaps(grid, groundLayer, solid, woodCells);

            var surface = new SurfaceMap(solid, woodCells);

            PlaceActors(scene, surface);
            PlaceScenery(scenery, surface);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[Mapa02] {Mapa02Layout.MaxX - Mapa02Layout.MinX} x " +
                      $"{Mapa02Layout.MaxY - Mapa02Layout.MinY} unidades, {solid.Count + woodCells.Count} " +
                      $"células, em {ScenePath}. Não está no Build Settings — adicione quando aprovar.");
        }

        private static Transform FindGrid(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Scene") continue;
                return root.transform.Find("Grid");
            }

            return null;
        }

        // ------------------------------------------------------------------- geometria

        /// <summary>Espessura da casca de rocha em volta das salas, em células (2 unidades).</summary>
        private const int ShellRadius = 8;

        /// <summary>
        /// Terra: uma casca em volta das salas, mais as saliências.
        ///
        /// <para>A versão anterior partia de uma massa sólida e escavava. Não escala: 248 x 82
        /// unidades são ~813 mil células de rocha, quase toda ela a dezenas de unidades de
        /// qualquer lugar que o jogador possa ver ou tocar — e cada uma vira geometria de
        /// collider. Dilatar a borda das salas dá o mesmo resultado visível por volta de um
        /// décimo do custo, e é o que permite o mapa ter este tamanho.</para>
        /// </summary>
        private static HashSet<Vector2Int> SolidCells()
        {
            var open = new HashSet<Vector2Int>();
            foreach (Area area in Mapa02Layout.Open) Fill(open, area);

            // Só as células de borda são dilatadas. Dilatar o interior daria o mesmo conjunto
            // (ele é descartado no ExceptWith) por um custo proporcional à área, não ao
            // perímetro — com salas abertas até o céu, essa diferença é de duas ordens.
            var border = new List<Vector2Int>();
            foreach (Vector2Int cell in open)
            {
                if (!open.Contains(new Vector2Int(cell.x + 1, cell.y)) ||
                    !open.Contains(new Vector2Int(cell.x - 1, cell.y)) ||
                    !open.Contains(new Vector2Int(cell.x, cell.y + 1)) ||
                    !open.Contains(new Vector2Int(cell.x, cell.y - 1)))
                {
                    border.Add(cell);
                }
            }

            var solid = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in border)
            {
                for (int dx = -ShellRadius; dx <= ShellRadius; dx++)
                for (int dy = -ShellRadius; dy <= ShellRadius; dy++)
                {
                    int x = cell.x + dx, y = cell.y + dy;
                    if (x < MinCellX || y < MinCellY || x >= MaxCellX || y >= MaxCellY) continue;
                    solid.Add(new Vector2Int(x, y));
                }
            }

            solid.ExceptWith(open);

            foreach (Area area in Mapa02Layout.Ledges) Fill(solid, area);
            foreach (Ladder ladder in Mapa02Layout.Ladders)
            {
                foreach (Area step in ladder.Steps()) Fill(solid, step);
            }

            return solid;
        }

        /// <summary>Madeira: tudo o que flutua — plataformas, escadas sobre a água e a ponte.</summary>
        private static HashSet<Vector2Int> WoodCells()
        {
            var cells = new HashSet<Vector2Int>();

            foreach (Area area in Mapa02Layout.WoodLedges) Fill(cells, area);
            foreach (Area area in Mapa02Layout.Bridge()) Fill(cells, area);
            foreach (Ladder ladder in Mapa02Layout.WoodLadders)
            {
                foreach (Area step in ladder.Steps()) Fill(cells, step);
            }

            return cells;
        }

        private static void Fill(HashSet<Vector2Int> cells, Area area)
        {
            int x0 = Mathf.RoundToInt(area.X0 * TilesPerUnit);
            int x1 = Mathf.RoundToInt(area.X1 * TilesPerUnit);
            int y0 = Mathf.RoundToInt(area.Y0 * TilesPerUnit);
            int y1 = Mathf.RoundToInt(area.Y1 * TilesPerUnit);

            for (int x = Mathf.Max(x0, MinCellX); x < Mathf.Min(x1, MaxCellX); x++)
            {
                for (int y = Mathf.Max(y0, MinCellY); y < Mathf.Min(y1, MaxCellY); y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
        }

        // -------------------------------------------------------------------- tilemaps

        private static void PaintTilemaps(
            Transform grid, int groundLayer, HashSet<Vector2Int> solid, HashSet<Vector2Int> wood)
        {
            // O Grid do Mapa_01 tem os filhos deslocados em x = 29,04 — herança de um rename
            // antigo. Zerando, a célula (0,0) volta a ser o mundo (0,0) e a planta pode ser
            // lida em unidades de mundo.
            grid.localPosition = Vector3.zero;
            foreach (Transform child in grid) child.localPosition = Vector3.zero;

            var grass = AssetDatabase.LoadAssetAtPath<TileBase>(GrassTilePath);
            var rock = AssetDatabase.LoadAssetAtPath<TileBase>(RockTilePath);
            var woodTile = AssetDatabase.LoadAssetAtPath<TileBase>(WoodTilePath);
            var water = AssetDatabase.LoadAssetAtPath<TileBase>(WaterTilePath);
            var foam = AssetDatabase.LoadAssetAtPath<TileBase>(FoamTilePath);

            Tilemap ground = EnsureTilemap(grid, "Tilemap_Ground", groundLayer, -10, solid: true);
            Tilemap rocks = EnsureTilemap(grid, "Tilemap_Rocha", groundLayer, -11, solid: true);
            Tilemap platforms = EnsureTilemap(grid, "Tilemap_Plataformas", groundLayer, -9, solid: true);
            Tilemap pools = EnsureTilemap(grid, "Tilemap_Agua", 0, -8, solid: false);
            Tilemap foamMap = EnsureTilemap(grid, "Tilemap_Agua_Espuma", 0, -7, solid: false);
            Tilemap fringe = EnsureTilemap(grid, "Tilemap_Deco_grass", 0, 0, solid: false);
            Tilemap deco = EnsureTilemap(grid, "Tilemap_Deco", 0, -50, solid: false);
            Tilemap foreground = EnsureTilemap(grid, "Tilemap_Foreground", 0, 15, solid: false);

            foreach (Tilemap map in new[] { ground, rocks, platforms, pools, foamMap, fringe, deco, foreground })
            {
                map.ClearAllTiles();
            }

            // O chão se estratifica na RockLine: grama do High Forest em cima, rocha marrom do
            // Debug Map embaixo. São dois autotiles distintos, então precisam de duas tilemaps —
            // uma RuleTile só sabe casar com ela mesma, e misturar as duas na mesma camada faria
            // cada uma tratar a outra como vazio e desenhar borda no meio da pedra.
            int rockLine = Mathf.RoundToInt(Mapa02Layout.RockLine * TilesPerUnit);
            var grassCells = new HashSet<Vector2Int>();
            var rockCells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in solid)
            {
                (cell.y >= rockLine ? grassCells : rockCells).Add(cell);
            }

            Paint(ground, grass, grassCells);
            if (rock != null) Paint(rocks, rock, rockCells);
            Paint(platforms, woodTile, wood);

            var pool = new HashSet<Vector2Int>();
            var foamRow = new HashSet<Vector2Int>();
            foreach (Area area in Mapa02Layout.Water)
            {
                Fill(pool, area);
                // A espuma mora na célula ACIMA da poça: na folha ela é 3/4 transparente.
                Fill(foamRow, new Area(area.X0, area.Y1, area.X1, area.Y1 + Cell));
            }

            if (water != null) Paint(pools, water, pool);
            if (foam != null) Paint(foamMap, foam, foamRow);

            PaintGrassFringe(fringe, solid);

            Finalize(ground);
            Finalize(rocks);
            Finalize(platforms);
            foreach (Tilemap map in new[] { pools, foamMap, fringe, deco, foreground }) map.CompressBounds();
        }

        /// <summary>
        /// Tufos de grama na célula acima de cada topo exposto. São as células da linha 0 da
        /// folha do High Forest — a franja que transborda para cima e que o autotile deixa de
        /// fora justamente porque o lugar dela é a camada de decoração.
        /// </summary>
        private static void PaintGrassFringe(Tilemap fringe, HashSet<Vector2Int> solid)
        {
            var tufts = new List<TileBase>();
            for (int i = 0; i < 5; i++)
            {
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(
                    TilePaletteBuilder.TilePath(TerrainBlocks.HighForestSet, $"Tiles_{i}"));
                if (tile != null) tufts.Add(tile);
            }

            if (tufts.Count == 0) return;

            var random = new System.Random(20250810); // semente fixa: rodar de novo dá o mesmo mapa
            var cells = new Dictionary<Vector2Int, TileBase>();

            foreach (Vector2Int cell in solid)
            {
                var above = new Vector2Int(cell.x, cell.y + 1);
                if (solid.Contains(above)) continue;
                if (above.y >= MaxCellY) continue;
                if (above.y < 0) continue; // tufo de grama não brota no teto de caverna
                if (random.Next(100) >= 35) continue;

                cells[above] = tufts[random.Next(tufts.Count)];
            }

            foreach (KeyValuePair<Vector2Int, TileBase> entry in cells)
            {
                fringe.SetTile(new Vector3Int(entry.Key.x, entry.Key.y, 0), entry.Value);
            }

            fringe.CompressBounds();
        }

        private static Tilemap EnsureTilemap(
            Transform grid, string name, int layer, int sortingOrder, bool solid)
        {
            Transform existing = grid.Find(name);
            GameObject go;

            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(grid);
                go.transform.localPosition = Vector3.zero;
                go.AddComponent<Tilemap>();
                go.AddComponent<TilemapRenderer>();
            }

            go.layer = layer;
            go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;

            var tilemap = go.GetComponent<Tilemap>();
            tilemap.color = Color.white; // o Deco do Mapa_01 vinha tingido
            if (!solid) return tilemap;

            // O Rigidbody2D vem primeiro de propósito: AddComponent<CompositeCollider2D>
            // satisfaz o [RequireComponent] criando um corpo Dynamic sozinho.
            var body = Require<Rigidbody2D>(go);
            body.bodyType = RigidbodyType2D.Static;

            var composite = Require<CompositeCollider2D>(go);
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons; // Outlines é oco: a cápsula tunela
            composite.generationType = CompositeCollider2D.GenerationType.Manual; // um rebuild só, no fim
            composite.edgeRadius = 0f;
            composite.offsetDistance = 0f;
            composite.useDelaunayMesh = true;

            var collider = Require<TilemapCollider2D>(go);
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
            collider.extrusionFactor = 0.0001f;
            collider.useDelaunayMesh = true;
            collider.maximumTileChangeCount = 4000;

            return tilemap;
        }

        /// <summary>
        /// GetComponent-ou-AddComponent. Não dá para escrever isto com <c>??</c>: um componente
        /// ausente volta como o "null falso" da Unity, que o operador de coalescência não
        /// reconhece — ele devolve o objeto morto e o AddComponent nunca acontece.
        /// </summary>
        private static T Require<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component == null ? go.AddComponent<T>() : component;
        }

        /// <summary>
        /// Pinta num único SetTilesBlock. O array é x-major com origem em bounds.min, e o
        /// size.z tem que ser 1 — com 0 a chamada é silenciosamente ignorada.
        /// </summary>
        private static void Paint(Tilemap tilemap, TileBase tile, HashSet<Vector2Int> cells)
        {
            var bounds = new BoundsInt(
                MinCellX, MinCellY, 0, MaxCellX - MinCellX, MaxCellY - MinCellY, 1);
            var tiles = new TileBase[bounds.size.x * bounds.size.y];

            foreach (Vector2Int cell in cells)
            {
                int x = cell.x - MinCellX;
                int y = cell.y - MinCellY;
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

        // ---------------------------------------------------------------- superfície

        /// <summary>Consulta "qual o topo do chão nesta coluna", em unidades de mundo.</summary>
        private sealed class SurfaceMap
        {
            private readonly Dictionary<int, int> _top = new Dictionary<int, int>();
            private readonly HashSet<Vector2Int> _cells = new HashSet<Vector2Int>();

            public SurfaceMap(params HashSet<Vector2Int>[] layers)
            {
                foreach (HashSet<Vector2Int> layer in layers)
                {
                    foreach (Vector2Int cell in layer)
                    {
                        _cells.Add(cell);
                        if (!_top.TryGetValue(cell.x, out int best) || cell.y > best) _top[cell.x] = cell.y;
                    }
                }
            }

            /// <summary>
            /// Encaixa um ator no chão da posição pedida: sobe se ele nasceu dentro da pedra,
            /// depois desce até encostar.
            ///
            /// <para>Não dá para usar <see cref="TopAt"/> aqui — ele devolve o ponto mais alto
            /// da coluna, que para qualquer coisa subterrânea é o teto da caverna ou a
            /// superfície lá em cima. O que importa é o piso <i>daquele andar</i>, e é por isso
            /// que a busca parte da altura pedida em vez de partir do céu.</para>
            /// </summary>
            public Vector3 Snap(Vector2 wanted, float lift)
            {
                int column = Mathf.FloorToInt(wanted.x / Cell);
                int y = Mathf.FloorToInt(wanted.y / Cell);

                for (int i = 0; i < 40 && _cells.Contains(new Vector2Int(column, y)); i++) y++;
                for (int i = 0; i < 400 && !_cells.Contains(new Vector2Int(column, y - 1)); i++) y--;

                return new Vector3(wanted.x, y * Cell + lift, 0f);
            }

            /// <summary>Topo do chão em x, ou <see cref="Mapa02Layout.MinY"/> se a coluna é vazia.</summary>
            public float TopAt(float x)
            {
                int column = Mathf.RoundToInt(x * TilesPerUnit);
                return _top.TryGetValue(column, out int y) ? (y + 1) * Cell : Mapa02Layout.MinY;
            }
        }

        /// <summary>Topo do chão em x lido de uma tilemap já pintada (usado no terreno antigo).</summary>
        private static float TopAt(Tilemap tilemap, float x, float below)
        {
            var column = Mathf.RoundToInt((x - tilemap.transform.position.x) / Cell);
            int start = Mathf.RoundToInt((below - tilemap.transform.position.y) / Cell);

            for (int y = start; y > tilemap.cellBounds.yMin; y--)
            {
                if (tilemap.HasTile(new Vector3Int(column, y, 0)))
                {
                    return (y + 1) * Cell + tilemap.transform.position.y;
                }
            }

            return 0f;
        }

        // -------------------------------------------------------------------- atores

        private static void PlaceActors(Scene scene, SurfaceMap surface)
        {
            GameObject sceneRoot = null;
            GameObject gameplay = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Scene") sceneRoot = root;
                if (root.name == "Gameplay") gameplay = root;
            }

            Transform player = sceneRoot.transform.Find("Player");
            player.position = Mapa02Layout.PlayerSpawn;

            Transform eye = sceneRoot.transform.Find("Guarana Eye");
            if (eye != null)
            {
                eye.position = Mapa02Layout.PlayerSpawn + new Vector3(-0.6f, 1.1f, 0f);
            }

            PlaceCamera(player);
            PlaceEnemies(sceneRoot.transform, surface);
            PlaceSacredGrove(sceneRoot.transform, gameplay);
            PlaceHearts(sceneRoot.transform, surface);
            PlaceKillZone(sceneRoot.transform);
        }

        /// <summary>
        /// Piso de morte sob o mapa. O <c>OutOfBoundsKillZone</c> já existia no projeto sem
        /// nenhuma cena usando; com quedas de 44 unidades e um poço central que é o caminho
        /// principal, qualquer buraco na casca vira queda infinita — e queda infinita não dá
        /// erro, só prende o jogador. Melhor matar e respawnar.
        /// </summary>
        private static void PlaceKillZone(Transform sceneRoot)
        {
            Transform existing = sceneRoot.Find("KillZone");
            GameObject go = existing != null ? existing.gameObject : new GameObject("KillZone");
            go.transform.SetParent(sceneRoot);

            float width = Mapa02Layout.MaxX - Mapa02Layout.MinX;
            go.transform.position = new Vector3(
                (Mapa02Layout.MinX + Mapa02Layout.MaxX) * 0.5f, Mapa02Layout.MinY - 4f, 0f);

            var box = Require<BoxCollider2D>(go);
            box.size = new Vector2(width, 4f);
            box.isTrigger = true;

            Require<OutOfBoundsKillZone>(go);
        }

        private static void PlaceCamera(Transform player)
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            camera.transform.position = new Vector3(player.position.x, player.position.y + 0.6f, -10f);

            var follow = camera.GetComponent<CameraFollow2D>();
            if (follow == null) return;

            // As duas unidades de parede das pontas ficam fora: a câmera para na rocha.
            follow.SetBounds(
                new Vector2(Mapa02Layout.MinX + 2f, Mapa02Layout.MinY),
                new Vector2(Mapa02Layout.MaxX - 2f, Mapa02Layout.MaxY));
            EditorUtility.SetDirty(follow);
        }

        private static void PlaceEnemies(Transform sceneRoot, SurfaceMap surface)
        {
            Transform enemies = sceneRoot.Find("Enemies");

            var ghosts = new List<Transform>();
            Transform boss = null;
            foreach (Transform child in enemies)
            {
                if (child.GetComponent<Warana.Enemies.Abomination>() != null) boss = child;
                else if (child.GetComponent<Warana.Enemies.MadGhost>() != null) ghosts.Add(child);
            }

            // Os fantasmas que já existem são reaproveitados em vez de recriados: cada um é uma
            // instância de prefab com overrides, e reinstanciar jogaria fora esses ajustes.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MadGhostPrefabPath);
            while (ghosts.Count < Mapa02Layout.MadGhosts.Length)
            {
                GameObject clone = ghosts.Count > 0
                    ? Object.Instantiate(ghosts[0].gameObject, enemies)
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefab, enemies);

                clone.name = "MadGhost";
                ghosts.Add(clone.transform);
            }

            for (int i = ghosts.Count - 1; i >= Mapa02Layout.MadGhosts.Length; i--)
            {
                Object.DestroyImmediate(ghosts[i].gameObject);
                ghosts.RemoveAt(i);
            }

            for (int i = 0; i < Mapa02Layout.MadGhosts.Length; i++)
            {
                ghosts[i].position = surface.Snap(Mapa02Layout.MadGhosts[i], 0.6f);
            }

            if (boss != null)
            {
                boss.position = surface.Snap(Mapa02Layout.Abomination, 0.55f);
            }
        }

        /// <summary>
        /// Move o bosque sagrado inteiro de uma vez. Os filhos do <c>Guardian_tree</c> têm
        /// deslocamentos locais calibrados à mão (visual, brilho, zona de canalização, dois
        /// sistemas de partículas); mexer em cada um desmontaria o conjunto, então o que se
        /// desloca é o pai, pelo delta que leva a zona sagrada ao lugar novo.
        /// </summary>
        private static void PlaceSacredGrove(Transform sceneRoot, GameObject gameplay)
        {
            Transform grove = sceneRoot.Find("Guardian_tree");
            if (grove == null) return;

            Transform zone = grove.Find("Sacred Zone");
            if (zone == null) return;

            var target = new Vector3(
                Mapa02Layout.SacredTree.x, Mapa02Layout.SacredTree.y + 2.25f, zone.position.z);
            grove.position += target - zone.position;

            if (gameplay == null) return;

            var ending = gameplay.GetComponent<EndingSequence>();
            if (ending == null) return;

            // Duas coordenadas do desfecho eram do Mapa_01: até onde a câmera se abre depois da
            // luta, e onde ela enquadra a copa da árvore.
            var so = new SerializedObject(ending);
            so.FindProperty("openedBoundsMaxX").floatValue = Mapa02Layout.MaxX - 2f;
            so.FindProperty("crownFraming").vector2Value =
                new Vector2(Mapa02Layout.SacredTree.x, Mapa02Layout.SacredTree.y + 9.4f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PlaceHearts(Transform sceneRoot, SurfaceMap surface)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeartPrefabPath);
            if (prefab == null) return;

            Transform existing = sceneRoot.Find("Pickups");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject("Pickups");
            root.transform.SetParent(sceneRoot);
            root.transform.localPosition = Vector3.zero;

            foreach (Vector2 spot in Mapa02Layout.Hearts)
            {
                var heart = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                heart.transform.position = surface.Snap(spot, 0.4f);
            }
        }

        // ----------------------------------------------------------------- vegetação

        /// <summary>
        /// Guarda, para cada árvore e efeito de ambiente do Mapa_01, a altura em que ele estava
        /// acima do chão. É esse número que se preserva na mudança: um tronco plantado 0,2
        /// acima da grama tem que continuar plantado, e o X pode ir para qualquer lugar.
        /// </summary>
        private static List<(Transform actor, float offset)> MeasureScenery(Scene scene, Tilemap oldGround)
        {
            var measured = new List<(Transform, float)>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Scene" && root.name != "VFX") continue;

                foreach (Transform group in root.transform)
                {
                    bool isScenery = group.name == "Trees" || group.name == "Red_Trees";
                    bool isAmbient = root.name == "VFX";
                    if (!isScenery && !isAmbient) continue;

                    if (isAmbient)
                    {
                        measured.Add((group, group.position.y - TopAt(oldGround, group.position.x, group.position.y)));
                        continue;
                    }

                    foreach (Transform tree in group)
                    {
                        measured.Add((tree, tree.position.y - TopAt(oldGround, tree.position.x, tree.position.y)));
                    }
                }
            }

            return measured;
        }

        /// <summary>
        /// Espalha a vegetação medida pelas faixas ao ar livre, na ordem em que estava — assim
        /// as árvores altas do fundo do Mapa_01 continuam agrupadas entre si — e replanta cada
        /// uma no chão novo, no mesmo deslocamento que tinha.
        /// </summary>
        private static void PlaceScenery(List<(Transform actor, float offset)> scenery, SurfaceMap surface)
        {
            if (scenery.Count == 0) return;

            scenery.Sort((a, b) => a.actor.position.x.CompareTo(b.actor.position.x));

            float span = 0f;
            foreach ((float from, float to) in OutdoorBands) span += to - from;

            float step = span / scenery.Count;
            float walked = step * 0.5f;

            foreach ((Transform actor, float offset) in scenery)
            {
                float remaining = walked;
                float x = OutdoorBands[OutdoorBands.Length - 1].to;

                foreach ((float from, float to) in OutdoorBands)
                {
                    float width = to - from;
                    if (remaining <= width)
                    {
                        x = from + remaining;
                        break;
                    }

                    remaining -= width;
                }

                actor.position = new Vector3(x, surface.TopAt(x) + offset, actor.position.z);
                walked += step;
            }
        }
    }
}
