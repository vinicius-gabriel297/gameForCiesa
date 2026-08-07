using System;
using UnityEngine;

namespace Warana.Combat
{
    /// <summary>
    /// Vida genérica com feedback de impacto. É o alvo de teste do raio do Waraná,
    /// mas serve como base para qualquer inimigo: o flash e o knockback existem para
    /// que o golpe *pareça* ter acertado mesmo antes de haver animação de dano.
    /// </summary>
    [AddComponentMenu("Waraná/Combate/Vida")]
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Vida")]
        [SerializeField] private float maxHealth = 5f;

        [Tooltip("Tempo de invulnerabilidade após levar dano. 0 = sem i-frames.")]
        [SerializeField] private float invulnerability = 0.08f;

        [Header("Reação ao dano")]
        [Tooltip("Cor do flash de impacto.")]
        [SerializeField] private Color flashColor = Color.white;

        [SerializeField] private float flashDuration = 0.08f;

        [Tooltip("Empurrão na direção do golpe. Precisa de Rigidbody2D dinâmico para valer.")]
        [SerializeField] private float knockback = 3f;

        [Header("Morte")]
        [Tooltip("Desativa o objeto ao morrer. Desligue para tratar a morte por evento.")]
        [SerializeField] private bool disableOnDeath = true;

        private SpriteRenderer _renderer;
        private Rigidbody2D _body;
        private Color _baseColor;
        private float _flashLeft;
        private float _invulnerableLeft;

        /// <summary>(vida atual, vida máxima) — gancho para barra de vida / HUD.</summary>
        public event Action<float, float> Changed;

        public event Action Died;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsAlive => Current > 0f;

        private void Awake()
        {
            Current = maxHealth;
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _body = GetComponent<Rigidbody2D>();

            if (_renderer != null) _baseColor = _renderer.color;
        }

        private void OnEnable()
        {
            Current = maxHealth;
            _invulnerableLeft = 0f;
            _flashLeft = 0f;
            if (_renderer != null) _renderer.color = _baseColor;
            Changed?.Invoke(Current, maxHealth);
        }

        private void Update()
        {
            _invulnerableLeft = Mathf.Max(0f, _invulnerableLeft - Time.deltaTime);

            if (_flashLeft <= 0f) return;

            _flashLeft -= Time.deltaTime;
            if (_flashLeft <= 0f && _renderer != null) _renderer.color = _baseColor;
        }

        public void TakeDamage(float amount, Vector2 point, Vector2 direction)
        {
            if (!IsAlive || _invulnerableLeft > 0f || amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            _invulnerableLeft = invulnerability;

            if (_renderer != null)
            {
                _renderer.color = flashColor;
                _flashLeft = flashDuration;
            }

            if (_body != null && _body.bodyType == RigidbodyType2D.Dynamic && knockback > 0f)
                _body.linearVelocity = direction.normalized * knockback;

            Changed?.Invoke(Current, maxHealth);

            if (Current > 0f) return;

            Died?.Invoke();
            if (disableOnDeath) gameObject.SetActive(false);
        }

        /// <summary>Cura sem passar por i-frames. Usado por respawn e itens.</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;

            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }

#if UNITY_EDITOR
        // Enquanto nenhum inimigo bate de volta, é por aqui que se testa a HUD:
        // botão direito no componente, com o jogo rodando.
        [ContextMenu("Debug/Levar 1 de dano")]
        private void DebugTakeDamage() => TakeDamage(1f, transform.position, Vector2.right);

        [ContextMenu("Debug/Curar 1")]
        private void DebugHeal() => Heal(1f);
#endif
    }
}
