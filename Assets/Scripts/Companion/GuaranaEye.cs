using UnityEngine;
using Warana.Player;

namespace Warana.Companion
{
    /// <summary>
    /// Orbe companheiro do Waraná, no espírito do Sein (Ori and the Blind Forest):
    /// flutua ao redor do jogador a uma distância fixa, oscila de leve quando parado
    /// e persegue o alvo com interpolação suave — sem física alguma.
    ///
    /// A aparência é um SpriteRenderer com os quadros do olho, do mais aberto ao
    /// fechado. A piscada é rara e rápida de propósito: ela existe para o orbe não
    /// parecer um adereço estático, não para chamar atenção.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Waraná/Guaraná Eye")]
    public class GuaranaEye : MonoBehaviour
    {
        [Header("Alvo")]
        [Tooltip("Quem o orbe acompanha. Se vazio, procura o PlayerController2D da cena no Awake.")]
        [SerializeField] private Transform target;

        [Tooltip("Espelha o lado do orbe conforme a direção que o jogador encara.")]
        [SerializeField] private bool mirrorWithFacing = true;

        [Header("Órbita")]
        [Tooltip("Distância fixa até o alvo, em unidades.")]
        [SerializeField] private float followDistance = 1.2f;

        [Tooltip("Ângulo de repouso em torno do alvo. 0° = à frente, 90° = acima.")]
        [Range(-180f, 180f)]
        [SerializeField] private float restAngle = 130f;

        [Tooltip("Quantos graus o orbe varre para cada lado do ângulo de repouso.")]
        [SerializeField] private float orbitArc = 12f;

        [Tooltip("Velocidade da varredura da órbita, em ciclos por segundo.")]
        [SerializeField] private float orbitFrequency = 0.35f;

        [Header("Respiro (idle)")]
        [Tooltip("Quanto a distância pulsa para dentro e para fora, em unidades.")]
        [SerializeField] private float breathAmplitude = 0.08f;

        [Tooltip("Velocidade do respiro, em ciclos por segundo.")]
        [SerializeField] private float breathFrequency = 0.5f;

        [Header("Suavização")]
        [Tooltip("Tempo aproximado para alcançar a posição desejada. Menor = mais colado.")]
        [SerializeField] private float smoothTime = 0.22f;

        [Tooltip("Teto de velocidade da perseguição, em unidades/segundo. 0 = sem limite.")]
        [SerializeField] private float maxSpeed = 24f;

        [Header("Piscada")]
        [Tooltip("Quadros do olho, do TOTALMENTE ABERTO ao TOTALMENTE FECHADO.")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("Duração de uma piscada inteira (fechar + abrir), em segundos.")]
        [SerializeField] private float blinkDuration = 0.16f;

        [Tooltip("Intervalo mínimo e máximo entre piscadas, em segundos.")]
        [SerializeField] private Vector2 blinkInterval = new Vector2(3.5f, 7f);

        [Tooltip("Chance de a piscada sair dupla, como um piscar nervoso ocasional.")]
        [Range(0f, 1f)]
        [SerializeField] private float doubleBlinkChance = 0.2f;

        [Header("Ordenação")]
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 10;

        private PlayerController2D _targetPlayer;
        private SpriteRenderer _renderer;

        /// <summary>Velocidade acumulada pelo SmoothDamp — não é física, só estado do amortecimento.</summary>
        private Vector3 _velocity;

        /// <summary>Deslocamento de fase por instância, para dois orbes nunca respirarem em sincronia.</summary>
        private float _phase;

        private float _blinkCountdown;
        private float _blinkElapsed = -1f; // < 0 = olho aberto, parado
        private int _blinksLeft;

        /// <summary>True quando o jogador encara a esquerda e o espelhamento está ligado.</summary>
        private bool Mirrored =>
            mirrorWithFacing && _targetPlayer != null && _targetPlayer.FacingDirection < 0;

        // ------------------------------------------------------------------- ciclo

        private void Awake()
        {
            CacheComponents();
            _phase = Random.value * 100f;
            ScheduleNextBlink();

            if (target == null && Application.isPlaying)
            {
                _targetPlayer = FindAnyObjectByType<PlayerController2D>();
                if (_targetPlayer != null) target = _targetPlayer.transform;
            }
            else if (target != null)
            {
                _targetPlayer = target.GetComponent<PlayerController2D>();
            }
        }

        private void OnEnable()
        {
            CacheComponents();
            ApplyRendererSettings();
            ShowFrame(0); // sempre entra de olho aberto

            // Sem esse teleporte o orbe entraria voando desde a origem do mundo.
            if (target != null) transform.position = DesiredPosition(0f);
        }

