using System;
using UnityEngine;
using Warana.Player;

namespace Warana.Combat
{
    /// <summary>
    /// Os slots do raio do Waraná. Sem eles a canalização é dano infinito por
    /// segurar um botão — o custo de posicionamento que o <see cref="PlayerChannel"/>
    /// cobra some assim que o jogador acha um canto seguro.
    ///
    /// Três cargas transformam a canalização em uma decisão com fundo: cada rajada
    /// tem um fim visível, e o jogador escolhe *quando* gastá-la. A recarga só corre
    /// fora da canalização, então repor munição custa sair da pose e se expor — o
    /// mesmo preço que o resto do sistema já cobra.
    /// </summary>
    [AddComponentMenu("Waraná/Combate/Cargas do Raio")]
    public class BoltCharges : MonoBehaviour
    {
        [Header("Slots")]
        [Tooltip("Quantos raios cabem antes de secar.")]
        [Min(1)]
        [SerializeField] private int maxCharges = 3;

        [Header("Recarga")]
        [Tooltip("Espera após o último disparo antes de a recarga começar a contar.")]
        [SerializeField] private float rechargeDelay = 0.6f;

        [Tooltip("Segundos por slot recuperado.")]
        [SerializeField] private float rechargeInterval = 2f;

        [Tooltip("Só recarrega fora da canalização. Desligue para deixar o Waraná repor mesmo com o botão segurado.")]
        [SerializeField] private bool rechargeOutsideChannelOnly = true;

        [Tooltip("Quem diz se estamos canalizando. Vazio = procura o PlayerChannel da cena.")]
        [SerializeField] private PlayerChannel channel;

        private float _delayLeft;
        private float _refillElapsed;

        /// <summary>(cargas atuais, máximo) — gancho para HUD e áudio.</summary>
        public event Action<int, int> Changed;

        public int Current { get; private set; }
        public int Max => maxCharges;
        public bool CanSpend => Current > 0;

        /// <summary>Progresso rumo ao próximo slot, em 0–1. Zero quando cheio ou em espera.</summary>
        public float RefillProgress { get; private set; }

        private void Awake()
        {
            if (channel == null) channel = FindAnyObjectByType<PlayerChannel>();
        }

        private void OnEnable()
        {
            Current = maxCharges;
            _delayLeft = 0f;
            _refillElapsed = 0f;
            RefillProgress = 0f;
            Changed?.Invoke(Current, maxCharges);
        }

        private void Update()
        {
            if (Current >= maxCharges)
            {
                _refillElapsed = 0f;
                RefillProgress = 0f;
                return;
            }

            if (_delayLeft > 0f)
            {
                _delayLeft -= Time.deltaTime;
                return;
            }

            // Congela o progresso, não o zera: interromper a recarga para dar um tiro
            // não deve apagar o que já foi esperado.
            if (rechargeOutsideChannelOnly && channel != null && channel.IsChanneling) return;

            _refillElapsed += Time.deltaTime;

            if (rechargeInterval > 0f && _refillElapsed < rechargeInterval)
            {
                RefillProgress = _refillElapsed / rechargeInterval;
                return;
            }

            _refillElapsed = 0f;
            RefillProgress = 0f;
            Current = Mathf.Min(maxCharges, Current + 1);
            Changed?.Invoke(Current, maxCharges);
        }

        /// <summary>Consome um slot. Retorna false — sem cobrar nada — se não houver carga.</summary>
        public bool TrySpend(int amount = 1)
        {
            if (amount <= 0 || Current < amount) return false;

            Current -= amount;

            // Disparar não acumula progresso: quem atira em rajada não recarrega junto.
            _delayLeft = rechargeDelay;
            _refillElapsed = 0f;
            RefillProgress = 0f;

            Changed?.Invoke(Current, maxCharges);
            return true;
        }

        /// <summary>Enche tudo de uma vez. Para itens, checkpoint e respawn.</summary>
        public void Refill()
        {
            if (Current == maxCharges) return;

            Current = maxCharges;
            _delayLeft = 0f;
            _refillElapsed = 0f;
            RefillProgress = 0f;
            Changed?.Invoke(Current, maxCharges);
        }

        /// <summary>
        /// Aumenta o número máximo de cargas e concede o mesmo tanto já carregado.
        /// Usado por recompensas (ex.: raio extra por abates).
        /// </summary>
        public void IncreaseMaxCharges(int amount)
        {
            if (amount <= 0) return;

            maxCharges += amount;
            Current = Mathf.Min(maxCharges, Current + amount);
            Changed?.Invoke(Current, maxCharges);
        }
    }
}
