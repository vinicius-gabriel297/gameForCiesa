using System.Collections;
using TMPro;
using UnityEngine;
using Warana.Player;
using Warana.UI;

namespace Warana.Gameplay
{
    /// <summary>
    /// A abertura da fase: a tela clareia, Waraná diz o que está em jogo e só então o
    /// jogador ganha o controle, com os comandos escritos na tela.
    ///
    /// A ordem não é decorativa. Entregar o controle antes das falas faria o jogador
    /// andar por cima delas — e elas são a única coisa que explica por que a floresta
    /// está daquele jeito e para onde ele deve ir. O lembrete de comandos vem *junto* com o
    /// controle, e não antes, porque ler um comando que ainda não funciona não ensina
    /// nada; ele some sozinho logo depois, quando já virou músculo.
    /// </summary>
    [AddComponentMenu("Warana/Gameplay/Abertura da Fase")]
    public class LevelIntroController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private PlayerController2D playerController;

        [SerializeField] private PlayerInputHandler playerInput;

        [Header("Fade")]
        [Tooltip("Painel preto de tela cheia. Começa opaco e clareia.")]
        [SerializeField] private CanvasGroup fadeGroup;

        [SerializeField] private float fadeInDuration = 2.5f;

        [Header("Fala de Waraná")]
        [SerializeField] private CanvasGroup dialogueGroup;

        [SerializeField] private TMP_Text dialogueText;

        /// <summary>
        /// A primeira linha diz o estado da mata; a segunda diz para onde ir.
        ///
        /// <para>A segunda existe porque a fase termina numa árvore sagrada, e chegar
        /// nela depois da chefe sairia do nada sem alguém ter dito antes que ela está
        /// lá. Dita aqui, nos primeiros dez segundos, ela transforma a fase inteira numa
        /// promessa — e é onde entra a regra de que cada região da floresta tem uma
        /// árvore sagrada só dela.</para>
        /// </summary>
        [TextArea]
        [SerializeField]
        private string[] waranaLines =
        {
            "Olhe o que Jurupari fez com esta mata, Piatã.",
            "A árvore sagrada desta região ainda resiste. Chegue até ela.",
        };

        [Tooltip("Silêncio entre duas falas seguidas da abertura.")]
        [SerializeField] private float gapBetweenLines = 0.4f;

        [Tooltip("Voz de Waraná. Opcional — sem clip a fala entra em silêncio.")]
        [SerializeField] private AudioSource voiceSource;

        [SerializeField] private AudioClip voiceClip;

        [Tooltip("Tempo mínimo que a fala fica na tela, mesmo se a voz for mais curta.")]
        [SerializeField] private float lineHold = 3.5f;

        [Header("Comandos")]
        [SerializeField] private CanvasGroup controlsGroup;

        [SerializeField] private TMP_Text controlsText;

        [Tooltip("Quanto tempo os comandos ficam na tela antes de sumirem.")]
        [SerializeField] private float controlsHold = 6f;

        [Header("Transições")]
        [SerializeField] private float textFadeDuration = 0.4f;

        [Header("Pular")]
        [Tooltip("Fonte do aviso de pular, no canto da tela.")]
        [SerializeField] private TMP_FontAsset skipFont;

        private SkipControl _skip;

        /// <summary>Pedido de pulo ainda não consumido pela etapa em andamento.</summary>
        private bool _skipRequested;

        private IEnumerator Start()
        {
            // A tela já nasce preta: deixar um frame do mapa aparecer antes do fade
            // estragaria a entrada vinda do Prólogo, que termina em preto.
            if (fadeGroup != null) fadeGroup.alpha = 1f;
            if (dialogueGroup != null) dialogueGroup.alpha = 0f;
            if (controlsGroup != null) controlsGroup.alpha = 0f;

            if (playerController != null) playerController.FreezeControl(true);
            if (playerInput != null) playerInput.SetChannelLocked(true);

            _skip = new SkipControl(skipFont);

            yield return Fade(fadeGroup, 0f, fadeInDuration);

            yield return SpeakWaranaLines();

            // Controle devolvido: daqui em diante os comandos na tela valem de verdade.
            if (playerController != null) playerController.FreezeControl(false);
            if (playerInput != null) playerInput.SetChannelLocked(false);

            // Um toque pula a abertura inteira, mas os comandos continuam aparecendo:
            // quem pulou por reflexo não fica sem saber como jogar. Daqui em diante o
            // pedido volta a valer, agora para dispensar o quadro de comandos.
            _skipRequested = false;

            yield return ShowControls();

            // A partir daqui não há mais nada para pular, e o Esc precisa voltar a ser
            // do menu de pausa — que fica bloqueado enquanto este objeto existir.
            _skip.Dispose();
            _skip = null;
        }

        private void Update()
        {
            if (_skip != null && _skip.Requested) _skipRequested = true;
        }

        private void OnDestroy()
        {
            // Sair da cena no meio da abertura deixaria a contagem de sequências
            // dirigidas presa em aberto, e o menu de pausa nunca mais abriria.
            _skip?.Dispose();
            _skip = null;
        }

        private IEnumerator SpeakWaranaLines()
        {
            if (dialogueGroup == null || dialogueText == null || waranaLines == null) yield break;

            bool first = true;

            foreach (string line in waranaLines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (_skipRequested) break;

                dialogueText.text = line;

                // A voz toca só na primeira: o mesmo clipe repetido a cada linha entrega
                // que é um stinger de chegada, não fala.
                bool playVoice = first && voiceSource != null && voiceClip != null;
                if (playVoice)
                {
                    voiceSource.clip = voiceClip;
                    voiceSource.Play();
                }

                yield return Fade(dialogueGroup, 1f, textFadeDuration);

                yield return Wait(Mathf.Max(lineHold, playVoice ? voiceClip.length : 0f));

                yield return Fade(dialogueGroup, 0f, textFadeDuration);

                if (!_skipRequested) yield return Wait(gapBetweenLines);

                first = false;
            }

            // A voz continuaria tocando por cima do jogo depois do texto sumir.
            if (_skipRequested && voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
        }

        private IEnumerator ShowControls()
        {
            if (controlsGroup == null || controlsText == null) yield break;

            // Fonte única: a lista vive em GameControls, junto com a do menu e a da
            // página da loja, porque escrita à mão aqui ela já tinha ficado para trás.
            controlsText.text = GameControls.Overlay();

            yield return Fade(controlsGroup, 1f, textFadeDuration);
            yield return Wait(controlsHold);
            yield return Fade(controlsGroup, 0f, textFadeDuration);
        }

        /// <summary>Espera que termina antes se o jogador pedir para pular.</summary>
        private IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;

            while (elapsed < seconds && !_skipRequested)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Fade que salta direto para o destino quando o jogador pede para pular — é o
        /// que faz o pedido valer já no fade de entrada, e não só depois dele.
        /// </summary>
        private IEnumerator Fade(CanvasGroup group, float target, float duration)
        {
            if (group == null) yield break;

            float from = group.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (_skipRequested) break;

                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, target, elapsed / duration);
                yield return null;
            }

            group.alpha = target;
        }
    }
}
