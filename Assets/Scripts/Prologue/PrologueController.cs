using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using TMPro;
using Warana.Companion;
using Warana.Enemies;
using Warana.Player;

namespace Warana.Prologue
{
    /// <summary>
    /// Roteiro do mapa Prólogo: fade in, caminhada travada para frente com narração,
    /// chegada na Guardian_tree, cutscene de canalização (música + VFX via Timeline),
    /// emboscada do Catto (servo de Jurupari) e o resgate de Waraná, que entrega o
    /// poder a Piatã antes da transição para o Mapa_01.
    /// Único componente afetado é o Player desta cena — nenhum outro mapa é tocado.
    /// </summary>
    [AddComponentMenu("Waraná/Prólogo/Prologue Controller")]
    public class PrologueController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private PlayerInputHandler playerInput;
        [SerializeField] private PlayerChannel playerChannel;
        [SerializeField] private PlayerAnimator playerAnimator;

        [Header("Fade")]
        [SerializeField] private CanvasGroup fadeGroup;
        [Tooltip("Duração do fade in inicial, em segundos.")]
        [SerializeField] private float fadeInDuration = 3.5f;

        [Header("Narração")]
        [SerializeField] private AudioSource narrationSource;
        [Tooltip("Os 4 áudios de narração, na ordem em que tocam.")]
        [SerializeField] private AudioClip[] narrationClips = new AudioClip[4];
        [Tooltip("Legendas dos 4 áudios de narração, na mesma ordem em que tocam.")]
        [SerializeField]
        private string[] narrationLines =
        {
            "Desde tempos imemoriais... o povo Sateré-Mawé guarda o maior legado da floresta...",
            "A cada geração, um único guerreiro é escolhido para proteger Waraná... o espírito que preserva o equilíbrio entre a vida e a escuridão...",
            "Na noite de sua iniciação... o jovem Piatã percorre sozinho o caminho até a Árvore Ancestral, onde receberá a bênção do Guardião...",
            "Mas, naquela noite... alguém já o aguardava.",
        };

        [Header("Guardian Tree")]
        [Tooltip("Ponto onde o Player para ao chegar na árvore (normalmente logo antes do colisor sólido).")]
        [SerializeField] private Transform arrivalPoint;
        [Tooltip("Distância antes do arrivalPoint em que o áudio 04 é disparado.")]
        [SerializeField] private float nearTreeDistance = 5f;
        [SerializeField] private PlayableDirector treeCutsceneDirector;

        [Header("Emboscada do Catto")]
        [Tooltip("O gato enviado por Jurupari. Começa desativado na cena.")]
        [SerializeField] private CattoActor catto;
        [SerializeField] private AudioSource cattoAudioSource;
        [Tooltip("Som do miado ao aparecer. Opcional — sem clip, o gato só entra em silêncio.")]
        [SerializeField] private AudioClip meowClip;
        [SerializeField] private Transform cattoSpawnPoint;
        [SerializeField] private Transform cattoSitPoint;
        [SerializeField] private float cattoWalkSpeed = 3f;
        [Tooltip("Quanto tempo o gato fica sentado (Idle Sit) antes de atacar.")]
        [SerializeField] private float idleSitHold = 0.6f;
        [Tooltip("Pausa depois do ataque, com Piatã já morto, antes de Waraná descer.")]
        [SerializeField] private float deathPause = 1.2f;

        [Header("Raio (flash + som)")]
        [SerializeField] private CanvasGroup flashGroup;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip zapClip;
        [SerializeField] private float flashInDuration = 0.05f;
        [SerializeField] private float flashOutDuration = 0.25f;

        [Header("Waraná desce")]
        [Tooltip("O orbe companheiro, ainda desativado na cena — é a forma visual de Waraná.")]
        [SerializeField] private GuaranaEye guaranaEye;
        [SerializeField] private AudioClip waranaVoiceClip;
        [Tooltip("Altura acima do ponto de repouso de onde o orbe começa a descer.")]
        [SerializeField] private float guaranaSkyHeight = 6f;
        [SerializeField] private float guaranaDescendDuration = 1.8f;

