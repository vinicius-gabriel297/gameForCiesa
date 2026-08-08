using UnityEngine;
using Warana.Combat;

namespace Warana.Gameplay
{
    /// <summary>
    /// Faixa invisível logo abaixo do mapa. Quem cair para fora da fase morre em vez
    /// de continuar caindo pelo vazio — para o Piatã isso encadeia com
    /// <see cref="Warana.Player.PlayerDeath"/> e reinicia a fase.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Warana/Gameplay/Zona de Queda")]
    public class OutOfBoundsKillZone : MonoBehaviour
    {
        [SerializeField] private float damage = 999f;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var health = other.GetComponentInParent<Health>();
            if (health == null || !health.IsAlive) return;

            health.TakeDamage(damage, other.bounds.center, Vector2.up);
        }
    }
}
