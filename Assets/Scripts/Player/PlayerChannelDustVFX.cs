using UnityEngine;

namespace Warana.Player
{
    /// <summary>
    /// Poeira sutil nos pés de Piatã ao entrar em Canalização — como se o peso de
    /// plantar os pés levantasse a terra solta. Só liga o emissor; quem decide
    /// quando entrar e sair do modo é o <see cref="PlayerChannel"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerChannel))]
    [AddComponentMenu("Waraná/Player/VFX de Poeira da Canalização")]
    public class PlayerChannelDustVFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem dust;

        private PlayerChannel _channel;

        private void Awake()
        {
            _channel = GetComponent<PlayerChannel>();
        }

        private void OnEnable()
        {
            if (_channel != null) _channel.ChannelingChanged += OnChannelingChanged;
        }

        private void OnDisable()
        {
            if (_channel != null) _channel.ChannelingChanged -= OnChannelingChanged;
        }

        private void OnChannelingChanged(bool channeling)
        {
            if (dust == null) return;

            if (channeling) dust.Play();
            else dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
