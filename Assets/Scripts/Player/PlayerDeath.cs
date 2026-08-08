using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Warana.Combat;

namespace Warana.Player
{
    /// <summary>
    /// Ao zerar a vida do Piatã, reinicia a fase. O <see cref="Health"/> em cena não
    /// desliga o objeto ao morrer (disableOnDeath = false), justamente para dar tempo
    /// da animação e do áudio de morte tocarem antes do reload.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Warana/Player/Morte do Piatã")]
    public class PlayerDeath : MonoBehaviour
    {
        [Tooltip("Espera entre a morte e o recarregamento da fase, para a animação/áudio de morte tocarem.")]
        [SerializeField] private float restartDelay = 1.2f;

        private Health _health;
        private PlayerController2D _controller;
        private PlayerAnimator _animator;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _controller = GetComponent<PlayerController2D>();
            _animator = GetComponent<PlayerAnimator>();
        }

        private void OnEnable() => _health.Died += OnDied;

        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied()
        {
            if (_controller != null) _controller.FreezeControl(true);
            if (_animator != null) _animator.SetDead(true);

            StartCoroutine(RestartAfterDelay());
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
