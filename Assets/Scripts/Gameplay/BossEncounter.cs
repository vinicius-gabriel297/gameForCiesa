using System.Collections;
using TMPro;
using UnityEngine;
using Warana.Combat;
using Warana.Enemies;
using Warana.UI;

namespace Warana.Gameplay
{
    /// <summary>
    /// A moldura da luta contra a Abomination: troca a trilha quando ela percebe o
    /// Piatã e encerra a fase quando ela cai.
    ///
    /// As duas coisas moram juntas porque são a mesma coisa — o começo e o fim do
    /// mesmo combate. Separá-las obrigaria uma a avisar a outra só para saber quando
    /// parar a música que ela mesma começou.
    ///
    /// <para>O que vem *depois* da queda não é mais assunto daqui: existindo um
    /// <see cref="EndingSequence"/> na cena, ele assume. Sem ele a fase termina no fade
    /// com frase de sempre — é o caminho que continua funcionando com o final desligado.</para>
    /// </summary>
    [RequireComponent(typeof(EnemySenses2D))]
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Warana/Gameplay/Luta de Chefe")]
    public class BossEncounter : MonoBehaviour
    {
        [Header("Trilha")]
        [Tooltip("A fonte de música da fase. Vazio = nenhuma troca de trilha acontece.")]
        [SerializeField] private AudioSource musicSource;

        [Tooltip("Faixa que entra quando a chefe percebe o jogador.")]
        [SerializeField] private AudioClip battleClip;

        [Tooltip("Tempo do cross-fade entre a trilha da floresta e a da luta.")]
        [SerializeField] private float musicFadeDuration = 1.2f;

        [Header("Fim de fase")]
        [Tooltip("Espera entre a morte da chefe e o início do fade — o tempo da animação de morte.")]
        [SerializeField] private float victoryDelay = 2.5f;

        [SerializeField] private float fadeDuration = 1.5f;

        [Tooltip("Cena carregada depois do fade.")]
        [SerializeField] private string menuSceneName = "MainMenu";

        [Header("Falas")]
        [Tooltip("Dita quando a chefe percebe Piatã, junto da troca de trilha. Vazia = silêncio.")]
        [TextArea]
        [SerializeField] private string awareLine = "Isso já foi vivo, Piatã. Jurupari não mata — ele torce.";

        [Tooltip("Ditas depois que a chefe cai, antes do fade. Só valem sem EndingSequence na cena.")]
        [TextArea]
        [SerializeField]
        private string[] defeatLines =
        {
            "Acabou. Mas a mata ainda está doente, Piatã.",
        };

        [Header("Final")]
        [Tooltip("Vazio = procura na cena. Sem EndingSequence, a fase termina aqui mesmo, " +
                 "no fade com frase — que continua sendo o caminho testável quando o final está desligado.")]
        [SerializeField] private EndingSequence ending;

        [Header("Epílogo")]
        [Tooltip("Frase escrita sobre a tela preta depois que a chefe cai.")]
        [TextArea]
        [SerializeField] private string victoryMessage = "Uma parte da floresta volta a respirar";

        [Tooltip("Entrada e saída da frase.")]
        [SerializeField] private float messageFadeDuration = 1f;

        [Tooltip("Quanto tempo a frase fica parada na tela, já legível.")]
        [SerializeField] private float messageHold = 3f;

        [Tooltip("Fonte do epílogo. Vazia = empresta a de algum texto da cena.")]
        [SerializeField] private TMP_FontAsset messageFont;

        private EnemySenses2D _senses;
        private Health _health;
        private float _baseMusicVolume;
        private bool _battleStarted;
        private bool _victoryStarted;

        private void Awake()
        {
            _senses = GetComponent<EnemySenses2D>();
            _health = GetComponent<Health>();

            if (musicSource != null) _baseMusicVolume = musicSource.volume;

            if (ending == null) ending = FindAnyObjectByType<EndingSequence>();

            // A fonte é pega agora, com a fase inteira ainda em pé: na hora do
            // epílogo a HUD e a abertura já podem ter sido desligadas.
            if (messageFont == null)
            {
                var anyText = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
                if (anyText != null) messageFont = anyText.font;
            }
        }

        private void OnEnable() => _health.Died += OnBossDefeated;

        private void OnDisable() => _health.Died -= OnBossDefeated;

        private void Update()
        {
            if (_battleStarted || !_senses.IsAware) return;

            _battleStarted = true;
            if (musicSource != null && battleClip != null) StartCoroutine(SwapMusic());

            // A Abomination entrava sem comentário nenhum, e sem isso ela é só um
            // inimigo maior. A fala diz que ela já foi um bicho desta mata — o que
            // torna matá-la uma perda, e não um troféu.
            if (WaranaVoice.Instance != null) WaranaVoice.Instance.Say(awareLine);
        }

        private IEnumerator SwapMusic()
        {
            yield return Fade(_baseMusicVolume, 0f, musicFadeDuration * 0.5f);

            musicSource.clip = battleClip;
            musicSource.loop = true;
            musicSource.Play();

            yield return Fade(0f, _baseMusicVolume, musicFadeDuration * 0.5f);
        }

        private void OnBossDefeated()
        {
            if (_victoryStarted) return;

            _victoryStarted = true;
            StartCoroutine(FinishLevel());
        }

        private IEnumerator FinishLevel()
        {
            yield return new WaitForSeconds(victoryDelay);

            // A trilha de luta desce antes das falas, e não junto com a imagem: o
            // Waraná fala sobre um campo que está esvaziando de som, em vez de gritar
            // por cima de um combate que já acabou.
            if (musicSource != null) StartCoroutine(Fade(musicSource.volume, 0f, fadeDuration));

            // Daqui em diante o final é de quem sabe encená-lo. Esta luta acaba quando
            // a chefe cai; o que vem depois — a caminhada até a árvore sagrada, a cura —
            // é outro assunto, e ele não pode viver pendurado num inimigo morto.
            if (ending != null)
            {
                ending.Begin();
                yield break;
            }

            // O epílogo não pode começar com uma frase ainda no ar: a tela preta
            // apagaria a fala no meio.
            if (WaranaVoice.Instance != null && defeatLines != null && defeatLines.Length > 0)
            {
                WaranaVoice.Instance.Say(defeatLines);
                yield return WaranaVoice.Instance.WaitUntilSilent();
            }

            ScreenFader.Get().FadeToBlackWithMessage(
                menuSceneName, victoryMessage, fadeDuration, messageFadeDuration, messageHold, messageFont);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (musicSource == null || duration <= 0f) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            musicSource.volume = to;
        }
    }
}
