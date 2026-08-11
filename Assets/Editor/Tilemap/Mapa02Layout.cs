using System.Collections.Generic;
using UnityEngine;

namespace Warana.EditorTools
{
    /// <summary>
    /// Retângulo em unidades de mundo. Esquerda e base inclusivas, direita e topo exclusivos —
    /// a mesma convenção do <c>FillRect</c> dos outros builders, para que dois retângulos
    /// encostados não pintem a mesma coluna duas vezes.
    /// </summary>
    public readonly struct Area
    {
        public readonly float X0, Y0, X1, Y1;

        public Area(float x0, float y0, float x1, float y1)
        {
            X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
        }

        public bool Contains(float x, float y) => x >= X0 && x < X1 && y >= Y0 && y < Y1;
    }

    /// <summary>
    /// Escada em ziguezague: degraus alternando entre duas colunas, subindo <see cref="Rise"/>
    /// por vez.
    ///
    /// As colunas são disjuntas de propósito. Degraus sobrepostos em X fazem o de cima virar
    /// teto do de baixo e o pulo bate a cabeça; separados por <see cref="Gap"/>, cada degrau é
    /// um pulo diagonal limpo.
    /// </summary>
    public sealed class Ladder
    {
        /// <summary>Borda esquerda da primeira coluna.</summary>
        public float X;

        public float Width = 3f;

        /// <summary>Vão horizontal entre as duas colunas.</summary>
        public float Gap = 1.5f;

        /// <summary>Topo do primeiro degrau e teto da série (inclusivo).</summary>
        public float YFrom, YTo;

        public float Rise = 1.5f;
        public float Thickness = 0.5f;

        /// <summary>Verdadeiro pinta na tilemap de plataformas (madeira) em vez do chão.</summary>
        public bool Wood;

        public IEnumerable<Area> Steps()
        {
            int index = 0;
            for (float y = YFrom; y <= YTo + 0.001f; y += Rise, index++)
            {
                float x = index % 2 == 0 ? X : X + Width + Gap;
                yield return new Area(x, y - Thickness, x + Width, y);
            }
        }
    }

    /// <summary>Bolsão que hoje não tem como ser alcançado, e qual habilidade abriria.</summary>
    public readonly struct AbilityGate
    {
        public readonly Area Where;
        public readonly string Ability;

        public AbilityGate(Area where, string ability)
        {
            Where = where; Ability = ability;
        }
    }

    /// <summary>
    /// Planta do Mapa 02 — <i>Noçoquém, as Raízes de Waraná</i>.
    ///
    /// <para><b>Topologia.</b> Não é um corredor: é um poço central (Noçoquém) com quatro bocas,
    /// dois eixos verticais nas pontas e três laços fechados. O jogador desce no meio, atravessa
    /// o subterrâneo, e sobe de novo pelas beiradas — cada volta ao ponto de partida abre um
    /// atalho permanente. É a estrutura de Forgotten Crossroads: um cruzamento vertical que o
    /// mapa inteiro orbita, não um caminho que se percorre uma vez.</para>
    ///
    /// <para><b>O que tranca.</b> Sem item de progressão: tranca a geometria. As quedas de mão
    /// única (poço central, ponte da copa) só descem, porque as saliências param mais de 2
    /// unidades abaixo da borda e o pulo alcança 1,75. Isso troca "chave que o jogador carrega"
    /// por "chave que o jogador constrói andando", que é o que dá para fazer honestamente com o
    /// moveset atual — pular, atacar e canalizar.</para>
    ///
    /// <para><b>Orçamento de movimento</b>, medido no <c>PlayerController2D</c> e conferido pelo
    /// <see cref="Mapa02Reachability"/>: o ápice do pulo é <b>1,75</b> (não 2,00 — a integração
    /// discreta come o resto), o alcance horizontal com 1,5 de ganho é ~2,4, e queda é livre.
    /// Daí <c>Rise = 1,5</c> e <c>Gap = 1,5</c> em todo caminho obrigatório, e degraus de 2,5+
    /// como parede deliberada.</para>
    /// </summary>
    public static class Mapa02Layout
    {
        // ------------------------------------------------------------------- dimensões

