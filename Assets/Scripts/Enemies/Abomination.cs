using System.Collections.Generic;
using UnityEngine;
using Warana.Combat;

namespace Warana.Enemies
{
    /// <summary>
    /// A Abomination: a chefe que fecha a floresta. Deliberadamente mais simples que o
    /// <see cref="MadGhost"/> — ela não patrulha nem telegrafa em três tempos, porque
    /// não é um obstáculo de percurso: é uma parede de 5 pontos de vida numa arena
    /// aberta, onde a pergunta é só "dá para canalizar mais um raio antes de ela chegar?".
    ///
    /// Ela espera parada até ver Piatã, marca a percepção com uma batida de Idle
    /// (o aviso de que a luta começou), e daí só alterna entre andar até ele e golpear.
    /// O dano sai pelo mesmo overlap do resto do jogo, e o dano recebido vem do
    /// Spirit Ray via <see cref="Health"/> — nenhum sistema novo.
    /// </summary>
    [RequireComponent(typeof(EnemyController2D))]
    [RequireComponent(typeof(EnemySenses2D))]
    [AddComponentMenu("Waraná/Inimigos/Abomination")]
    public class Abomination : MonoBehaviour
    {
        private enum State
        {
            Dormant,
            Notice,
            Chase,
            Attack,
            Hurt,
            Dead,
        }

        [Header("Percepção")]
        [Tooltip("Para que lado ela encara enquanto dorme. -1 = esquerda, a boca da arena.")]
        [SerializeField] private int startFacing = -1;

        [Tooltip("Batida de Idle entre ver o jogador e sair andando. É o aviso de que a luta começou.")]
        [SerializeField] private float noticeDuration = 0.7f;

        [Header("Perseguição")]
        [SerializeField] private float chaseSpeed = 1.6f;

        [Header("Ataque")]
        [Tooltip("Distância horizontal em que ela decide golpear.")]
        [SerializeField] private float attackRange = 1.1f;

        [Tooltip("Descanso depois do golpe, antes de poder atacar de novo.")]
        [SerializeField] private float attackCooldown = 0.8f;

        [SerializeField] private float damage = 1f;

        [Tooltip("Atraso dentro do golpe até o dano sair. O clip tem 10 frames; o braço encosta no meio.")]
        [SerializeField] private float hitDelay = 0.45f;

        [Tooltip("Centro da área de dano, relativo ao pivô. O X é medido para a frente.")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.6f, 0.35f);

        [SerializeField] private Vector2 hitboxSize = new Vector2(0.9f, 0.6f);

        [Tooltip("Quem pode levar o golpe. Deixe só a layer do Player.")]
        [SerializeField] private LayerMask hitMask;

        [Header("Reação a dano")]
        [Tooltip("Intervalo mínimo entre dois tropeços. Impede prender a chefe em stunlock.")]
        [SerializeField] private float staggerCooldown = 0.5f;

        private EnemyController2D _controller;
        private EnemySenses2D _senses;
        private AbominationAnimator _animator;
        private Health _health;

        private State _state = State.Dormant;
        private float _timer;
        private float _cooldownLeft;
        private float _staggerLeft;
        private bool _struck;

        private readonly List<Collider2D> _hits = new List<Collider2D>();
        private ContactFilter2D _hitFilter;

        private void Awake()
        {
            _controller = GetComponent<EnemyController2D>();
            _senses = GetComponent<EnemySenses2D>();
            _animator = GetComponent<AbominationAnimator>();
            _health = GetComponent<Health>();

            _hitFilter = new ContactFilter2D { useTriggers = true };
            _hitFilter.SetLayerMask(hitMask);
        }

