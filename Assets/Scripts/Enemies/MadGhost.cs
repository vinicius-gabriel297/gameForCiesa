using System;
using System.Collections.Generic;
using UnityEngine;
using Warana.Combat;

namespace Warana.Enemies
{
    /// <summary>
    /// O Mad Ghost: o primeiro inimigo do jogo. Patrulha um trecho de chão, persegue
    /// Piatã quando o vê, e ataca de perto.
    ///
    /// O ataque não é um golpe só — o pacote de arte separa sacar, golpear e guardar
    /// a arma, e o comportamento respeita essa divisão porque ela *é* o design:
    ///
    ///   Unsheathe  telegrafia — o inimigo para e saca. É o aviso de que dá tempo de sair.
    ///   Attack     compromisso — o golpe sai, e não pode mais ser cancelado.
    ///   Sheathe    punição — ele guarda a arma devagar, exposto. É a vez do jogador.
    ///
    /// Sem essas três fases o inimigo viraria um dano aleatório de encostar; com elas,
    /// chegar perto passa a ser uma decisão com resposta certa.
    /// </summary>
    [RequireComponent(typeof(EnemyController2D))]
    [RequireComponent(typeof(EnemySenses2D))]
    [AddComponentMenu("Waraná/Inimigos/Mad Ghost")]
    public class MadGhost : MonoBehaviour
    {
        private enum State
        {
            Patrol,
            Chase,
            Unsheathe,
            Strike,
            Sheathe,
            Hurt,
            Dead,
        }

        [Header("Patrulha")]
        [SerializeField] private float patrolSpeed = 1.1f;

        [Tooltip("Quanto tempo fica parado ao dar meia-volta. Uma pausa curta tira o ar de robô.")]
        [SerializeField] private float turnPause = 0.35f;

        [Tooltip("Quanto pode se afastar do ponto onde nasceu, em unidades. 0 = só parede e beirada limitam.")]
        [SerializeField] private float patrolRange = 4f;

        [Header("Perseguição")]
        [SerializeField] private float chaseSpeed = 2.2f;

        [Tooltip("Quanto tempo ele volta a patrulhar depois de bater numa parede ou beirada perseguindo. " +
                 "Impede que fique encarando o obstáculo com o jogador do outro lado.")]
        [SerializeField] private float giveUpDuration = 2f;

        [Header("Ataque")]
        [Tooltip("Distância horizontal em que ele decide sacar a arma.")]
        [SerializeField] private float attackRange = 0.85f;

        [Tooltip("Descanso depois de guardar a arma, antes de poder atacar de novo.")]
        [SerializeField] private float attackCooldown = 0.9f;

        [SerializeField] private float damage = 1f;

        [Tooltip("Atraso dentro do golpe até o dano sair. 0 = no primeiro frame do clip.")]
        [SerializeField] private float hitDelay = 0.07f;

        [Tooltip("Centro da área de dano, relativo ao pivô. O X é medido para a frente.")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.34f, 0.3f);

        [SerializeField] private Vector2 hitboxSize = new Vector2(0.62f, 0.5f);

        [Tooltip("Quem pode levar o golpe. Deixe só a layer do Player.")]
        [SerializeField] private LayerMask hitMask;

        [Header("Reação a dano")]
        [Tooltip("Levar dano interrompe o que ele estiver fazendo (menos o golpe já iniciado).")]
        [SerializeField] private bool staggerOnHit = true;

        [Tooltip("Intervalo mínimo entre dois tropeços. Impede prender o inimigo em stunlock.")]
        [SerializeField] private float staggerCooldown = 0.6f;

        [Header("Morte")]
        [Tooltip("Segundos entre o fim da animação de morte e o objeto sumir. Negativo = não some.")]
        [SerializeField] private float despawnDelay = 0.4f;

        private EnemyController2D _controller;
        private EnemySenses2D _senses;
        private MadGhostAnimator _animator;
        private Health _health;

