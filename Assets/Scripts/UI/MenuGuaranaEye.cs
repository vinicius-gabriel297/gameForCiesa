using UnityEngine;
using UnityEngine.UI;

namespace Warana.UI
{
    /// <summary>
    /// Olho do Guaraná decorativo do menu principal: os mesmos quadros do orbe de
    /// <see cref="Warana.Companion.GuaranaEye"/>, mas sem alvo pra seguir e sem
    /// piscada rara — aqui ele é o símbolo do jogo, abrindo e fechando em loop
    /// contínuo, só pra dar vida à tela de título.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Waraná/UI/Olho do Menu")]
    public class MenuGuaranaEye : MonoBehaviour
    {
        [Tooltip("Quadros do olho, do TOTALMENTE ABERTO ao TOTALMENTE FECHADO.")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("Duração de um ciclo completo de abrir+fechar, em segundos.")]
        [SerializeField] private float cycleDuration = 1.4f;

        [Tooltip("Quanto tempo o olho segura bem aberto entre um ciclo e o próximo.")]
        [SerializeField] private float openHold = 1.6f;

        private Image _image;
        private float _elapsed;
        private bool _holding;

        private void OnEnable()
        {
            _image = GetComponent<Image>();
            _elapsed = 0f;
            _holding = true;
            ShowFrame(0);
        }

        private void Update()
        {
            if (frames == null || frames.Length < 2) return;

            _elapsed += Time.deltaTime;

            if (_holding)
            {
                if (_elapsed < openHold) return;
                _elapsed -= openHold;
                _holding = false;
            }

            float t = _elapsed / cycleDuration;
            if (t >= 1f)
            {
                _elapsed -= cycleDuration;
                _holding = true;
                ShowFrame(0);
                return;
            }

            // Onda triangular: aberto -> fechado -> aberto ao longo do ciclo.
            float closed = 1f - Mathf.Abs(t * 2f - 1f);
            ShowFrame(Mathf.RoundToInt(closed * (frames.Length - 1)));
        }

        private void ShowFrame(int index)
        {
            if (_image == null || frames == null || frames.Length == 0) return;
            _image.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }
    }
}
