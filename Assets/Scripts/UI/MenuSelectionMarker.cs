using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Warana.UI
{
    /// <summary>
    /// Marca visualmente qual item do menu está selecionado.
    ///
    /// O destaque por cor sozinho não resolve: os botões são placas de pergaminho
    /// claro, e tingir claro sobre claro dá uma diferença que some na tela do jogador —
    /// no controle, onde o destaque é a <em>única</em> forma de saber onde se está, isso
    /// deixa o menu inutilizável. Uma marca ao lado da placa não depende da arte do
    /// botão nem de o jogador comparar duas tonalidades próximas.
    ///
    /// <para>A marca vive no canvas, e não dentro do botão, porque os botões estão sob um
    /// layout group — um filho a mais entraria na conta do layout e empurraria a lista.</para>
    /// </summary>
    [AddComponentMenu("Waraná/UI/Marca de Seleção")]
    [RequireComponent(typeof(Canvas))]
    public class MenuSelectionMarker : MonoBehaviour
    {
        [Tooltip("Distância entre a marca e a borda esquerda do item selecionado, " +
                 "em pixels da resolução de referência.")]
        [SerializeField] private float gap = 22f;

        [SerializeField] private Vector2 size = new Vector2(14f, 40f);

        [SerializeField] private Color color = new Color(1f, 0.847f, 0.529f, 1f);

        [Tooltip("Amplitude do vaivém da marca, em pixels. Zero deixa parada.")]
        [SerializeField] private float bobAmplitude = 5f;

        private const float BobPeriod = 1.1f;

        private Canvas _canvas;
        private RectTransform _marker;
        private GameObject _current;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            BuildMarker();
        }

        private void BuildMarker()
        {
            var go = new GameObject("Selection Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            _marker = (RectTransform)go.transform;
            _marker.anchorMin = _marker.anchorMax = _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;
            // Sem sprite o Image desenha um retângulo cheio — é o bastante para uma marca,
            // e evita depender de um asset de seta que o pacote de UI pode não ter.
            image.raycastTarget = false;

            // Fica por último para desenhar por cima das placas.
            go.transform.SetAsLastSibling();
            go.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_marker == null) return;

            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            // Um item desligado (os botões de trás com um painel aberto) não deve levar
            // a marca junto: ela apontaria para algo que não responde.
            if (selected != null)
            {
                var selectable = selected.GetComponent<Selectable>();
                if (selectable == null || !selectable.IsInteractable() || !selected.activeInHierarchy)
                    selected = null;
            }

            if (selected == null)
            {
                if (_marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);
                _current = null;
                return;
            }

            if (!_marker.gameObject.activeSelf)
            {
                _marker.gameObject.SetActive(true);
                // De novo a cada vez que aparece: no menu de pausa a marca nasce antes do
                // painel, e a ordem de irmãos do canvas é o que decide quem desenha por
                // cima — sem isto ela ficaria escondida atrás da placa.
                _marker.SetAsLastSibling();
            }

            _current = selected;

            Follow((RectTransform)_current.transform);
        }

        /// <summary>
        /// Acompanha o alvo pelos cantos em espaço de tela: os itens do menu estão em
        /// pais diferentes (lista, painel de opções, painel de controles), então posição
        /// local não serve de referência comum.
        /// </summary>
        private void Follow(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Vector3 leftEdge = (corners[0] + corners[1]) * 0.5f;

            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            float bob = bobAmplitude <= 0f
                ? 0f
                : Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / BobPeriod) * bobAmplitude;

            _marker.position = leftEdge + new Vector3(-(gap + bob) * scale, 0f, 0f);
            _marker.sizeDelta = size;
        }
    }
}