        [Header("Texto")]
        [SerializeField] private CanvasGroup dialogueGroup;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private float lineFadeDuration = 0.35f;
        [Tooltip("Tempo mínimo que cada fala fica na tela, mesmo se o clip de voz for curto.")]
        [SerializeField] private float lineHoldFallback = 2.2f;

        [Header("Próxima cena")]
        [SerializeField] private string nextSceneName = "Mapa_01";
        [SerializeField] private float finalFadeDuration = 2f;

        /// <summary>Fala e a pausa depois dela. As rubricas entre parênteses do roteiro
        /// (olhar, aproximar-se) viram só tempo — sem personagem animável para elas.</summary>
        private static readonly (string Line, float Pause)[] AmbushDialogue =
        {
            ("Piatã... levante-se.", 1.0f),
            ("A escuridão despertou.", 0.6f),
            ("Jurupari enviou seus servos para impedir sua iniciação.", 0.6f),
            ("Mas você foi escolhido.", 0.6f),
            ("Proteja Waraná. Proteja a floresta.", 1.2f),
        };

        private int _forwardSign = 1;

        private IEnumerator Start()
        {
            playerAnimator.WalkMode = true;
            playerController.FreezeControl(true);
            fadeGroup.alpha = 1f;

            _forwardSign = arrivalPoint.position.x >= playerController.transform.position.x ? 1 : -1;

            yield return FadeTo(fadeGroup, 0f, fadeInDuration);

            playerController.FreezeControl(false);
            playerInput.SetForwardOnly(true, _forwardSign);
            playerInput.SetChannelLocked(true);

            yield return RunNarrationAndWalk();

            playerChannel.BeginScripted();

            yield return null; // deixa a pose de canalização assentar um frame antes da música

            treeCutsceneDirector.Play();
            yield return new WaitUntil(() => treeCutsceneDirector.state != PlayState.Playing);

            playerChannel.EndScripted();

            // EndScripted() devolve o controle (é o comportamento normal de Stop());
            // aqui a cena deve ficar em silêncio, então travamos de novo.
            playerController.FreezeControl(true);

            yield return RunAmbushSequence();
        }

        private IEnumerator RunNarrationAndWalk()
        {
            yield return PlayAndWait(0);
            yield return PlayAndWait(1);
            yield return PlayAndWait(2);

            yield return new WaitUntil(IsNearTree);
            StartCoroutine(PlayAndWait(3)); // não bloqueia: o áudio 04 toca enquanto Piatã ainda anda até a árvore

            yield return new WaitUntil(HasArrived);
        }

        /// <summary>Toca o áudio de narração de índice <paramref name="index"/> e mostra
        /// a legenda correspondente (mesmo padrão de fade usado em <see cref="SpeakLines"/>
        /// para a fala de Waraná), pelo tempo de duração do clip.</summary>
        private IEnumerator PlayAndWait(int index)
        {
            AudioClip clip = narrationClips[index];
            string line = index < narrationLines.Length ? narrationLines[index] : null;
            bool hasLine = !string.IsNullOrEmpty(line) && dialogueText != null && dialogueGroup != null;

            if (hasLine) dialogueText.text = line;
            PlayNarration(clip);

            if (hasLine) yield return FadeTo(dialogueGroup, 1f, lineFadeDuration);

            yield return new WaitForSeconds(clip.length);

            if (hasLine) yield return FadeTo(dialogueGroup, 0f, lineFadeDuration);
        }

        private void PlayNarration(AudioClip clip)
        {
            narrationSource.clip = clip;
            narrationSource.Play();
        }

        private bool IsNearTree()
        {
            float threshold = arrivalPoint.position.x - _forwardSign * nearTreeDistance;
            return _forwardSign > 0
                ? playerController.transform.position.x >= threshold
                : playerController.transform.position.x <= threshold;
        }