        private void OnEnable()
        {
            _state = State.Dormant;
            _timer = 0f;
            _cooldownLeft = 0f;
            _staggerLeft = 0f;
            _struck = false;

            // Os sentidos só descobrem alguém que esteja à frente. Uma chefe encarando
            // o fundo da arena deixaria Piatã passar por trás dela sem a luta começar.
            _controller.SetFacing(startFacing);

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
            _timer -= dt;

            switch (_state)
            {
                case State.Dormant: TickDormant(); break;
                case State.Notice: TickNotice(); break;
                case State.Chase: TickChase(); break;
                case State.Attack: TickAttack(); break;
                case State.Hurt: TickHurt(); break;
            }
        }

        private void TickDormant()
        {
            _controller.Stop();
            if (_senses.IsAware) Enter(State.Notice);
        }

        private void TickNotice()
        {
            _controller.Stop();
            _controller.SetFacing(_senses.DirectionToTarget);

            if (_timer <= 0f) Enter(State.Chase);
        }

        private void TickChase()
        {
            // Uma vez acordada, a chefe não volta a dormir: perder o alvo de vista no
            // meio da arena não pode zerar a luta.
            int toTarget = _senses.DirectionToTarget;
            if (toTarget == 0)
            {
                _controller.Stop();
                return;
            }

            _controller.SetFacing(toTarget);

            if (_senses.HorizontalDistance <= attackRange)
            {
                _controller.Stop();
                if (_cooldownLeft <= 0f) Enter(State.Attack);
                return;
            }

            if (_controller.IsBlockedAhead)
            {
                _controller.Stop();
                return;
            }

            _controller.Move(toTarget, chaseSpeed);
        }

        private void TickAttack()
        {
            _controller.Stop();

            float elapsed = AbominationAnimation.DurationOf(AbominationAnimation.State.Attack) - _timer;
            if (!_struck && elapsed >= hitDelay)
            {
                _struck = true;
                DealDamage();
            }

            if (_timer > 0f) return;

            _cooldownLeft = attackCooldown;
            Enter(State.Chase);
        }

        private void TickHurt()
        {
            _controller.Stop();
            if (_timer <= 0f) Enter(State.Chase);
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
                if (damageable is Component component && component.gameObject == gameObject) continue;

                Vector2 direction = ((Vector2)hit.bounds.center - center).normalized;
                if (direction == Vector2.zero) direction = Vector2.right * _controller.FacingDirection;

                damageable.TakeDamage(damage, hit.bounds.center, direction);
            }
        }

        private void OnDamaged(float amount, Vector2 direction)
        {
            if (_state == State.Dead) return;

            // Levar um raio acorda a chefe mesmo se ela ainda não tinha visto ninguém.
            if (_state == State.Dormant || _state == State.Notice)
            {
                Enter(State.Chase);
                return;
            }

            // O golpe já iniciado não é cancelável: sem isso, encher a chefe de raios
            // à queima-roupa tiraria todo o risco de chegar perto.
            if (_state == State.Attack) return;
            if (_staggerLeft > 0f) return;

            _staggerLeft = staggerCooldown;
            Enter(State.Hurt);
        }

        private void OnDied()
        {
            if (_state == State.Dead) return;

            Enter(State.Dead);

            // O cadáver da chefe fica na arena, então ele precisa parar de vez: sem
            // desligar o controller e travar o corpo, a gravidade manual continuaria
            // rodando e — com os colliders já fora — a Abomination cairia pelo chão
            // no meio da própria animação de morte.
            _controller.Stop();
            _controller.enabled = false;

            Rigidbody2D body = _controller.Body;
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;

            foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
                collider.enabled = false;
        }

        private void Enter(State next)
        {
            _state = next;

            switch (next)
            {
                case State.Notice:
                    _timer = noticeDuration;
                    break;

                case State.Attack:
                    _timer = AbominationAnimation.DurationOf(AbominationAnimation.State.Attack);
                    _struck = false;
                    if (_animator != null) _animator.PlayAttack();
                    break;

                case State.Hurt:
                    _timer = AbominationAnimation.DurationOf(AbominationAnimation.State.Hit);
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

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawLine(
                transform.position + Vector3.left * attackRange,
                transform.position + Vector3.right * attackRange);
        }
#endif
    }
}