        private State _state = State.Patrol;
        private float _timer;
        private float _cooldownLeft;
        private float _staggerLeft;
        private float _giveUpLeft;
        private bool _struck;
        private bool _corpseSettled;
        private float _spawnX;

        private readonly List<Collider2D> _hits = new List<Collider2D>();
        private ContactFilter2D _hitFilter;

        /// <summary>Disparado no frame em que o golpe sai — o gancho de áudio escuta.</summary>
        public event Action AttackStarted;

        private void Awake()
        {
            _controller = GetComponent<EnemyController2D>();
            _senses = GetComponent<EnemySenses2D>();
            _animator = GetComponent<MadGhostAnimator>();
            _health = GetComponent<Health>();

            // Triggers contam: o Player não precisa ter colisão sólida para apanhar.
            _hitFilter = new ContactFilter2D { useTriggers = true };
            _hitFilter.SetLayerMask(hitMask);

            _spawnX = transform.position.x;
        }

        private void OnEnable()
        {
            _state = State.Patrol;
            _timer = 0f;
            _cooldownLeft = 0f;
            _staggerLeft = 0f;
            _giveUpLeft = 0f;
            _struck = false;
            _corpseSettled = false;
            _spawnX = transform.position.x;

            if (_health == null) return;

            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health == null) return;

            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            _cooldownLeft = Mathf.Max(0f, _cooldownLeft - dt);
            _staggerLeft = Mathf.Max(0f, _staggerLeft - dt);
            _giveUpLeft = Mathf.Max(0f, _giveUpLeft - dt);
            _timer -= dt;

            switch (_state)
            {
                case State.Patrol: TickPatrol(); break;
                case State.Chase: TickChase(); break;
                case State.Unsheathe: TickUnsheathe(); break;
                case State.Strike: TickStrike(); break;
                case State.Sheathe: TickSheathe(); break;
                case State.Hurt: TickHurt(); break;
                case State.Dead: TickDead(); break;
            }
        }

        // ---------------------------------------------------------------- patrulha

        private void TickPatrol()
        {
            // Enquanto o "desisti" corre, ele ignora estar alerta e volta a rondar.
            // Sem essa carência, sair da perseguição por causa de uma parede caía
            // direto em Chase de novo no frame seguinte, e ele voltava a encarar o
            // obstáculo — só que agora piscando entre os dois estados.
            if (_senses.IsAware && _giveUpLeft <= 0f)
            {
                Enter(State.Chase);
                return;
            }

            // A pausa do meia-volta: parado, mas ainda vigiando.
            if (_timer > 0f)
            {
                _controller.Stop();
                return;
            }

            if (ShouldTurnAround())
            {
                _controller.Flip();
                _timer = turnPause;
                _controller.Stop();
                return;
            }

            _controller.Move(_controller.FacingDirection, patrolSpeed);
        }

        /// <summary>
        /// Parede, beirada ou o limite da ronda. O limite só vale para o lado que
        /// afasta: encostar nele andando de volta não pode travar o inimigo na quina.
        /// </summary>
        private bool ShouldTurnAround()
        {
            if (_controller.IsBlockedAhead) return true;
            if (patrolRange <= 0f) return false;

            float offset = transform.position.x - _spawnX;
            return Mathf.Abs(offset) >= patrolRange && Mathf.Sign(offset) == _controller.FacingDirection;
        }

        // ------------------------------------------------------------- perseguição

