using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using TMPro;
using Warana.Combat;
using Warana.Companion;
using Warana.Enemies;
using Warana.Player;
using Warana.UI;

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

        [Tooltip("Silêncio entre duas narrações seguidas — é ele que dá compasso à sequência.")]
        [SerializeField] private float narrationGap = 1.2f;

        [Header("Guardian Tree")]
        [Tooltip("Ponto onde o Player para ao chegar na árvore (normalmente logo antes do colisor sólido).")]
        [SerializeField] private Transform arrivalPoint;
        [Tooltip("Distância antes do arrivalPoint em que o áudio 04 é disparado.")]
        [SerializeField] private float nearTreeDistance = 5f;
        [SerializeField] private PlayableDirector treeCutsceneDirector;

        [Header("Emboscada do Catto")]
        [Tooltip("O gato enviado por Jurupari. Começa desativado na cena.")]
        [SerializeField] private CattoActor catto;

        [Tooltip("Quanto tempo Piatã canaliza sozinho antes de o gato entrar. O ritual " +
                 "precisa ser cortado no meio: esperar a Timeline inteira terminar " +
                 "entregaria a bênção completa e só então traria o ataque, que é o " +
                 "oposto do que a cena conta.")]
        [SerializeField] private float cattoEntryDelay = 15f;
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

        [Header("Raio (VFX + flash + som)")]
        [Tooltip("O mesmo prefab do raio do jogo — Vefects/Zap VFX URP/.../VFX_Zap_03_Yellow. " +
                 "Vazio = só o flash, como era antes.")]
        [SerializeField] private GameObject boltPrefab;

        [Tooltip("Altura acima do Catto de onde o raio desce.")]
        [SerializeField] private float boltSkyHeight = 7f;

        [Tooltip("Espessura do raio. Mesmo valor do SpiritBoltEmitter do Mapa 01.")]
        [SerializeField] private float boltThickness = 0.22f;

        [Tooltip("Ajuste fino do comprimento. 0,72 é o valor medido para o Zap VFX da Vefects.")]
        [Range(0.2f, 2f)]
        [SerializeField] private float boltLengthCalibration = 0.72f;

        [SerializeField] private float boltLifetime = 1.2f;

        [Tooltip("Sorting order do VFX, para o raio ficar à frente da arte da cena.")]
        [SerializeField] private int boltSortingOrder = 30;

        [Tooltip("Pausa entre o raio acertar o gato e a tela estourar em branco.")]
        [SerializeField] private float boltToFlashDelay = 0.08f;

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

        [Header("Pular")]
        [Tooltip("Fonte do aviso de pular, no canto da tela.")]
        [SerializeField] private TMP_FontAsset skipFont;

        [Tooltip("Fade de saída quando o jogador pula o prólogo. Curto de propósito: " +
                 "quem pulou está com pressa.")]
        [SerializeField] private float skipFadeDuration = 0.6f;

        /// <summary>
        /// Fala e a pausa depois dela. As rubricas entre parênteses do roteiro
        /// (olhar, aproximar-se) viram só tempo — sem personagem animável para elas.
        ///
        /// <para>Piatã está desacordado: o Waraná fala com um corpo no chão, não com um
        /// aprendiz de pé. Por isso nenhuma linha explica regra nem dá ordem de missão.
        /// A narração 02 já disse que um guerreiro é escolhido a cada geração, então
        /// repetir "você é o escolhido" gastaria a fala mais forte da cena com
        /// informação que o jogador tem há três minutos; "eu escolhi você" põe a
        /// escolha na boca de quem escolheu. A última linha deixa a bênção em aberto de
        /// propósito — é a dívida que o final da fase vem pagar.</para>
        /// </summary>
        private static readonly (string Line, float Pause)[] AmbushDialogue =
        {
            ("Piatã... não é aqui que você cai.", 1.2f),
            ("O gato de Jurupari veio no meio da sua bênção. De propósito.", 0.8f),
            ("Ele não veio te matar. Veio te deixar sem mim.", 1.0f),
            ("Foi por isso que eu desci em raio. Eu escolhi você.", 1.0f),
            ("A bênção ficou pela metade. A floresta vai completar ela.", 1.4f),
        };

        private int _forwardSign = 1;

        private SkipControl _skip;
        private bool _skipping;

        private IEnumerator Start()
        {
            _skip = new SkipControl(skipFont);

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

            // A Timeline segue tocando por baixo da emboscada. Esperá-la terminar aqui
            // era o que fazia a bênção acontecer inteira antes do gato aparecer — e a
            // iniciação de Piatã é justamente a canalização que nunca terminou. Quem
            // encerra o ritual é o golpe, lá dentro de RunAmbushSequence.
            yield return new WaitForSeconds(cattoEntryDelay);

            yield return RunAmbushSequence();
        }

        private void Update()
        {
            if (_skipping || _skip == null || !_skip.Requested) return;

            _skipping = true;

            // O prólogo é uma cena dirigida do começo ao fim: não há um "próximo trecho"
            // para avançar, então pular é sair dela. Interromper o roteiro no meio é
            // seguro justamente porque nada aqui altera estado que o Mapa 01 leia —
            // o poder que Waraná entrega é narrativo, não um flag de progressão.
            StopAllCoroutines();
            StartCoroutine(SkipToNextScene());
        }

        private void OnDestroy()
        {
            // Sem isso a contagem de sequências dirigidas ficaria aberta para sempre.
            _skip?.Dispose();
            _skip = null;
        }

        /// <summary>
        /// Corta tudo o que o roteiro deixou no ar — voz, efeito, Timeline e os textos —
        /// antes de escurecer. Sem isso a narração seguiria tocando por cima do Mapa 01,
        /// porque um AudioSource não para sozinho na troca de cena dentro do mesmo frame.
        /// </summary>
        private IEnumerator SkipToNextScene()
        {
            if (narrationSource != null) narrationSource.Stop();
            if (sfxSource != null) sfxSource.Stop();
            if (cattoAudioSource != null) cattoAudioSource.Stop();

            if (treeCutsceneDirector != null && treeCutsceneDirector.state == PlayState.Playing)
                treeCutsceneDirector.Stop();

            if (dialogueGroup != null) dialogueGroup.alpha = 0f;
            if (flashGroup != null) flashGroup.alpha = 0f;

            _skip?.Dispose();
            _skip = null;

            yield return FadeTo(fadeGroup, 1f, skipFadeDuration);

            SceneManager.LoadScene(nextSceneName);
        }

        private IEnumerator RunNarrationAndWalk()
        {
            yield return PlayAndWait(0);
            yield return new WaitForSeconds(narrationGap);
            yield return PlayAndWait(1);
            yield return new WaitForSeconds(narrationGap);
            yield return PlayAndWait(2);

            // A 04 quer duas coisas ao mesmo tempo: manter o compasso das outras três e
            // cair um pouco antes da chegada. Só por distância, o silêncio entre a 03 e
            // a 04 dependia de onde Piatã estivesse e esticava fora de ritmo; só por
            // compasso, ela arriscava soar com a árvore já enquadrada. O que vier
            // primeiro atende as duas.
            yield return WaitForGapOrNearTree();
            StartCoroutine(PlayAndWait(3)); // não bloqueia: o áudio 04 toca enquanto Piatã ainda anda até a árvore

            yield return new WaitUntil(HasArrived);
        }

        private IEnumerator WaitForGapOrNearTree()
        {
            float elapsed = 0f;

            while (elapsed < narrationGap && !IsNearTree())
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
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

            // O golpe é o que quebra a canalização. A partir daqui a bênção está pela
            // metade, que é a dívida que o Waraná cobra na última fala.
            StopRitual();

            yield return StrikeCatto();
            yield return FlashScreen();

            catto.gameObject.SetActive(false);
            playerAnimator.SetDead(true);

            yield return new WaitForSeconds(deathPause);

            yield return DescendGuaranaEye();
            yield return SpeakLines();

            yield return FadeTo(fadeGroup, 1f, finalFadeDuration);

            SceneManager.LoadScene(nextSceneName);
        }

        /// <summary>
        /// Encerra a canalização pela metade e corta a Timeline junto. A música da
        /// bênção não pode sobreviver ao golpe que interrompeu a bênção — o corte seco
        /// é o som da coisa sendo quebrada, e não um bug de mixagem.
        /// </summary>
        private void StopRitual()
        {
            playerChannel.EndScripted();

            // EndScripted() devolve o controle (é o comportamento normal de Stop());
            // daqui até o fim a cena continua dirigida, então travamos de novo.
            playerController.FreezeControl(true);

            if (treeCutsceneDirector != null && treeCutsceneDirector.state == PlayState.Playing)
                treeCutsceneDirector.Stop();
        }

        /// <summary>
        /// O raio do Waraná cai no gato. É a primeira vez que ele age no mundo — ainda
        /// sem corpo, sem fala e sem o jogador saber que ele existe — e é de propósito
        /// o mesmo VFX que vira a arma do jogador no Mapa 01: quando o orbe descer e
        /// disser "eu desci em raio", a frase tem uma imagem para apontar.
        /// </summary>
        private IEnumerator StrikeCatto()
        {
            Vector2 target = catto.transform.position;

            BoltVfx.Spawn(
                boltPrefab,
                target + Vector2.up * boltSkyHeight,
                target,
                Vector3.up,
                boltThickness,
                boltLengthCalibration,
                boltSortingOrder,
                boltLifetime);

            if (sfxSource != null && zapClip != null) sfxSource.PlayOneShot(zapClip);

            // O estouro branco vem logo depois, não junto: com os dois no mesmo frame o
            // flash come o raio e sobra só a tela piscando, que é o que já acontecia.
            yield return new WaitForSeconds(boltToFlashDelay);
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
            bool first = true;

            foreach ((string line, float pause) in AmbushDialogue)
            {
                dialogueText.text = line;

                // A voz toca só na primeira fala: é o som do Waraná chegando, não uma
                // sílaba por linha. Repetido nas cinco, o mesmo clipe curto entregava
                // que era um stinger, e cada repetição doía mais que a anterior.
                if (first && waranaVoiceClip != null)
                {
                    narrationSource.clip = waranaVoiceClip;
                    narrationSource.Play();
                }

                yield return FadeTo(dialogueGroup, 1f, lineFadeDuration);

                float voiceLength = first && waranaVoiceClip != null ? waranaVoiceClip.length : 0f;
                yield return new WaitForSeconds(Mathf.Max(lineHoldFallback, voiceLength));

                yield return FadeTo(dialogueGroup, 0f, lineFadeDuration);
                yield return new WaitForSeconds(pause);

                first = false;
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
