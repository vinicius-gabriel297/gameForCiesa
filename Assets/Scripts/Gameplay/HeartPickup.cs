using UnityEngine;
using Warana.Combat;

namespace Warana.Gameplay
{
    /// <summary>
    /// Coração largado por um inimigo abatido. Cura um ponto ao ser encostado.
    ///
    /// Ele flutua no lugar em vez de cair com física: o drop nasce de um inimigo que
    /// pode ter morrido numa beirada, e um item que rola para o abismo é uma
    /// recompensa que o jogo dá e tira no mesmo segundo. Some sozinho depois de um
    /// tempo para a fase não virar um depósito de cura acumulada.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [AddComponentMenu("Warana/Gameplay/Coração de Cura")]
    public class HeartPickup : MonoBehaviour
    {
        [Tooltip("Quanto cura ao ser pego.")]
        [SerializeField] private float healAmount = 1f;

        [Tooltip("Segundos até sumir sozinho. 0 ou menos = fica para sempre.")]
        [SerializeField] private float lifetime = 12f;

        [Header("Flutuação")]
        [SerializeField] private float bobHeight = 0.12f;

        [SerializeField] private float bobSpeed = 2.5f;

        [Header("Aviso de fim")]
        [Tooltip("Últimos segundos em que ele pisca antes de sumir.")]
        [SerializeField] private float blinkWindow = 3f;

        private SpriteRenderer _renderer;
        private Vector3 _origin;
        private float _age;

        private void Awake()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        private void OnEnable()
        {
            _origin = transform.position;
            _age = 0f;
        }

        private void Update()
        {
            _age += Time.deltaTime;

            transform.position = _origin + Vector3.up * (Mathf.Sin(_age * bobSpeed) * bobHeight);

            if (lifetime <= 0f) return;

            float left = lifetime - _age;
            if (left <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Pisca cada vez mais rápido conforme o prazo aperta: o jogador precisa
            // saber que a cura tem hora para acabar antes de decidir ir buscá-la.
            if (_renderer != null && left <= blinkWindow)
            {
                float rate = Mathf.Lerp(12f, 3f, left / blinkWindow);
                _renderer.enabled = Mathf.Sin(_age * rate) > -0.3f;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            var health = other.GetComponentInParent<Health>();
            if (health == null || !health.IsAlive) return;

            // Já com a vida cheia o coração fica no chão: sumir sem curar nada faria
            // o jogador achar que o item bugou.
            if (health.Current >= health.Max) return;

            health.Heal(healAmount);
            Destroy(gameObject);
        }
    }
}
