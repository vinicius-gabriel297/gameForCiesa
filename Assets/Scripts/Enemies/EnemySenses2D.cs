using UnityEngine;

namespace Warana.Enemies
{
    /// <summary>
    /// A percepção do inimigo: onde está o alvo e se ele é visível agora.
    /// Separado da IA porque "enxergar" e "decidir" mudam por motivos diferentes —
    /// dá para trocar o alcance de um inimigo sem tocar no comportamento dele.
    ///
    /// Duas regras dão a sensação de justiça:
    ///
    /// 1. O inimigo só *descobre* o jogador à frente e com linha de visão limpa —
    ///    ninguém é visto pelas costas nem através de uma parede.
    /// 2. Uma vez alertado, ele leva <see cref="memory"/> segundos para desistir.
    ///    Sem essa memória, andar meio passo atrás de uma quina desligaria a
    ///    perseguição no mesmo frame, e o inimigo pareceria cego em vez de burro.
    /// </summary>
    [AddComponentMenu("Waraná/Inimigos/Sentidos de Inimigo 2D")]
    public class EnemySenses2D : MonoBehaviour
    {
        [Header("Alvo")]
        [Tooltip("Quem é procurado. Vazio = o objeto com a tag Player.")]
        [SerializeField] private Transform target;

        [Tooltip("Altura dos olhos acima do pivô, em unidades. É de onde parte a linha de visão.")]
        [SerializeField] private float eyeHeight = 0.35f;

        [Header("Visão")]
        [Tooltip("Distância em que o jogador é notado.")]
        [SerializeField] private float sightRange = 6f;

        [Tooltip("Distância em que a perseguição é abandonada. Maior que a de visão, para não piscar na borda.")]
        [SerializeField] private float loseRange = 9f;

        [Tooltip("Diferença de altura tolerada. Impede perseguir quem está numa plataforma bem acima.")]
        [SerializeField] private float verticalTolerance = 2.5f;

        [Tooltip("Camadas que cortam a linha de visão (paredes, chão).")]
        [SerializeField] private LayerMask blockingMask;

        [Tooltip("Segundos em que o inimigo continua alerta depois de perder o alvo de vista.")]
        [SerializeField] private float memory = 2.5f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private EnemyController2D _controller;
        private float _memoryLeft;

        /// <summary>O alvo perseguido. Null enquanto ninguém foi encontrado na cena.</summary>
        public Transform Target => target;

        /// <summary>True enquanto o inimigo está caçando — vendo o alvo ou ainda lembrando dele.</summary>
        public bool IsAware { get; private set; }

        /// <summary>True só quando o alvo está de fato visível neste frame.</summary>
        public bool HasLineOfSight { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<EnemyController2D>();
            if (target == null) FindPlayer();
        }

        private void OnEnable()
        {
            IsAware = false;
            HasLineOfSight = false;
            _memoryLeft = 0f;
        }

        private void Update()
        {
            // O Player pode ser criado depois do inimigo (builders montam a cena em
            // ordem própria), então vale insistir enquanto não houver alvo.
            if (target == null)
            {
                FindPlayer();
                if (target == null) return;
            }

            HasLineOfSight = CheckSight();

            if (HasLineOfSight)
            {
                IsAware = true;
                _memoryLeft = memory;
                return;
            }

            if (!IsAware) return;

            // Longe demais encerra a caçada na hora: a memória serve para quinas e
            // obstáculos, não para perseguir alguém que já escapou.
            if (DistanceTo(target) > loseRange)
            {
                IsAware = false;
                _memoryLeft = 0f;
                return;
            }

            _memoryLeft -= Time.deltaTime;
            if (_memoryLeft <= 0f) IsAware = false;
        }

        // ------------------------------------------------------------------ visão

        private bool CheckSight()
        {
            Vector2 origin = EyePosition;
            Vector2 delta = (Vector2)target.position - origin;

            if (Mathf.Abs(delta.y) > verticalTolerance) return false;

            float distance = delta.magnitude;

            // Já alerta, o inimigo enxerga mais longe e não precisa estar de frente:
            // ele sabe que você está lá, e só depende da linha de visão.
            float range = IsAware ? loseRange : sightRange;
            if (distance > range) return false;

            if (!IsAware && _controller != null && delta.x * _controller.FacingDirection < 0f) return false;

            if (blockingMask != 0 &&
                Physics2D.Raycast(origin, delta.normalized, distance, blockingMask))
                return false;

            return true;
        }

        private float DistanceTo(Transform other) =>
            Vector2.Distance(EyePosition, other.position);

        private Vector2 EyePosition => (Vector2)transform.position + Vector2.up * eyeHeight;

        private void FindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        // ------------------------------------------------------------- consultas

        /// <summary>Distância horizontal até o alvo. Infinito sem alvo.</summary>
        public float HorizontalDistance =>
            target == null ? float.PositiveInfinity : Mathf.Abs(target.position.x - transform.position.x);

        /// <summary>-1 se o alvo está à esquerda, +1 à direita. 0 sem alvo.</summary>
        public int DirectionToTarget
        {
            get
            {
                if (target == null) return 0;

                float dx = target.position.x - transform.position.x;
                if (Mathf.Approximately(dx, 0f)) return 0;

                return dx > 0f ? 1 : -1;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Vector3 eye = transform.position + Vector3.up * eyeHeight;

            Gizmos.color = new Color(0.9f, 0.3f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(eye, sightRange);

            Gizmos.color = new Color(0.9f, 0.3f, 0.4f, 0.15f);
            Gizmos.DrawWireSphere(eye, loseRange);

            if (Application.isPlaying && target != null && HasLineOfSight)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(eye, target.position);
            }
        }
    }
}
