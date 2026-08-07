namespace Warana.Player
{
    /// <summary>
    /// Nomes de parâmetros e estados do Animator do Waraná.
    /// Compartilhado entre o runtime (<see cref="PlayerAnimator"/>) e o gerador
    /// do controller no editor — assim os dois nunca saem de sincronia.
    /// </summary>
    public static class WaranaAnimation
    {
        public static class Param
        {
            /// <summary>Velocidade horizontal absoluta, em unidades/segundo.</summary>
            public const string Speed = "Speed";

            public const string Grounded = "Grounded";

            /// <summary>Velocidade vertical com sinal: separa subida de queda.</summary>
            public const string VelocityY = "VelocityY";

            /// <summary>True troca a locomoção de Run para Walk.</summary>
            public const string Walk = "Walk";

            public const string Attack = "Attack";
            public const string Dead = "Dead";
        }

        public static class State
        {
            public const string Idle = "Idle";
            public const string Run = "Run";
            public const string Walk = "Walk";
            public const string Jump = "Jump";
            public const string Fall = "Fall";
            public const string Attack = "Attack";
            public const string Death = "Death";
        }

        /// <summary>Abaixo disso a locomoção conta como parada.</summary>
        public const float SpeedThreshold = 0.1f;

        /// <summary>Margem em torno de zero para decidir entre subir e cair.</summary>
        public const float VerticalThreshold = 0.01f;
    }
}
