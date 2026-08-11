using System.Collections;
using TMPro;
using UnityEngine;
using Warana.CameraRig;
using Warana.Environment;
using Warana.Player;
using Warana.UI;

namespace Warana.Gameplay
{
    /// <summary>
    /// O final da fase: a queda da chefe estanca a corrupção, a árvore sagrada cura a
    /// região.
    ///
    /// <para>Mora num objeto de cena, e não na chefe, porque um final de vários minutos
    /// pendurado num inimigo morto é frágil — basta alguém ligar o
    /// <c>disableOnDeath</c> do <c>Health</c> para o epílogo inteiro sumir no meio. O
    /// <see cref="BossEncounter"/> só entrega o bastão; se este componente não existir na
    /// cena, ele termina a fase no fade de sempre.</para>
    ///
    /// <para>O beat que justifica tudo é o da canalização diante da árvore. A fase
    /// inteira ensinou que canalizar é atacar; ali o mesmo botão cura. Nenhum comando
    /// novo, nenhuma tela nova — um verbo já conhecido relido.</para>
    ///
    /// <para>Nenhuma fala aqui cobra a noite do Prólogo. A emboscada deixou Waraná
    /// <em>sem um guardião</em>, e a consequência disso são regiões inteiras adoecendo —
    /// o Mapa 01 é a primeira que Piatã devolve ao equilíbrio, não um acerto de contas
    /// com aquela noite. Por isso o fecho aponta para as regiões que faltam, e não para
    /// um ritual concluído.</para>
    /// </summary>
    [AddComponentMenu("Waraná/Gameplay/Final da Fase")]
    public class EndingSequence : MonoBehaviour
    {
        [Header("Cena")]
        [SerializeField] private ForestRestoration restoration;

        [SerializeField] private SacredTree sacredTree;

        [SerializeField] private PlayerController2D playerController;

        [SerializeField] private CameraFollow2D cameraFollow;

        [Tooltip("HUD de vida e cargas. Sai de cena no plano final — ela mede combate, e não há mais.")]
        [SerializeField] private GameObject hud;

        [Header("1. Alívio")]
        [Tooltip("Quanto da floresta a morte da chefe devolve. Matar a chefe estanca; quem cura é a árvore.")]
        [Range(0f, 1f)]
        [SerializeField] private float reliefProgress = 0.35f;

        [SerializeField] private float reliefDuration = 2.5f;

        [Header("2. Redireção")]
        [TextArea]
        [SerializeField]
        private string[] redirectLines =
        {
            "Acabou, Piatã. Mas matar o que adoeceu a mata não cura a mata.",
            "A árvore sagrada está logo ali. Vá até ela.",
        };

        [Tooltip("Limite direito da câmera depois da luta. A árvore fica além do limite de combate.")]
        [SerializeField] private float openedBoundsMaxX = 42f;

        [Header("3. Convite")]
        [TextArea]
        [SerializeField] private string inviteLine = "Devolve a ela o que Jurupari levou.";

        [Tooltip("Painel do lembrete de comando, no canvas da voz do Waraná.")]
        [SerializeField] private CanvasGroup promptGroup;

        [SerializeField] private TMP_Text promptText;

        [Tooltip("Comando lembrado no convite. O texto vem de GameControls, nunca escrito à mão.")]
        [SerializeField] private string promptAction = "Canalizar";

        [SerializeField] private float promptFadeDuration = 0.4f;

        [Header("4. Ritual")]
        [TextArea]
        [SerializeField] private string ritualLine = "Isso. Deixa a força passar.";

        [Tooltip("Efeitos que só existem na cura. Deixe-os desligados na cena.")]
        [SerializeField] private GameObject[] restorationVfx;

        [Header("5. Plano final")]
        [Tooltip("Para onde a câmera sobe. A árvore tem ~9,7 unidades de altura contra uma janela de 5,6 — " +
                 "ela não cabe na tela, então a luz sobe pelo tronco até a copa em vez de afastar o plano.")]
        [SerializeField] private Vector2 crownFraming = new Vector2(35.5f, 9.4f);

