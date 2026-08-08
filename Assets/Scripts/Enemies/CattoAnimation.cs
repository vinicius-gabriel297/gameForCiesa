namespace Warana.Enemies
{
    /// <summary>
    /// Nomes de estados e a tabela de clips do Catto (o gato enviado por Jurupari no
    /// Prólogo). A folha já vem cortada pelo fornecedor ("Cat monster-Sheet Separated
    /// tags.png", sprites nomeados "..._0" a "..._33", tamanhos não uniformes); por
    /// isso aqui guardamos o índice inicial de cada animação na folha, e não uma
    /// célula de grade como no Mad Ghost.
    ///
    /// Faixas conferidas contra o ReadMe do pacote (Catto: Stare 1, Stand up 2,
    /// Walk 4, Turn 5, Sit down 2, Idle sit 4, Attack 6, Hit 2, Death 9) e a posição
    /// de cada sprite na folha (uma faixa de Y por animação, na mesma ordem do ReadMe).
    /// Só as três animações usadas na cutscene do Prólogo estão listadas.
    /// </summary>
    public static class CattoAnimation
    {
        public static class State
        {
            public const string Walk = "Walk";
            public const string IdleSit = "IdleSit";
            public const string Attack = "Attack";
        }

        /// <summary>Uma animação: de onde na folha ela começa, quantos frames e a que cadência.</summary>
        public readonly struct ClipDef
        {
            public readonly string Name;
            public readonly int FirstIndex;
            public readonly int Frames;
            public readonly float FrameRate;
            public readonly bool Loop;

            public ClipDef(string name, int firstIndex, int frames, float frameRate, bool loop)
            {
                Name = name;
                FirstIndex = firstIndex;
                Frames = frames;
                FrameRate = frameRate;
                Loop = loop;
            }

            /// <summary>Comprimento do clip em segundos. É daqui que a cutscene tira seus tempos.</summary>
            public float Duration => Frames / FrameRate;
        }

        public static readonly ClipDef[] Clips =
        {
            new ClipDef(State.Walk,    3,  4, 8f,  true),
            new ClipDef(State.IdleSit, 14, 4, 5f,  true),
            new ClipDef(State.Attack,  18, 6, 10f, false),
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
