using UnityEngine;

namespace Warana.Enemies
{
    /// <summary>
    /// Ponte entre o estado do Mad Ghost e o Animator, no mesmo molde do
    /// <c>PlayerAnimator</c>: só lê o controller e escreve parâmetros. Nenhuma decisão
    /// de gameplay mora aqui — a FSM manda tocar, este componente só obedece.
    /// </summary>
    [RequireComponent(typeof(EnemyController2D))]
    [AddComponentMenu("Waraná/Inimigos/Animação do Mad Ghost")]
    public class MadGhostAnimator : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Animator do filho Visual. Vazio = busca nos filhos.")]
        [SerializeField] private Animator animator;

        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Configuração")]
        [Tooltip("A arte do Mad Ghost é desenhada virada para a direita (o olho fica à direita).")]
        [SerializeField] private bool spriteFacesRight = true;

        private EnemyController2D _controller;

        private static readonly int SpeedHash = Animator.StringToHash(MadGhostAnimation.Param.Speed);
        private static readonly int UnsheatheHash = Animator.StringToHash(MadGhostAnimation.Param.Unsheathe);
        private static readonly int AttackHash = Animator.StringToHash(MadGhostAnimation.Param.Attack);
        private static readonly int SheatheHash = Animator.StringToHash(MadGhostAnimation.Param.Sheathe);
        private static readonly int HitHash = Animator.StringToHash(MadGhostAnimation.Param.Hit);
        private static readonly int DeadHash = Animator.StringToHash(MadGhostAnimation.Param.Dead);

        private void Awake()
        {
            _controller = GetComponent<EnemyController2D>();

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (animator == null)
                Debug.LogError("[MadGhostAnimator] Nenhum Animator encontrado nos filhos.", this);
        }

        private void LateUpdate()
        {
            if (animator == null) return;

            animator.SetFloat(SpeedHash, Mathf.Abs(_controller.VelocityX));

            UpdateFacing();
        }

        private void UpdateFacing()
        {
            if (spriteRenderer == null) return;

            bool facingRight = _controller.FacingDirection > 0;
            spriteRenderer.flipX = facingRight != spriteFacesRight;
        }

        public void PlayUnsheathe() => SetTrigger(UnsheatheHash);

        public void PlayAttack() => SetTrigger(AttackHash);

        public void PlaySheathe() => SetTrigger(SheatheHash);

        public void PlayHit() => SetTrigger(HitHash);

        public void SetDead(bool dead)
        {
            if (animator != null) animator.SetBool(DeadHash, dead);
        }

        private void SetTrigger(int hash)
        {
            if (animator != null) animator.SetTrigger(hash);
        }
    }
}
