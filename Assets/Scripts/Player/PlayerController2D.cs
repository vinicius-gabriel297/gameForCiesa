using System.Collections.Generic;
using UnityEngine;

namespace Warana.Player
{
    /// <summary>
    /// Movimentação do Waraná: aceleração/desaceleração, pulo de altura variável,
    /// gravidade dinâmica, coyote time, jump buffer e controle aéreo.
    /// Feeling de referência: Hollow Knight — responsivo, com peso, queda rápida.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(GroundSensor2D))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController2D : MonoBehaviour
    {
        [Header("Movimento Horizontal")]
        [Tooltip("Velocidade máxima de corrida, em unidades/segundo.")]
        // Calibrado com o clip de corrida (8 frames a 14 fps = 0,57 s por ciclo):
        // a 5 u/s o personagem avança ~2,9 unidades por ciclo, sem patinar no chão.
        [SerializeField] private float moveSpeed = 5f;

        [Tooltip("Quão rápido atinge a velocidade máxima no chão.")]
        [SerializeField] private float groundAcceleration = 55f;

        [Tooltip("Quão rápido freia no chão. Maior que a aceleração = parada firme, sem patinar.")]
        [SerializeField] private float groundDeceleration = 70f;

        [Header("Controle Aéreo")]
        [Tooltip("Aceleração no ar. Menor que a do chão = o pulo compromete a direção.")]
        [SerializeField] private float airAcceleration = 35f;

        [SerializeField] private float airDeceleration = 25f;

        [Header("Pulo")]
        [Tooltip("Altura máxima do pulo em unidades (segurando o botão). A velocidade inicial é derivada daqui.")]
        // 2 unidades sob riseGravity 34 dão ~0,34 s de subida — praticamente o mesmo
        // tempo dos 4 frames de subida do clip de pulo (0,29 s a 14 fps).
        [SerializeField] private float jumpHeight = 2f;

        [Tooltip("Gravidade durante a subida com o botão pressionado. Baixa = mais hangtime no ápice.")]
        [SerializeField] private float riseGravity = 34f;

        [Tooltip("Multiplicador de gravidade na queda. É o que dá a 'queda rápida'.")]
        [SerializeField] private float fallMultiplier = 1.7f;

        [Tooltip("Multiplicador aplicado ao soltar o botão ainda subindo (pulo curto).")]
        [SerializeField] private float lowJumpMultiplier = 2.4f;

        [Tooltip("Velocidade terminal de queda, em unidades/segundo.")]
        [SerializeField] private float maxFallSpeed = 18f;

        [Header("Assistências")]
        [Tooltip("Janela para pular depois de sair da beirada.")]
        [SerializeField] private float coyoteTime = 0.1f;

        [Tooltip("Janela para o pulo apertado no ar ser executado ao aterrissar.")]
        [SerializeField] private float jumpBufferTime = 0.1f;

        [Header("Visual")]
        [Tooltip("Vira o personagem invertendo a escala X. Desligue se a arte usar SpriteRenderer.flipX.")]
        [SerializeField] private bool flipWithScale = true;

        private Rigidbody2D _rb;
        private GroundSensor2D _ground;
        private PlayerInputHandler _input;
        private readonly List<PlayerAbility> _abilities = new List<PlayerAbility>();

        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private bool _isJumping;
        private bool _controlFrozen;
        private int _facing = 1;

        /// <summary>-1 olhando para a esquerda, +1 para a direita.</summary>
        public int FacingDirection => _facing;

        public bool IsGrounded => _ground.IsGrounded;
        public Rigidbody2D Body => _rb;

        /// <summary>Velocidade inicial necessária para atingir <c>jumpHeight</c> sob <c>riseGravity</c>.</summary>
        private float JumpVelocity => Mathf.Sqrt(2f * riseGravity * jumpHeight);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _ground = GetComponent<GroundSensor2D>();
            _input = GetComponent<PlayerInputHandler>();

            _rb.freezeRotation = true;
            _rb.gravityScale = 0f;

            GetComponents(_abilities);
            _abilities.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        private void Update()
        {
            TickTimers();

            if (_input.JumpPressedThisFrame)
                _jumpBufferCounter = jumpBufferTime;
        }

        private void FixedUpdate()
        {
            // Encostar no chão sem estar subindo encerra o pulo. Antes isso só acontecia
            // no frame exato do pouso, e bastava o sensor nunca fazer a transição
            // ar -> chão (bater a cabeça e escorregar de volta para um degrau em que a
            // caixa já sobrepunha o chão) para _isJumping ficar preso em true — o que
            // zera o coyote time para sempre e trava o pulo até o próximo pouso limpo.
            if (_ground.IsGrounded && _rb.linearVelocity.y <= 0f)
                _isJumping = false;

            if (_ground.LandedThisFrame)
            {
                for (int i = 0; i < _abilities.Count; i++)
                    _abilities[i].OnLanded();
            }

            PlayerAbility active = ResolveActiveAbility();
            if (active != null)
            {
                active.Tick(Time.fixedDeltaTime);
                if (active.OverridesMovement) return;
            }

            if (_controlFrozen)
            {
                ApplyHorizontal(0f);
                ApplyGravity();
                return;
            }

            TryJump();
            ApplyHorizontal(_input.MoveX);
            ApplyGravity();
            UpdateFacing(_input.MoveX);
        }

        private void TickTimers()
        {
            _coyoteCounter = _ground.IsGrounded && !_isJumping
                ? coyoteTime
                : Mathf.Max(0f, _coyoteCounter - Time.deltaTime);

            _jumpBufferCounter = Mathf.Max(0f, _jumpBufferCounter - Time.deltaTime);
        }

        private void TryJump()
        {
            if (_jumpBufferCounter <= 0f || _coyoteCounter <= 0f) return;

            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, JumpVelocity);
            _isJumping = true;

            // Consome ambas as janelas: sem isso um único toque geraria pulos repetidos.
            _jumpBufferCounter = 0f;
            _coyoteCounter = 0f;
        }

