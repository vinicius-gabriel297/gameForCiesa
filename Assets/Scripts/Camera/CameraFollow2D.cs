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

        [Header("Tremor de Câmera")]
        [Tooltip("Multiplicador geral da força de qualquer tremor pedido.")]
        [SerializeField] private float shakeStrength = 1f;

        [Header("Limites")]
        [Tooltip("Prende a câmera dentro da fase, para nunca mostrar o vazio fora do mapa.")]
        [SerializeField] private bool useBounds;

        [Tooltip("Canto inferior esquerdo da área jogável, em unidades de mundo.")]
        [SerializeField] private Vector2 boundsMin;

        [Tooltip("Canto superior direito da área jogável, em unidades de mundo.")]
        [SerializeField] private Vector2 boundsMax;

        private Camera _camera;
        private Rigidbody2D _targetBody;
        private Vector3 _velocity;
        private float _lookAhead;
        private float _lookAheadVelocity;

        private float _shakeDuration;
        private float _shakeTimeLeft;
        private float _shakeMagnitude;

        /// <summary>
        /// Acesso direto para quem dispara o tremor (dano, morte, impacto) sem precisar
        /// arrastar uma referência de câmera até cada script de combate.
        /// </summary>
        public static CameraFollow2D Instance { get; private set; }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
        }

        /// <summary>
        /// Pede um tremor de câmera. Um pedido mais forte substitui um tremor fraco em
        /// andamento; um pedido mais fraco nunca interrompe um mais forte.
        /// </summary>
        public void Shake(float duration, float magnitude)
        {
            if (_shakeTimeLeft > 0f && magnitude < _shakeMagnitude) return;

            _shakeDuration = duration;
            _shakeTimeLeft = duration;
            _shakeMagnitude = magnitude;
        }

        private void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
            if (target != null) SetTarget(target);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

            transform.position = ClampToBounds(next + (Vector3)ConsumeShakeOffset());
        }

        /// <summary>
        /// Trava o centro da câmera para que a área visível nunca saia dos limites da
        /// fase. Sem isso, o jogador veria o vazio além do mapa nas beiradas.
        /// </summary>
        private Vector3 ClampToBounds(Vector3 position)
        {
            if (!useBounds || _camera == null || !_camera.orthographic) return position;

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            float minX = boundsMin.x + halfWidth;
            float maxX = boundsMax.x - halfWidth;
            float minY = boundsMin.y + halfHeight;
            float maxY = boundsMax.y - halfHeight;

            // Se a fase for menor que a janela da câmera num eixo, centraliza em vez
            // de deixar o clamp inverter min/max.
            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : (boundsMin.x + boundsMax.x) * 0.5f;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : (boundsMin.y + boundsMax.y) * 0.5f;

            return position;
        }

        /// <summary>Amplitude decai linearmente até zero ao longo de <c>_shakeDuration</c>.</summary>
        private Vector2 ConsumeShakeOffset()
        {
            if (_shakeTimeLeft <= 0f) return Vector2.zero;

            float t = _shakeDuration > 0f ? _shakeTimeLeft / _shakeDuration : 0f;
            _shakeTimeLeft = Mathf.Max(0f, _shakeTimeLeft - Time.deltaTime);

            return Random.insideUnitCircle * _shakeMagnitude * shakeStrength * t;
        }
    }
}