        private bool HasArrived()
        {
            return _forwardSign > 0
                ? playerController.transform.position.x >= arrivalPoint.position.x
                : playerController.transform.position.x <= arrivalPoint.position.x;
        }

        // ------------------------------------------------------------- emboscada

        /// <summary>
        /// Jurupari manda o Catto atrapalhar a iniciação: o gato entra andando, senta
        /// na frente de Piatã e ataca. Um raio "mata" Piatã, e é aí que Waraná desce
        /// do céu para devolver o poder e revelar o que está por trás do ataque.
        /// Tudo roteirizado — sem física, sem IA, um evento só na vida do jogo.
        /// </summary>
        private IEnumerator RunAmbushSequence()
        {
            catto.transform.position = cattoSpawnPoint.position;
            catto.gameObject.SetActive(true);

            if (meowClip != null && cattoAudioSource != null) cattoAudioSource.PlayOneShot(meowClip);

            yield return catto.WalkTo(cattoSitPoint.position, cattoWalkSpeed);

            catto.PlayIdleSit();
            yield return new WaitForSeconds(idleSitHold);

            catto.PlayAttack();
            yield return new WaitForSeconds(CattoAnimation.DurationOf(CattoAnimation.State.Attack));

            if (sfxSource != null && zapClip != null) sfxSource.PlayOneShot(zapClip);
            yield return FlashScreen();

            catto.gameObject.SetActive(false);
            playerAnimator.SetDead(true);

            yield return new WaitForSeconds(deathPause);

            yield return DescendGuaranaEye();
            yield return SpeakLines();

            yield return FadeTo(fadeGroup, 1f, finalFadeDuration);

            SceneManager.LoadScene(nextSceneName);
        }

        private IEnumerator FlashScreen()
        {
            yield return FadeTo(flashGroup, 1f, flashInDuration);
            yield return FadeTo(flashGroup, 0f, flashOutDuration);
        }

        /// <summary>
        /// Waraná é o orbe (GuaranaEye), não um personagem novo. Ele desce de um ponto
        /// alto até o repouso perto de Piatã com o script de órbita desligado — só o
        /// transform se move — e liga o script só ao chegar, o que faz o orbe assumir
        /// a órbita normal sem pulo perceptível (o ponto de pouso já está bem perto de
        /// onde o OnEnable dele ia colocar o orbe de qualquer forma).
        /// </summary>
        private IEnumerator DescendGuaranaEye()
        {
            Transform eye = guaranaEye.transform;
            Vector3 restPoint = arrivalPoint.position + new Vector3(-0.3f, 1.3f, 0f);
            Vector3 skyPoint = restPoint + Vector3.up * guaranaSkyHeight;

            guaranaEye.enabled = false; // objeto ainda inativo: Awake roda, OnEnable não
            eye.position = skyPoint;
            guaranaEye.gameObject.SetActive(true);

            float t = 0f;
            while (t < guaranaDescendDuration)
            {
                t += Time.deltaTime;
                eye.position = Vector3.Lerp(skyPoint, restPoint, t / guaranaDescendDuration);
                yield return null;
            }

            eye.position = restPoint;
            guaranaEye.enabled = true; // a partir daqui o próprio orbe cuida da órbita
        }

        private IEnumerator SpeakLines()
        {
            foreach ((string line, float pause) in AmbushDialogue)
            {
                dialogueText.text = line;

                if (waranaVoiceClip != null)
                {
                    narrationSource.clip = waranaVoiceClip;
                    narrationSource.Play();
                }

                yield return FadeTo(dialogueGroup, 1f, lineFadeDuration);

                float hold = Mathf.Max(lineHoldFallback, waranaVoiceClip != null ? waranaVoiceClip.length : 0f);
                yield return new WaitForSeconds(hold);

                yield return FadeTo(dialogueGroup, 0f, lineFadeDuration);
                yield return new WaitForSeconds(pause);
            }
        }

        private IEnumerator FadeTo(CanvasGroup group, float target, float duration)
        {
            float start = group.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }

            group.alpha = target;
        }
    }
}
