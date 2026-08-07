using System;
using UnityEngine;

namespace Warana.Player
{
    /// <summary>
    /// Golpe de lança. Habilidade plugável: o <see cref="PlayerController2D"/>
    /// descobre sozinho no Awake. No chão o ataque prende o personagem no lugar;
    /// no ar ele mantém o controle aéreo para não travar a trajetória do pulo.
    /// </summary>
    public class PlayerAttack : PlayerAbility
    {
        [Header("Timing")]
        [Tooltip("Duração do golpe. Deve bater com o comprimento do clip Warana_Attack.")]
        [SerializeField] private float duration = 0.4f;

        [Tooltip("Intervalo mínimo entre dois golpes, contado a partir do fim do anterior.")]
        [SerializeField] private float cooldown = 0.1f;

        [Tooltip("Janela para o botão apertado um pouco cedo ainda contar.")]
        [SerializeField] private float bufferTime = 0.12f;

        [Header("Comportamento")]
        [Tooltip("No chão, trava o movimento durante o golpe — dá peso e impede atacar correndo.")]
        [SerializeField] private bool rootWhenGrounded = true;

        private float _timeLeft;
        private float _cooldownLeft;
        private float _bufferLeft;
        private bool _rooted;

        /// <summary>Disparado no frame em que o golpe começa. O <see cref="PlayerAnimator"/> escuta.</summary>
        public event Action Started;

        public bool IsAttacking => IsActive;

        // O controller aplica gravidade e movimento normalmente; quem trava o
        // deslocamento no chão é o FreezeControl, não este override.
        public override bool OverridesMovement => false;

        // Depois de habilidades de deslocamento (dash, wall jump), que têm prioridade.
        public override int Priority => 10;

        private void Update()
        {
            _cooldownLeft = Mathf.Max(0f, _cooldownLeft - Time.deltaTime);

            // O input é lido no Update e consumido no FixedUpdate: sem o buffer
            // um toque curto cairia entre dois passos de física e sumiria.
            _bufferLeft = Input.AttackPressedThisFrame
                ? bufferTime
                : Mathf.Max(0f, _bufferLeft - Time.deltaTime);
        }

        public override bool TryActivate()
        {
            if (_bufferLeft <= 0f || _cooldownLeft > 0f) return false;

            _bufferLeft = 0f;
            _timeLeft = duration;
            _rooted = rootWhenGrounded && Controller.IsGrounded;
            IsActive = true;

            if (_rooted) Controller.FreezeControl(true);

            Started?.Invoke();
            return true;
        }

        public override void Tick(float deltaTime)
        {
            _timeLeft -= deltaTime;
            if (_timeLeft > 0f) return;

            if (_rooted) Controller.FreezeControl(false);
            _rooted = false;
            _cooldownLeft = cooldown;
            End();
        }

        private void OnDisable()
        {
            // Desligar o componente no meio do golpe deixaria o controle congelado.
            if (_rooted) Controller.FreezeControl(false);
            _rooted = false;
            IsActive = false;
        }
    }
}
