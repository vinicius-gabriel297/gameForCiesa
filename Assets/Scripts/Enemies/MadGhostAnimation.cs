namespace Warana.Enemies
{
    /// <summary>
    /// Nomes de parâmetros/estados e a tabela de clips do Mad Ghost.
    /// Compartilhado entre o runtime (<see cref="MadGhost"/>, <see cref="MadGhostAnimator"/>)
    /// e o gerador do controller no editor — assim as durações do comportamento e o
    /// comprimento das animações nunca saem de sincronia.
    ///
    /// A ordem de <see cref="Clips"/> é a ordem das linhas na folha
    /// "Mad Ghost-Sheet Separated tags.png" (9 x 7 células de 64x64, uma linha por tag).
    /// Mexer na ordem daqui reordena o corte da folha — não reordene sem conferir a arte.
    /// </summary>
    public static class MadGhostAnimation
    {
        public static class Param
        {
            /// <summary>Velocidade horizontal absoluta, em unidades/segundo.</summary>
            public const string Speed = "Speed";

            /// <summary>Dispara o saque da arma — o início da sequência de ataque.</summary>
            public const string Unsheathe = "Unsheathe";

            /// <summary>Dispara o golpe em si, depois do saque.</summary>
            public const string Attack = "Attack";

            /// <summary>Dispara o guardar da arma — a recuperação depois do golpe.</summary>
            public const string Sheathe = "Sheathe";

            public const string Hit = "Hit";

            public const string Dead = "Dead";
        }

        public static class State
        {
            public const string Idle = "Idle";
            public const string Move = "Move";

            /// <summary>Saca a arma. É a telegrafia: o aviso de que o golpe vem.</summary>
            public const string Unsheathe = "Unsheathe";

            public const string Attack = "Attack";

            /// <summary>Guarda a arma. É a janela de punição: lento e sem defesa.</summary>
            public const string Sheathe = "Sheathe";

            public const string Hit = "Hit";
            public const string Death = "Death";
        }

        /// <summary>Abaixo disso a locomoção conta como parada.</summary>
        public const float SpeedThreshold = 0.05f;

        /// <summary>Uma animação: quantos frames, a que cadência, e se repete.</summary>
        public readonly struct ClipDef
        {
            public readonly string Name;
            public readonly int Frames;
            public readonly float FrameRate;
            public readonly bool Loop;

            public ClipDef(string name, int frames, float frameRate, bool loop)
            {
                Name = name;
                Frames = frames;
                FrameRate = frameRate;
                Loop = loop;
            }

            /// <summary>Comprimento do clip em segundos. É daqui que a FSM tira seus tempos.</summary>
            public float Duration => Frames / FrameRate;
        }

        // Contagem de frames conforme o ReadMe do pacote e conferida na folha:
        // 4 + 4 + 5 + 3 + 4 + 2 + 9 = 31 frames.
        public static readonly ClipDef[] Clips =
        {
            new ClipDef(State.Idle,      4,  8f,  true),
            new ClipDef(State.Move,      4, 10f,  true),
            // O saque é o aviso, então ele é de propósito o passo mais demorado da
            // sequência: 5 frames a 12 fps dão ~0,42 s para o jogador ler e sair.
            new ClipDef(State.Unsheathe, 5, 12f,  false),
            new ClipDef(State.Attack,    3, 14f,  false),
            new ClipDef(State.Sheathe,   4, 10f,  false),
            new ClipDef(State.Hit,       2, 14f,  false),
            new ClipDef(State.Death,     9, 10f,  false),
        };

        /// <summary>Busca um clip pelo nome do estado. Usado para derivar durações.</summary>
        public static ClipDef Find(string state)
        {
            for (int i = 0; i < Clips.Length; i++)
            {
                if (Clips[i].Name == state) return Clips[i];
            }

            return default;
        }

        /// <summary>Duração do clip do estado, em segundos.</summary>
        public static float DurationOf(string state) => Find(state).Duration;
    }
}