        [SerializeField] private float panDuration = 4.5f;

        [SerializeField] private float panHold = 1.5f;

        [TextArea]
        [SerializeField]
        private string[] closingLines =
        {
            "Esta região respira de novo.",
            "Ainda há outras, e cada uma tem a sua árvore.",
            "Jurupari dorme fundo. Nós temos tempo — não muito.",
        };

        [Header("6. Cartões")]
        [TextArea]
        [SerializeField]
        private string[] closingCards =
        {
            "Uma região da floresta voltou a respirar.",
            "Faltam as outras.",
        };

        [SerializeField] private string menuSceneName = "MainMenu";

        [SerializeField] private float fadeDuration = 2f;

        [SerializeField] private float cardFadeDuration = 1f;

        [SerializeField] private float cardHold = 3.5f;

        [Tooltip("Fonte dos cartões. Vazia = empresta a de algum texto da cena.")]
        [SerializeField] private TMP_FontAsset cardFont;

        [Header("Áudio")]
        [SerializeField] private AudioSource musicSource;

        [SerializeField] private AudioSource ambienceSource;

        [Tooltip("Ambiência que entra com o alívio: a mata volta a ter bicho.")]
        [SerializeField] private AudioClip forestAmbienceClip;

        [Tooltip("Trilha da chegada à árvore sagrada.")]
        [SerializeField] private AudioClip sacredGroveClip;

        [SerializeField] private float crossFadeDuration = 2.5f;

        private float _baseMusicVolume;
        private float _baseAmbienceVolume;
        private bool _started;

        private void Awake()
        {
            // Os volumes são lidos agora porque a chefe já está baixando a trilha da luta
            // quando ela entrega o bastão: mais tarde este valor seria zero.
            if (musicSource != null) _baseMusicVolume = musicSource.volume;
            if (ambienceSource != null) _baseAmbienceVolume = ambienceSource.volume;

            if (promptGroup != null) promptGroup.alpha = 0f;

            if (cardFont == null)
            {
                var anyText = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
                if (anyText != null) cardFont = anyText.font;
            }
        }

        /// <summary>Chamado pelo <see cref="BossEncounter"/> quando a chefe cai.</summary>
        public void Begin()
        {
            if (_started) return;

            _started = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return Relief();
            yield return Redirect();

            // Sem árvore na cena não há para onde mandar o jogador; o epílogo entra
            // direto, e a fase ainda termina inteira.
            if (sacredTree != null)
            {
                yield return new WaitUntil(() => sacredTree.PlayerArrived);
                yield return Invite();
                yield return Ritual();
            }

            yield return PanToCrown();
            yield return Closing();
        }

        /// <summary>A corrupção para de crescer e a mata volta a ter som.</summary>
        private IEnumerator Relief()
        {
            if (ambienceSource != null && forestAmbienceClip != null)
                StartCoroutine(CrossFade(ambienceSource, forestAmbienceClip, _baseAmbienceVolume));

            yield return LerpRestoration(0f, reliefProgress, reliefDuration);
        }

        private IEnumerator Redirect()
        {
            // A porta abre antes da fala que manda atravessá-la: pedir para ir até a
            // árvore com a câmera ainda travada no limite do combate seria pedir uma
            // coisa impossível por dois segundos.
            if (cameraFollow != null)
                cameraFollow.SetBounds(
                    cameraFollow.BoundsMin,
                    new Vector2(openedBoundsMaxX, cameraFollow.BoundsMax.y));

            yield return Speak(redirectLines);
        }

        private IEnumerator Invite()
        {
            if (musicSource != null && sacredGroveClip != null)
                StartCoroutine(CrossFade(musicSource, sacredGroveClip, _baseMusicVolume));

            if (sacredTree != null) sacredTree.OpenRitual();

            if (WaranaVoice.Instance != null) WaranaVoice.Instance.Say(inviteLine);

            if (promptText != null) promptText.text = GameControls.Prompt(promptAction);
            yield return FadeGroup(promptGroup, 1f);
        }

