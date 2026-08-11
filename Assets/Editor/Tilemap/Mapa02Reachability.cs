using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;

namespace Warana.EditorTools
{
    /// <summary>
    /// Teste de aceitação do <see cref="Mapa02Layout"/>: o mapa é atravessável de ponta a ponta
    /// com o orçamento de pulo real do <c>PlayerController2D</c>?
    ///
    /// Faz uma busca em largura sobre as posições em que o jogador consegue ficar de pé,
    /// expandindo por caminhada, queda e pulo. Os arcos de pulo saem da mesma integração de
    /// gravidade do controller (subida <c>riseGravity</c>, corte <c>lowJumpMultiplier</c>,
    /// queda <c>fallMultiplier</c>), pré-calculados uma vez como listas de offsets — a
    /// trajetória não muda de lugar para lugar, só a colisão muda.
    ///
    /// O modelo é deliberadamente <b>conservador</b>: o corpo de 0,39 x 0,83 vira uma caixa
    /// alinhada à grade de 2 x 4 células, a caminhada não sobe degrau e o arco morre no primeiro
    /// bloqueio. Ele erra para o lado de dizer "não alcança" — um falso negativo vira uma
    /// inspeção à toa, um falso positivo viraria uma fase impossível.
    /// </summary>
    public static class Mapa02Reachability
    {
        // Espelham os SerializeField do PlayerController2D. Se lá mudar, muda aqui.
        private const float MoveSpeed = 5f;
        private const float RiseGravity = 34f;
        private const float FallMultiplier = 1.7f;
        private const float LowJumpMultiplier = 2f;
        private const float JumpHeight = 2f;
        private const float MaxFallSpeed = 18f;
        private const float FixedDelta = 0.02f;

        /// <summary>Tempos de segurar o pulo: toque, meio e completo.</summary>
        private static readonly float[] Holds = { 0.06f, 0.15f, 0.35f };

        /// <summary>Atraso até inclinar o direcional: sair de lado na hora, ou subir e derivar.</summary>
        private static readonly float[] Delays = { 0f, 0.1f, 0.2f, 0.3f };

        /// <summary>Orçamento de relógio. Estourou, aborta e devolve o parcial — nunca segura o Editor.</summary>
        private const long BudgetMs = 15000;

        [MenuItem("Waraná/Tilemap/Rascunho/Validar Alcance do Mapa 02")]
        public static void Validate()
        {
            var solids = FindCollidingTilemaps();
            if (solids.Count == 0)
            {
                Debug.LogError("[Mapa02] Nenhuma tilemap com TilemapCollider2D na cena aberta.");
                return;
            }

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Mapa02] Nenhum objeto com tag Player na cena aberta.");
                return;
            }