        /// <summary>
        /// 248 x 82 unidades contra os 107 x 12,25 do Mapa_01 — 15x a área. A altura é o que
        /// muda o gênero: 82 unidades cabem quatro andares empilhados, e é entre andares que
        /// um metroidvania acontece.
        /// </summary>
        public const float MinX = -140f, MaxX = 108f, MinY = -48f, MaxY = 34f;

        public static readonly Vector3 PlayerSpawn = new Vector3(-132f, 1f, 0f);

        /// <summary>
        /// Abaixo desta linha o chão é pintado com a rocha marrom do Debug Map em vez da grama
        /// do High Forest. Estratificar por altura, e não por sala, faz o corte cair sempre no
        /// mesmo lugar do mundo — o jogador aprende "marrom = fundo" em vez de decorar salas.
        /// </summary>
        public const float RockLine = -4f;


        // ------------------------------------------------------------------------ ar

        /// <summary>
        /// O espaço vazio: as salas. Tudo o que é rocha é derivado disto (uma casca em volta),
        /// então esta é a única lista que descreve o mapa jogável.
        ///
        /// As salas ao ar livre sobem até <see cref="MaxY"/> de propósito — assim ficam sem
        /// tampa, abertas para o parallax.
        /// </summary>
        public static readonly Area[] Open =
        {
            // ---- superfície oeste: a mata que Piatã já curou, o chão conhecido -----------
            new Area(-136f,   0f, -108f, MaxY), // R1 Clareira do Retorno — spawn
            new Area(-108f,   0f,  -86f, MaxY), // R2 Trilha das Samaúmas
            new Area( -86f,   0f,  -68f, MaxY), // R3 Mirante das Águas — termina no abismo

            // ---- o poço central ---------------------------------------------------------
            // A boca fica no chão do Mirante e o fundo a 44 unidades abaixo. Quem anda para o
            // leste cai; não há o que escalar de volta. É a porta de mão única do mapa.
            // Aberto até o teto do mapa, não até 26: a casca de rocha se forma na borda do que
            // é declarado, então um poço que "termina" em 26 ganha uma tampa de pedra em cima —
            // e a ponte da copa passa por ali a 24.
            new Area( -68f, -44f,  -54f, MaxY), // R4 Noçoquém

            // ---- superfície leste: o lado que só se alcança pelo subsolo -----------------
            new Area( -54f,   0f,  -30f,   8f), // R5 Passagem do Mel — túnel dentro do maciço
            // O céu por cima do maciço. Sem declarar isto, o espaço acima da Passagem não é
            // sala nem rocha — e a dilatação trata tudo o que não é sala como pedra, murando a
            // ponte da copa. O piso do vão fecha exatamente com a casca da Passagem (8..10 de
            // um lado, 10..12 do outro), então o maciço continua maciço e ainda ganha um teto
            // de 12 onde dá para andar.
            new Area( -54f,  12f,  -30f, MaxY), // R5b Lajes do Maciço
            new Area( -30f,   0f,    6f, MaxY), // R6 Terraço do Sol
            new Area(   6f,   0f,   30f, MaxY), // R7 Escadaria de Pedra
            new Area(  30f,   0f,   70f, MaxY), // R8 Câmara da Abominação
            new Area(  70f,   0f,  104f, MaxY), // R9 Bosque Sagrado — a árvore

            // ---- andar do meio ----------------------------------------------------------
            new Area(-112f, -22f, -100f, -14f), // R10 Galeria Inundada
            new Area(-100f, -44f,  -90f,  -8f), // R11 Chaminé das Raízes — eixo vertical oeste
            new Area(-100f,  -8f,  -94f,   0f), // R12 Fenda da Trilha — desemboca na superfície
            new Area( -54f, -22f,  -14f,  -6f), // R13 Cisterna
            new Area( -14f, -22f,   20f,  -8f), // R14 Raízes Fundas
            new Area(  20f, -22f,   30f,   0f), // R15 Subida das Raízes — eixo vertical leste

            // ---- andar profundo ---------------------------------------------------------
            new Area( -90f, -44f,  -68f, -30f), // R16 Águas de Uaicutéria
            new Area( -54f, -44f,  -14f, -30f), // R17 Ninho das Sombras
            new Area( -14f, -44f,   20f, -30f), // R18 Veia de Guaraná
            // Os dois poços que ligam o profundo ao andar do meio. 6 de largura: exatamente o
            // que a escada em ziguezague ocupa (2,5 + 1 + 2,5). Mais largo que isso e o último
            // degrau para longe da beira do piso; mais estreito e uma das colunas nasce dentro
            // da rocha.
            new Area( -41f, -30f,  -35f, -22f), // R19 Poço da Cisterna
            new Area(   9f, -30f,   15f, -22f), // R20 Chaminé da Veia

            // ---- bolsão trancado por habilidade -----------------------------------------
            // Fenda de 1,5 de largura e 9 de altura, encostada na parede oeste da Galeria: só
            // sobe quem tiver pulo na parede. Fica na ponta da sala de propósito — no meio ela
            // partiria a Galeria em dois e trancaria metade do andar atrás de uma habilidade
            // que ainda não existe.
            new Area(-110.5f, -22f, -109f, -13f), // chaminé estreita
            new Area(-113f,   -13f, -107f,  -9f), // o bolsão lá em cima
        };