        private void ApplyHorizontal(float inputX)
        {
            float targetSpeed = inputX * moveSpeed;
            bool accelerating = !Mathf.Approximately(inputX, 0f);

            float rate = _ground.IsGrounded
                ? (accelerating ? groundAcceleration : groundDeceleration)
                : (accelerating ? airAcceleration : airDeceleration);

            float newX = Mathf.MoveTowards(_rb.linearVelocity.x, targetSpeed, rate * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
        }

        private void ApplyGravity()
        {
            float velocityY = _rb.linearVelocity.y;
            float gravity;

            if (velocityY > 0f && _isJumping && !_input.JumpHeld)
                gravity = riseGravity * lowJumpMultiplier;   // soltou cedo: corta o arco
            else if (velocityY > 0f)
                gravity = riseGravity;                       // subida leve, hangtime no ápice
            else
                gravity = riseGravity * fallMultiplier;      // queda rápida

            velocityY -= gravity * Time.fixedDeltaTime;
            velocityY = Mathf.Max(velocityY, -maxFallSpeed);

            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, velocityY);
        }

        private void UpdateFacing(float inputX)
        {
            if (Mathf.Approximately(inputX, 0f)) return;

            SetFacing(inputX > 0f ? 1 : -1);
        }

        private PlayerAbility ResolveActiveAbility()
        {
            for (int i = 0; i < _abilities.Count; i++)
            {
                if (_abilities[i].IsActive) return _abilities[i];
            }

            for (int i = 0; i < _abilities.Count; i++)
            {
                if (_abilities[i].isActiveAndEnabled && _abilities[i].TryActivate())
                    return _abilities[i];
            }

            return null;
        }

        // ---- API para habilidades, dano/knockback e cutscenes ----

        public void SetVelocity(Vector2 velocity) => _rb.linearVelocity = velocity;

        public void AddImpulse(Vector2 impulse) => _rb.linearVelocity += impulse;

        /// <summary>Bloqueia o input de movimento sem desligar a física (knockback, diálogo).</summary>
        public void FreezeControl(bool frozen) => _controlFrozen = frozen;

        /// <summary>
        /// Vira o personagem sem passar pelo movimento. Existe para os estados em que
        /// o deslocamento está congelado mas a mira continua livre — a canalização é o caso.
        /// </summary>
        public void SetFacing(int direction)
        {
            if (direction == 0 || direction == _facing) return;

            _facing = direction > 0 ? 1 : -1;

            if (!flipWithScale) return;

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * _facing;
            transform.localScale = scale;
        }

        /// <summary>Zera o estado de pulo — útil ao terminar um dash ou wall jump.</summary>
        public void CancelJumpState() => _isJumping = false;

        /// <summary>Reabre a janela de coyote (ex.: ao soltar de uma parede).</summary>
        public void GrantCoyoteTime() => _coyoteCounter = coyoteTime;
    }
}
