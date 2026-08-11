using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Warana.EditorTools
{
    /// <summary>
    /// Papel de uma célula dentro de um terreno: qual combinação de vizinhos a seleciona.
    ///
    /// Os nove primeiros são o nine-slice clássico. Os outros sete cobrem geometria fina —
    /// pilar de uma célula de largura, plataforma de uma célula de altura, célula isolada —
    /// que é exatamente onde o autotile ingênuo erra, e os quatro últimos são os cantos
    /// côncavos. Nenhuma folha do projeto tem arte para os sete últimos; eles existem porque
    /// <see cref="TerrainRuleTileBuilder"/> sabe cair para o papel mais próximo quando falta.
    /// </summary>
    public enum TerrainRole
    {
        Center,
        Top,
        Bottom,
        Left,
        Right,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,

        /// <summary>Exposto à esquerda e à direita ao mesmo tempo.</summary>
        PillarTop,
        PillarMid,
        PillarBottom,

        /// <summary>Exposto acima e abaixo ao mesmo tempo.</summary>
        StripLeft,
        StripMid,
        StripRight,

        /// <summary>Sem nenhum vizinho.</summary>
        Single,

        /// <summary>Quatro ortogonais presentes, uma diagonal faltando.</summary>
        InnerTopLeft,
        InnerTopRight,
        InnerBottomLeft,
        InnerBottomRight,
    }

    /// <summary>
    /// Um terreno na folha: o retângulo que contém o nine-slice, mais papéis avulsos.
    ///
    /// O retângulo não é simplificação, é o que as folhas são. Medido pelo
    /// <see cref="TilesheetInspector"/>: High Forest tem dois blocos 5x4 e o Debug Map dois
    /// blocos 4x4, todos nine-slice puro. <see cref="Extra"/> fica para a folha que um dia
    /// trouxer canto côncavo ou pilar dedicado.
    /// </summary>
    public sealed class TerrainBlock
    {
        /// <summary>Nome do asset, sem extensão. Vira também o nome que aparece na Palette.</summary>
        public string AssetName;

        /// <summary>Coluna e linha na grade da folha, linha 0 no topo (índice = row * columns + col).</summary>
        public int ColLeft, ColRight, RowTop, RowBottom;

        public Tile.ColliderType Collider = Tile.ColliderType.Grid;

        /// <summary>Papéis fora do retângulo. Célula em (col, row) na grade da folha.</summary>
        public Dictionary<TerrainRole, Vector2Int[]> Extra;
    }

    /// <summary>
    /// Catálogo dos terrenos de cada folha.
    ///
    /// Todos os retângulos aqui saíram do relatório do <see cref="TilesheetInspector"/> sobre
    /// os pixels da folha, não de leitura a olho: o mapa de classes mostra o miolo repetido e
    /// o retângulo do nine-slice em volta dele.
    /// </summary>
    public static class TerrainBlocks
    {
        public const string DebugMapSet = "DebugMap";
        public const string HighForestSet = "HighForest";

        /// <summary>
        /// High Forest, grade 25x25.
        ///
        /// As linhas 0 e 5 ficam de fora de propósito: são a franja que transborda para cima
        /// (o tufo de grama e a ponta dos troncos), desenhadas para a célula ACIMA do bloco.
        /// Entrariam como topo errado no autotile; o lugar delas é a camada de decoração.
        ///
        /// Os dois blocos compartilham corpo e base — as linhas 2..4 e 7..9 são pixel a pixel
        /// idênticas. Só a linha de topo difere: grama numa, tronco cortado na outra.
        /// </summary>
        private static readonly TerrainBlock[] HighForest =
        {
            new TerrainBlock
            {
                AssetName = "RT_Forest_Grass",
                ColLeft = 0, ColRight = 4, RowTop = 1, RowBottom = 4,
            },
            new TerrainBlock
            {
                AssetName = "RT_Forest_Wood",
                ColLeft = 0, ColRight = 4, RowTop = 6, RowBottom = 9,
            },
        };

        /// <summary>
        /// Debug Map, grade 26x29. Os dois blocos de 64 px do greybox.
        ///
        /// A célula do canto superior esquerdo tem o texto "64" desenhado e não existe canto
        /// limpo em lugar nenhum da folha. Fica assim: num greybox o rótulo até ajuda a marcar
        /// onde o bloco começa.
        /// </summary>
        private static readonly TerrainBlock[] DebugMap =
        {
            new TerrainBlock
            {
                AssetName = "RT_Ground",
                ColLeft = 0, ColRight = 3, RowTop = 0, RowBottom = 3,
            },
            new TerrainBlock
            {
                AssetName = "RT_Ground_Brown",
                ColLeft = 8, ColRight = 11, RowTop = 0, RowBottom = 3,
            },
        };

        public static IReadOnlyList<TerrainBlock> For(string setName)
        {
            return setName switch
            {
                HighForestSet => HighForest,
                DebugMapSet => DebugMap,
                _ => System.Array.Empty<TerrainBlock>(),
            };
        }
    }
}
