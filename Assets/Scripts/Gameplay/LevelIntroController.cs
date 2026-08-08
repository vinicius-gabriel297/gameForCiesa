using System.Collections;
using TMPro;
using UnityEngine;
using Warana.Player;

namespace Warana.Gameplay
{
    /// <summary>
    /// A abertura da fase: a tela clareia, Waraná diz o que está em jogo e só então o
    /// jogador ganha o controle, com os comandos escritos na tela.
    ///
    /// A ordem não é decorativa. Entregar o controle antes da fala faria o jogador
    /// andar por cima dela — e a frase de Waraná é a única coisa que explica por que
    /// a floresta está daquele jeito. O lembrete de comandos vem *junto* com o
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

        [TextArea]
        [SerializeField]
        private string waranaLine = "A corrupção de Jurupari está se espalhando pela floresta.";

        [Tooltip("Voz de Waraná. Opcional — sem clip a fala entra em silêncio.")]
        [SerializeField] private AudioSource voiceSource;

        [SerializeField] private AudioClip voiceClip;

        [Tooltip("Tempo mínimo que a fala fica na tela, mesmo se a voz for mais curta.")]
        [SerializeField] private float lineHold = 3.5f;

        [Header("Comandos")]
        [SerializeField] private CanvasGroup controlsGroup;

        [SerializeField] private TMP_Text controlsText;

        [TextArea]
        [SerializeField]
        private string controlsLine =
            "Mover:  A / D  ou  ← →\nPular:  Espaço\nCanalizar:  Botão direito do mouse";

        [Tooltip("Quanto tempo os comandos ficam na tela antes de sumirem.")]
        [SerializeField] private float controlsHold = 6f;

        [Header("Transições")]
        [SerializeField] private float textFadeDuration = 0.4f;

        private IEnumerator Start()
        {
            // A tela já nasce preta: deixar um frame do mapa aparecer antes do fade
            // estragaria a entrada vinda do Prólogo, que termina em preto.
            if (fadeGroup != null) fadeGroup.alpha = 1f;
            if (dialogueGroup != null) dialogueGroup.alpha = 0f;
            if (controlsGroup != null) controlsGroup.alpha = 0f;

            if (playerController != null) playerController.FreezeControl(true);
            if (playerInput != null) playerInput.SetChannelLocked(true);

            yield return Fade(fadeGroup, 0f, fadeInDuration);

            yield return SpeakWaranaLine();

            // Controle devolvido: daqui em diante os comandos na tela valem de verdade.
            if (playerController != null) playerController.FreezeControl(false);
            if (playerInput != null) playerInput.SetChannelLocked(false);

            yield return ShowControls();
        }

        private IEnumerator SpeakWaranaLine()
        {
            if (dialogueGroup == null || dialogueText == null) yield break;

            dialogueText.text = waranaLine;

            if (voiceSource != null && voiceClip != null)
            {
                voiceSource.clip = voiceClip;
                voiceSource.Play();
            }

            yield return Fade(dialogueGroup, 1f, textFadeDuration);

            float hold = Mathf.Max(lineHold, voiceClip != null ? voiceClip.length : 0f);
            yield return new WaitForSeconds(hold);

            yield return Fade(dialogueGroup, 0f, textFadeDuration);
        }

        private IEnumerator ShowControls()
        {
            if (controlsGroup == null || controlsText == null) yield break;

            controlsText.text = controlsLine;

            yield return Fade(controlsGroup, 1f, textFadeDuration);
            yield return new WaitForSeconds(controlsHold);
            yield return Fade(controlsGroup, 0f, textFadeDuration);
        }

        private IEnumerator Fade(CanvasGroup group, float target, float duration)
        {
            if (group == null) yield break;

            float from = group.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, target, elapsed / duration);
                yield return null;
            }

            group.alpha = target;
        }
    }
}
