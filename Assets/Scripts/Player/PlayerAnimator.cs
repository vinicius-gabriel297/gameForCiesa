using UnityEngine;

namespace Warana.Player
{
    /// <summary>
    /// Ponte entre o estado físico do Waraná e o Animator. Só lê o controller e
    /// escreve parâmetros — nenhuma decisão de gameplay mora aqui.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public class PlayerAnimator : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Animator do filho Visual. Vazio = busca nos filhos.")]
        [SerializeField] private Animator animator;

        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Configuração")]
        [Tooltip("Desmarque se a arte for desenhada virada para a esquerda.")]
        [SerializeField] private bool spriteFacesRight = true;

        [Tooltip("Começa em Walking em vez de Running. A locomoção padrão é Running.")]
        [SerializeField] private bool walkMode;

        private PlayerController2D _controller;
        private PlayerAttack _attack;
        private Rigidbody2D _body;

        private static readonly int SpeedHash = Animator.StringToHash(WaranaAnimation.Param.Speed);
        private static readonly int GroundedHash = Animator.StringToHash(WaranaAnimation.Param.Grounded);
        private static readonly int VelocityYHash = Animator.StringToHash(WaranaAnimation.Param.VelocityY);
        private static readonly int WalkHash = Animator.StringToHash(WaranaAnimation.Param.Walk);
        private static readonly int AttackHash = Animator.StringToHash(WaranaAnimation.Param.Attack);
        private static readonly int DeadHash = Animator.StringToHash(WaranaAnimation.Param.Dead);

        /// <summary>
        /// Running é o padrão. Ligue para trocar por Walking em situações específicas
        /// (área segura, diálogo, carregando algo, cena roteirizada).
        /// </summary>
        public bool WalkMode
        {
            get => walkMode;
            set => walkMode = value;
        }

        private void Awake()
        {
            _controller = GetComponent<PlayerController2D>();
            _attack = GetComponent<PlayerAttack>();
            _body = GetComponent<Rigidbody2D>();

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (animator == null)
                Debug.LogError("[PlayerAnimator] Nenhum Animator encontrado nos filhos.", this);
        }

        private void OnEnable()
        {
            if (_attack != null) _attack.Started += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_attack != null) _attack.Started -= OnAttackStarted;
        }

        private void LateUpdate()
        {
            if (animator == null) return;

            Vector2 velocity = _body.linearVelocity;

            animator.SetFloat(SpeedHash, Mathf.Abs(velocity.x));
            animator.SetFloat(VelocityYHash, velocity.y);
            animator.SetBool(GroundedHash, _controller.IsGrounded);
            animator.SetBool(WalkHash, walkMode);

            UpdateFacing();
        }

        private void UpdateFacing()
        {
            if (spriteRenderer == null) return;

            bool facingRight = _controller.FacingDirection > 0;
            spriteRenderer.flipX = facingRight != spriteFacesRight;
        }

        private void OnAttackStarted() => animator.SetTrigger(AttackHash);

        /// <summary>Entra (ou sai) do estado de morte. Sem sistema de vida ainda: chame pelo gameplay.</summary>
        public void SetDead(bool dead)
        {
            if (animator != null) animator.SetBool(DeadHash, dead);
        }
    }
}
