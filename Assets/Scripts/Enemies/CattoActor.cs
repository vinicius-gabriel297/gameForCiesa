using System.Collections;
using UnityEngine;

namespace Warana.Enemies
{
    /// <summary>
    /// Ator do Catto na cutscene do Prólogo: sem rigidbody, sem FSM, sem IA — só
    /// obedece quem chama (o <c>PrologueController</c>). Aparece uma única vez para
    /// atrapalhar o ritual, então não precisa de nada além de tocar clip e mover o
    /// transform num roteiro fixo.
    /// </summary>
    [AddComponentMenu("Waraná/Inimigos/Ator do Catto")]
    public class CattoActor : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Animator do filho Visual. Vazio = busca nos filhos.")]
        [SerializeField] private Animator animator;

        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("A arte é desenhada virada para a direita.")]
        [SerializeField] private bool spriteFacesRight = true;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void PlayWalk() => animator.Play(CattoAnimation.State.Walk, 0, 0f);

        public void PlayIdleSit() => animator.Play(CattoAnimation.State.IdleSit, 0, 0f);

        public void PlayAttack() => animator.Play(CattoAnimation.State.Attack, 0, 0f);

        /// <summary>Espelha a arte para encarar a direção dada (positivo = direita).</summary>
        public void FaceDirection(float directionX)
        {
            if (spriteRenderer == null || Mathf.Approximately(directionX, 0f)) return;

            bool facingRight = directionX > 0f;
            spriteRenderer.flipX = facingRight != spriteFacesRight;
        }

        /// <summary>
        /// Anda em linha reta até <paramref name="target"/>, tocando Walk. Sem física:
        /// é um MoveTowards direto no transform, suficiente para uma entrada roteirizada.
        /// </summary>
        public IEnumerator WalkTo(Vector3 target, float speed)
        {
            FaceDirection(target.x - transform.position.x);
            PlayWalk();

            while ((transform.position - target).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }

            transform.position = target;
        }
    }
}
