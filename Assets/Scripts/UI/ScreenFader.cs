using System.Collections;
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

        private IEnumerator FadeAlpha(float target, float duration)
        {
            float from = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, target, elapsed / duration);
                yield return null;
            }

            _canvasGroup.alpha = target;
        }
    }
}