        // ------------------------------------------------------------------ relevo

        /// <summary>Saliências de terra: degraus, prateleiras, pilares e estalactites.</summary>
        public static readonly Area[] Ledges =
        {
            // Clareira e trilha: relevo raso, só para a caminhada inicial não ser uma reta.
            new Area(-130f, 0f, -126f, 1.00f),
            new Area(-122f, 0f, -118f, 1.50f),
            new Area(-114f, 0f, -111f, 1.00f),
            new Area(-104f, 0f, -101f, 1.50f),
            new Area( -92f, 0f,  -89f, 1.00f),
            new Area( -80f, 0f,  -77f, 1.50f),

            // Mirante: um degrau alto na beira do poço. Serve de sacada — o jogador vê o vão
            // antes de cair nele, que é a diferença entre uma queda e uma armadilha.
            new Area( -72f, 0f, -69f, 1.5f),

            // Patamar leste do poço, 8 abaixo da boca. Só se chega nele pela Cisterna; quem cai
            // do Mirante passa a 6 unidades de distância e não consegue derivar até aqui.
            new Area( -60f, -8.5f, -54f, -8f),

            // Passagem do Mel: relevo baixo dentro do túnel.
            new Area( -48f, 0f, -45f, 1.00f),
            new Area( -38f, 0f, -35f, 1.50f),

            // Terraço do Sol.
            new Area( -26f, 0f, -23f, 1.50f),
            new Area( -12f, 0f,  -9f, 1.00f),
            new Area(  -4f, 0f,   0f, 1.50f),

            // Escadaria de Pedra: degraus largos subindo, o corredor de aproximação da chefe.
            new Area(   8f, 0f,  12f, 1.50f),
            new Area(  14f, 0f,  18f, 3.00f),
            new Area(  20f, 0f,  24f, 4.50f),

            // Câmara da Abominação: duas prateleiras baixas — cobertura na luta, sem virar
            // plataforma que anule o chefe.
            new Area(  36f, 0f,  39f, 1.50f),
            new Area(  60f, 0f,  63f, 1.50f),

            // Galeria Inundada: piso irregular e as duas paredes da fenda estreita. A parede
            // leste flutua 2,5 acima do piso — é por baixo dela que se entra na fenda; se ela
            // descesse até o chão, o bolsão não teria entrada nenhuma e a tranca deixaria de
            // ser "falta habilidade" para virar "não tem caminho".
            new Area(-111.5f, -22f, -110.5f, -13f), // parede oeste da fenda
            new Area(-109f, -19.5f, -108f, -13f),   // parede leste, suspensa
            new Area(-106f, -22f, -103f, -20.5f),
            new Area(-102f, -22f, -100f, -20.5f),

            // Cisterna: degraus de pedra sobre a água e o mezanino que leva ao poço central.
            new Area( -50f, -22f, -47f, -20.5f),
            new Area( -32f, -22f, -29f, -20.5f),
            new Area( -20f, -22f, -17f, -20.5f),
            // Mezanino: encosta no patamar do poço central. Para em -41 e não em -37 porque a
            // escada de madeira sobe justamente em -40..-37 — se o mezanino cobrisse a coluna
            // dela, o último degrau viraria porão do mezanino em vez de chegada.
            new Area( -54f, -8.5f, -41f, -8f),

            // Raízes Fundas: relevo e estalactites — as formas finas que o autotile resolve.
            new Area( -10f, -22f,  -7f, -20.5f),
            // Dois degraus de 1,5 em vez de um bloco de 3,0 — o topo de 3,0 fica 1,25 acima do
            // ápice do pulo e vira enfeite inalcançável.
            new Area(   0f, -22f,   4f, -20.5f),
            new Area(   2f, -22f,   4f, -19.0f),
            new Area(  12f, -22f,  15f, -20.5f),
            new Area(  -4f, -12f,  -3.5f, -8f), // estalactite
            new Area(   8f, -11.5f, 8.5f, -8f), // estalactite

            // Profundo: piso quebrado nas três salas.
            new Area( -86f, -44f, -83f, -42.5f),
            new Area( -74f, -44f, -71f, -42.5f),
            new Area( -50f, -44f, -47f, -42.5f),
            new Area( -37.5f, -44f, -34f, -42.5f), // 1,0 de folga até a escada, não 0,5: a fresta
                                                   // estreita virava um degrau que ninguém alcança
            new Area( -26f, -44f, -23f, -42.5f),
            new Area(  -8f, -44f,  -5f, -42.5f),
            new Area(   4f, -44f,   7f, -42.5f),
            new Area( -30f, -34f, -29.5f, -30f), // estalactite
            new Area(   0f, -34f,   0.5f, -30f), // estalactite
        };

