using UnityEngine;

namespace Warana.Player
{
    /// <summary>
    /// Detecção de chão por OverlapBox. Separado do controller para poder ser
    /// reusado por outras entidades (inimigos) e ajustado visualmente via gizmo.
    /// </summary>
    [DefaultExecutionOrder(-100)] // roda antes do PlayerController2D
    public class GroundSensor2D : MonoBehaviour
    {
        [Header("Detecção")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("Centro da caixa de checagem, relativo ao pivô do personagem.")]
        [SerializeField] private Vector2 offset = new Vector2(0f, -0.5f);

        [Tooltip("Largura um pouco menor que o collider evita pegar paredes laterais.")]
        [SerializeField] private Vector2 size = new Vector2(0.8f, 0.12f);

        [Header("Debug")]
        [SerializeField] private bool drawGizmo = true;

        /// <summary>True enquanto houver chão sob os pés.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>True apenas no FixedUpdate em que o personagem tocou o chão.</summary>
        public bool LandedThisFrame { get; private set; }

        public LayerMask GroundLayer => groundLayer;

        private bool _wasGrounded;

        private void FixedUpdate()
        {
            _wasGrounded = IsGrounded;
            IsGrounded = Physics2D.OverlapBox(BoxCenter, size, 0f, groundLayer) != null;
            LandedThisFrame = IsGrounded && !_wasGrounded;
        }

        private Vector2 BoxCenter => (Vector2)transform.position + offset;

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(BoxCenter, size);
        }
    }
}
