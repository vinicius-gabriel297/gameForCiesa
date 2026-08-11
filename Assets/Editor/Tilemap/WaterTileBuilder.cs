using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Warana.EditorTools
{
    /// <summary>
    /// Água do High Forest: uma <see cref="RuleTile"/> que sabe onde é superfície e um
    /// <see cref="AnimatedTile"/> de espuma para a camada de decoração.
    ///
    /// <b>O que a folha realmente tem</b> (medido pelo <see cref="TilesheetInspector"/>, porque
    /// olhando a folha a estrutura engana):
    /// - linha 18: transparente nos 12 px de cima, espuma só nos 4 de baixo. Não é a superfície
    ///   da água — é a espuma que vai na célula ACIMA da poça, igual à franja de grama.
    ///   Quatro quadros que rolam 4 px: deslocar a célula 6 em 4 px dá a célula 7 exata.
    /// - linha 19: a superfície de verdade, banda clara em cima e corpo embaixo. Quatro quadros,
    ///   com 73 px de 256 diferindo entre vizinhos (o controle contra o corpo dá 256).
    /// - linha 20: corpo. Quatro quadros que rolam 3 px, também com casamento exato.
    ///
    /// <b>A cascata das colunas 3..5 ficou de fora, e por medição.</b> Entre colunas vizinhas
    /// há 36..61 px de diferença e NENHUM deslocamento em toro melhora isso (o melhor é sempre
    /// (0,0)) — ao contrário da poça, onde deslocar 12 px derruba a diferença de 73 para 26.
    /// São três variantes espaciais para uma queda larga não ficar repetida, não três quadros.
    /// Animá-las seria inventar movimento que a arte não tem; elas continuam Tiles comuns.
    /// </summary>
    public static class WaterTileBuilder
    {
        /// <summary>Índices na folha do High Forest (25 colunas), linha 0 no topo.</summary>
        private static readonly int[] FoamFrames = { 456, 457, 458, 459 };     // row 18, cols 6..9
        private static readonly int[] SurfaceFrames = { 481, 482, 483, 484 };  // row 19, cols 6..9
        private static readonly int[] BodyFrames = { 506, 507, 508, 509 };     // row 20, cols 6..9

        /// <summary>
        /// Quadros por segundo. Min e Max iguais não é descuido: o RuleTile sorteia
        /// Random.Range(min, max) POR CÉLULA, então qualquer folga faz cada pedaço da poça
        /// animar num ritmo próprio e a água vira chuvisco em vez de onda.
        /// </summary>
        private const float FramesPerSecond = 4f;

        /// <summary>Gera a água da folha. Devolve o que saiu, na ordem em que vai para a palette.</summary>
        public static List<TileBase> Build(
            string folder, SortedDictionary<int, Sprite> sprites)
        {
            var result = new List<TileBase>();

            RuleTile water = BuildWater($"{folder}/RT_Forest_Water.asset", sprites);
            if (water != null) result.Add(water);

            AnimatedTile foam = BuildAnimated(
                $"{folder}/AT_Water_Foam.asset", sprites, FoamFrames, Tile.ColliderType.None);
            if (foam != null) result.Add(foam);

            return result;
        }

        /// <summary>
        /// Superfície e corpo. As duas regras são Animation de propósito: GetTileAnimationData
        /// procura a PRIMEIRA regra com saída animada que casar, sem olhar qual venceu no
        /// desenho — misturar Single com Animation faz o par dessincronizar em silêncio assim
        /// que alguém acrescentar uma regra.
        ///
        /// Sem colisão: água é para atravessar, e o CompositeCollider2D do chão não deve
        /// ganhar geometria de poça.
        /// </summary>
        private static RuleTile BuildWater(string path, SortedDictionary<int, Sprite> sprites)
        {
            Sprite[] surface = Frames(sprites, SurfaceFrames);
            Sprite[] body = Frames(sprites, BodyFrames);

            if (surface.Length == 0 || body.Length == 0)
            {
                Debug.LogError($"[Autotile] Água não encontrada na folha para '{path}'. " +
                               "Rode 'Waraná/Pixel Art/Configurar Legacy Fantasy' antes.");
                return null;
            }

            var ruleTile = ScriptableObject.CreateInstance<RuleTile>();
            ruleTile.m_DefaultSprite = surface[0];
            ruleTile.m_DefaultColliderType = Tile.ColliderType.None;
            ruleTile.m_TilingRules = new List<RuleTile.TilingRule>
            {
                // Disjuntas e exaustivas, como no terreno: ou tem água em cima, ou não tem.
                Animated(surface, (Vector3Int.up, false)),
                Animated(body, (Vector3Int.up, true)),
            };

            for (int i = 0; i < ruleTile.m_TilingRules.Count; i++) ruleTile.m_TilingRules[i].m_Id = i;

            return TileAssetWriter.Save(ruleTile, path);
        }

        private static AnimatedTile BuildAnimated(
            string path, SortedDictionary<int, Sprite> sprites, int[] indices, Tile.ColliderType collider)
        {
            Sprite[] frames = Frames(sprites, indices);
            if (frames.Length == 0)
            {
                Debug.LogError($"[Autotile] Quadros não encontrados na folha para '{path}'.");
                return null;
            }

            var tile = ScriptableObject.CreateInstance<AnimatedTile>();
            tile.m_AnimatedSprites = frames;
            tile.m_MinSpeed = FramesPerSecond;
            tile.m_MaxSpeed = FramesPerSecond;
            tile.m_TileColliderType = collider;

            return TileAssetWriter.Save(tile, path);
        }

        private static RuleTile.TilingRule Animated(
            Sprite[] frames, params (Vector3Int Dir, bool IsThis)[] neighbors)
        {
            var rule = new RuleTile.TilingRule
            {
                m_Sprites = frames,
                m_ColliderType = Tile.ColliderType.None,
                m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_RandomTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Animation,
                m_MinAnimationSpeed = FramesPerSecond,
                m_MaxAnimationSpeed = FramesPerSecond,
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

        private static Sprite[] Frames(SortedDictionary<int, Sprite> sprites, int[] indices)
        {
            var found = new List<Sprite>(indices.Length);
            foreach (int index in indices)
            {
                if (sprites.TryGetValue(index, out Sprite sprite)) found.Add(sprite);
            }

            return found.ToArray();
        }
    }
}