        /// <summary>Escadas de terra.</summary>
        public static readonly Ladder[] Ladders =
        {
            // Poço central: do patamar leste (-8) até a boca da Passagem do Mel. Seis degraus
            // terminam na coluna da direita, encostada na parede do túnel — com cinco pararia
            // na coluna esquerda, a 4,5 de distância, e o patamar viraria uma prateleira morta.
            new Ladder { X = -61.5f, Width = 3f, Gap = 1.5f, YFrom = -6.5f, YTo = 1f },

            // Eixo vertical oeste: Chaminé das Raízes, do fundo (-44) até o andar do meio.
            new Ladder { X = -98.5f, Width = 3f, Gap = 1.5f, YFrom = -42.5f, YTo = -9.5f },

            // Fenda da Trilha: os últimos 8 até a superfície. Começa em -8 para encostar no
            // topo da Chaminé (-9,5) — com -6,5 a emenda entre as duas escadas abria 3
            // unidades, o dobro do pulo.
            new Ladder { X = -99.5f, Width = 2f, Gap = 1f, YFrom = -8f, YTo = -0.5f },

            // Do piso do profundo (-44) direto até o piso do meio (-22), atravessando o poço
            // sem emenda. Foram duas escadas empilhadas até eu medir: a de baixo terminava
            // rente ao teto do andar profundo, e sobrava meia unidade de espaço livre — o
            // jogador tem 0,83 de altura e simplesmente não cabia lá. Uma série contínua não
            // tem essa junta. O primeiro degrau em -43 fica 1,0 acima do chão, e o último em
            // -22 encosta na beira do piso de cima.
            new Ladder { X = -41f, Width = 2.5f, Gap = 1f, YFrom = -43f, YTo = -22f },
            new Ladder { X = 9f, Width = 2.5f, Gap = 1f, YFrom = -43f, YTo = -22f },

            // Eixo vertical leste: Subida das Raízes, do meio até o pé da Escadaria.
            new Ladder { X = 21f, Width = 3f, Gap = 1.5f, YFrom = -20.5f, YTo = -0.5f },

            // Terraço -> copa: quinze degraus, o único jeito de chegar na ponte.
            new Ladder { X = -28f, Width = 3f, Gap = 1.5f, YFrom = 1.5f, YTo = 22.5f },
        };

