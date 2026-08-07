using UnityEngine;
using Warana.Combat;

namespace Warana.UI
{
    /// <summary>
    /// Os corações de Piatã. Lê a vida direto do <see cref="Health"/> do jogador,
    /// então mudar a vida máxima no Inspector já muda a quantidade de corações na
    /// tela — a HUD nunca guarda um número próprio que possa discordar do jogo.
    /// </summary>
    [AddComponentMenu("Waraná/UI/Corações")]
    public class HeartsHUD : IconRowHUD
    {
        [Header("Vinculação")]
        [Tooltip("Vida acompanhada. Vazio = procura o Health do objeto com a tag Player.")]
        [SerializeField] private Health health;

        private void OnEnable()
        {
            if (health == null) health = FindPlayerHealth();

            if (health == null)
            {
                Debug.LogWarning("[HeartsHUD] Nenhum Health encontrado; os corações ficam vazios.", this);
                return;
            }

            health.Changed += OnHealthChanged;

            // O Health dispara Changed no próprio OnEnable dele, que pode já ter
            // acontecido. Pintar agora tira a HUD da dependência dessa ordem.
            OnHealthChanged(health.Current, health.Max);
        }

        private void OnDisable()
        {
            if (health != null) health.Changed -= OnHealthChanged;
        }

        private void OnHealthChanged(float current, float max)
        {
            int slots = Mathf.Max(0, Mathf.RoundToInt(max));
            if (IconCount != slots) Build(slots);

            // Arredonda para cima: com vida fracionada, um coração só apaga quando o
            // dano é de fato levado por inteiro — a HUD não mente para menos.
            Paint(Mathf.CeilToInt(current));
        }

        private static Health FindPlayerHealth()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponentInChildren<Health>() : null;
        }
    }
}
