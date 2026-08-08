using UnityEngine;
using Warana.Combat;
using Warana.Player;

namespace Warana.Enemies
{
    /// <summary>
    /// Locomoção de inimigo terrestre. Faz o mesmo papel que o
    /// <see cref="PlayerController2D"/> faz para Piatã — gravidade manual, aceleração
    /// e direção encarada — mas sem input: quem decide para onde ir é a IA.
    ///
    /// Gravidade é aplicada à mão (gravityScale = 0) pelo mesmo motivo do Player:
    /// queda com curva própria é previsível, e a física da Unity não decide sozinha
    /// o peso do personagem.
    ///
    /// O transform nunca é espelhado. Inverter a escala em X viraria também os
    /// colliders e as sondas de parede/beirada, e a IA passaria a enxergar o mundo
    /// invertido. Quem espelha é o SpriteRenderer, no <see cref="MadGhostAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(GroundSensor2D))]
    [AddComponentMenu("Waraná/Inimigos/Controle de Inimigo 2D")]
    public class EnemyController2D : MonoBehaviour, IKnockbackReceiver
    {
        [Header("Movimento")]
        [Tooltip("Quão rápido atinge a velocidade pedida. Baixo = inimigo pesado, arranque lento.")]
        [SerializeField] private float acceleration = 30f;

        [Tooltip("Quão rápido freia quando mandam parar.")]
        [SerializeField] private float deceleration = 45f;

        [Header("Gravidade")]
        [SerializeField] private float gravity = 34f;

        [SerializeField] private float maxFallSpeed = 18f;

        [Header("Sondas")]
        [Tooltip("O que conta como chão e como parede.")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("Altura do raio que procura parede, a partir do pivô (os pés).")]
        [SerializeField] private float wallProbeHeight = 0.28f;

        [Tooltip("Distância à frente em que uma parede já conta como bloqueio.")]
        [SerializeField] private float wallProbeDistance = 0.22f;

        [Tooltip("Quão à frente do pivô o inimigo procura o chão para não cair da beirada.")]
        [SerializeField] private float ledgeProbeAhead = 0.32f;

        [Tooltip("Profundidade da busca por chão à frente. Maior = aceita degraus mais fundos.")]
        [SerializeField] private float ledgeProbeDepth = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private Rigidbody2D _rb;
        private GroundSensor2D _ground;
        private int _facing = 1;
        private float _targetSpeed;
        private float _knockbackTimeLeft;

        /// <summary>-1 olhando para a esquerda, +1 para a direita.</summary>
        public int FacingDirection => _facing;

        public bool IsGrounded => _ground.IsGrounded;

        public Rigidbody2D Body => _rb;

        /// <summary>Velocidade horizontal atual, com sinal.</summary>
        public float VelocityX => _rb.linearVelocity.x;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _ground = GetComponent<GroundSensor2D>();

            _rb.freezeRotation = true;
            _rb.gravityScale = 0f;
        }

        private void FixedUpdate()
        {
            if (_knockbackTimeLeft > 0f)
            {
                _knockbackTimeLeft -= Time.fixedDeltaTime;
                ApplyGravity();
                _targetSpeed = 0f;
                return;
            }

            ApplyHorizontal();
            ApplyGravity();

            // O alvo dura um passo de física: quem quiser andar precisa pedir todo
            // frame. Sem isso, uma IA que troca de estado no meio do caminho deixaria
            // o inimigo deslizando com a última ordem para sempre.
            _targetSpeed = 0f;
        }

        /// <summary>
        /// Empurrão de dano: aplica a velocidade e suspende a locomoção da IA por
        /// <paramref name="duration"/> segundos, deixando só a gravidade agir.
        /// </summary>
        public void ApplyKnockback(Vector2 velocity, float duration)
        {
            _rb.linearVelocity = velocity;
            _knockbackTimeLeft = duration;
        }

        // ------------------------------------------------------------------ ordens

        /// <summary>Anda na direção dada (-1 ou +1) à velocidade pedida, virando para lá.</summary>
        public void Move(int direction, float speed)
        {
            if (direction == 0 || speed <= 0f)
            {
                Stop();
                return;
            }

            SetFacing(direction);
            _targetSpeed = direction * speed;
        }

        /// <summary>Freia até parar, sem mexer na direção encarada.</summary>
        public void Stop() => _targetSpeed = 0f;

        public void SetFacing(int direction)
        {
            if (direction == 0) return;

            _facing = direction > 0 ? 1 : -1;
        }

        public void Flip() => SetFacing(-_facing);

        // ------------------------------------------------------------------ física

        private void ApplyHorizontal()
        {
            bool accelerating = !Mathf.Approximately(_targetSpeed, 0f);
            float rate = accelerating ? acceleration : deceleration;

            float newX = Mathf.MoveTowards(_rb.linearVelocity.x, _targetSpeed, rate * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
        }

        private void ApplyGravity()
        {
            float velocityY = _rb.linearVelocity.y - gravity * Time.fixedDeltaTime;
            velocityY = Mathf.Max(velocityY, -maxFallSpeed);

            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, velocityY);
        }

        // ------------------------------------------------------------------ sondas

        /// <summary>True se há parede logo à frente, na direção encarada.</summary>
        public bool HasWallAhead =>
            Physics2D.Raycast(WallProbeOrigin, Vector2.right * _facing, wallProbeDistance, groundLayer);

        /// <summary>
        /// True enquanto ainda há chão à frente. É o que impede a patrulha de andar
        /// para fora da plataforma — o inimigo respeita a beirada mesmo perseguindo.
        /// </summary>
        public bool HasGroundAhead =>
            Physics2D.Raycast(LedgeProbeOrigin, Vector2.down, ledgeProbeDepth, groundLayer);

        /// <summary>Parede à frente ou fim do chão: os dois motivos para dar meia-volta.</summary>
        public bool IsBlockedAhead => HasWallAhead || !HasGroundAhead;

        private Vector2 WallProbeOrigin => (Vector2)transform.position + Vector2.up * wallProbeHeight;

        private Vector2 LedgeProbeOrigin =>
            (Vector2)transform.position + new Vector2(_facing * ledgeProbeAhead, 0.1f);

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            // Fora do Play o facing ainda não foi inicializado pelo Awake; assumir a
            // direita deixa o gizmo utilizável na hora de montar o inimigo na cena.
            int facing = Application.isPlaying ? _facing : 1;

            Vector2 wallOrigin = (Vector2)transform.position + Vector2.up * wallProbeHeight;
            Gizmos.color = Application.isPlaying && HasWallAhead ? Color.red : Color.cyan;
            Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.right * facing * wallProbeDistance);

            Vector2 ledgeOrigin = (Vector2)transform.position + new Vector2(facing * ledgeProbeAhead, 0.1f);
            Gizmos.color = Application.isPlaying && !HasGroundAhead ? Color.red : Color.yellow;
            Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + Vector2.down * ledgeProbeDepth);
        }
    }
}