        // -------------------------------------------------------------- madeira

        /// <summary>Plataformas de madeira: tudo o que flutua.</summary>
        public static readonly Area[] WoodLedges =
        {
            // Tábua sobre a Fenda da Trilha. A fenda tem 6 de largura — o caminho obrigatório
            // do início não pode ter um buraco que o pulo não cruza, senão quem cai fica preso
            // do lado errado. A tábua no meio deixa dois vãos de 2,5: dá para pular por cima
            // indo para o leste e dá para se jogar dentro quando o atalho interessar.
            new Area(-97.5f, -0.5f, -95.5f, 0f),

            // Copa (opcional): sobe da ponte para o leste, 1,5 por plataforma e 2,0 de vão.
            new Area(-23f, 23.5f, -20f, 24f),
            new Area(-18f, 25.0f, -15f, 25.5f),
            new Area(-13f, 26.5f, -10f, 27f),
            new Area( -8f, 28.0f,  -5f, 28.5f),
            new Area( -3f, 29.5f,   0f, 30f),
            new Area(  2f, 31.0f,   5f, 31.5f),

            // --- trancado por habilidade ---
            // Plataforma isolada a 4,0 do chão, na Câmara: 2,25 acima do ápice do pulo. Estava
            // no Terraço até eu medir — e lá a copa passa por cima, então o jogador não subia
            // nela, ele *caía* nela vindo das plataformas de 30 unidades acima. Tranca de
            // altura só tranca onde não há nada mais alto por perto.
            new Area( 32f, 3.5f,  36f, 4f),

            // Última plataforma da copa, a 5,5 da anterior: vão de dash. Precisa ser aqui em
            // cima, e não no chão da Câmara como eu tinha posto — lá o jogador simplesmente
            // pulava do piso para a plataforma e a distância horizontal não trancava nada. A
            // 31 unidades do chão não existe "por baixo".
            new Area( 10.5f, 31f, 13.5f, 31.5f),
        };

        public static readonly Ladder[] WoodLadders =
        {
            // Cisterna: nove degraus sobre a água, do piso até o mezanino.
            new Ladder { X = -40f, Width = 3f, Gap = 1.5f, YFrom = -20.5f, YTo = -8.5f, Wood = true },
        };

        /// <summary>
        /// Ponte da copa: o atalho grande. Atravessa o mapa inteiro por cima, do Terraço até o
        /// Mirante, e larga o jogador 24 unidades acima do chão — ele cai, não volta, e reabre
        /// o oeste sem ter que refazer o subsolo. É o que transforma a segunda volta do mapa
        /// em trinta segundos em vez de cinco minutos.
        /// </summary>
        public static IEnumerable<Area> Bridge()
        {
            // Para em -80, dentro do Mirante: a oeste da boca do poço, senão o atalho
            // devolveria o jogador na beirada do buraco que ele acabou de escapar.
            // A 24, e não a 25: o topo da escada do Terraço para em 22,5, e 2,5 de degrau é
            // meia unidade acima do que o pulo alcança.
            for (float right = -30f; right >= -80f; right -= 5.25f)
            {
                yield return new Area(right - 3f, 23.5f, right, 24f);
            }
        }

        // ---------------------------------------------------------------------- água

        /// <summary>Poças. Sem colisão: dá para atravessar andando.</summary>
        public static readonly Area[] Water =
        {
            new Area(-112f, -22f, -100f, -20.5f), // Galeria Inundada
            new Area( -54f, -22f,  -14f, -20.5f), // Cisterna
            new Area( -90f, -44f,  -68f, -42.5f), // Águas de Uaicutéria
            new Area( -14f, -44f,   20f, -42.5f), // Veia de Guaraná
        };

