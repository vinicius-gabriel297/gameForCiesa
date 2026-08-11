using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Warana.Environment
{
    /// <summary>
    /// Respiração lenta de intensidade para um Light2D — o que diferencia um brilho
    /// mágico de uma lâmpada. Usado na Árvore Guardiã para destacá-la no mapa sem
    /// ela parecer estática demais.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    [AddComponentMenu("Waraná/Ambiente/Luz 2D Pulsante")]
    public class PulsingLight2D : MonoBehaviour
    {
        [SerializeField] private float minIntensity = 1.3f;
        [SerializeField] private float maxIntensity = 2.1f;
        [SerializeField] private float period = 3.2f;

        private Light2D _light;

        /// <summary>
        /// Multiplicador da respiração, para quem controla o *estado* da luz sem querer
        /// conhecer o pulso. A restauração da floresta usa isto: o brilho da árvore
        /// sagrada sobe junto com a cura e a pulsação continua. Escrever
        /// <c>intensity</c> direto não funcionaria — o Update abaixo sobrescreveria no
        /// frame seguinte.
        /// </summary>
        public float IntensityScale { get; set; } = 1f;

        private void Awake() => _light = GetComponent<Light2D>();

        private void Update()
        {
            float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / period)) + 1f) * 0.5f;
            _light.intensity = Mathf.Lerp(minIntensity, maxIntensity, t) * IntensityScale;
        }
    }
}
