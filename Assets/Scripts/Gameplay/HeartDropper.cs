using UnityEngine;
using Warana.Combat;

namespace Warana.Gameplay
{
    /// <summary>
    /// Sorteia um coração de cura quando o inimigo morre. Fica no inimigo, e não no
    /// contador de abates, porque o que cai é decisão de *quem* morreu — um chefe ou
    /// um inimigo raro pode ter outra chance sem que nada mais mude.
    ///
    /// O sorteio tem piedade embutida: com a vida baixa a chance sobe, e ela nunca
    /// desce abaixo do valor base. Um jogador quase morto que abate três inimigos e
    /// não vê cura nenhuma perde a fase para o dado, não para o jogo.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Warana/Gameplay/Sorteio de Coração")]
    public class HeartDropper : MonoBehaviour
    {
        [Tooltip("Prefab do coração largado.")]
        [SerializeField] private GameObject heartPrefab;

        [Range(0f, 1f)]
        [Tooltip("Chance de cair com o jogador de vida cheia.")]
        [SerializeField] private float dropChance = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("Chance quando o jogador está com um ponto de vida.")]
        [SerializeField] private float desperateChance = 0.6f;

        [Tooltip("Altura acima do pivô em que o coração aparece.")]
        [SerializeField] private float spawnHeight = 0.5f;

        private Health _health;

        private void Awake() => _health = GetComponent<Health>();

        private void OnEnable() => _health.Died += OnDied;

        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied()
        {
            if (heartPrefab == null) return;
            if (Random.value > ChanceFor(FindPlayerHealth())) return;

            Instantiate(heartPrefab, transform.position + Vector3.up * spawnHeight, Quaternion.identity);
        }

        private float ChanceFor(Health player)
        {
            if (player == null || player.Max <= 1f) return dropChance;

            // 1 de vida -> desperateChance; vida cheia -> dropChance.
            float missing = Mathf.Clamp01((player.Max - player.Current) / (player.Max - 1f));
            return Mathf.Lerp(dropChance, Mathf.Max(dropChance, desperateChance), missing);
        }

        private static Health FindPlayerHealth()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player == null ? null : player.GetComponentInChildren<Health>();
        }
    }
}