        private void TickChase()
        {
            if (!_senses.IsAware)
            {
                Enter(State.Patrol);
                return;
            }

            int toTarget = _senses.DirectionToTarget;

            if (_senses.HorizontalDistance <= attackRange && _cooldownLeft <= 0f)
            {
                _controller.SetFacing(toTarget);
                Enter(State.Unsheathe);
                return;
            }

            // Encarar o alvo mesmo sem poder avançar: o inimigo esperando o cooldown
            // de costas para o jogador leria como bug, não como pausa.
            _controller.SetFacing(toTarget);

            // Já colado no jogador ele para, mas continua alerta — é só o cooldown
            // correndo, e o golpe vem em seguida.
            if (_senses.HorizontalDistance <= attackRange)
            {
                _controller.Stop();
                return;
            }

            // Parede ou beirada no caminho: perseguir dali vira encarar o obstáculo
            // para sempre, porque a memória dos sentidos se renova enquanto o jogador
            // estiver visível do outro lado. Ele desiste, dá meia-volta e volta a
            // rondar — o mesmo que faria se tivesse batido na parede patrulhando.
            if (_controller.IsBlockedAhead)
            {
                _giveUpLeft = giveUpDuration;
                _controller.Flip();
                _controller.Stop();

                // Enter zera o timer, então a pausa da meia-volta vem depois dele.
                Enter(State.Patrol);
                _timer = turnPause;
                return;
            }

            _controller.Move(toTarget, chaseSpeed);
        }

        // ------------------------------------------------------- sequência de golpe

        private void TickUnsheathe()
        {
            _controller.Stop();
            if (_timer > 0f) return;

            _struck = false;
            Enter(State.Strike);
        }

        private void TickStrike()
        {
            _controller.Stop();

            // O dano sai uma vez só, no meio do clip — não a cada frame em que a
            // caixa encosta no jogador.
            float elapsed = MadGhostAnimation.DurationOf(MadGhostAnimation.State.Attack) - _timer;
            if (!_struck && elapsed >= hitDelay)
            {
                _struck = true;
                DealDamage();
            }

            if (_timer > 0f) return;

            Enter(State.Sheathe);
        }

        private void TickSheathe()
        {
            _controller.Stop();
            if (_timer > 0f) return;

            _cooldownLeft = attackCooldown;
            Enter(_senses.IsAware ? State.Chase : State.Patrol);
        }

        private void DealDamage()
        {
            Vector2 center = (Vector2)transform.position
                             + new Vector2(hitboxOffset.x * _controller.FacingDirection, hitboxOffset.y);

            _hits.Clear();
            Physics2D.OverlapBox(center, hitboxSize, 0f, _hitFilter, _hits);

            foreach (Collider2D hit in _hits)
            {
                if (hit == null) continue;

                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                // Não se acerta nem acerta outro Mad Ghost: o alvo é quem estiver na
                // hitMask, e o próprio inimigo pode estar nela por engano de setup.
                if (damageable is Component component && component.gameObject == gameObject) continue;

                Vector2 direction = ((Vector2)hit.bounds.center - center).normalized;
                if (direction == Vector2.zero) direction = Vector2.right * _controller.FacingDirection;

                damageable.TakeDamage(damage, hit.bounds.center, direction);
            }
        }

        // ------------------------------------------------------------------- dano

        private void TickHurt()
        {
            _controller.Stop();
            if (_timer > 0f) return;

            Enter(_senses.IsAware ? State.Chase : State.Patrol);
        }

        private void OnDamaged(float amount, Vector2 direction)
        {
            if (_state == State.Dead) return;
            if (!staggerOnHit || _staggerLeft > 0f) return;

            // O golpe já saqueado não é cancelável. Interromper o wind-up premia quem
            // ataca rápido; interromper o golpe em si tiraria qualquer risco de chegar
            // perto, e o inimigo deixaria de ter uma pergunta a fazer ao jogador.
            if (_state == State.Strike) return;

            // "direction" é o sentido do golpe (de quem bateu para quem apanhou), então
            // quem bateu está do lado oposto. Sem virar para lá, um golpe pelas costas
            // deixava o inimigo apanhando de costas até a próxima virada da patrulha.
            if (!Mathf.Approximately(direction.x, 0f))
                _controller.SetFacing(direction.x > 0f ? -1 : 1);

            _staggerLeft = staggerCooldown;
            Enter(State.Hurt);
        }

