using UnityEngine;

namespace Warana.Enemies
{
    /// <summary>
    /// Ponte entre o estado da Abomination e o Animator. Só lê o controller e escreve
    /// parâmetros — a FSM manda tocar, este componente obedece.
    /// </summary>
    [RequireComponent(typeof(EnemyController2D))]
    [AddComponentMenu("Waraná/Inimigos/Animação da Abomination")]
    public class AbominationAnimator : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Animator do filho Visual. Vazio = busca nos filhos.")]
        [SerializeField] private Animator animator;

        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Configuração")]
        [Tooltip("A arte da Abomination é desenhada virada para a direita.")]
        [SerializeField] private bool spriteFacesRight = true;

        private EnemyController2D _controller;

        private static readonly int SpeedHash = Animator.StringToHash(AbominationAnimation.Param.Speed);
        private static readonly int AttackHash = Animator.StringToHash(AbominationAnimation.Param.Attack);
        private static readonly int HitHash = Animator.StringToHash(AbominationAnimation.Param.Hit);
        private static readonly int DeadHash = Animator.StringToHash(AbominationAnimation.Param.Dead);

        private void Awake()
        {
            _controller = GetComponent<EnemyController2D>();

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (animator == null)
                Debug.LogError("[AbominationAnimator] Nenhum Animator encontrado nos filhos.", this);
        }

        private void LateUpdate()
        {
            if (animator == null) return;

            animator.SetFloat(SpeedHash, Mathf.Abs(_controller.VelocityX));

            if (spriteRenderer == null) return;

            bool facingRight = _controller.FacingDirection > 0;
            spriteRenderer.flipX = facingRight != spriteFacesRight;
        }

        public void PlayAttack() => SetTrigger(AttackHash);

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
