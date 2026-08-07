using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Warana.UI
{
    /// <summary>
    /// Fileira de ícones cheio/vazio — o jeito de mostrar um recurso pequeno sem
    /// escrever número nenhum. Serve tanto para os corações quanto para as cargas
    /// do raio, porque os dois contam a mesma coisa: quantos usos ainda restam.
    ///
    /// Slot gasto escurece em vez de sumir. O total precisa continuar visível: é
    /// ele que diz ao jogador o quanto ele perdeu, e não só o quanto ainda tem.
    ///
    /// A fileira é montada em runtime a partir do máximo informado pela fonte, para
    /// que mudar a vida máxima no Inspector não exija remontar a HUD na mão.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class IconRowHUD : MonoBehaviour
    {
        [Header("Ícone")]
        [Tooltip("Sprite desenhado em cada slot.")]
        [SerializeField] private Sprite icon;

        [Tooltip("Altura do ícone em pixels de referência do Canvas. A largura sai do aspecto do sprite.")]
        [SerializeField] private float iconHeight = 22f;

        [Tooltip("Espaço entre dois ícones, em pixels de referência.")]
        [SerializeField] private float spacing = 3f;

        [Header("Cores")]
        [SerializeField] private Color filledColor = Color.white;

        [Tooltip("Slot gasto. Escuro e translúcido em vez de invisível.")]
        [SerializeField] private Color emptyColor = new Color(0f, 0f, 0f, 0.55f);

        private readonly List<Image> _icons = new List<Image>();

        /// <summary>Quantos slots a fileira tem hoje. Compare antes de remontar.</summary>
        protected int IconCount => _icons.Count;

        /// <summary>Ajusta a fileira para ter exatamente <paramref name="count"/> slots.</summary>
        protected void Build(int count)
        {
            count = Mathf.Max(0, count);

            while (_icons.Count > count)
            {
                int last = _icons.Count - 1;
                Image extra = _icons[last];
                _icons.RemoveAt(last);
                if (extra != null) Destroy(extra.gameObject);
            }

            while (_icons.Count < count) _icons.Add(CreateIcon(_icons.Count));

            Layout();
        }

        /// <summary>
        /// Pinta a fileira. O slot logo depois do último cheio recebe
        /// <paramref name="partial"/> (0–1) e serve de barra de recarga: é ele que
        /// avisa quando o próximo uso volta, sem precisar de outro elemento na tela.
        /// </summary>
        protected void Paint(int filled, float partial = 0f)
        {
            for (int i = 0; i < _icons.Count; i++)
            {
                if (_icons[i] == null) continue;

                Color color;
                if (i < filled) color = filledColor;
                else if (i == filled) color = Color.Lerp(emptyColor, filledColor, Mathf.Clamp01(partial));
                else color = emptyColor;

                _icons[i].color = color;
            }
        }

        private Image CreateIcon(int index)
        {
            var go = new GameObject($"Icon_{index}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);

            var image = go.GetComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;

            // HUD nunca intercepta clique: o jogo inteiro acontece atrás dela.
            image.raycastTarget = false;

            return image;
        }

        private void Layout()
        {
            Vector2 size = IconSize();

            for (int i = 0; i < _icons.Count; i++)
            {
                if (_icons[i] == null) continue;

                var rect = (RectTransform)_icons[i].transform;
                rect.sizeDelta = size;
                rect.anchoredPosition = new Vector2(i * (size.x + spacing), 0f);
            }

            float width = _icons.Count == 0 ? 0f : _icons.Count * size.x + (_icons.Count - 1) * spacing;
            ((RectTransform)transform).sizeDelta = new Vector2(width, size.y);
        }

        private Vector2 IconSize()
        {
            float aspect = 1f;
            if (icon != null && icon.rect.height > 0f) aspect = icon.rect.width / icon.rect.height;

            return new Vector2(iconHeight * aspect, iconHeight);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Permite calibrar tamanho e espaçamento com o jogo rodando.
            if (Application.isPlaying && _icons.Count > 0) Layout();
        }
#endif
    }
}