            var grid = new SolidGrid(solids);
            var report = Flood(grid, player.transform.position);

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Mapa02_Alcance.txt", report);
            Debug.Log("[Mapa02] Relatório de alcance em Logs/Mapa02_Alcance.txt\n" + report);
        }

        /// <summary>
        /// Todas as camadas sólidas, não só o chão: a madeira do
        /// <c>Tilemap_Plataformas</c> também tem collider e é degrau legítimo. Testar só uma
        /// camada mede um mapa que não é o que o jogador encontra.
        /// </summary>
        private static List<Tilemap> FindCollidingTilemaps()
        {
            var list = new List<Tilemap>();
            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
                if (tm.GetComponent<TilemapCollider2D>() != null)
                    list.Add(tm);
            return list;
        }

        /// <summary>Grade booleana achatada — o laço interno não pode pagar GetTile nem FloorToInt.</summary>
        private sealed class SolidGrid
        {
            public readonly int W, H;
            public readonly float Cell;
            public readonly Vector3 Origin;
            private readonly bool[] _solid;
            public readonly int Painted;

            /// <summary>Corpo do jogador (0,39 x 0,83) arredondado para cima na grade de 0,25.</summary>
            public const int BodyW = 2;
            public const int BodyH = 4;

            public SolidGrid(List<Tilemap> maps)
            {
                // As camadas dividem o mesmo Grid, então as coordenadas de célula são
                // comparáveis entre elas e a união é só o mín/máx dos limites.
                int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
                foreach (var m in maps)
                {
                    var mb = m.cellBounds;
                    xMin = Mathf.Min(xMin, mb.xMin);
                    yMin = Mathf.Min(yMin, mb.yMin);
                    xMax = Mathf.Max(xMax, mb.xMax);
                    yMax = Mathf.Max(yMax, mb.yMax);
                }

                W = xMax - xMin;
                H = yMax - yMin;
                Cell = maps[0].cellSize.x;
                Origin = maps[0].CellToWorld(new Vector3Int(xMin, yMin, 0));
                _solid = new bool[W * H];

                foreach (var m in maps)
                {
                    var mb = m.cellBounds;
                    foreach (var p in mb.allPositionsWithin)
                    {
                        if (m.GetTile(p) == null) continue;
                        int i = (p.x - xMin) * H + (p.y - yMin);
                        if (_solid[i]) continue;   // sobreposição entre camadas conta uma vez
                        _solid[i] = true;
                        Painted++;
                    }
                }
            }

            public bool Solid(int x, int y)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return false;
                return _solid[x * H + y];
            }

            /// <summary>Caixa do corpo com o canto inferior esquerdo em (x, y) está livre?</summary>
            public bool Free(int x, int y)
            {
                for (int dx = 0; dx < BodyW; dx++)
                for (int dy = 0; dy < BodyH; dy++)
                    if (Solid(x + dx, y + dy)) return false;
                return true;
            }

            /// <summary>Há chão imediatamente sob a caixa?</summary>
            public bool Supported(int x, int y)
            {
                for (int dx = 0; dx < BodyW; dx++)
                    if (Solid(x + dx, y - 1)) return true;
                return false;
            }

            public bool Standable(int x, int y) => Free(x, y) && Supported(x, y);

            public float WorldX(int x) => Origin.x + x * Cell;
            public float WorldY(int y) => Origin.y + y * Cell;
        }

        /// <summary>
        /// Um arco pré-calculado: offsets em células, na ordem em que o corpo os ocupa.
        ///
        /// <paramref name="delay"/> é o tempo até o jogador inclinar o direcional. Sem ele o
        /// corpo sai andando de lado já no primeiro frame e qualquer pulo colado numa parede
        /// entra nela — o modelo declararia intransponível um degrau que o jogador sobe pulando
        /// reto e derivando no ápice. O controller tem controle aéreo total, então esse atraso
        /// é uma escolha real de quem joga, não um truque.
        /// </summary>
        private static Vector2Int[] BuildArc(float dirX, float hold, float cell, float delay)
        {
            var path = new List<Vector2Int>();
            float px = 0f, py = 0f, vy = Mathf.Sqrt(2f * RiseGravity * JumpHeight), t = 0f;
            var last = new Vector2Int(0, 0);

            for (int step = 0; step < 400; step++)
            {
                t += FixedDelta;
                float g = vy > 0f
                    ? (t > hold ? RiseGravity * LowJumpMultiplier : RiseGravity)
                    : RiseGravity * FallMultiplier;
                vy = Mathf.Max(vy - g * FixedDelta, -MaxFallSpeed);
                py += vy * FixedDelta;
                if (t >= delay) px += dirX * MoveSpeed * FixedDelta;

                var c = new Vector2Int(Mathf.RoundToInt(px / cell), Mathf.FloorToInt(py / cell));
                if (c != last) { path.Add(c); last = c; }

                // Descer mais de 40 células abaixo da partida já é queda livre, não arco.
                if (py < -10f) break;
            }
            return path.ToArray();
        }

        private static string Flood(SolidGrid grid, Vector3 spawn)
        {
            // 3 tempos de pulo x (subida reta + 2 direções x 4 atrasos de direcional) = 27 arcos.
            var arcs = new List<Vector2Int[]>();
            foreach (float hold in Holds)
            {
                arcs.Add(BuildArc(0f, hold, grid.Cell, 0f));
                for (int d = -1; d <= 1; d += 2)
                    foreach (float delay in Delays)
                        arcs.Add(BuildArc(d, hold, grid.Cell, delay));
            }

            // Queda pura (andar para fora da borda), usada quando a caminhada perde o apoio.
            var falls = new[] { BuildFall(-1, grid.Cell), BuildFall(0, grid.Cell), BuildFall(1, grid.Cell) };

            int sx = Mathf.FloorToInt((spawn.x - grid.Origin.x) / grid.Cell);
            int sy = Mathf.FloorToInt((spawn.y - grid.Origin.y) / grid.Cell);
            var start = Settle(grid, sx, sy);

            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            if (start.HasValue) { seen.Add(start.Value); queue.Enqueue(start.Value); }

            var clock = Stopwatch.StartNew();
            bool aborted = false;

            while (queue.Count > 0)
            {
                if (clock.ElapsedMilliseconds > BudgetMs) { aborted = true; break; }
                var c = queue.Dequeue();

                // Caminhada lateral, sem degrau: uma célula por vez enquanto houver apoio.
                for (int d = -1; d <= 1; d += 2)
                {
                    int nx = c.x + d;
                    if (!grid.Free(nx, c.y)) continue;
                    if (grid.Supported(nx, c.y))
                    {
                        var w = new Vector2Int(nx, c.y);
                        if (seen.Add(w)) queue.Enqueue(w);
                    }
                    else
                    {
                        foreach (var landing in Traverse(grid, nx, c.y, falls[d + 1]))
                            if (seen.Add(landing)) queue.Enqueue(landing);
                    }
                }

                foreach (var arc in arcs)
                    foreach (var landing in Traverse(grid, c.x, c.y, arc))
                        if (seen.Add(landing)) queue.Enqueue(landing);
            }

            HashSet<Vector2Int> air = start.HasValue
                ? ConnectedAir(grid, start.Value)
                : new HashSet<Vector2Int>();

            return Describe(grid, seen, air, start, spawn, aborted, clock.ElapsedMilliseconds);
        }

        /// <summary>
        /// Todo o vazio ligado ao spawn, atravessando parede nenhuma.
        ///
        /// <para>A casca de rocha do builder deixa bolhas seladas entre salas vizinhas — o teto
        /// de uma sala e o piso da de cima não se encostam, e sobra um vão fechado no meio da
        /// pedra. O "chão" dessas bolhas passa no teste de "dá para ficar de pé aqui", e sem
        /// este filtro o relatório acusava mil apoios inalcançáveis que nenhum jogador poderia
        /// querer alcançar. Inundar o ar responde a pergunta certa — este lugar sequer se
        /// comunica com o mapa? — sem depender de eu ter declarado as salas corretamente.</para>
        /// </summary>
        private static HashSet<Vector2Int> ConnectedAir(SolidGrid grid, Vector2Int start)
        {
            var air = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            var steps = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };

            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                foreach (Vector2Int step in steps)
                {
                    var n = new Vector2Int(c.x + step.x, c.y + step.y);
                    if (n.x < 0 || n.y < 0 || n.x >= grid.W || n.y >= grid.H) continue;
                    if (grid.Solid(n.x, n.y)) continue;
                    if (air.Add(n)) queue.Enqueue(n);
                }
            }

            return air;
        }

        private static Vector2Int[] BuildFall(float dirX, float cell)
        {
            var path = new List<Vector2Int>();
            float px = 0f, py = 0f, vy = 0f;
            var last = new Vector2Int(0, 0);
            for (int step = 0; step < 400; step++)
            {
                vy = Mathf.Max(vy - RiseGravity * FallMultiplier * FixedDelta, -MaxFallSpeed);
                py += vy * FixedDelta;
                px += dirX * MoveSpeed * FixedDelta;
                var c = new Vector2Int(Mathf.RoundToInt(px / cell), Mathf.FloorToInt(py / cell));
                if (c != last) { path.Add(c); last = c; }
                if (py < -60f) break;
            }
            return path.ToArray();
        }

        /// <summary>
        /// Percorre um arco a partir de (x0, y0) e devolve todo apoio que ele encosta.
        ///
        /// Qualquer amostra com o corpo livre e chão embaixo conta como pouso, inclusive no
        /// ápice e na subida — não só descendo. O ápice é justamente onde se pousa em degraus
        /// na altura máxima do pulo, e a amostra seguinte já está afundada no bloco. Exigir
        /// descida ali declarava intransponível um degrau de 1,75 com pulo de 2,00.
        ///
        /// O arco não para no primeiro apoio: segue até bater em alguma coisa. Passar raspando
        /// por cima de uma saliência e seguir adiante é um pulo só, e as duas coisas são
        /// alcançáveis.
        /// </summary>
        private static IEnumerable<Vector2Int> Traverse(SolidGrid grid, int x0, int y0, Vector2Int[] arc)
        {
            foreach (var off in arc)
            {
                int x = x0 + off.x, y = y0 + off.y;
                if (!grid.Free(x, y)) yield break;      // bateu; o arco morre aqui
                if (grid.Supported(x, y)) yield return new Vector2Int(x, y);
            }
        }

        /// <summary>Deixa o spawn cair até o primeiro apoio — ele costuma nascer no ar.</summary>
        private static Vector2Int? Settle(SolidGrid grid, int x, int y)
        {
            for (int i = 0; i < 400 && y > 0; i++, y--)
                if (grid.Free(x, y) && grid.Supported(x, y)) return new Vector2Int(x, y);
            return null;
        }

        private static string Describe(SolidGrid grid, HashSet<Vector2Int> seen, HashSet<Vector2Int> air,
                                       Vector2Int? start, Vector3 spawn, bool aborted, long ms)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Mapa 02 — alcance a partir do spawn " + spawn);
            sb.AppendLine("células pintadas: " + grid.Painted);
            sb.AppendLine("apoios alcançados: " + seen.Count + (aborted ? "  [ABORTADO no orçamento de tempo]" : ""));
            sb.AppendLine("tempo: " + ms + " ms");

            if (!start.HasValue)
            {
                sb.AppendLine("ERRO: o spawn não assenta em nenhum apoio.");
                return sb.ToString();
            }

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var c in seen)
            {
                float wx = grid.WorldX(c.x), wy = grid.WorldY(c.y);
                if (wx < minX) minX = wx;
                if (wx > maxX) maxX = wx;
                if (wy < minY) minY = wy;
                if (wy > maxY) maxY = wy;
            }
            sb.AppendLine($"caixa alcançada: X {minX:0.0}..{maxX:0.0}  Y {minY:0.0}..{maxY:0.0}");

            // Apoios que existem no mapa mas ficaram de fora. Os que caem dentro de um
            // AbilityGate declarado são contados à parte: aquilo ali é o projeto funcionando,
            // e misturar os dois faria o relatório sempre acusar problema e virar ruído.
            var orphans = new Dictionary<Vector2Int, int>();
            var gated = new int[Mapa02Layout.AbilityGates.Length];
            int orphanTotal = 0;

            for (int x = 0; x < grid.W - SolidGrid.BodyW; x++)
            for (int y = 1; y < grid.H - SolidGrid.BodyH; y++)
            {
                if (!grid.Standable(x, y)) continue;
                if (seen.Contains(new Vector2Int(x, y))) continue;

                if (!air.Contains(new Vector2Int(x, y))) continue; // bolha selada dentro da rocha

                float wx = grid.WorldX(x), wy = grid.WorldY(y);
                int gate = -1;
                for (int g = 0; g < Mapa02Layout.AbilityGates.Length; g++)
                {
                    if (Mapa02Layout.AbilityGates[g].Where.Contains(wx, wy)) { gate = g; break; }
                }

                if (gate >= 0) { gated[gate]++; continue; }

                orphanTotal++;
                var bucket = new Vector2Int(Mathf.FloorToInt(wx / 8f), Mathf.FloorToInt(wy / 8f));
                orphans.TryGetValue(bucket, out int n);
                orphans[bucket] = n + 1;
            }

            sb.AppendLine();
            sb.AppendLine("trancado de propósito (esperado):");
            for (int g = 0; g < Mapa02Layout.AbilityGates.Length; g++)
            {
                string state = gated[g] > 0 ? gated[g] + " apoios" : "ALCANÇÁVEL — a tranca não está segurando";
                sb.AppendLine($"  {Mapa02Layout.AbilityGates[g].Ability}: {state}");
            }

            sb.AppendLine();
            sb.AppendLine("inalcançável SEM explicação: " + orphanTotal + (orphanTotal == 0 ? "  OK" : "  <-- erro de planta"));
            if (orphanTotal > 0)
            {
                sb.AppendLine("regiões (canto inferior esquerdo do bloco de 8x8 unidades, nº de apoios):");
                var keys = new List<Vector2Int>(orphans.Keys);
                keys.Sort((a, b) => orphans[b].CompareTo(orphans[a]));
                for (int i = 0; i < keys.Count && i < 30; i++)
                    sb.AppendLine($"  ({keys[i].x * 8}, {keys[i].y * 8})  {orphans[keys[i]]}");
            }
            return sb.ToString();
        }
    }
}