        private void LateUpdate()
        {
            if (Application.isPlaying) TickBlink(Time.deltaTime);

            if (target == null) return;

            // A pupila é lateral: sem virar o sprite o olho ficaria olhando para trás.
            if (_renderer != null) _renderer.flipX = Mirrored;

            // LateUpdate: o alvo é um Rigidbody2D interpolado, então neste ponto do frame
            // a transform dele já está na posição visual final. Seguir aqui evita tremida.
            Vector3 desired = DesiredPosition(Time.time + _phase);

            if (!Application.isPlaying)
            {
                transform.position = desired;
                return;
            }

            transform.position = maxSpeed > 0f
                ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime, maxSpeed)
                : Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }

        // ---------------------------------------------------------------- movimento

        /// <summary>
        /// Ponto que o orbe persegue: coordenada polar em torno do alvo, com o ângulo
        /// varrendo devagar e o raio respirando. As duas oscilações usam frequências
        /// incomensuráveis, então o desenho no ar nunca se repete de forma óbvia.
        /// </summary>
        private Vector3 DesiredPosition(float time)
        {
            float angle = restAngle + Mathf.Sin(time * orbitFrequency * Mathf.PI * 2f) * orbitArc;

            // Espelhar em torno da vertical mantém o orbe "atrás do ombro" nos dois sentidos.
            if (Mirrored) angle = 180f - angle;

            // Golden ratio na frequência: o respiro nunca cai em fase com a órbita.
            float breath = Mathf.Sin(time * breathFrequency * 1.618f * Mathf.PI * 2f) * breathAmplitude;
            float distance = followDistance + breath;

            float radians = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * distance;

            Vector3 anchor = target.position + offset;
            anchor.z = transform.position.z; // o orbe fica no próprio plano, não no do alvo
            return anchor;
        }

        // ----------------------------------------------------------------- piscada

        /// <summary>
        /// Conta o intervalo até a próxima piscada e, durante ela, percorre os quadros
        /// em ida e volta (aberto -> fechado -> aberto).
        /// </summary>
        private void TickBlink(float deltaTime)
        {
            if (frames == null || frames.Length < 2) return;

            if (_blinkElapsed < 0f)
            {
                _blinkCountdown -= deltaTime;
                if (_blinkCountdown > 0f) return;

                _blinkElapsed = 0f;
                return;
            }

            _blinkElapsed += deltaTime;

            if (_blinkElapsed >= blinkDuration)
            {
                _blinkElapsed = -1f;
                ShowFrame(0);

                // A piscada dupla é só a mesma piscada repetida quase sem pausa.
                if (_blinksLeft > 0)
                {
                    _blinksLeft--;
                    _blinkCountdown = blinkDuration * 0.75f;
                }
                else
                {
                    ScheduleNextBlink();
                }

                return;
            }

            // Onda triangular: 0 -> 1 -> 0 ao longo da piscada. O último quadro (olho
            // fechado) cai exatamente no meio da duração.
            float t = _blinkElapsed / blinkDuration;
            float closed = 1f - Mathf.Abs(t * 2f - 1f);

            // Round em vez de Floor: o quadro fechado chega a aparecer de fato,
            // em vez de ser sempre atropelado pelo penúltimo.
            ShowFrame(Mathf.RoundToInt(closed * (frames.Length - 1)));
        }

        private void ScheduleNextBlink()
        {
            _blinkCountdown = Random.Range(blinkInterval.x, blinkInterval.y);
            _blinksLeft = Random.value < doubleBlinkChance ? 1 : 0;
        }

        private void ShowFrame(int index)
        {
            if (_renderer == null || frames == null || frames.Length == 0) return;

            _renderer.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        // ------------------------------------------------------------------ visual

        private void CacheComponents()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        }

        private void ApplyRendererSettings()
        {
            if (_renderer == null) return;

            _renderer.sortingLayerName = sortingLayer;
            _renderer.sortingOrder = sortingOrder;
        }

        // --------------------------------------------------------------------- API

        /// <summary>Troca o alvo em runtime (respawn, troca de personagem, cutscene).</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _targetPlayer = newTarget != null ? newTarget.GetComponent<PlayerController2D>() : null;
        }

        /// <summary>Cola o orbe na posição de repouso, sem transição — útil ao teleportar o jogador.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            transform.position = DesiredPosition(Time.time + _phase);
            _velocity = Vector3.zero;
        }

        /// <summary>Força uma piscada agora — gancho para reagir a dano, susto ou diálogo.</summary>
        public void Blink()
        {
            _blinkElapsed = 0f;
            _blinkCountdown = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // O delayCall existe porque a Unity proíbe mexer em componentes durante o OnValidate.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;

                CacheComponents();
                ApplyRendererSettings();
                ShowFrame(0);
            };
        }
#endif
    }
}