        /// <summary>
        /// O cadáver cai até o chão e para lá. Enquanto não pousa, o controller segue
        /// ligado só pela gravidade — é o que deixa o inimigo morto no ar (arremessado
        /// pelo golpe que o matou) descer em vez de congelar no meio do salto.
        /// </summary>
        private void TickDead()
        {
            _controller.Stop();

            // O arremesso do golpe fatal vale para o cadáver também: travar o corpo no
            // frame da morte engoliria justamente o empurrão que dá impacto ao golpe.
            if (_corpseSettled || _controller.IsKnockedBack || !_controller.IsGrounded) return;

            _corpseSettled = true;

            // Só agora o corpo sai da física: parado no chão, ele não empurra mais o
            // jogador nem escorrega, e a animação de morte roda até o fim no lugar.
            _controller.Body.linearVelocity = Vector2.zero;
            _controller.enabled = false;
            _controller.Body.simulated = false;

            foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
                collider.enabled = false;
        }

        private void OnDied()
        {
            if (_state == State.Dead) return;

            Enter(State.Dead);
            _controller.Stop();
            _corpseSettled = false;

            // O corpo mantém a velocidade do golpe que o matou e continua caindo: é o
            // TickDead que o desliga, quando encostar no chão. Desligar os colliders
            // aqui — como era antes — tirava o chão de baixo do cadáver, e o inimigo
            // atravessava o mapa no meio da própria animação de morte.
            if (despawnDelay >= 0f)
            {
                float clip = MadGhostAnimation.DurationOf(MadGhostAnimation.State.Death);
                Destroy(gameObject, clip + despawnDelay);
            }
        }

        // ------------------------------------------------------------------ estados

        private void Enter(State next)
        {
            _state = next;

            switch (next)
            {
                case State.Unsheathe:
                    _timer = MadGhostAnimation.DurationOf(MadGhostAnimation.State.Unsheathe);
                    if (_animator != null) _animator.PlayUnsheathe();
                    break;

                case State.Strike:
                    _timer = MadGhostAnimation.DurationOf(MadGhostAnimation.State.Attack);
                    if (_animator != null) _animator.PlayAttack();
                    AttackStarted?.Invoke();
                    break;

                case State.Sheathe:
                    _timer = MadGhostAnimation.DurationOf(MadGhostAnimation.State.Sheathe);
                    if (_animator != null) _animator.PlaySheathe();
                    break;

                case State.Hurt:
                    _timer = MadGhostAnimation.DurationOf(MadGhostAnimation.State.Hit);
                    if (_animator != null) _animator.PlayHit();
                    break;

                case State.Dead:
                    _timer = 0f;
                    if (_animator != null) _animator.SetDead(true);
                    break;

                default:
                    _timer = 0f;
                    break;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Permite mexer na máscara com o jogo rodando e ver o efeito na hora.
            _hitFilter.useTriggers = true;
            _hitFilter.SetLayerMask(hitMask);
        }

        private void OnDrawGizmosSelected()
        {
            int facing = Application.isPlaying && _controller != null ? _controller.FacingDirection : 1;

            Vector3 center = transform.position
                             + new Vector3(hitboxOffset.x * facing, hitboxOffset.y, 0f);

            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.8f);
            Gizmos.DrawWireCube(center, hitboxSize);

            // Alcance do ataque e limites da ronda, no plano dos pés.
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawLine(
                transform.position + Vector3.left * attackRange,
                transform.position + Vector3.right * attackRange);

            if (patrolRange <= 0f) return;

            float originX = Application.isPlaying ? _spawnX : transform.position.x;
            var left = new Vector3(originX - patrolRange, transform.position.y, 0f);
            var right = new Vector3(originX + patrolRange, transform.position.y, 0f);

            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.7f);
            Gizmos.DrawLine(left + Vector3.down * 0.2f, left + Vector3.up * 0.6f);
            Gizmos.DrawLine(right + Vector3.down * 0.2f, right + Vector3.up * 0.6f);
        }
#endif
    }
}
