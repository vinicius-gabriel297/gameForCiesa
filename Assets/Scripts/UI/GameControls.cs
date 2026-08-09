using System.Text;

namespace Warana.UI
{
    /// <summary>
    /// Os comandos do jogo em um lugar só.
    ///
    /// Existe porque a mesma informação aparece em três lugares que ninguém atualiza
    /// junto: a abertura da fase, o painel de controles do menu e a página da loja. A
    /// tela de abertura já tinha divergido — listava mover, pular e canalizar, e não
    /// citava o ataque, num jogo em que o combate é metade da fase.
    ///
    /// <para>Os textos abaixo descrevem o que está em
    /// <c>Assets/Settings/InputSystem_Actions.inputactions</c>. Mexeu lá, mexa aqui.</para>
    /// </summary>
    public static class GameControls
    {
        /// <summary>Um comando e como acioná-lo em cada aparelho.</summary>
        public readonly struct Binding
        {
            public readonly string Action;
            public readonly string Keyboard;
            public readonly string Gamepad;

            public Binding(string action, string keyboard, string gamepad)
            {
                Action = action;
                Keyboard = keyboard;
                Gamepad = gamepad;
            }
        }

        /// <summary>Nomes de botão seguem a nomenclatura do controle de Xbox.</summary>
        public static readonly Binding[] All =
        {
            new Binding("Mover",     "A / D  ou  ← →",                  "Analógico esquerdo ou direcional"),
            new Binding("Pular",     "Espaço",                          "A"),
            new Binding("Atacar",    "Botão esquerdo do mouse  ou  Z",  "X"),
            new Binding("Canalizar", "Botão direito do mouse  ou  X",   "Gatilho direito"),
            new Binding("Pausar",    "Esc",                             "Start"),
        };

        /// <summary>Texto mostrado quando dá para pular uma cena dirigida.</summary>
        public const string SkipHint = "Esc  —  pular";

        /// <summary>
        /// Versão curta para a abertura da fase: só teclado, porque ali o texto divide
        /// a tela com o jogo já rodando e uma segunda coluna competiria com ele.
        /// </summary>
        public static string Overlay()
        {
            var sb = new StringBuilder();

            foreach (Binding b in All)
            {
                if (b.Action == "Pausar") continue; // não é comando de jogo, e a fase acabou de começar
                sb.Append(b.Action).Append(":  ").Append(b.Keyboard).Append('\n');
            }

            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// Versão completa, com gamepad, para o painel de controles do menu. Duas linhas
        /// por comando em vez de três: a lista precisa caber no painel sem rolagem, e um
        /// cabeçalho único diz o que é cada coluna melhor do que repetir "Teclado" e
        /// "Controle" cinco vezes.
        /// </summary>
        public static string Panel()
        {
            var sb = new StringBuilder();
            sb.Append("<size=65%>Teclado   ·   Controle</size>\n\n");

            foreach (Binding b in All)
            {
                sb.Append("<b>").Append(b.Action).Append("</b>\n")
                  .Append("<size=80%>").Append(b.Keyboard)
                  .Append("   ·   ").Append(b.Gamepad).Append("</size>\n\n");
            }

            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// Texto puro para colar na descrição da página da loja (itch.io). Sem
        /// caracteres que dependam da fonte do jogo, porque quem renderiza é o site.
        /// </summary>
        public static string StorePage()
        {
            var sb = new StringBuilder();
            sb.Append("CONTROLES\n\n");

            foreach (Binding b in All)
                sb.Append(b.Action).Append(" — ").Append(b.Keyboard)
                  .Append("  |  Controle: ").Append(b.Gamepad).Append('\n');

            return sb.ToString().TrimEnd('\n');
        }
    }
}
