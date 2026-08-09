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

        [Header("Epílogo")]
        [Tooltip("Frase escrita sobre a tela preta depois que a chefe cai.")]
        [TextArea]
        [SerializeField] private string victoryMessage = "A floresta volta a respirar";

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

            // A trilha desce junto com a imagem: música de luta a todo volume sobre
            // uma tela já preta entrega que o corte foi só visual.
            if (musicSource != null) StartCoroutine(Fade(musicSource.volume, 0f, fadeDuration));

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
