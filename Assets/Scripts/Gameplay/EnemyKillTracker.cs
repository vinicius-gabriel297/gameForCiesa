using UnityEngine;
using Warana.Combat;
using Warana.Enemies;

namespace Warana.Gameplay
{
    /// <summary>
    /// Conta os inimigos abatidos na fase e concede recompensas em marcos fixos: um
    /// coração extra no terceiro abate, uma carga de raio extra no quarto. Os dois
    /// marcos são independentes — cada um dispara uma vez só, mesmo que o jogador
    /// abata muito mais que isso.
    /// </summary>
    [AddComponentMenu("Warana/Gameplay/Contador de Abates")]
    public class EnemyKillTracker : MonoBehaviour
    {
        [Header("Recompensas")]
        [Tooltip("Abates necessários para ganhar um coração extra.")]
        [SerializeField] private int killsForExtraHeart = 3;

        [SerializeField] private float extraHeartAmount = 1f;

        [Tooltip("Abates necessários para ganhar uma carga extra de raio.")]
        [SerializeField] private int killsForExtraBolt = 4;

        [SerializeField] private int extraBoltAmount = 1;

        [Header("Vinculação")]
        [Tooltip("Vazio = procura o Health do objeto com a tag Player.")]
        [SerializeField] private Health playerHealth;

        [Tooltip("Vazio = procura o BoltCharges da cena.")]
        [SerializeField] private BoltCharges playerBoltCharges;

        private int _kills;
        private bool _heartGranted;
        private bool _boltGranted;

        private void Start()
        {
            if (playerHealth == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerHealth = player.GetComponentInChildren<Health>();
            }

            if (playerBoltCharges == null) playerBoltCharges = FindAnyObjectByType<BoltCharges>();

            foreach (MadGhost ghost in FindObjectsByType<MadGhost>())
            {
                Health health = ghost.GetComponent<Health>();
                if (health != null) health.Died += OnEnemyDied;
            }
        }

        private void OnEnemyDied()
        {
            _kills++;

            if (!_heartGranted && _kills >= killsForExtraHeart)
            {
                _heartGranted = true;
                if (playerHealth != null) playerHealth.IncreaseMaxHealth(extraHeartAmount);
            }

            if (!_boltGranted && _kills >= killsForExtraBolt)
            {
                _boltGranted = true;
                if (playerBoltCharges != null) playerBoltCharges.IncreaseMaxCharges(extraBoltAmount);
            }
        }
    }
}
