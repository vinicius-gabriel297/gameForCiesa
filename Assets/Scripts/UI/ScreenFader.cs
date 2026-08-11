using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Warana.UI
{
    /// <summary>
    /// Painel preto de tela cheia para transições entre cenas. Se monta sozinho na
    /// primeira vez que é pedido — nenhuma cena precisa preparar Canvas nenhum de
    /// antemão — e sobrevive à troca de cena para poder terminar o fade do outro lado.
    /// </summary>
    [AddComponentMenu("Warana/UI/Fade de Tela")]
    public class ScreenFader : MonoBehaviour
    {
        private static ScreenFader _instance;

        [SerializeField] private float defaultFadeDuration = 1f;

        private CanvasGroup _canvasGroup;
        private CanvasGroup _messageGroup;
        private TMP_Text _messageText;

        public static ScreenFader Get()
        {
            if (_instance != null) return _instance;

            var root = new GameObject("ScreenFader", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            DontDestroyOnLoad(root);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var imageGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageGO.transform.SetParent(root.transform, false);

            var imageRect = (RectTransform)imageGO.transform;
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            var image = imageGO.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            _instance = root.AddComponent<ScreenFader>();
            _instance._canvasGroup = root.GetComponent<CanvasGroup>();
            _instance._canvasGroup.alpha = 0f;
            _instance._canvasGroup.blocksRaycasts = false;

            return _instance;
        }

        /// <summary>
        /// Escurece a tela, carrega <paramref name="sceneName"/> e clareia do outro
        /// lado. O painel sobrevive à troca de cena justamente para poder terminar a
        /// transição — sem a volta, a cena nova nasceria atrás de um preto permanente.
        /// </summary>
        public void FadeToBlackAndLoad(string sceneName, float duration = -1f)
        {
            StartCoroutine(FadeAndLoad(sceneName, duration > 0f ? duration : defaultFadeDuration));
        }

        /// <summary>
        /// Escurece a tela, escreve <paramref name="message"/> sobre o preto, segura a
        /// frase e só então carrega <paramref name="sceneName"/>.
        ///
        /// A frase e a troca de cena moram na mesma coroutine porque uma depende da
        /// outra: o texto só existe *porque* a tela está preta, e carregar a cena antes
        /// de ele sair cortaria o epílogo no meio de uma linha.
        /// </summary>
        public void FadeToBlackWithMessage(
            string sceneName,
            string message,
            float duration = -1f,
            float messageFade = 1f,
            float messageHold = 3f,
            TMP_FontAsset font = null)
        {
            FadeToBlackWithMessages(sceneName, new[] { message }, duration, messageFade, messageHold, font);
        }

        /// <summary>
        /// Mesma coisa com vários cartões em sequência, um de cada vez sobre o preto.
        ///
        /// O epílogo da fase precisa de dois: um diz o que aconteceu com esta região,
        /// o outro diz o que sobrou de floresta. Ditos na mesma tela eles viram
        /// parágrafo; separados, cada um tem o tempo de assentar.
        /// </summary>
        public void FadeToBlackWithMessages(
            string sceneName,
            string[] messages,
            float duration = -1f,
            float messageFade = 1f,
            float messageHold = 3f,
            TMP_FontAsset font = null)
        {
            StartCoroutine(FadeMessageAndLoad(
                sceneName,
                messages,
                duration > 0f ? duration : defaultFadeDuration,
                messageFade,
                messageHold,
                font));
        }

        private IEnumerator FadeMessageAndLoad(
            string sceneName, string[] messages, float duration, float messageFade, float messageHold, TMP_FontAsset font)
        {
            _canvasGroup.blocksRaycasts = true;
            EnsureMessage(font);

            yield return FadeAlpha(1f, duration);

            if (messages != null)
            {
                foreach (string message in messages)
                {
                    if (string.IsNullOrEmpty(message)) continue;

                    _messageText.text = message;
                    yield return FadeGroup(_messageGroup, 1f, messageFade);
                    yield return new WaitForSeconds(messageHold);
                    yield return FadeGroup(_messageGroup, 0f, messageFade);
                }
            }

            if (!string.IsNullOrEmpty(sceneName))
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
                while (!load.isDone) yield return null;

                yield return FadeAlpha(0f, duration);
            }

            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Monta o cartão de texto na primeira vez que ele é pedido. O painel tem
        /// CanvasGroup próprio para poder entrar e sair *por cima* do preto — se ele
        /// dependesse do alpha da raiz, a frase apareceria junto com o escurecimento.
        /// </summary>
        private void EnsureMessage(TMP_FontAsset font)
        {
            if (_messageGroup == null)
            {
                var messageGO = new GameObject("Message", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
                messageGO.transform.SetParent(transform, false);

                // Margens relativas em vez de tamanho fixo: o cartão precisa caber
                // igual em 640x360 e em 1920x1080.
                var rect = (RectTransform)messageGO.transform;
                rect.anchorMin = new Vector2(0.1f, 0.35f);
                rect.anchorMax = new Vector2(0.9f, 0.65f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                _messageGroup = messageGO.GetComponent<CanvasGroup>();
                _messageGroup.alpha = 0f;
                _messageGroup.blocksRaycasts = false;

                _messageText = messageGO.GetComponent<TextMeshProUGUI>();
                _messageText.alignment = TextAlignmentOptions.Center;
                _messageText.color = new Color(0.93f, 0.9f, 0.82f);
                _messageText.raycastTarget = false;
                _messageText.enableAutoSizing = true;
                _messageText.fontSizeMin = 20f;
                _messageText.fontSizeMax = 72f;
            }

            if (font != null) _messageText.font = font;
        }

        private IEnumerator FadeAndLoad(string sceneName, float duration)
        {
            _canvasGroup.blocksRaycasts = true;

            yield return FadeAlpha(1f, duration);

            // Sem o Async, a cena nova só aparece depois do carregamento inteiro; com
            // o yield abaixo, ela já está montada quando o preto começa a sair.
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            while (!load.isDone) yield return null;

            yield return FadeAlpha(0f, duration);

            _canvasGroup.blocksRaycasts = false;
        }

        private IEnumerator FadeAlpha(float target, float duration) =>
            FadeGroup(_canvasGroup, target, duration);

        private static IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
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
