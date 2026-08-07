using System;
using UnityEngine;

namespace Warana.Player
{
    /// <summary>
    /// Modo de Canalização. Enquanto o RMB estiver pressionado, Piatã planta os pés,
    /// assume a pose de concentração e abre mão de tudo menos da direção que encara —
    /// quem ataca é o Waraná, não ele.
    ///
    /// A troca entre explorar (móvel, sem dano) e canalizar (parado, com dano) é o
    /// coração da mecânica: o custo do ataque é o posicionamento, decidido *antes*.
    /// Por isso a habilidade não tem duração nem cooldown próprios; ela dura
    /// exatamente enquanto o botão estiver segurado.
    /// </summary>
    [AddComponentMenu("Waraná/Player/Canalização")]
    public class PlayerChannel : PlayerAbility
    {
        [Header("Condições")]
        [Tooltip("Só canaliza com os pés no chão. Desligue para permitir canalizar no ar.")]
        [SerializeField] private bool requireGrounded = true;

        [Tooltip("Tempo mínimo em canalização antes de poder sair. Evita liga/desliga de 1 frame.")]
        [SerializeField] private float minimumDuration = 0.1f;

        [Header("Mira")]
        [Tooltip("Altura do ponto de mira acima do pivô do jogador, em unidades.")]
        [SerializeField] private float aimHeight = 0.55f;

        private float _elapsed;

        /// <summary>Disparado ao entrar (true) e ao sair (false) da canalização.</summary>
        public event Action<bool> ChannelingChanged;

        public bool IsChanneling => IsActive;

        /// <summary>Origem lógica da mira — a altura dos olhos, não os pés.</summary>
        public Vector2 AimOrigin => (Vector2)transform.position + Vector2.up * aimHeight;

        /// <summary>Direção que Piatã encara, como vetor unitário no plano.</summary>
        public Vector2 AimDirection => new Vector2(Controller.FacingDirection, 0f);

        // O controller continua aplicando gravidade e colisão; quem zera o
        // deslocamento é o FreezeControl. Assim Piatã fica preso ao chão de verdade,
        // sem virar um objeto cinemático flutuando no meio da queda.
        public override bool OverridesMovement => false;

        // Antes do PlayerAttack (10): segurar o RMB tem prioridade sobre o golpe de lança.
        public override int Priority => 5;

        public override bool TryActivate()
        {
            if (!Input.ChannelHeld) return false;
            if (requireGrounded && !Controller.IsGrounded) return false;

            _elapsed = 0f;
            IsActive = true;
            Controller.FreezeControl(true);
            ChannelingChanged?.Invoke(true);
            return true;
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            // A única liberdade durante a canalização. Ler o eixo bruto (e não o
            // movimento aplicado) é o que faz a virada continuar respondendo mesmo
            // com o controle congelado.
            float inputX = Input.MoveX;
            if (!Mathf.Approximately(inputX, 0f))
                Controller.SetFacing(inputX > 0f ? 1 : -1);

            if (_elapsed < minimumDuration) return;

            bool keepGoing = Input.ChannelHeld && (!requireGrounded || Controller.IsGrounded);
            if (!keepGoing) Stop();
        }

        /// <summary>Corta a canalização de fora — dano, cutscene, queda.</summary>
        public void Cancel()
        {
            if (IsActive) Stop();
        }

        private void Stop()
        {
            Controller.FreezeControl(false);
            IsActive = false;
            ChannelingChanged?.Invoke(false);
        }

        private void OnDisable()
        {
            // Desligar o componente canalizando deixaria o controle congelado para sempre.
            if (IsActive) Stop();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Vector3 origin = transform.position + Vector3.up * aimHeight;
            Gizmos.DrawWireSphere(origin, 0.08f);
        }
#endif
    }
}
