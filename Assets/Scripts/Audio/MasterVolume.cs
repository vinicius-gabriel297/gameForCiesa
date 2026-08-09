using UnityEngine;

namespace Warana.Audio
{
    /// <summary>
    /// Volume mestre do jogo, num só lugar. Existe por dois motivos:
    ///
    /// 1. O slider de Opções mexia direto em <see cref="AudioListener.volume"/>, então
    ///    a escolha do jogador morria ao trocar de cena ou fechar o jogo.
    /// 2. No build o <see cref="AudioListener"/> começa em 1, mas nada garantia isso
    ///    depois de qualquer cena ter baixado o volume — agora o valor é reaplicado
    ///    antes da primeira cena carregar, sempre.
    ///
    /// O ganho por som mora no próprio AudioSource / nos campos de volume dos scripts;
    /// aqui é só o mestre, que o jogador controla.
    /// </summary>
    public static class MasterVolume
    {
        /// <summary>Volume de fábrica: sem atenuação. O mix já é feito por fonte.</summary>
        public const float Default = 1f;

        private const string PrefKey = "Warana.MasterVolume";

        /// <summary>
        /// Volume mestre em 0..1. Escrever aplica no <see cref="AudioListener"/> e
        /// anota o valor, para sobreviver à troca de cena. O slider chama isso a cada
        /// frame enquanto é arrastado, então aqui não há gravação em disco — quem
        /// termina a interação chama <see cref="Flush"/>.
        /// </summary>
        public static float Value
        {
            get => AudioListener.volume;
            set
            {
                float clamped = Mathf.Clamp01(value);
                AudioListener.volume = clamped;
                PlayerPrefs.SetFloat(PrefKey, clamped);
            }
        }

        /// <summary>Grava em disco o volume anotado. Chamar ao sair das Opções ou do jogo.</summary>
        public static void Flush() => PlayerPrefs.Save();

        /// <summary>
        /// Reaplica o volume salvo antes da primeira cena. Sem isso, um build recém
        /// aberto herdaria o que estivesse serializado em cena em vez da escolha do
        /// jogador.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKey, Default));
        }
    }
}