        private IEnumerator Ritual()
        {
            bool spoke = false;

            while (sacredTree.HoldProgress < 1f)
            {
                // Recongelado a cada frame de propósito: a PlayerChannel devolve o
                // controle sozinha ao sair da canalização, e sem isto Piatã sairia
                // andando no primeiro instante em que soltasse o botão.
                if (playerController != null) playerController.FreezeControl(true);

                if (restoration != null)
                    restoration.SetProgress(Mathf.Lerp(reliefProgress, 1f, sacredTree.HoldProgress));

                if (!spoke && sacredTree.HoldProgress > 0.1f)
                {
                    spoke = true;
                    ShowRestorationVfx();
                    StartCoroutine(FadeGroup(promptGroup, 0f));
                    if (WaranaVoice.Instance != null) WaranaVoice.Instance.Say(ritualLine);
                }

                yield return null;
            }

            if (restoration != null) restoration.SetProgress(1f);
            if (playerController != null) playerController.FreezeControl(true);

            yield return FadeGroup(promptGroup, 0f);
        }

        private IEnumerator PanToCrown()
        {
            if (hud != null) hud.SetActive(false);
            if (WaranaVoice.Instance != null) yield return WaranaVoice.Instance.WaitUntilSilent();

            if (cameraFollow == null) yield break;

            // Solta o alvo em vez de mover o jogador: sem alvo a câmera para de se
            // corrigir sozinha e aceita ser dirigida daqui.
            cameraFollow.SetTarget(null);

            Transform cam = cameraFollow.transform;
            Vector3 from = cam.position;
            Vector3 to = new Vector3(crownFraming.x, crownFraming.y, from.z);

            float elapsed = 0f;
            while (elapsed < panDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / panDuration);
                cam.position = Vector3.Lerp(from, to, t);
                yield return null;
            }

            cam.position = to;
            yield return new WaitForSeconds(panHold);
        }

        private IEnumerator Closing()
        {
            yield return Speak(closingLines);

            ScreenFader.Get().FadeToBlackWithMessages(
                menuSceneName, closingCards, fadeDuration, cardFadeDuration, cardHold, cardFont);
        }

        private void ShowRestorationVfx()
        {
            if (restorationVfx == null) return;

            foreach (GameObject fx in restorationVfx)
                if (fx != null) fx.SetActive(true);
        }

        private IEnumerator Speak(string[] lines)
        {
            if (WaranaVoice.Instance == null || lines == null || lines.Length == 0) yield break;

            WaranaVoice.Instance.Say(lines);
            yield return WaranaVoice.Instance.WaitUntilSilent();
        }

        private IEnumerator LerpRestoration(float from, float to, float duration)
        {
            if (restoration == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                restoration.SetProgress(Mathf.Lerp(from, to, elapsed / duration));
                yield return null;
            }

            restoration.SetProgress(to);
        }

        private IEnumerator CrossFade(AudioSource source, AudioClip clip, float baseVolume)
        {
            if (source == null || clip == null) yield break;

            float half = crossFadeDuration * 0.5f;

            yield return FadeVolume(source, source.volume, 0f, half);

            source.clip = clip;
            source.loop = true;
            source.Play();

            yield return FadeVolume(source, 0f, baseVolume, half);
        }

        private static IEnumerator FadeVolume(AudioSource source, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            source.volume = to;
        }

        private IEnumerator FadeGroup(CanvasGroup group, float target)
        {
            if (group == null) yield break;

            float from = group.alpha;
            float elapsed = 0f;

            while (elapsed < promptFadeDuration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, target, elapsed / promptFadeDuration);
                yield return null;
            }

            group.alpha = target;
        }
    }
}