        // -------------------------------------------------------------------- atores

        /// <summary>
        /// Mad Ghosts. A densidade sobe conforme se desce: a superfície oeste é o território
        /// conhecido, o profundo é o que Jurupari tomou.
        /// </summary>
        public static readonly Vector2[] MadGhosts =
        {
            // superfície oeste — poucos, espaçados
            new Vector2(-128f, 0.6f), new Vector2(-118f, 1.6f), new Vector2(-104f, 1.6f),
            new Vector2( -92f, 1.1f), new Vector2( -76f, 0.6f),
            // túnel e terraço
            new Vector2( -48f, 1.1f), new Vector2( -38f, 1.6f),
            new Vector2( -24f, 0.6f), new Vector2( -12f, 1.1f), new Vector2(  0f, 0.6f),
            // escadaria e câmara
            new Vector2(  10f, 1.6f), new Vector2(  22f, 4.6f),
            new Vector2(  38f, 1.6f), new Vector2(  48f, 0.6f), new Vector2( 58f, 0.6f),
            // andar do meio
            new Vector2(-104f, -20.0f), new Vector2(-101f, -21.4f),
            new Vector2( -48f, -21.4f), new Vector2( -30f, -20.4f), new Vector2(-20f, -20.4f),
            new Vector2(  -8f, -20.4f), new Vector2(   2f, -18.9f), new Vector2( 14f, -20.4f),
            // profundo
            new Vector2( -84f, -42.4f), new Vector2( -74f, -42.4f), new Vector2(-60f, -43.4f),
            // -31 e não -36: em -36 o fantasma nasce embaixo do segundo degrau da escada do
            // Poço da Cisterna, com o corpo dentro da madeira.
            new Vector2( -48f, -42.4f), new Vector2( -31f, -43.4f), new Vector2(-24f, -42.4f),
            new Vector2(  -6f, -42.4f), new Vector2(   6f, -42.4f), new Vector2( 16f, -43.4f),
        };

        public static readonly Vector2 Abomination = new Vector2(52f, 0.55f);

        /// <summary>Onde fica a árvore sagrada (base do tronco), no fundo do Bosque.</summary>
        public static readonly Vector2 SacredTree = new Vector2(88f, 0f);

        /// <summary>
        /// Corações. Os cinco primeiros premiam desvio e são alcançáveis hoje; os três últimos
        /// estão dentro dos <see cref="AbilityGates"/> e ficam visíveis de propósito — bolsão
        /// que não se vê não ensina nada.
        /// </summary>
        public static readonly Vector2[] Hearts =
        {
            new Vector2(-102f, -20.2f), // fundo da Galeria, depois da fenda
            new Vector2( -85f, -42.8f), // fundo das Águas, atrás da queda
            new Vector2( -52f,  -7.6f), // mezanino da Cisterna, ao lado do poço
            new Vector2(  26f, -20.8f), // pé do eixo vertical leste
            new Vector2(   3.5f, 31.8f), // fim da copa
            // --- trancados ---
            new Vector2(  34f,  4.3f),   // pulo duplo
            new Vector2(  12f, 31.8f),   // dash
            new Vector2(-110f, -12.7f),  // pulo na parede
        };

        /// <summary>
        /// Os três bolsões que o moveset atual não abre. Declarados aqui para o
        /// <see cref="Mapa02Reachability"/> saber separar "trancado de propósito" de "erro de
        /// planta" — sem esta lista o relatório vira ruído e o teste para de valer.
        /// </summary>
        public static readonly AbilityGate[] AbilityGates =
        {
            new AbilityGate(new Area(31f, 3f, 37f, 6f), "Pulo duplo"),
            new AbilityGate(new Area(10f, 30.5f, 14f, 33f), "Dash"),
            new AbilityGate(new Area(-113f, -13.5f, -107f, -9f), "Pulo na parede"),
        };
    }
}
