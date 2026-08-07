using UnityEngine;

namespace Warana.CameraRig
{
    /// <summary>
    /// Câmera de plataforma: segue o alvo com SmoothDamp e um leve look-ahead
    /// na direção do movimento, para dar visão do que vem pela frente.
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("Alvo")]
        [SerializeField] private Transform target;

        [Tooltip("Enquadramento acima do personagem. A janela útil é ~5,6 unidades de altura.")]
        [SerializeField] private Vector2 offset = new Vector2(0f, 0.6f);

        [Header("Suavização")]
        [Tooltip("Tempo aproximado para alcançar o alvo. Menor = câmera mais colada.")]
        [SerializeField] private float smoothTime = 0.06f;

        [Tooltip("Distância máxima que a câmera aceita ficar atrás do alvo. Acima disso ela cola, sem esticar.")]
        [SerializeField] private float maxFollowDistance = 1.6f;

        [Header("Look Ahead")]
        [Tooltip("Quanto a câmera se adianta na direção do movimento horizontal.")]
        [SerializeField] private float lookAheadDistance = 1f;

        [SerializeField] private float lookAheadSmoothTime = 0.2f;

        private Rigidbody2D _targetBody;
        private Vector3 _velocity;
        private float _lookAhead;
        private float _lookAheadVelocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
        }

        private void Awake()
        {
            if (target != null) SetTarget(target);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float horizontalSpeed = _targetBody != null ? _targetBody.linearVelocity.x : 0f;
            float desiredLookAhead = Mathf.Sign(horizontalSpeed) * lookAheadDistance;
            if (Mathf.Abs(horizontalSpeed) < 0.5f) desiredLookAhead = 0f;

            _lookAhead = Mathf.SmoothDamp(_lookAhead, desiredLookAhead, ref _lookAheadVelocity, lookAheadSmoothTime);

            Vector3 desired = new Vector3(
                target.position.x + offset.x + _lookAhead,
                target.position.y + offset.y,
                transform.position.z);

            Vector3 next = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);

            // Trava de segurança: em aceleração forte o SmoothDamp ainda abre uma folga
            // crescente. Aqui a câmera nunca fica mais do que maxFollowDistance atrás.
            Vector3 lag = next - desired;
            lag.z = 0f;
            if (lag.sqrMagnitude > maxFollowDistance * maxFollowDistance)
                next = desired + lag.normalized * maxFollowDistance;

            transform.position = next;
        }
    }
}
