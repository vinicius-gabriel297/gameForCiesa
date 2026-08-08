namespace Warana.Enemies
{
    /// <summary>
    /// Nomes de parâmetros/estados e a tabela de clips da Abomination, a chefe da
    /// floresta. Mesmo papel que <see cref="MadGhostAnimation"/>: uma fonte só para
    /// o runtime e para o gerador do controller no editor.
    ///
    /// A ordem de <see cref="Clips"/> é a ordem das linhas na folha
    /// "Abomination-Sheet Separated Tags.png" (10 x 5 células de 112x48, uma linha
    /// por tag). Não reordene sem conferir a arte.
    /// </summary>
    public static class AbominationAnimation
    {
        public static class Param
        {
            public const string Speed = "Speed";
            public const string Attack = "Attack";
            public const string Hit = "Hit";
            public const string Dead = "Dead";
        }

        public static class State
        {
            public const string Idle = "Idle";
            public const string Walk = "Walk";
            public const string Attack = "Attack";
            public const string Hit = "Hit";
            public const string Death = "Death";
        }

        /// <summary>Abaixo disso a locomoção conta como parada.</summary>
        public const float SpeedThreshold = 0.05f;

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

            public float Duration => Frames / FrameRate;
        }

        // O ReadMe do pacote diz 9 frames de Attack, mas a folha tem 10 colunas
        // preenchidas nessa linha (e a "Single Row" tem 29 frames, não 28).
        // A contagem aqui segue a arte, não o ReadMe.
        public static readonly ClipDef[] Clips =
        {
            new ClipDef(State.Idle,   4,  8f,  true),
            new ClipDef(State.Walk,   4,  8f,  true),
            new ClipDef(State.Attack, 10, 12f, false),
            new ClipDef(State.Hit,    2,  14f, false),
            new ClipDef(State.Death,  9,  10f, false),
        };

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
