using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Warana.EditorTools
{
    /// <summary>
    /// Lê uma folha de tiles pixel a pixel e descreve a estrutura dela, para que os retângulos
    /// de <see cref="TerrainBlocks"/> saiam de medição e não de chute — os índices que estavam
    /// no builder antes foram escolhidos à mão e nunca conferidos.
    ///
    /// Três leituras, em ordem de utilidade:
    /// - <b>mapa de classes</b>: células pixel-idênticas recebem a mesma letra. O miolo de um
    ///   bloco de terreno se repete, então o retângulo do nine-slice salta aos olhos.
    /// - <b>blocos candidatos</b>: toda classe repetida que preenche um retângulo cheio é miolo
    ///   de bloco; o nine-slice é esse retângulo dilatado em uma célula para cada lado.
    /// - <b>corridas de rolagem</b>: se a célula vizinha é a mesma imagem deslocada (comparação
    ///   em toro, diferença zero com deslocamento != 0), aquilo é quadro de animação e não
    ///   variante espacial. É o que separa a água da grama sem abrir a folha no Photoshop.
    ///
    /// O relatório vai para Logs/ (ignorado pelo git) porque não cabe no console.
    /// </summary>
    public static class TilesheetInspector
    {
        private const int TileSize = LegacyFantasyImporter.TileSize;

        /// <summary>Classe repetida vira letra; a ordem é só de apresentação.</summary>
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [MenuItem("Waraná/Tilemap/Inspecionar Folha", true)]
        private static bool ValidateInspect() => Selection.activeObject is Texture2D;

        [MenuItem("Waraná/Tilemap/Inspecionar Folha")]
        public static void Inspect()
        {
            var texture = Selection.activeObject as Texture2D;
            if (texture == null)
            {
                Debug.LogError("[Folha] Selecione a textura da folha no Project antes de rodar.");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            Color32[] pixels = ReadPixels(assetPath, out int width, out int height);
            if (pixels == null) return;

            int columns = width / TileSize;
            int rows = height / TileSize;

            var report = new StringBuilder();
            report.AppendLine($"Folha: {assetPath}");
            report.AppendLine($"{width}x{height} px, grade {columns}x{rows}, célula {TileSize} px, " +
                              $"índice = row * {columns} + col");
            report.AppendLine();

            var cells = SliceCells(pixels, width, height, columns, rows);
            var classes = Classify(cells, columns, rows, report);

            AppendClassMap(report, classes, columns, rows);
            AppendBlockCandidates(report, classes, columns, rows);
            AppendScrollRuns(report, cells, columns, rows);

            string outputPath = WriteReport(assetPath, report.ToString());
            Debug.Log($"[Folha] {texture.name}: {columns}x{rows} células. Relatório em {outputPath}");
        }

        // ------------------------------------------------------------------------- leitura

        /// <summary>
        /// Lê o PNG do disco para uma textura temporária. Não dá para usar
        /// <see cref="Texture2D.GetPixels32()"/> no asset importado: o
        /// <see cref="PixelArtTextureDefaults"/> deixa isReadable = false, e ligar isso aqui
        /// sujaria o .meta de um pacote de terceiros só para rodar uma inspeção.
        /// </summary>
        private static Color32[] ReadPixels(string assetPath, out int width, out int height)
        {
            width = 0;
            height = 0;

            string root = Directory.GetParent(Application.dataPath)!.FullName;
            string fullPath = Path.Combine(root, assetPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[Folha] Arquivo não encontrado em disco: {fullPath}");
                return null;
            }

            var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(temp, File.ReadAllBytes(fullPath)))
                {
                    Debug.LogError($"[Folha] '{assetPath}' não é uma imagem que a Unity saiba decodificar.");
                    return null;
                }

                width = temp.width;
                height = temp.height;
                return temp.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        /// <summary>
        /// Recorta a grade. Devolve indexado por (row * columns + col) com row 0 no topo —
        /// a mesma convenção do nome Tiles_N que o <see cref="LegacyFantasyImporter"/> grava.
        /// </summary>
        private static Color32[][] SliceCells(
            Color32[] pixels, int width, int height, int columns, int rows)
        {
            var cells = new Color32[columns * rows][];

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    var cell = new Color32[TileSize * TileSize];

                    // GetPixels32 devolve de baixo para cima; a grade conta de cima para baixo.
                    int originY = height - (row + 1) * TileSize;
                    int originX = col * TileSize;

                    for (int y = 0; y < TileSize; y++)
                    {
                        for (int x = 0; x < TileSize; x++)
                        {
                            cell[y * TileSize + x] = pixels[(originY + y) * width + originX + x];
                        }
                    }

                    cells[row * columns + col] = cell;
                }
            }

            return cells;
        }

        // ------------------------------------------------------------------------- classes

        /// <summary>-1 = célula vazia. Índices >= 0 são classes de células pixel-idênticas.</summary>
        private static int[] Classify(
            Color32[][] cells, int columns, int rows, StringBuilder report)
        {
            var byKey = new Dictionary<string, int>();
            var counts = new List<int>();
            var classes = new int[columns * rows];
            int empty = 0;

            for (int i = 0; i < cells.Length; i++)
            {
                if (IsEmpty(cells[i]))
                {
                    classes[i] = -1;
                    empty++;
                    continue;
                }

                string key = Key(cells[i]);
                if (!byKey.TryGetValue(key, out int id))
                {
                    id = counts.Count;
                    byKey.Add(key, id);
                    counts.Add(0);
                }

                counts[id]++;
                classes[i] = id;
            }

            int repeated = 0;
            foreach (int count in counts)
            {
                if (count > 1) repeated++;
            }

            report.AppendLine($"Células: {cells.Length} total, {empty} vazias, " +
                              $"{counts.Count} imagens distintas, {repeated} delas repetidas.");
            report.AppendLine();

            // Reindexa para que as letras saiam por frequência: o miolo de terreno, que é o
            // que mais se repete, fica sempre no começo do alfabeto e é fácil de achar no mapa.
            var order = new List<int>();
            for (int id = 0; id < counts.Count; id++)
            {
                if (counts[id] > 1) order.Add(id);
            }

            order.Sort((a, b) => counts[b].CompareTo(counts[a]));

            var rank = new Dictionary<int, int>(order.Count);
            for (int i = 0; i < order.Count; i++) rank[order[i]] = i;

            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] < 0) continue;
                classes[i] = rank.TryGetValue(classes[i], out int r) ? r : -2; // -2 = única
            }

            return classes;
        }

        private static bool IsEmpty(Color32[] cell)
        {
            foreach (Color32 pixel in cell)
            {
                if (pixel.a != 0) return false;
            }

            return true;
        }

        /// <summary>Chave exata (não hash): a folha é pequena e colisão aqui viraria índice errado.</summary>
        private static string Key(Color32[] cell)
        {
            var bytes = new byte[cell.Length * 4];
            for (int i = 0; i < cell.Length; i++)
            {
                // Pixel transparente: a cor por baixo do alfa 0 não é vista, e as folhas do
                // pacote têm lixo nela. Sem normalizar, células visualmente idênticas caem
                // em classes diferentes e o mapa fica inútil.
                Color32 pixel = cell[i];
                if (pixel.a == 0) pixel = new Color32(0, 0, 0, 0);

                bytes[i * 4] = pixel.r;
                bytes[i * 4 + 1] = pixel.g;
                bytes[i * 4 + 2] = pixel.b;
                bytes[i * 4 + 3] = pixel.a;
            }

            return System.Convert.ToBase64String(bytes);
        }

        private static void AppendClassMap(StringBuilder report, int[] classes, int columns, int rows)
        {
            report.AppendLine("MAPA DE CLASSES  ('.' vazia, '-' imagem única, letra = classe repetida)");
            report.Append("      ");
            for (int col = 0; col < columns; col++) report.Append(col % 10);
            report.AppendLine();

            for (int row = 0; row < rows; row++)
            {
                report.Append($"  {row,3} ");
                for (int col = 0; col < columns; col++)
                {
                    int id = classes[row * columns + col];
                    report.Append(id == -1 ? '.' : id == -2 ? '-' : Letter(id));
                }

                report.AppendLine();
            }

            report.AppendLine();
        }

        private static char Letter(int id) => id < Alphabet.Length ? Alphabet[id] : '#';

        // -------------------------------------------------------------------------- blocos

        /// <summary>
        /// Toda classe repetida que preenche um retângulo cheio é candidata a miolo de bloco.
        /// O nine-slice é esse retângulo dilatado em uma célula para cada lado — e só vale se
        /// a moldura inteira existir na folha.
        ///
        /// É palpite, não veredito: quando a faixa do meio tem variantes (no High Forest a
        /// linha 2 tem três desenhos distintos e só a 3 é preenchimento puro), o miolo medido
        /// sai menor que o real e o retângulo aparece uma linha curto. Quem decide é o mapa
        /// de classes; isto aqui só aponta onde olhar.
        /// </summary>
        private static void AppendBlockCandidates(
            StringBuilder report, int[] classes, int columns, int rows)
        {
            report.AppendLine("BLOCOS CANDIDATOS  (miolo retangular dilatado em 1 célula)");

            var byClass = new Dictionary<int, List<Vector2Int>>();
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int id = classes[row * columns + col];
                    if (id < 0) continue;

                    if (!byClass.TryGetValue(id, out List<Vector2Int> list))
                    {
                        list = new List<Vector2Int>();
                        byClass.Add(id, list);
                    }

                    list.Add(new Vector2Int(col, row));
                }
            }

            int found = 0;
            foreach (var pair in byClass)
            {
                // Separar em componentes conexas antes de medir é obrigatório: na folha do High
                // Forest os dois blocos de terreno usam o MESMO miolo, e a caixa que abraça as
                // duas ocorrências não é retângulo cheio — o bloco passava despercebido.
                foreach (List<Vector2Int> cells in Components(pair.Value))
                {
                    int minCol = int.MaxValue, maxCol = int.MinValue;
                    int minRow = int.MaxValue, maxRow = int.MinValue;

                    foreach (Vector2Int cell in cells)
                    {
                        minCol = Mathf.Min(minCol, cell.x);
                        maxCol = Mathf.Max(maxCol, cell.x);
                        minRow = Mathf.Min(minRow, cell.y);
                        maxRow = Mathf.Max(maxRow, cell.y);
                    }

                    int area = (maxCol - minCol + 1) * (maxRow - minRow + 1);
                    if (area != cells.Count) continue; // não preenche a caixa, não é miolo

                    // Uma única célula repetida em outro lugar da folha não é miolo de nada, e
                    // sem este corte o relatório sai com dezenas de blocos 1x1 que não existem.
                    if (cells.Count < 2) continue;

                    int colLeft = minCol - 1, colRight = maxCol + 1;
                    int rowTop = minRow - 1, rowBottom = maxRow + 1;
                    if (colLeft < 0 || rowTop < 0 || colRight >= columns || rowBottom >= rows) continue;

                    bool frameComplete = true;
                    for (int row = rowTop; row <= rowBottom && frameComplete; row++)
                    {
                        for (int col = colLeft; col <= colRight; col++)
                        {
                            if (classes[row * columns + col] != -1) continue;
                            frameComplete = false;
                            break;
                        }
                    }

                    if (!frameComplete) continue;

                    found++;
                    report.AppendLine($"  classe '{Letter(pair.Key)}': miolo cols {minCol}..{maxCol}, " +
                                      $"rows {minRow}..{maxRow}  =>  bloco cols {colLeft}..{colRight}, " +
                                      $"rows {rowTop}..{rowBottom}");
                    AppendRoleIndices(report, columns, colLeft, colRight, rowTop, rowBottom);
                }
            }

            if (found == 0) report.AppendLine("  nenhum.");
            report.AppendLine();
        }

        /// <summary>Quebra as ocorrências de uma classe em grupos 4-conexos.</summary>
        private static List<List<Vector2Int>> Components(List<Vector2Int> cells)
        {
            var remaining = new HashSet<Vector2Int>(cells);
            var result = new List<List<Vector2Int>>();

            while (remaining.Count > 0)
            {
                var group = new List<Vector2Int>();
                var queue = new Queue<Vector2Int>();

                Vector2Int seed = default;
                foreach (Vector2Int cell in remaining)
                {
                    seed = cell;
                    break;
                }

                queue.Enqueue(seed);
                remaining.Remove(seed);

                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    group.Add(cell);

                    foreach (Vector2Int step in Steps)
                    {
                        Vector2Int next = cell + step;
                        if (!remaining.Remove(next)) continue;
                        queue.Enqueue(next);
                    }
                }

                result.Add(group);
            }

            return result;
        }

        private static readonly Vector2Int[] Steps =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        };

        private static void AppendRoleIndices(
            StringBuilder report, int columns, int colLeft, int colRight, int rowTop, int rowBottom)
        {
            string Range(int rowFrom, int rowTo, int colFrom, int colTo)
            {
                var indices = new List<int>();
                for (int row = rowFrom; row <= rowTo; row++)
                {
                    for (int col = colFrom; col <= colTo; col++) indices.Add(row * columns + col);
                }

                return string.Join(",", indices);
            }

            int midColFrom = colLeft + 1, midColTo = colRight - 1;
            int midRowFrom = rowTop + 1, midRowTo = rowBottom - 1;

            report.AppendLine($"      TopLeft {Range(rowTop, rowTop, colLeft, colLeft)}  " +
                              $"Top {Range(rowTop, rowTop, midColFrom, midColTo)}  " +
                              $"TopRight {Range(rowTop, rowTop, colRight, colRight)}");
            report.AppendLine($"      Left {Range(midRowFrom, midRowTo, colLeft, colLeft)}  " +
                              $"Center {Range(midRowFrom, midRowTo, midColFrom, midColTo)}  " +
                              $"Right {Range(midRowFrom, midRowTo, colRight, colRight)}");
            report.AppendLine($"      BottomLeft {Range(rowBottom, rowBottom, colLeft, colLeft)}  " +
                              $"Bottom {Range(rowBottom, rowBottom, midColFrom, midColTo)}  " +
                              $"BottomRight {Range(rowBottom, rowBottom, colRight, colRight)}");
        }

        // ------------------------------------------------------------------------- rolagem

        /// <summary>
        /// Marca pares de células vizinhas em que uma é a outra deslocada. Diferença zero com
        /// deslocamento != 0 é quadro de animação: a arte rola e dá a volta. Sem esta leitura
        /// não há como distinguir "4 variantes de água" de "4 quadros da mesma água".
        /// </summary>
        private static void AppendScrollRuns(
            StringBuilder report, Color32[][] cells, int columns, int rows)
        {
            report.AppendLine("CORRIDAS DE ROLAGEM  (vizinhas que são a mesma imagem deslocada)");
            int found = 0;

            for (int row = 0; row < rows; row++)
            {
                int col = 0;
                while (col < columns - 1)
                {
                    int start = col;
                    var shifts = new List<Vector2Int>();

                    while (col < columns - 1)
                    {
                        Color32[] a = cells[row * columns + col];
                        Color32[] b = cells[row * columns + col + 1];
                        if (IsEmpty(a) || IsEmpty(b)) break;
                        if (!BestShift(a, b, out Vector2Int shift)) break;

                        shifts.Add(shift);
                        col++;
                    }

                    if (shifts.Count >= 2)
                    {
                        found++;
                        report.AppendLine($"  row {row}, cols {start}..{col}  " +
                                          $"({shifts.Count + 1} quadros, deslocamento {shifts[0]})  " +
                                          $"índices {IndexList(columns, row, start, col)}");
                    }

                    col = Mathf.Max(col + 1, start + 1);
                }
            }

            for (int col = 0; col < columns; col++)
            {
                int row = 0;
                while (row < rows - 1)
                {
                    int start = row;
                    var shifts = new List<Vector2Int>();

                    while (row < rows - 1)
                    {
                        Color32[] a = cells[row * columns + col];
                        Color32[] b = cells[(row + 1) * columns + col];
                        if (IsEmpty(a) || IsEmpty(b)) break;
                        if (!BestShift(a, b, out Vector2Int shift)) break;

                        shifts.Add(shift);
                        row++;
                    }

                    if (shifts.Count >= 2)
                    {
                        found++;
                        report.AppendLine($"  col {col}, rows {start}..{row}  " +
                                          $"({shifts.Count + 1} quadros, deslocamento {shifts[0]})  " +
                                          "(vertical)");
                    }

                    row = Mathf.Max(row + 1, start + 1);
                }
            }

            if (found == 0) report.AppendLine("  nenhuma.");
            report.AppendLine();
        }

        private static string IndexList(int columns, int row, int colFrom, int colTo)
        {
            var indices = new List<int>();
            for (int col = colFrom; col <= colTo; col++) indices.Add(row * columns + col);
            return string.Join(",", indices);
        }

        /// <summary>
        /// Procura o deslocamento que zera a diferença entre duas células, comparando em toro
        /// (o que sai de um lado volta pelo outro) porque é assim que arte de rolagem é feita.
        /// Só eixos puros: rolagem diagonal não existe nestes pacotes e o custo cairia 30x.
        /// </summary>
        private static bool BestShift(Color32[] a, Color32[] b, out Vector2Int shift)
        {
            shift = Vector2Int.zero;

            // Células iguais são a mesma variante repetida, não quadros. Descartar aqui também
            // resolve o caso chato do preenchimento chapado, que casa com qualquer deslocamento.
            if (Differences(a, b, 0, 0) == 0) return false;

            for (int dx = 1; dx < TileSize; dx++)
            {
                if (Differences(a, b, dx, 0) != 0) continue;
                shift = new Vector2Int(dx, 0);
                return true;
            }

            for (int dy = 1; dy < TileSize; dy++)
            {
                if (Differences(a, b, 0, dy) != 0) continue;
                shift = new Vector2Int(0, dy);
                return true;
            }

            shift = Vector2Int.zero;
            return false;
        }

        private static int Differences(Color32[] a, Color32[] b, int dx, int dy)
        {
            int differing = 0;

            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    Color32 pa = a[y * TileSize + x];
                    int sx = (x + dx) % TileSize;
                    int sy = (y + dy) % TileSize;
                    Color32 pb = b[sy * TileSize + sx];

                    if (pa.a == 0 && pb.a == 0) continue;
                    if (pa.r == pb.r && pa.g == pb.g && pa.b == pb.b && pa.a == pb.a) continue;

                    differing++;
                }
            }

            return differing;
        }

        // ------------------------------------------------------------------------ relatório

        /// <summary>
        /// O nome sai do caminho inteiro, não do nome do arquivo: as duas folhas de terreno do
        /// projeto se chamam Tiles.png e um relatório sobrescreveria o outro.
        /// </summary>
        private static string WriteReport(string assetPath, string content)
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            string folder = Path.Combine(root, "Logs");
            Directory.CreateDirectory(folder);

            var name = new StringBuilder();
            foreach (char c in Path.ChangeExtension(assetPath, null))
            {
                name.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            string path = Path.Combine(folder, $"tilesheet_{name}.txt");
            File.WriteAllText(path, content);
            return path;
        }
    }
}
