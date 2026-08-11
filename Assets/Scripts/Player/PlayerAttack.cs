using System;
using System.Collections.Generic;
using UnityEngine;
using Warana.Combat;

namespace Warana.Player
{
    /// <summary>
    /// Golpe de lança — a estocada básica do Piatã, no clique esquerdo. Habilidade
    /// plugável: o <see cref="PlayerController2D"/> descobre sozinho no Awake. No chão
    /// o ataque prende o personagem no lugar; no ar ele mantém o controle aéreo para
    /// não travar a trajetória do pulo.
    ///
    /// <para>Um movimento só, sem combo: como em Hollow Knight, o que o jogador decide
    /// é *quando* e *de onde* estocar, não qual botão vem depois. Por isso o golpe tem
    /// uma janela de dano curta no meio da animação, e não durante a duração inteira —
    /// acertar depende do timing, e a recuperação é o custo de errar.</para>
    ///
    /// <para>É o irmão corpo-a-corpo do <see cref="Warana.Combat.SpiritBoltEmitter"/>:
    /// a lança é rápida, curta e sempre disponível; o raio do Waraná é à distância, mas
    /// cobra a canalização. Segurar o RMB tem prioridade, então nunca se ataca com a
    /// lança dentro da canalização.</para>
    /// </summary>
    [AddComponentMenu("Waraná/Player/Golpe de Lança")]
    public class PlayerAttack : PlayerAbility
    {
        [Header("Timing")]
        [Tooltip("Duração do golpe. Deve bater com o comprimento do clip Warana_Attack.")]
        [SerializeField] private float duration = 0.4f;

        [Tooltip("Intervalo mínimo entre dois golpes, contado a partir do fim do anterior.")]
        [SerializeField] private float cooldown = 0.1f;

        [Tooltip("Janela para o botão apertado um pouco cedo ainda contar.")]
        [SerializeField] private float bufferTime = 0.12f;

        [Header("Comportamento")]
        [Tooltip("No chão, trava o movimento durante o golpe — dá peso e impede atacar correndo.")]
        [SerializeField] private bool rootWhenGrounded = true;

        [Header("Dano")]
        [Tooltip("Dano por estocada. Um coração dos inimigos comuns.")]
        [SerializeField] private float damage = 1f;

        [Tooltip("Camadas que a lança fere. Padrão do projeto: Enemy (layer 9).")]
        [SerializeField] private LayerMask targetMask = 1 << 9;

        // Medidos na arte: a ponta da lança estendida chega a x ≈ 0,47 do pivô, na
        // altura do quadril. A caixa é maior que o desenho nos dois eixos, de
        // propósito — à frente, para o golpe visualmente encostado não falhar; em
        // altura, porque o Mad Ghost flutua com o corpo acima da linha da lança, e
        // uma caixa fiel ao sprite não alcançaria o inimigo mais comum da fase.
        [Tooltip("Centro da caixa de dano, relativo ao pivô do Player. X é medido para a frente.")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.55f, 0f);

        [Tooltip("Tamanho da caixa de dano, em unidades.")]
        [SerializeField] private Vector2 hitboxSize = new Vector2(0.8f, 1f);

        // O clip roda a 20 fps: a lança sai da guarda no frame 2 (0,10 s) e volta no
        // frame 7 (0,35 s). A janela fecha antes disso de propósito — o recolhimento
        // é a recuperação, e acertar com ele seria acertar sem estar mais estocando.
        [Tooltip("Quando a ponta da lança começa a ferir, contado do início do golpe.")]
        [SerializeField] private float hitStart = 0.1f;

        [Tooltip("Quando a ponta para de ferir. Depois disso é só a recuperação.")]
        [SerializeField] private float hitEnd = 0.3f;

        private readonly List<Collider2D> _overlaps = new List<Collider2D>();

        // Um alvo por estocada: sem isso a mesma caixa acertaria o mesmo inimigo em
        // todo passo de física da janela, e o golpe único viraria dano contínuo.
        private readonly HashSet<Transform> _alreadyHit = new HashSet<Transform>();

        private ContactFilter2D _targetFilter;
        private float _elapsed;
        private float _cooldownLeft;
        private float _bufferLeft;
        private bool _rooted;

        /// <summary>Disparado no frame em que o golpe começa. O <see cref="PlayerAnimator"/> escuta.</summary>
        public event Action Started;

        /// <summary>Disparado uma vez por inimigo atingido, no ponto do impacto.</summary>
        public event Action<Vector2> Hit;

