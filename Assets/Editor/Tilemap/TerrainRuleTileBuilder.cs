using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Warana.EditorTools
{
    /// <summary>
    /// Monta a <see cref="RuleTile"/> de um <see cref="TerrainBlock"/>: pinta-se a forma do
    /// terreno e a Unity escolhe borda, canto e miolo sozinha.
    ///
    /// <b>Por que RuleTile e não o AutoTile nativo do 2D Tilemap Extras.</b> O AutoTile parece
    /// a resposta óbvia e não serve para estas folhas: Mask_3x3 exige 47 sprites (o template de
    /// exemplo do próprio pacote tem 47) e cada bloco daqui tem 13; Mask_2x2 (15 sprites) exige
    /// os dois ortogonais E a diagonal por quadrante, então um pilar de uma célula zera a
    /// máscara e o pilar inteiro vira "célula isolada"; o dicionário de máscaras é internal e a
    /// única porta pública mapeia um sprite por máscara, sem repetir; e AutoTile não sobrescreve
    /// GetTileAnimationData, ou seja, não anima — a água ficaria de fora.
    ///
    /// <b>Como as regras são montadas.</b> Toda regra declara os quatro vizinhos ortogonais,
    /// então os 16 casos são disjuntos e exaustivos e a ordem entre eles não importa — ao
    /// contrário do nine-slice de condições parciais que existia antes, onde trocar duas
    /// linhas de lugar mudava o desenho. Só os cantos côncavos olham diagonal, e por isso são
    /// os únicos que precisam vir antes.
    ///
    /// <b>Limitação conhecida.</b> RuleTiles diferentes não se fundem: a comparação de vizinho
    /// é de ponteiro, então grama encostada em madeira faz as duas desenharem borda externa e
    /// aparece costura. Resolver isso pede RuleTile&lt;T&gt; com vizinho customizado.
    /// </summary>
    public static class TerrainRuleTileBuilder
    {
        /// <summary>
        /// 1/pi. O default do campo é 0.5, e aí (x + 100000) * 0.5 cai exatamente em ponto de
        /// rede do Perlin sempre que x é par — o ruído devolve 0.5 cravado e um quarto das
        /// células escolhe sempre a mesma variante, formando xadrez em parede longa.
        ///
        /// Medido numa parede 24x8 com 6 variantes: com 0.5 uma variante leva 98 das 192
        /// células e linhas inteiras saem idênticas; com 0.3183 a mais comum leva 60 e não
        /// sobra linha travada. (Nos dois casos a variante do topo da faixa quase não aparece:
        /// o Perlin se concentra no meio, e isso é do pacote, não da escala.)
        /// </summary>
        private const float PerlinScale = 0.3183f;

        /// <summary>
        /// Máscara de cada papel: os quatro ortogonais, sem "não me importo". São 16 linhas
        /// porque são 2^4 combinações — a tabela é a especificação inteira do autotile.
        /// </summary>
        private static readonly (TerrainRole Role, bool Up, bool Down, bool Left, bool Right)[] Orthogonal =
        {
            (TerrainRole.Center,       true,  true,  true,  true),
            (TerrainRole.Top,          false, true,  true,  true),
            (TerrainRole.Bottom,       true,  false, true,  true),
            (TerrainRole.Left,         true,  true,  false, true),
            (TerrainRole.Right,        true,  true,  true,  false),
            (TerrainRole.TopLeft,      false, true,  false, true),
            (TerrainRole.TopRight,     false, true,  true,  false),
            (TerrainRole.BottomLeft,   true,  false, false, true),
            (TerrainRole.BottomRight,  true,  false, true,  false),
            (TerrainRole.PillarMid,    true,  true,  false, false),
            (TerrainRole.PillarTop,    false, true,  false, false),
            (TerrainRole.PillarBottom, true,  false, false, false),
            (TerrainRole.StripMid,     false, false, true,  true),
            (TerrainRole.StripLeft,    false, false, false, true),
            (TerrainRole.StripRight,   false, false, true,  false),
            (TerrainRole.Single,       false, false, false, false),
        };

        /// <summary>Canto côncavo: os quatro ortogonais presentes e uma diagonal faltando.</summary>
        private static readonly (TerrainRole Role, Vector3Int Diagonal)[] InnerCorners =
        {
            (TerrainRole.InnerTopLeft, new Vector3Int(-1, 1, 0)),
            (TerrainRole.InnerTopRight, new Vector3Int(1, 1, 0)),
            (TerrainRole.InnerBottomLeft, new Vector3Int(-1, -1, 0)),
            (TerrainRole.InnerBottomRight, new Vector3Int(1, -1, 0)),
        };

        /// <summary>
        /// Para onde cair quando a folha não tem arte do papel. Resolver aqui, e não deixando
        /// "a regra mais genérica pegar", é o que mantém as 16 regras independentes de ordem:
        /// toda regra sai do builder já com sprite, mesmo que emprestado.
        /// </summary>
        private static readonly Dictionary<TerrainRole, TerrainRole> Fallback = new()
        {
            { TerrainRole.Top, TerrainRole.Center },
            { TerrainRole.Bottom, TerrainRole.Center },
            { TerrainRole.Left, TerrainRole.Center },
            { TerrainRole.Right, TerrainRole.Center },
            { TerrainRole.TopLeft, TerrainRole.Top },
            { TerrainRole.TopRight, TerrainRole.Top },
            { TerrainRole.BottomLeft, TerrainRole.Bottom },
            { TerrainRole.BottomRight, TerrainRole.Bottom },
            { TerrainRole.PillarMid, TerrainRole.Center },
            { TerrainRole.PillarTop, TerrainRole.Top },
            { TerrainRole.PillarBottom, TerrainRole.Bottom },

            // A faixa fina de uma célula de altura tem topo e base expostos ao mesmo tempo.
            // Só dá para mostrar um dos dois, e o que o jogador vê andando em cima é o topo.
            { TerrainRole.StripMid, TerrainRole.Top },
            { TerrainRole.StripLeft, TerrainRole.TopLeft },
            { TerrainRole.StripRight, TerrainRole.TopRight },
            { TerrainRole.Single, TerrainRole.PillarTop },

            { TerrainRole.InnerTopLeft, TerrainRole.Center },
            { TerrainRole.InnerTopRight, TerrainRole.Center },
            { TerrainRole.InnerBottomLeft, TerrainRole.Center },
            { TerrainRole.InnerBottomRight, TerrainRole.Center },
        };

        /// <summary>
        /// Grava (ou atualiza) a RuleTile do bloco. Devolve null se a folha não tiver nem o
        /// miolo, que é o único papel sem substituto.
        /// </summary>
        public static RuleTile Build(
            TerrainBlock block, SortedDictionary<int, Sprite> sprites, int columns, string assetPath)
        {
            Dictionary<TerrainRole, Sprite[]> art = CollectArt(block, sprites, columns);

            if (!art.TryGetValue(TerrainRole.Center, out Sprite[] center) || center.Length == 0)
            {
                Debug.LogError($"[Autotile] '{block.AssetName}': o miolo do bloco " +
                               $"(cols {block.ColLeft}..{block.ColRight}, rows {block.RowTop}..{block.RowBottom}) " +
                               "está vazio na folha. Rode 'Waraná/Pixel Art/Configurar Legacy Fantasy' antes.");
                return null;
            }

            var ruleTile = ScriptableObject.CreateInstance<RuleTile>();
            ruleTile.m_DefaultSprite = center[0];
            ruleTile.m_DefaultColliderType = block.Collider;
            ruleTile.m_TilingRules = new List<RuleTile.TilingRule>(Orthogonal.Length + InnerCorners.Length);

            // Cantos côncavos primeiro: são as únicas regras que olham diagonal, e casariam
            // junto com a de miolo (que também exige os quatro ortogonais presentes).
            foreach ((TerrainRole role, Vector3Int diagonal) in InnerCorners)
            {
                if (!art.ContainsKey(role)) continue; // nenhuma folha do projeto tem; sem arte, sem regra

                ruleTile.m_TilingRules.Add(Rule(
                    Resolve(role, art, block.AssetName), block.Collider,
                    (Vector3Int.up, true), (Vector3Int.down, true),
                    (Vector3Int.left, true), (Vector3Int.right, true),
                    (diagonal, false)));
            }

            foreach ((TerrainRole role, bool up, bool down, bool left, bool right) in Orthogonal)
            {
                ruleTile.m_TilingRules.Add(Rule(
                    Resolve(role, art, block.AssetName), block.Collider,
                    (Vector3Int.up, up), (Vector3Int.down, down),
                    (Vector3Int.left, left), (Vector3Int.right, right)));
            }

            for (int i = 0; i < ruleTile.m_TilingRules.Count; i++)
            {
                // Id não faz diferença para RuleTile pura, mas AdvancedRuleOverrideTile casa
                // regra por id — deixar zerado agora custaria uma migração depois.
                ruleTile.m_TilingRules[i].m_Id = i;
            }

            return TileAssetWriter.Save(ruleTile, assetPath);
        }

        // ---------------------------------------------------------------------------- arte

        /// <summary>Lê os nove papéis do retângulo e depois aplica o que vier em Extra.</summary>
        private static Dictionary<TerrainRole, Sprite[]> CollectArt(
            TerrainBlock block, SortedDictionary<int, Sprite> sprites, int columns)
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

            int midColFrom = block.ColLeft + 1, midColTo = block.ColRight - 1;
            int midRowFrom = block.RowTop + 1, midRowTo = block.RowBottom - 1;

            var art = new Dictionary<TerrainRole, Sprite[]>
            {
                [TerrainRole.TopLeft] = Cells(block.RowTop, block.RowTop, block.ColLeft, block.ColLeft),
                [TerrainRole.Top] = Cells(block.RowTop, block.RowTop, midColFrom, midColTo),
                [TerrainRole.TopRight] = Cells(block.RowTop, block.RowTop, block.ColRight, block.ColRight),
                [TerrainRole.Left] = Cells(midRowFrom, midRowTo, block.ColLeft, block.ColLeft),
                [TerrainRole.Center] = Cells(midRowFrom, midRowTo, midColFrom, midColTo),
                [TerrainRole.Right] = Cells(midRowFrom, midRowTo, block.ColRight, block.ColRight),
                [TerrainRole.BottomLeft] = Cells(block.RowBottom, block.RowBottom, block.ColLeft, block.ColLeft),
                [TerrainRole.Bottom] = Cells(block.RowBottom, block.RowBottom, midColFrom, midColTo),
                [TerrainRole.BottomRight] = Cells(block.RowBottom, block.RowBottom, block.ColRight, block.ColRight),
            };

            foreach (TerrainRole role in new List<TerrainRole>(art.Keys))
            {
                if (art[role].Length == 0) art.Remove(role);
            }

            if (block.Extra == null) return art;

            foreach (var pair in block.Extra)
            {
                var found = new List<Sprite>(pair.Value.Length);
                foreach (Vector2Int cell in pair.Value)
                {
                    if (sprites.TryGetValue(cell.y * columns + cell.x, out Sprite sprite)) found.Add(sprite);
                }

                if (found.Count > 0) art[pair.Key] = found.ToArray();
            }

            return art;
        }

        /// <summary>Segue a cadeia de substituição até achar arte, avisando no console.</summary>
        private static Sprite[] Resolve(
            TerrainRole role, Dictionary<TerrainRole, Sprite[]> art, string context)
        {
            TerrainRole current = role;

            while (!art.TryGetValue(current, out Sprite[] found) || found.Length == 0)
            {
                if (!Fallback.TryGetValue(current, out TerrainRole next))
                {
                    // Só o miolo não tem substituto, e Build já barrou esse caso antes.
                    Debug.LogError($"[Autotile] {context}: papel '{role}' sem arte e sem substituto.");
                    return System.Array.Empty<Sprite>();
                }

                current = next;
            }

            if (current != role)
            {
                Debug.Log($"[Autotile] {context}: '{role}' não existe na folha, usando '{current}'.");
            }

            return art[current];
        }

        // --------------------------------------------------------------------------- regras

        /// <summary>
        /// Uma regra. Cada vizinho é (direção, precisa ser deste mesmo tile) — posição que não
        /// entrar nas listas é "não me importo", porque RuleMatch devolve true no default.
        ///
        /// Fixed nos dois transforms não é preferência, é medição: espelhar a lateral do High
        /// Forest muda 143 px de 256 (a rocha é assimétrica de propósito) e no Debug Map piora,
        /// porque a linha de grade fica sempre na direita e embaixo de cada célula.
        /// </summary>
        private static RuleTile.TilingRule Rule(
            Sprite[] variants, Tile.ColliderType collider, params (Vector3Int Dir, bool IsThis)[] neighbors)
        {
            var rule = new RuleTile.TilingRule
            {
                m_Sprites = variants,
                m_ColliderType = collider,
                m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_RandomTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_PerlinScale = PerlinScale,
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
    }
}