        public bool IsAttacking => IsActive;

        // O controller aplica gravidade e movimento normalmente; quem trava o
        // deslocamento no chão é o FreezeControl, não este override.
        public override bool OverridesMovement => false;

        // Depois de habilidades de deslocamento (dash, wall jump), que têm prioridade.
        public override int Priority => 10;

        protected override void Awake()
        {
            base.Awake();

            // Triggers contam: um inimigo não precisa ter colisão física para apanhar.
            _targetFilter = new ContactFilter2D { useTriggers = true };
            _targetFilter.SetLayerMask(targetMask);
        }

        private void Update()
        {
            _cooldownLeft = Mathf.Max(0f, _cooldownLeft - Time.deltaTime);

            // O input é lido no Update e consumido no FixedUpdate: sem o buffer
            // um toque curto cairia entre dois passos de física e sumiria.
            _bufferLeft = Input.AttackPressedThisFrame
                ? bufferTime
                : Mathf.Max(0f, _bufferLeft - Time.deltaTime);
        }

        public override bool TryActivate()
        {
            if (_bufferLeft <= 0f || _cooldownLeft > 0f) return false;

            _bufferLeft = 0f;
            _elapsed = 0f;
            _alreadyHit.Clear();
            _rooted = rootWhenGrounded && Controller.IsGrounded;
            IsActive = true;

            if (_rooted) Controller.FreezeControl(true);

            Started?.Invoke();
            return true;
        }

        public override void Tick(float deltaTime)
        {
            float previous = _elapsed;
            _elapsed += deltaTime;

            // O passo de física pode ser maior que a janela inteira em queda de frames;
            // testar o intervalo percorrido (e não só o instante atual) impede que um
            // golpe bem mirado simplesmente não exista naquele frame.
            if (previous < hitEnd && _elapsed > hitStart) Strike();

            if (_elapsed < duration) return;

            if (_rooted) Controller.FreezeControl(false);
            _rooted = false;
            _cooldownLeft = cooldown;
            End();
        }

        /// <summary>Varre a caixa à frente do Piatã e fere quem ainda não foi ferido nesta estocada.</summary>
        private void Strike()
        {
            Vector2 center = HitboxCenter();

            _overlaps.Clear();
            Physics2D.OverlapBox(center, hitboxSize, 0f, _targetFilter, _overlaps);

            Vector2 direction = new Vector2(Controller.FacingDirection, 0f);

            foreach (Collider2D overlap in _overlaps)
            {
                if (overlap == null) continue;

                var damageable = overlap.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                // A identidade é a do alvo, não a do collider: inimigos com mais de um
                // collider levariam dano múltiplo pela mesma estocada.
                Transform victim = damageable is Component component ? component.transform : overlap.transform;
                if (!_alreadyHit.Add(victim)) continue;

                Vector2 point = overlap.ClosestPoint(center);
                damageable.TakeDamage(damage, point, direction);
                Hit?.Invoke(point);
            }
        }

        /// <summary>
        /// Centro da caixa em coordenadas de mundo. Espelhado por
        /// <see cref="PlayerController2D.FacingDirection"/> e não pela escala do
        /// transform: o projeto vira só o SpriteRenderer (flipWithScale desligado),
        /// então uma caixa em espaço local ficaria sempre apontando para a direita.
        /// </summary>
        private Vector2 HitboxCenter()
        {
            return (Vector2)transform.position +
                   new Vector2(hitboxOffset.x * Controller.FacingDirection, hitboxOffset.y);
        }

        private void OnDisable()
        {
            // Desligar o componente no meio do golpe deixaria o controle congelado.
            if (_rooted) Controller.FreezeControl(false);
            _rooted = false;
            IsActive = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            hitEnd = Mathf.Max(hitEnd, hitStart);
            duration = Mathf.Max(duration, hitEnd);

            // Permite ajustar a máscara com o jogo rodando e ver o efeito na hora.
            _targetFilter.useTriggers = true;
            _targetFilter.SetLayerMask(targetMask);
        }

        private void OnDrawGizmosSelected()
        {
            // Sem Controller fora do play mode: desenha para a direita, que é como a
            // arte foi feita, e basta para calibrar alcance e altura.
            int facing = Controller != null ? Controller.FacingDirection : 1;
            var center = (Vector2)transform.position +
                         new Vector2(hitboxOffset.x * facing, hitboxOffset.y);

            Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.9f);
            Gizmos.DrawWireCube(center, hitboxSize);
        }
#endif
    }
}
